using System;
using System.IO;
using System.Security.Cryptography;
using PortalNes.Emulator;
using PortalNes.Emulator.Mappers;
using PortalNes.Rendering3D;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PortalNes.UnityBridge
{
    public sealed class NesRunner : MonoBehaviour
    {
        private enum RegionMode { Auto, Ntsc, Pal }
        public static Func<string, string> RomPathPicker { get; set; }
        private const double NtscFrameSeconds = 1.0 / 60.0988;
        private const double PalFrameSeconds = 1.0 / 50.0070;
        [SerializeField] private NesDisplayMode displayMode = NesDisplayMode.Both;
        [SerializeField] private NesTextureRenderer textureRenderer;
        [SerializeField] private NesSceneRenderer sceneRenderer;
        [SerializeField] private NesInputProvider inputProvider;
        [SerializeField, Tooltip("Local path only. ROM files are never imported into the project.")] private string romPath;
        [SerializeField] private bool loadRomOnStart;
        [SerializeField, Tooltip("Auto uses the iNES header and common ROM filename region tags such as (E), (Europe), and (PAL).")]
        private RegionMode region = RegionMode.Auto;
        [SerializeField, Range(1, 5)] private int maxFramesPerUpdate = 3;
        [SerializeField, Range(0f, 1f)] private float audioVolume = 0.8f;
        [SerializeField, Tooltip("Runs NES APU emulation and creates the Unity audio output.")]
        private bool enableAudio = true;
        private readonly NesMachine machine = new NesMachine();
        private double accumulator;
        private bool loaded;
        private bool faulted;
        private bool filePickerOpen;
        private string lastError;
        private string romLoadError;
        private string romHash;
        private string stateMessage;
        private float stateMessageUntil;
        private bool emulationPaused;
        private NesRuntimeProfileEditor runtimeProfileEditor;
        private Canvas portalgraphMainScreenCanvas;
        private Canvas portalgraphCalibrationCanvas;
        private GUIStyle manualTitleStyle;
        private GUIStyle manualTextStyle;

        public NesDisplayMode DisplayMode => displayMode;
        public NesMachine Machine => machine;
        public bool IsLoaded => loaded;
        public bool IsFaulted => faulted;
        public string LastError => lastError;
        public string RomPath { get => romPath; set => romPath = value; }
        public NesSceneRenderer SceneRenderer => sceneRenderer;
        public bool EmulationPaused
        {
            get => emulationPaused;
            set => emulationPaused = value;
        }

        private void Awake()
        {
            if (enableAudio) EnsureAudioOutput();
            runtimeProfileEditor = GetComponent<NesRuntimeProfileEditor>();
            if (runtimeProfileEditor == null)
                runtimeProfileEditor = gameObject.AddComponent<NesRuntimeProfileEditor>();
            runtimeProfileEditor.Initialize(this);
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (RomPathPicker == null) RomPathPicker = WindowsRomFilePicker.Open;
#endif
        }

        public string GetDiagnostics()
        {
            if (!loaded || machine.Cpu == null || machine.Ppu == null) return "ROM is not loaded.";
            var r = machine.Cpu.Registers;
            var oam = machine.Ppu.Oam;
            string mapperState = machine.Cartridge.HeaderMapperNumber == machine.Cartridge.MapperNumber
                ? $"Mapper={machine.Cartridge.MapperNumber}"
                : $"Mapper={machine.Cartridge.MapperNumber}(Header={machine.Cartridge.HeaderMapperNumber})";
            if (machine.Cartridge.Mapper is Mapper007 mapper7)
                mapperState += $" Bank={mapper7.SelectedPrgBank} Mirror={mapper7.MirroringOverride}";
            else if (machine.Cartridge.Mapper is Mapper009 mapper9)
                mapperState +=
                    $" PRG={mapper9.SelectedPrgBank} L={mapper9.Latch0:X2}/{mapper9.Latch1:X2} " +
                    $"CHR=[{mapper9.ChrFd0},{mapper9.ChrFe0},{mapper9.ChrFd1},{mapper9.ChrFe1}] " +
                    $"LT={mapper9.Latch0Transitions},{mapper9.Latch1Transitions} " +
                    $"Trig=[{mapper9.Latch0FdTriggers}/{mapper9.Latch0FeTriggers}," +
                    $"{mapper9.Latch1FdTriggers}/{mapper9.Latch1FeTriggers}] " +
                    $"Mirror={mapper9.MirroringOverride}({mapper9.MirroringWrites})";
            else if (machine.Cartridge.Mapper is Mapper010 mapper10)
                mapperState += $" PRG={mapper10.SelectedPrgBank} L={mapper10.Latch0:X2}/{mapper10.Latch1:X2}";
            else if (machine.Cartridge.Mapper is Mapper018 mapper18)
                mapperState +=
                    $" PRG=[{mapper18.GetPrgBank(0)},{mapper18.GetPrgBank(1)},{mapper18.GetPrgBank(2)}] " +
                    $"CHR=[{mapper18.GetChrBank(0)},{mapper18.GetChrBank(1)}," +
                    $"{mapper18.GetChrBank(2)},{mapper18.GetChrBank(3)}," +
                    $"{mapper18.GetChrBank(4)},{mapper18.GetChrBank(5)}," +
                    $"{mapper18.GetChrBank(6)},{mapper18.GetChrBank(7)}] " +
                    $"IRQ={mapper18.IrqCounter:X4}/{mapper18.IrqReload:X4}/" +
                    $"{mapper18.IrqControl:X1}/{mapper18.IrqPending} " +
                    $"Mirror={mapper18.MirroringOverride}";
            else if (machine.Cartridge.Mapper is Mapper019 mapper19)
                mapperState +=
                    $" PRG=[{mapper19.GetPrgBank(0)},{mapper19.GetPrgBank(1)},{mapper19.GetPrgBank(2)}] " +
                    $"CHR=[{mapper19.GetChrBank(0)},{mapper19.GetChrBank(1)}," +
                    $"{mapper19.GetChrBank(2)},{mapper19.GetChrBank(3)}," +
                    $"{mapper19.GetChrBank(4)},{mapper19.GetChrBank(5)}," +
                    $"{mapper19.GetChrBank(6)},{mapper19.GetChrBank(7)}] " +
                    $"NT=[{mapper19.GetNametableBank(0):X2},{mapper19.GetNametableBank(1):X2}," +
                    $"{mapper19.GetNametableBank(2):X2},{mapper19.GetNametableBank(3):X2}] " +
                    $"IRQ={mapper19.IrqCounter:X4}/{mapper19.IrqEnabled}/{mapper19.IrqPending} " +
                    $"IRQCount={mapper19.IrqTriggerCount}/{mapper19.IrqAcknowledgeCount}/" +
                    $"{machine.Cpu.IrqServiceCount} NMI={machine.Cpu.NmiServiceCount} " +
                    $"SndOff={mapper19.SoundDisabled}";
            else if (machine.Cartridge.Mapper is Mapper032 mapper32)
                mapperState +=
                    $" PRG=[{mapper32.PrgBank0},{mapper32.PrgBank1}] Swap={mapper32.SwapPrg} " +
                    $"CHR=[{mapper32.GetChrBank(0)},{mapper32.GetChrBank(1)}," +
                    $"{mapper32.GetChrBank(2)},{mapper32.GetChrBank(3)}," +
                    $"{mapper32.GetChrBank(4)},{mapper32.GetChrBank(5)}," +
                    $"{mapper32.GetChrBank(6)},{mapper32.GetChrBank(7)}] " +
                    $"Mirror={mapper32.MirroringOverride}";
            else if (machine.Cartridge.Mapper is Mapper033 mapper33)
                mapperState +=
                    $" PRG=[{mapper33.GetPrgBank(0)},{mapper33.GetPrgBank(1)}] " +
                    $"CHR=[{mapper33.GetChrBank(0)},{mapper33.GetChrBank(1)}," +
                    $"{mapper33.GetChrBank(2)},{mapper33.GetChrBank(3)}," +
                    $"{mapper33.GetChrBank(4)},{mapper33.GetChrBank(5)}," +
                    $"{mapper33.GetChrBank(6)},{mapper33.GetChrBank(7)}] " +
                    $"Mirror={mapper33.MirroringOverride}";
            else if (machine.Cartridge.Mapper is Mapper048 mapper48)
                mapperState +=
                    $" PRG=[{mapper48.GetPrgBank(0)},{mapper48.GetPrgBank(1)}] " +
                    $"CHR=[{mapper48.GetChrBank(0)},{mapper48.GetChrBank(1)}," +
                    $"{mapper48.GetChrBank(2)},{mapper48.GetChrBank(3)}," +
                    $"{mapper48.GetChrBank(4)},{mapper48.GetChrBank(5)}," +
                    $"{mapper48.GetChrBank(6)},{mapper48.GetChrBank(7)}] " +
                    $"IRQ={mapper48.IrqCounter:X2}/{mapper48.IrqLatch:X2}/" +
                    $"{mapper48.IrqEnabled}/{mapper48.IrqPending} " +
                    $"Mirror={mapper48.MirroringOverride}";
            else if (machine.Cartridge.Mapper is Mapper065 mapper65)
                mapperState +=
                    $" PRG=[{mapper65.GetPrgBank(0)},{mapper65.GetPrgBank(1)},{mapper65.GetPrgBank(2)}] " +
                    $"CHR=[{mapper65.GetChrBank(0)},{mapper65.GetChrBank(1)}," +
                    $"{mapper65.GetChrBank(2)},{mapper65.GetChrBank(3)}," +
                    $"{mapper65.GetChrBank(4)},{mapper65.GetChrBank(5)}," +
                    $"{mapper65.GetChrBank(6)},{mapper65.GetChrBank(7)}] " +
                    $"IRQ={mapper65.IrqCounter:X4}/{mapper65.IrqReload:X4}/" +
                    $"{mapper65.IrqEnabled}/{mapper65.IrqPending} " +
                    $"Mirror={mapper65.MirroringOverride}";
            else if (machine.Cartridge.Mapper is Mapper087 mapper87)
                mapperState += $" CHR={mapper87.SelectedChrBank}";
            else if (machine.Cartridge.Mapper is Mapper140 mapper140)
                mapperState += $" PRG={mapper140.SelectedPrgBank} CHR={mapper140.SelectedChrBank}";
            else if (machine.Cartridge.Mapper is Mapper072 mapper72)
                mapperState += $" PRG={mapper72.SelectedPrgBank} CHR={mapper72.SelectedChrBank}";
            else if (machine.Cartridge.Mapper is Mapper086 mapper86)
                mapperState += $" PRG={mapper86.SelectedPrgBank} CHR={mapper86.SelectedChrBank} ADPCM={mapper86.AudioTrack}";
            else if (machine.Cartridge.Mapper is Mapper093 mapper93)
                mapperState += $" PRG={mapper93.SelectedPrgBank} CHRRAM={(mapper93.ChrRamEnabled ? "On" : "Off")}";
            else if (machine.Cartridge.Mapper is Mapper089 mapper89)
                mapperState += $" PRG={mapper89.SelectedPrgBank} CHR={mapper89.SelectedChrBank}";
            else if (machine.Cartridge.Mapper is Mapper067 mapper67)
                mapperState += $" PRG={mapper67.SelectedPrgBank} IRQ={mapper67.IrqCounter:X4}/{mapper67.IrqEnabled}";
            else if (machine.Cartridge.Mapper is Mapper078 mapper78)
                mapperState +=
                    $" PRG={mapper78.SelectedPrgBank} CHR={mapper78.SelectedChrBank} " +
                    $"Board={(mapper78.UsesHolyDiverMirroring ? "HolyDiver" : "CosmoCarrier")} " +
                    $"Mirror={mapper78.MirroringOverride}";
            else if (machine.Cartridge.Mapper is Mapper080 mapper80)
                mapperState +=
                    $" PRG=[{mapper80.GetPrgBank(0)},{mapper80.GetPrgBank(1)},{mapper80.GetPrgBank(2)}] " +
                    $"CHR=[{mapper80.GetChrBank(0)},{mapper80.GetChrBank(1)}," +
                    $"{mapper80.GetChrBank(2)},{mapper80.GetChrBank(3)}," +
                    $"{mapper80.GetChrBank(4)},{mapper80.GetChrBank(5)}," +
                    $"{mapper80.GetChrBank(6)},{mapper80.GetChrBank(7)}] " +
                    $"RAM={(mapper80.InternalRamEnabled ? "On" : "Off")} " +
                    $"Mirror={mapper80.MirroringOverride}";
            else if (machine.Cartridge.Mapper is Mapper097 mapper97)
                mapperState +=
                    $" PRG={mapper97.SelectedPrgBank} Mirror={mapper97.MirroringOverride}";
            else if (machine.Cartridge.Mapper is Mapper069 mapper69)
                mapperState += $" PRG=[{mapper69.GetPrgBank(0)},{mapper69.GetPrgBank(1)},{mapper69.GetPrgBank(2)},{mapper69.GetPrgBank(3)}] IRQ={mapper69.IrqCounter:X4}/{mapper69.IrqCounterEnabled}/{mapper69.IrqEnabled}";
            else if (machine.Cartridge.Mapper is Mapper070 mapper70)
                mapperState += $" PRG={mapper70.SelectedPrgBank} CHR={mapper70.SelectedChrBank} Mirror={mapper70.MirroringOverride}/{mapper70.MirroringControlEnabled} BusConflict={mapper70.HasBusConflicts}";
            else if (machine.Cartridge.Mapper is Mapper005 mapper5)
                mapperState += $" PRGMode={mapper5.PrgMode} CHRMode={mapper5.ChrMode} ExRAM={mapper5.ExRamMode} IRQ={mapper5.IrqScanline}/{mapper5.IrqPending}/{mapper5.InFrame}";
            else if (machine.Cartridge.Mapper is Mapper071 mapper71)
                mapperState += $" PRG={mapper71.SelectedPrgBank} Mirror={mapper71.MirroringOverride}";
            else if (machine.Cartridge.Mapper is Mapper184 mapper184)
                mapperState += $" CHR={mapper184.LowerChrBank}/{mapper184.UpperChrBank}";
            else if (machine.Cartridge.Mapper is Mapper068 mapper68)
                mapperState += $" PRG={mapper68.SelectedPrgBank} NTROM={(mapper68.UsesChrRomNametables ? "On" : "Off")}";
            else if (machine.Cartridge.Mapper is Mapper154 mapper154)
                mapperState +=
                    $" Sel={mapper154.SelectedRegister} " +
                    $"Banks=[{mapper154.GetBankRegister(0):X2},{mapper154.GetBankRegister(1):X2}," +
                    $"{mapper154.GetBankRegister(2):X2},{mapper154.GetBankRegister(3):X2}," +
                    $"{mapper154.GetBankRegister(4):X2},{mapper154.GetBankRegister(5):X2}," +
                    $"{mapper154.GetBankRegister(6):X2},{mapper154.GetBankRegister(7):X2}] " +
                    $"Mirror={mapper154.MirroringOverride} " +
                    $"Last=${mapper154.LastWriteAddress:X4}:${mapper154.LastWriteValue:X2} " +
                    $"Writes={mapper154.MirroringWrites}";
            string code = "";
            if (r.ProgramCounter >= 0x8000)
            {
                for (int i = 0; i < 6; i++)
                    code += $"{machine.Cartridge.Mapper.CpuRead((ushort)(r.ProgramCounter + i)):X2}" +
                        (i == 5 ? "" : " ");
            }
            string queue = "";
            for (ushort address = 0x0300; address < 0x0320; address++)
                queue += $"{machine.Bus.ReadRam(address):X2}" +
                    (address == 0x031F ? "" : " ");
            return $"ROM='{Path.GetFileName(romPath)}' Region={machine.Cartridge.Region} Mode={displayMode} " +
                   $"PC=${r.ProgramCounter:X4} A=${r.A:X2} X=${r.X:X2} " +
                   $"Y=${r.Y:X2} SP=${r.StackPointer:X2} P=${r.Status:X2} CPU={machine.Cpu.TotalCycles} " +
                   $"PPU={machine.Ppu.Scanline},{machine.Ppu.Dot} PPUSTATUS=${machine.Ppu.Registers.Status:X2} " +
                   $"CTRL=${machine.Ppu.Registers.Control:X2} MASK=${machine.Ppu.Registers.Mask:X2} " +
                   $"Scroll={machine.Ppu.ScrollX},{machine.Ppu.ScrollY} " +
                   $"OAM0=[Y:${oam[0]:X2} Tile:${oam[1]:X2} Attr:${oam[2]:X2} X:${oam[3]:X2}] " +
                   $"Frame={machine.Ppu.FrameNumber} NMI={machine.Cpu.NmiServiceCount} " +
                   $"IRQ={machine.Cpu.IrqServiceCount} " +
                   $"ZP36-3E=[{machine.Bus.ReadRam(0x36):X2} {machine.Bus.ReadRam(0x37):X2} " +
                   $"{machine.Bus.ReadRam(0x38):X2} {machine.Bus.ReadRam(0x39):X2} " +
                   $"{machine.Bus.ReadRam(0x3A):X2} {machine.Bus.ReadRam(0x3B):X2} " +
                   $"{machine.Bus.ReadRam(0x3C):X2} {machine.Bus.ReadRam(0x3D):X2} " +
                   $"{machine.Bus.ReadRam(0x3E):X2}] Queue0300=[{queue}] " +
                   $"{mapperState} Code=[{code}] " +
                   $"{machine.Ppu.TimingDiagnostics} {machine.NmiTimingDiagnostics} " +
                   $"Accumulator={accumulator:F6} Faulted={faulted}";
        }

        private void Start()
        {
            if (loadRomOnStart && !string.IsNullOrWhiteSpace(romPath))
            {
                try
                {
                    LoadRomFromPath(romPath);
                }
                catch (Exception exception)
                {
                    SetRomLoadError(exception);
                }
            }
        }

        public void LoadRomFromPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A ROM path is required.", nameof(path));
            byte[] romData = File.ReadAllBytes(path);
            machine.LoadRom(romData, ResolveRegionOverride(path));
            using (SHA256 sha = SHA256.Create())
                romHash = BitConverter.ToString(sha.ComputeHash(romData)).Replace("-", "");
            machine.AudioEnabled = enableAudio;
            machine.Apu.SetSampleRate(AudioSettings.outputSampleRate);
            machine.Reset();
            romPath = path;
            LoadMatchingRenderProfile(path);
            accumulator = 0; loaded = true; faulted = false; lastError = null; romLoadError = null;
            runtimeProfileEditor?.HandleRomLoaded();
        }

        internal float AudioVolume => audioVolume;

        private PortalNes.Emulator.Cartridge.NesRegion? ResolveRegionOverride(string path)
        {
            if (region == RegionMode.Ntsc) return PortalNes.Emulator.Cartridge.NesRegion.Ntsc;
            if (region == RegionMode.Pal) return PortalNes.Emulator.Cartridge.NesRegion.Pal;
            string name = Path.GetFileNameWithoutExtension(path);
            if (name.IndexOf("(E)", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("(Europe", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("(PAL", StringComparison.OrdinalIgnoreCase) >= 0)
                return PortalNes.Emulator.Cartridge.NesRegion.Pal;
            return null;
        }

        private void EnsureAudioOutput()
        {
            var audioOutput = GetComponent<NesAudioOutput>();
            if (audioOutput == null) audioOutput = gameObject.AddComponent<NesAudioOutput>();
            audioOutput.Initialize(this);
        }

        private void LoadMatchingRenderProfile(string path)
        {
            string matchingProfilePath = Path.ChangeExtension(path, ".nesprof");
            if (!File.Exists(matchingProfilePath))
            {
                var newProfile = ScriptableObject.CreateInstance<NesRenderProfile>();
                newProfile.name = Path.GetFileNameWithoutExtension(matchingProfilePath);
                newProfile.DefaultSpriteThickness = 1f;
                newProfile.Rules = Array.Empty<NesRenderRule>();
                try
                {
                    File.WriteAllText(matchingProfilePath, JsonUtility.ToJson(newProfile, true));
                    Debug.Log($"PortalNes created render profile '{matchingProfilePath}'.", this);
                }
                finally
                {
                    Destroy(newProfile);
                }
            }
            if (sceneRenderer == null) return;
            try
            {
                sceneRenderer.LoadRenderProfileJson(matchingProfilePath);
                Debug.Log($"PortalNes automatically loaded render profile '{Path.GetFileName(matchingProfilePath)}'.", this);
            }
            catch (Exception exception)
            {
                Debug.LogError($"PortalNes could not load matching render profile '{Path.GetFileName(matchingProfilePath)}': {exception.Message}", this);
                throw;
            }
        }

        private void Update()
        {
            if (machine.AudioEnabled != enableAudio)
            {
                machine.AudioEnabled = enableAudio;
                if (enableAudio) EnsureAudioOutput();
            }
            if (Keyboard.current?.f1Key.wasPressedThisFrame == true)
                ChooseAndLoadRom();
            if (Keyboard.current?.f5Key.wasPressedThisFrame == true)
                ResetEmulation();
            if (Keyboard.current?.f6Key.wasPressedThisFrame == true)
                SaveState();
            if (Keyboard.current?.f7Key.wasPressedThisFrame == true)
                LoadState();
            Gamepad gamepad = Gamepad.current;
            if (gamepad?.leftShoulder.wasPressedThisFrame == true)
                SaveState();
            if (gamepad?.rightShoulder.wasPressedThisFrame == true)
                LoadState();
            if (!loaded || faulted || emulationPaused) return;
            accumulator += Time.unscaledDeltaTime;
            double frameSeconds = machine.Cartridge.Region == PortalNes.Emulator.Cartridge.NesRegion.Pal
                ? PalFrameSeconds : NtscFrameSeconds;
            int frames = 0;
            while (accumulator >= frameSeconds && frames++ < maxFramesPerUpdate)
            {
                if (inputProvider != null)
                {
                    machine.SetControllerState(0, inputProvider.GetControllerState(0));
                    machine.SetControllerState(1, inputProvider.GetControllerState(1));
                }
                try
                {
                    machine.RunFrame();
                }
                catch (Exception exception)
                {
                    faulted = true;
                    var registers = machine.Cpu.Registers;
                    lastError = $"{exception.Message} ROM='{Path.GetFileName(romPath)}', " +
                                $"PC=${registers.ProgramCounter:X4}, A=${registers.A:X2}, X=${registers.X:X2}, " +
                                $"Y=${registers.Y:X2}, SP=${registers.StackPointer:X2}, P=${registers.Status:X2}, " +
                                $"CPU cycles={machine.Cpu.TotalCycles}. Emulation has been paused.";
                    Debug.LogError($"PortalNes stopped: {lastError}", this);
                    return;
                }
                accumulator -= frameSeconds;
            }
            // When Unity falls behind, emulate up to maxFramesPerUpdate frames
            // but upload/rebuild only the newest one. Rebuilding 2D textures
            // and the complete 3D scene for every catch-up frame makes a
            // transient hitch feed back into several increasingly expensive
            // Unity frames.
            if (frames > 0)
            {
                if (textureRenderer != null && displayMode != NesDisplayMode.Scene3D)
                    textureRenderer.Present(machine);
                if (sceneRenderer != null && displayMode != NesDisplayMode.Texture2D)
                    sceneRenderer.Present(machine.GetSceneSnapshot());
            }
            if (frames >= maxFramesPerUpdate && accumulator > frameSeconds * maxFramesPerUpdate)
                accumulator = frameSeconds * maxFramesPerUpdate;
        }

        private void LateUpdate()
        {
            bool showCursor = filePickerOpen ||
                runtimeProfileEditor != null && runtimeProfileEditor.IsVisible ||
                IsPortalgraphCanvasVisible(ref portalgraphMainScreenCanvas, "MainScreenCanvas") ||
                IsPortalgraphCanvasVisible(ref portalgraphCalibrationCanvas, "CalibrationCanvas");
            if (Cursor.visible != showCursor) Cursor.visible = showCursor;
            // These interfaces need a freely moving pointer. Gameplay only
            // hides it; it never captures the pointer at the screen center.
            if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;
        }

        private static bool IsPortalgraphCanvasVisible(ref Canvas cachedCanvas, string objectName)
        {
            if (cachedCanvas == null)
            {
                GameObject canvasObject = GameObject.Find(objectName) ??
                    GameObject.Find(objectName + "(Clone)");
                if (canvasObject != null) cachedCanvas = canvasObject.GetComponent<Canvas>();
            }
            return cachedCanvas != null && cachedCanvas.isActiveAndEnabled;
        }

        private void OnDisable()
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        private void OnGUI()
        {
            if (loaded && !string.IsNullOrWhiteSpace(stateMessage) &&
                Time.unscaledTime < stateMessageUntil)
            {
                GUI.Box(new Rect(16, 16, 360, 42), stateMessage);
            }
            if (!string.IsNullOrWhiteSpace(romLoadError))
            {
                DrawRomLoadError();
                return;
            }
            if (loaded) return;
            if (manualTitleStyle == null)
            {
                manualTitleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 26,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                manualTextStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 18,
                    alignment = TextAnchor.UpperLeft,
                    richText = true,
                    wordWrap = false
                };
            }

            const float width = 720f;
            const float height = 620f;
            var panel = new Rect((Screen.width - width) * .5f,
                (Screen.height - height) * .5f, width, height);
            GUI.Box(panel, GUIContent.none);
            GUI.Label(new Rect(panel.x + 20, panel.y + 16, panel.width - 40, 44),
                "PortalNES Controls", manualTitleStyle);
            GUI.Label(new Rect(panel.x + 54, panel.y + 72, panel.width - 108, panel.height - 88),
                "<b>PortalNES</b>\n" +
                "F1       Load ROM\n" +
                "F2       Toggle background\n" +
                "F3       Open tile profile editor\n" +
                "F5       Reset game\n" +
                "F6       Save state\n" +
                "F7       Load state\n\n" +
                "<b>NES Controller</b>\n" +
                "Arrow keys                 D-pad\n" +
                "J / K                      A / B\n" +
                "U / L                      Turbo A / Turbo B\n" +
                "Enter                      START\n" +
                "Right Shift                SELECT\n" +
                "LB / RB                    Quick Save / Quick Load\n\n" +
                "<b>Portalgraph</b>\n" +
                "W / A / S / D / R / F    Move\n" +
                "Q / E, T / G, Z / C      Rotate\n" +
                "1 / 2                     Scale up / down\n" +
                "3                         Reset position, rotation, and scale\n" +
                "F12                       Open Portalgraph settings",
                manualTextStyle);
        }

        private string StatePath
        {
            get
            {
                if (string.IsNullOrWhiteSpace(romHash))
                    throw new InvalidOperationException("ROM identity is not available.");
                string directory = Path.Combine(Application.persistentDataPath, "SaveStates");
                Directory.CreateDirectory(directory);
                return Path.Combine(directory, romHash + ".slot0.pns");
            }
        }

        public void SaveState()
        {
            if (!loaded || machine.Cpu == null) return;
            try
            {
                File.WriteAllBytes(StatePath, machine.SaveState());
                ShowStateMessage("State saved (F6 / LB)");
                Debug.Log($"PortalNes saved state for '{Path.GetFileName(romPath)}'.", this);
            }
            catch (Exception exception)
            {
                ShowStateMessage("State save failed");
                Debug.LogError($"PortalNes could not save state: {exception.Message}", this);
            }
        }

        public void LoadState()
        {
            if (!loaded || machine.Cpu == null) return;
            try
            {
                string path = StatePath;
                if (!File.Exists(path))
                {
                    ShowStateMessage("No saved state (F6 / LB to save)");
                    return;
                }
                machine.LoadState(File.ReadAllBytes(path));
                accumulator = 0;
                faulted = false;
                lastError = null;
                if (textureRenderer != null && displayMode != NesDisplayMode.Scene3D)
                    textureRenderer.Present(machine);
                if (sceneRenderer != null && displayMode != NesDisplayMode.Texture2D)
                    sceneRenderer.Present(machine.GetSceneSnapshot());
                ShowStateMessage("State loaded (F7 / RB)");
                Debug.Log($"PortalNes loaded state for '{Path.GetFileName(romPath)}'.", this);
            }
            catch (Exception exception)
            {
                ShowStateMessage("State load failed");
                Debug.LogError($"PortalNes could not load state: {exception.Message}", this);
            }
        }

        private void ShowStateMessage(string message)
        {
            stateMessage = message;
            stateMessageUntil = Time.unscaledTime + 2f;
        }

        private void DrawRomLoadError()
        {
            if (manualTitleStyle == null)
            {
                manualTitleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 26, fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                manualTextStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 18, alignment = TextAnchor.UpperCenter,
                    wordWrap = true
                };
            }
            float width = Mathf.Min(760f, Screen.width - 32f);
            float height = 260f;
            var panel = new Rect((Screen.width - width) * .5f,
                (Screen.height - height) * .5f, width, height);
            GUI.Box(panel, GUIContent.none);
            Color previousColor = GUI.color;
            GUI.color = new Color(1f, .55f, .45f);
            GUI.Label(new Rect(panel.x + 20, panel.y + 20, panel.width - 40, 44),
                "Could Not Load ROM", manualTitleStyle);
            GUI.color = previousColor;
            GUI.Label(new Rect(panel.x + 38, panel.y + 76, panel.width - 76, 112),
                romLoadError, manualTextStyle);
            GUI.Label(new Rect(panel.x + 38, panel.y + 202, panel.width - 76, 34),
                "Press F1 to select another ROM.", manualTextStyle);
        }

        private void SetRomLoadError(Exception exception)
        {
            romLoadError = exception?.Message ?? "An unknown ROM loading error occurred.";
            loaded = false;
            faulted = false;
            lastError = romLoadError;
            Debug.LogError($"PortalNes could not load ROM: {romLoadError}", this);
        }

        public void ChooseAndLoadRom()
        {
            // The native Windows dialog runs a nested message loop. Unity can
            // re-enter Update during it while F1 is still marked as pressed.
            if (filePickerOpen) return;
            if (RomPathPicker == null)
            {
                Debug.LogWarning("No runtime ROM file picker is registered on this platform.", this);
                return;
            }
            filePickerOpen = true;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            try
            {
                string selectedPath = RomPathPicker(romPath);
                if (string.IsNullOrWhiteSpace(selectedPath)) return;
                romPath = selectedPath;
                LoadRomFromPath(selectedPath);
                Debug.Log($"PortalNes loaded ROM '{Path.GetFileName(selectedPath)}'.", this);
            }
            catch (Exception exception)
            {
                SetRomLoadError(exception);
            }
            finally
            {
                filePickerOpen = false;
            }
        }

        public void ResetEmulation()
        {
            if (!loaded || machine.Cpu == null) return;
            machine.Reset();
            accumulator = 0;
            faulted = false;
            lastError = null;
            romLoadError = null;
            Debug.Log($"PortalNes reset ROM '{Path.GetFileName(romPath)}'.", this);
        }

    }
}
