using System;
using System.IO;
using PortalNes.Emulator.Cartridge;
using PortalNes.Rendering3D;
using PortalNes.UnityBridge;
using UnityEditor;
using UnityEngine;

namespace PortalNes.Editor
{
    public sealed class NesChrProfileWindow : EditorWindow
    {
        private const string LastRomPathKey = "PortalNes.ChrProfileEditor.LastRomPath";
        private const string LastProfilePathKey = "PortalNes.ChrProfileEditor.LastProfilePath";
        private Texture2D atlas;
        private Cartridge cartridge;
        private string romPath;
        private NesRenderProfile profile;
        private string externalProfilePath;
        private bool ownsExternalProfile;
        private int selectedTable;
        private int selectedPattern;
        private int chrBank;
        private int palette = -1;
        private bool matchTileContents;
        private int excludedPatternColors;
        private NesElementType elementType = NesElementType.Background;
        private string ruleName;
        private float depth;
        private bool useBoxMesh;
        private float thickness = 0.18f;
        private int surfaceUnitWidth = 1;
        private int surfaceUnitHeight = 1;
        private bool hide;
        private NesGeometryType geometry;
        private float bevel = .12f;
        private int cylinderSegments = 12;
        private Mesh customMesh;
        private Vector3 geometryOffset;
        private Vector3 geometryRotation;
        private Vector3 geometryScale = Vector3.one;
        private readonly bool[] selectedTiles = new bool[512];
        private int dragStart = -1;
        private int dragCurrent = -1;

        [MenuItem("PortalNes/CHR Profile Editor")]
        private static void OpenWindow() => GetWindow<NesChrProfileWindow>("NES CHR Profile");

        private void OnGUI()
        {
            EditorGUILayout.LabelField("ROM CHR Pattern Browser", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.SelectableLabel(string.IsNullOrEmpty(romPath) ? "No ROM selected" : Path.GetFileName(romPath),
                    EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                if (GUILayout.Button("Open ROM...", GUILayout.Width(100))) OpenRom();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.SelectableLabel(string.IsNullOrWhiteSpace(externalProfilePath)
                        ? "No .nesprof selected" : Path.GetFileName(externalProfilePath),
                    EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                if (GUILayout.Button("Open Profile...", GUILayout.Width(110))) OpenProfile();
                if (GUILayout.Button("Create Profile...", GUILayout.Width(115))) CreateProfile();
            }
            if (!string.IsNullOrWhiteSpace(externalProfilePath))
                EditorGUILayout.HelpBox($"Editing JSON profile: {externalProfilePath}", MessageType.Info);

            if (cartridge != null && cartridge.MapperNumber == 3)
            {
                int bankCount = cartridge.ChrRom.Length / 8192;
                int nextBank = EditorGUILayout.IntSlider("CHR Bank", chrBank, 0, bankCount - 1);
                if (nextBank != chrBank)
                {
                    chrBank = nextBank;
                    cartridge.Mapper.CpuWrite(0x8000, (byte)chrBank);
                    BuildAtlas();
                }
            }

            if (atlas == null)
            {
                EditorGUILayout.HelpBox("Open an iNES ROM to generate both 256-tile CHR pattern tables.", MessageType.Info);
                return;
            }

            Rect imageRect = GUILayoutUtility.GetAspectRect(2f, GUILayout.MaxHeight(320));
            GUI.DrawTexture(imageRect, atlas, ScaleMode.StretchToFill, false);
            DrawAtlasGrid(imageRect);
            HandleAtlasClick(imageRect);

            int selectedCount = CountSelected();
            EditorGUILayout.LabelField($"Selected: {selectedCount} tile(s). Active: Table {selectedTable}, Pattern ${selectedPattern:X2}");
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Clear Selection")) Array.Clear(selectedTiles, 0, selectedTiles.Length);
                if (GUILayout.Button("Select Active Only")) { Array.Clear(selectedTiles, 0, selectedTiles.Length); selectedTiles[selectedTable * 256 + selectedPattern] = true; }
            }
            if (profile != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Default Background", EditorStyles.boldLabel);
                profile.UseDefaultBackgroundSettings = EditorGUILayout.Toggle(
                    "Use Default Settings", profile.UseDefaultBackgroundSettings);
                using (new EditorGUI.DisabledScope(!profile.UseDefaultBackgroundSettings))
                {
                    profile.DefaultBackgroundDepth = EditorGUILayout.FloatField(
                        "Default Depth", profile.DefaultBackgroundDepth);
                    profile.DefaultBackgroundGeometry = (NesGeometryType)EditorGUILayout.EnumPopup(
                        "Default Geometry", profile.DefaultBackgroundGeometry);
                    using (new EditorGUI.DisabledScope(
                               profile.DefaultBackgroundGeometry == NesGeometryType.Flat))
                        profile.DefaultBackgroundThickness = EditorGUILayout.FloatField(
                            "Default Thickness", profile.DefaultBackgroundThickness);
                }
                using (new EditorGUI.DisabledScope(
                           profile.DefaultBackgroundGeometry == NesGeometryType.CustomMesh))
                    if (GUILayout.Button("Save Default Background Settings"))
                        SaveDefaultBackgroundSettings();
                if (profile.DefaultBackgroundGeometry == NesGeometryType.CustomMesh)
                    EditorGUILayout.HelpBox("Custom Mesh cannot be stored as a JSON background default.",
                        MessageType.Warning);
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Default Sprite", EditorStyles.boldLabel);
                profile.DefaultSpriteDepth = EditorGUILayout.FloatField("Default Depth", profile.DefaultSpriteDepth);
                profile.DefaultSpriteGeometry = (NesGeometryType)EditorGUILayout.EnumPopup(
                    "Default Geometry", profile.DefaultSpriteGeometry);
                using (new EditorGUI.DisabledScope(profile.DefaultSpriteGeometry == NesGeometryType.Flat))
                    profile.DefaultSpriteThickness = EditorGUILayout.FloatField(
                        "Default Thickness", profile.DefaultSpriteThickness);
                using (new EditorGUI.DisabledScope(profile.DefaultSpriteGeometry == NesGeometryType.CustomMesh))
                    if (GUILayout.Button("Save Default Sprite Settings")) SaveDefaultSpriteSettings();
                if (profile.DefaultSpriteGeometry == NesGeometryType.CustomMesh)
                    EditorGUILayout.HelpBox("Custom Mesh cannot be stored as a JSON sprite default.",
                        MessageType.Warning);
                EditorGUILayout.Space();
            }
            elementType = (NesElementType)EditorGUILayout.EnumPopup("Element Type", elementType);
            palette = EditorGUILayout.IntSlider("Palette (-1 = any)", palette, -1, 3);
            matchTileContents = EditorGUILayout.Toggle(
                new GUIContent("Match Tile Contents", "Recommended for Mapper 2/3. Distinguishes graphics that reuse the same pattern number."),
                matchTileContents);
            ruleName = EditorGUILayout.TextField("Rule Name", ruleName);
            depth = EditorGUILayout.FloatField("Depth", depth);
            geometry = (NesGeometryType)EditorGUILayout.EnumPopup("Geometry", geometry);
            useBoxMesh = geometry == NesGeometryType.Box;
            using (new EditorGUI.DisabledScope(geometry == NesGeometryType.Flat))
                thickness = EditorGUILayout.FloatField("Thickness", thickness);
            if (geometry == NesGeometryType.Box)
            {
                surfaceUnitWidth = EditorGUILayout.IntSlider("Surface Unit Width", surfaceUnitWidth, 1, 8);
                surfaceUnitHeight = EditorGUILayout.IntSlider("Surface Unit Height", surfaceUnitHeight, 1, 8);
            }
            if (geometry == NesGeometryType.BeveledBox) bevel = EditorGUILayout.Slider("Bevel", bevel, .001f, .49f);
            if (geometry == NesGeometryType.Cylinder) cylinderSegments = EditorGUILayout.IntSlider("Cylinder Segments", cylinderSegments, 3, 32);
            if (geometry == NesGeometryType.CustomMesh) customMesh = (Mesh)EditorGUILayout.ObjectField("Custom Mesh", customMesh, typeof(Mesh), false);
            if (geometry == NesGeometryType.PixelExtrusion)
                excludedPatternColors = DrawExcludedPatternColors(excludedPatternColors);
            if (geometry != NesGeometryType.Flat)
            {
                geometryOffset = EditorGUILayout.Vector3Field("Geometry Offset", geometryOffset);
                geometryRotation = EditorGUILayout.Vector3Field("Geometry Rotation", geometryRotation);
                geometryScale = EditorGUILayout.Vector3Field("Geometry Scale", geometryScale);
            }
            hide = EditorGUILayout.Toggle("Hide", hide);

            using (new EditorGUI.DisabledScope(profile == null || selectedCount == 0))
                if (GUILayout.Button("Add / Update Rules For Selected Tiles")) SaveRules();
            if (profile == null)
                EditorGUILayout.HelpBox("Open a .nesprof JSON file before saving a rule.", MessageType.Warning);
            EditorGUILayout.HelpBox("CHR stores pixel values, not the runtime NES colors. Palette selects the profile match (0-3); -1 matches any palette.", MessageType.None);
        }

        private void OpenRom()
        {
            string path = EditorUtility.OpenFilePanel("Open iNES ROM", "", "nes");
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                cartridge = INesLoader.Load(File.ReadAllBytes(path));
                romPath = path;
                EditorPrefs.SetString(LastRomPathKey, romPath);
                chrBank = 0;
                matchTileContents = cartridge.MapperNumber == 2 || cartridge.MapperNumber == 3;
                LoadMatchingExternalProfile();
                BuildAtlas();
            }
            catch (Exception exception)
            {
                EditorUtility.DisplayDialog("PortalNes", exception.Message, "OK");
            }
        }

        private void OpenProfile()
        {
            string directory = string.IsNullOrWhiteSpace(externalProfilePath)
                ? "" : Path.GetDirectoryName(externalProfilePath);
            string path = EditorUtility.OpenFilePanel("Open PortalNes Render Profile", directory, "nesprof");
            if (string.IsNullOrWhiteSpace(path)) return;
            try
            {
                LoadExternalProfile(path);
            }
            catch (Exception exception)
            {
                EditorUtility.DisplayDialog("PortalNes", exception.Message, "OK");
            }
        }

        private void CreateProfile()
        {
            string directory = !string.IsNullOrWhiteSpace(romPath)
                ? Path.GetDirectoryName(romPath)
                : !string.IsNullOrWhiteSpace(externalProfilePath)
                    ? Path.GetDirectoryName(externalProfilePath) : "";
            string defaultName = !string.IsNullOrWhiteSpace(romPath)
                ? Path.GetFileNameWithoutExtension(romPath) + ".nesprof"
                : "NewProfile.nesprof";
            string path = EditorUtility.SaveFilePanel("Create PortalNes Render Profile",
                directory, defaultName, "nesprof");
            if (string.IsNullOrWhiteSpace(path)) return;
            if (File.Exists(path) && !EditorUtility.DisplayDialog("PortalNes",
                    $"Overwrite the existing profile?\n{path}", "Overwrite", "Cancel")) return;

            var created = CreateInstance<NesRenderProfile>();
            created.name = Path.GetFileNameWithoutExtension(path);
            created.DefaultSpriteThickness = 1f;
            created.Rules = Array.Empty<NesRenderRule>();
            try
            {
                File.WriteAllText(path, JsonUtility.ToJson(created, true));
            }
            catch (Exception exception)
            {
                EditorUtility.DisplayDialog("PortalNes", exception.Message, "OK");
                return;
            }
            finally
            {
                DestroyImmediate(created);
            }
            LoadExternalProfile(path);
            Debug.Log($"Created PortalNes render profile '{path}'.");
        }

        private void SaveDefaultSpriteSettings()
        {
            if (profile == null || string.IsNullOrWhiteSpace(externalProfilePath)) return;
            profile.DefaultSpriteThickness = profile.DefaultSpriteGeometry == NesGeometryType.Flat
                ? 0f : Mathf.Max(0f, profile.DefaultSpriteThickness);
            File.WriteAllText(externalProfilePath, JsonUtility.ToJson(profile, true));
            ReloadRunningProfile();
        }

        private void SaveDefaultBackgroundSettings()
        {
            if (profile == null || string.IsNullOrWhiteSpace(externalProfilePath)) return;
            profile.DefaultBackgroundThickness =
                profile.DefaultBackgroundGeometry == NesGeometryType.Flat
                    ? 0f : Mathf.Max(0f, profile.DefaultBackgroundThickness);
            File.WriteAllText(externalProfilePath, JsonUtility.ToJson(profile, true));
            ReloadRunningProfile();
        }

        private void BuildAtlas()
        {
            if (atlas != null) DestroyImmediate(atlas);
            atlas = new Texture2D(256, 128, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = "NES CHR Atlas" };
            Color32[] colors = new Color32[256 * 128];
            Color32[] shades = { new Color32(20,20,20,255), new Color32(90,90,90,255), new Color32(170,170,170,255), new Color32(245,245,245,255) };
            for (int table = 0; table < 2; table++)
            for (int tile = 0; tile < 256; tile++)
            for (int row = 0; row < 8; row++)
            {
                int address = table * 0x1000 + tile * 16 + row;
                byte lo = cartridge.Mapper.PpuRead((ushort)address), hi = cartridge.Mapper.PpuRead((ushort)(address + 8));
                int tileX = table * 16 + tile % 16, tileY = tile / 16;
                for (int x = 0; x < 8; x++)
                {
                    int bit = 7 - x, value = ((lo >> bit) & 1) | (((hi >> bit) & 1) << 1);
                    int px = tileX * 8 + x, py = 127 - (tileY * 8 + row);
                    colors[py * 256 + px] = shades[value];
                }
            }
            atlas.SetPixels32(colors); atlas.Apply(false, false);
        }

        private void DrawAtlasGrid(Rect rect)
        {
            float cell = rect.width / 32f;
            Color line = new Color(1, 1, 1, 0.15f);
            for (int x = 1; x < 32; x++) EditorGUI.DrawRect(new Rect(rect.x + x * cell, rect.y, 1, rect.height), line);
            for (int y = 1; y < 16; y++) EditorGUI.DrawRect(new Rect(rect.x, rect.y + y * cell, rect.width, 1), line);
            int column = selectedTable * 16 + selectedPattern % 16, row = selectedPattern / 16;
            for (int index = 0; index < selectedTiles.Length; index++)
            {
                if (!selectedTiles[index]) continue;
                int table = index / 256, pattern = index & 255;
                int x = table * 16 + pattern % 16, y = pattern / 16;
                EditorGUI.DrawRect(new Rect(rect.x + x * cell, rect.y + y * cell, cell, cell), new Color(1, 0.8f, 0, 0.25f));
            }
            Rect active = new Rect(rect.x + column * cell, rect.y + row * cell, cell, cell);
            EditorGUI.DrawRect(new Rect(active.x, active.y, active.width, 2), Color.yellow);
            EditorGUI.DrawRect(new Rect(active.x, active.yMax - 2, active.width, 2), Color.yellow);
            EditorGUI.DrawRect(new Rect(active.x, active.y, 2, active.height), Color.yellow);
            EditorGUI.DrawRect(new Rect(active.xMax - 2, active.y, 2, active.height), Color.yellow);
        }

        private void HandleAtlasClick(Rect rect)
        {
            Event e = Event.current;
            if (e.button != 0) return;
            if (e.type == EventType.MouseDown && rect.Contains(e.mousePosition))
            {
                dragStart = dragCurrent = AtlasIndexAt(rect, e.mousePosition);
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && dragStart >= 0)
            {
                dragCurrent = AtlasIndexAt(rect, e.mousePosition); e.Use(); Repaint();
            }
            else if (e.type == EventType.MouseUp && dragStart >= 0)
            {
                dragCurrent = AtlasIndexAt(rect, e.mousePosition);
                ApplyDragSelection(e.control || e.command);
                selectedTable = dragCurrent / 256; selectedPattern = dragCurrent & 255;
                ruleName = $"{elementType} T{selectedTable} ${selectedPattern:X2}";
                LoadExistingRule(); dragStart = dragCurrent = -1; e.Use(); Repaint();
            }
        }

        private static int AtlasIndexAt(Rect rect, Vector2 position)
        {
            int column = Mathf.Clamp((int)((position.x - rect.x) / rect.width * 32), 0, 31);
            int row = Mathf.Clamp((int)((position.y - rect.y) / rect.height * 16), 0, 15);
            return (column / 16) * 256 + row * 16 + column % 16;
        }

        private void ApplyDragSelection(bool additive)
        {
            if (!additive) Array.Clear(selectedTiles, 0, selectedTiles.Length);
            int startTable = dragStart / 256, endTable = dragCurrent / 256;
            int startColumn = startTable * 16 + (dragStart & 15), endColumn = endTable * 16 + (dragCurrent & 15);
            int startRow = (dragStart & 255) / 16, endRow = (dragCurrent & 255) / 16;
            for (int row = Mathf.Min(startRow, endRow); row <= Mathf.Max(startRow, endRow); row++)
            for (int column = Mathf.Min(startColumn, endColumn); column <= Mathf.Max(startColumn, endColumn); column++)
            {
                int index = (column / 16) * 256 + row * 16 + column % 16;
                selectedTiles[index] = additive ? !selectedTiles[index] : true;
            }
        }

        private void LoadExistingRule()
        {
            if (profile?.Rules == null) return;
            NesRenderRule rule = Array.Find(profile.Rules, r => r != null && r.ElementType == elementType &&
                r.PatternIndexMin == selectedPattern && r.PatternIndexMax == selectedPattern && r.PaletteIndex == palette &&
                !r.MatchAnyPattern &&
                r.MatchTileHash == matchTileContents && (!matchTileContents || r.TileHash == SelectedTileHash()));
            if (rule == null) return;
            ruleName = rule.Name; depth = rule.Depth; useBoxMesh = rule.UseBoxMesh;
            thickness = rule.Thickness; hide = rule.Hide;
            surfaceUnitWidth = Mathf.Max(1, rule.SurfaceUnitWidth);
            surfaceUnitHeight = Mathf.Max(1, rule.SurfaceUnitHeight);
            geometry = rule.EffectiveGeometry; bevel = rule.Bevel; cylinderSegments = rule.CylinderSegments;
            customMesh = rule.CustomMesh; geometryOffset = rule.GeometryOffset;
            geometryRotation = rule.GeometryRotation; geometryScale = rule.GeometryScale;
            matchTileContents = rule.MatchTileHash;
            excludedPatternColors = rule.PixelExtrusionExcludedColorMask;
        }

        private void SaveRules()
        {
            Undo.RecordObject(profile, "Add or Update CHR Render Rules");
            for (int index = 0; index < selectedTiles.Length; index++)
            {
                if (!selectedTiles[index]) continue;
                int table = index / 256, pattern = index & 255;
                uint tileHash = ComputeTileHash(table, pattern);
                NesRenderRule rule = profile.Rules == null ? null : Array.Find(profile.Rules, r => r != null &&
                    r.ElementType == elementType && r.PatternIndexMin == pattern &&
                    r.PatternIndexMax == pattern && r.PaletteIndex == palette &&
                    !r.MatchAnyPattern &&
                    r.MatchTileHash == matchTileContents && (!matchTileContents || r.TileHash == tileHash));
                if (rule == null)
                {
                    int count = profile.Rules?.Length ?? 0;
                    var rules = new NesRenderRule[count + 1]; rule = new NesRenderRule(); rules[0] = rule;
                    if (count > 0) Array.Copy(profile.Rules, 0, rules, 1, count); profile.Rules = rules;
                }
                rule.Name = CountSelected() == 1 && !string.IsNullOrWhiteSpace(ruleName)
                    ? ruleName : $"{elementType} T{table} ${pattern:X2}";
                rule.PatternIndexMin = rule.PatternIndexMax = pattern; rule.PaletteIndex = palette;
                rule.MatchTileHash = matchTileContents;
                rule.TileHash = matchTileContents ? tileHash : 0;
                rule.PixelExtrusionExcludedColorMask = geometry == NesGeometryType.PixelExtrusion
                    ? excludedPatternColors & 0x0E : 0;
                rule.ElementType = elementType; rule.Depth = depth; rule.Geometry = geometry;
                rule.UseBoxMesh = geometry == NesGeometryType.Box;
                rule.Thickness = geometry != NesGeometryType.Flat ? Mathf.Max(0, thickness) : 0; rule.Hide = hide;
                rule.SurfaceUnitWidth = Mathf.Max(1, surfaceUnitWidth);
                rule.SurfaceUnitHeight = Mathf.Max(1, surfaceUnitHeight);
                rule.Bevel = bevel; rule.CylinderSegments = cylinderSegments; rule.CustomMesh = customMesh;
                rule.GeometryOffset = geometryOffset; rule.GeometryRotation = geometryRotation; rule.GeometryScale = geometryScale;
            }
            File.WriteAllText(externalProfilePath, JsonUtility.ToJson(profile, true));
            ReloadRunningProfile();
            Debug.Log($"Saved PortalNes CHR rules to '{externalProfilePath}'.");
            Repaint();
        }

        private int CountSelected()
        {
            int count = 0; for (int i = 0; i < selectedTiles.Length; i++) if (selectedTiles[i]) count++;
            return count;
        }

        private void OnEnable()
        {
            string profilePath = EditorPrefs.GetString(LastProfilePathKey, "");
            if (!string.IsNullOrWhiteSpace(profilePath) && File.Exists(profilePath))
            {
                try { LoadExternalProfile(profilePath); }
                catch (Exception exception) { Debug.LogWarning($"PortalNes could not restore the previous profile: {exception.Message}"); }
            }

            string lastRomPath = EditorPrefs.GetString(LastRomPathKey, "");
            if (string.IsNullOrWhiteSpace(lastRomPath) || !File.Exists(lastRomPath)) return;
            try
            {
                cartridge = INesLoader.Load(File.ReadAllBytes(lastRomPath));
                romPath = lastRomPath;
                chrBank = 0;
                matchTileContents = cartridge.MapperNumber == 2 || cartridge.MapperNumber == 3;
                LoadMatchingExternalProfile();
                BuildAtlas();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"PortalNes could not restore the previous CHR ROM: {exception.Message}");
            }
        }

        private void SaveLastProfile()
        {
            EditorPrefs.SetString(LastProfilePathKey, externalProfilePath ?? "");
        }

        private void LoadMatchingExternalProfile()
        {
            string path = Path.ChangeExtension(romPath, ".nesprof");
            if (!File.Exists(path)) return;
            LoadExternalProfile(path);
        }

        private void LoadExternalProfile(string path)
        {
            var loaded = CreateInstance<NesRenderProfile>();
            loaded.name = Path.GetFileNameWithoutExtension(path);
            try
            {
                JsonUtility.FromJsonOverwrite(File.ReadAllText(path), loaded);
                if (loaded.Rules == null) loaded.Rules = Array.Empty<NesRenderRule>();
            }
            catch
            {
                DestroyImmediate(loaded);
                throw;
            }
            if (ownsExternalProfile && profile != null) DestroyImmediate(profile);
            profile = loaded;
            externalProfilePath = Path.GetFullPath(path);
            ownsExternalProfile = true;
            SaveLastProfile();
            Repaint();
        }

        private void ReloadRunningProfile()
        {
            if (!EditorApplication.isPlaying) return;
            var reloaded = new System.Collections.Generic.HashSet<NesSceneRenderer>();
            foreach (NesSceneRenderer renderer in Resources.FindObjectsOfTypeAll<NesSceneRenderer>())
            {
                if (renderer == null || !renderer.gameObject.scene.IsValid() ||
                    !renderer.HasRuntimeRenderProfile) continue;
                if (!string.Equals(Path.GetFullPath(renderer.RuntimeRenderProfilePath), externalProfilePath,
                    StringComparison.OrdinalIgnoreCase)) continue;
                renderer.LoadRenderProfileJson(externalProfilePath);
                reloaded.Add(renderer);
            }
            // A scene can intentionally start with an assigned asset profile.
            // Editing the matching ROM JSON during Play Mode is an explicit
            // request to switch that runner to the JSON and preview it live.
            foreach (NesRunner runner in Resources.FindObjectsOfTypeAll<NesRunner>())
            {
                if (runner == null || !runner.gameObject.scene.IsValid() ||
                    runner.SceneRenderer == null || string.IsNullOrWhiteSpace(runner.RomPath)) continue;
                string matchingPath = Path.GetFullPath(Path.ChangeExtension(runner.RomPath, ".nesprof"));
                if (!string.Equals(matchingPath, externalProfilePath, StringComparison.OrdinalIgnoreCase) ||
                    reloaded.Contains(runner.SceneRenderer)) continue;
                runner.SceneRenderer.LoadRenderProfileJson(externalProfilePath);
                reloaded.Add(runner.SceneRenderer);
            }
            if (reloaded.Count > 0)
                Debug.Log($"Applied CHR profile changes to {reloaded.Count} running NesSceneRenderer(s).");
            else
                Debug.LogWarning($"Saved CHR profile, but no running ROM matched '{externalProfilePath}'.");
        }

        private uint SelectedTileHash() => ComputeTileHash(selectedTable, selectedPattern);

        private static int DrawExcludedPatternColors(int mask)
        {
            EditorGUILayout.LabelField("Pattern Colors Left At Base", EditorStyles.miniBoldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                for (int color = 1; color <= 3; color++)
                {
                    bool selected = (mask & (1 << color)) != 0;
                    bool next = GUILayout.Toggle(selected, $"Color {color}", "Button");
                    if (next) mask |= 1 << color; else mask &= ~(1 << color);
                }
            }
            return mask;
        }

        private uint ComputeTileHash(int table, int pattern)
        {
            const uint offsetBasis = 2166136261u, prime = 16777619u;
            uint hash = offsetBasis;
            int address = table * 0x1000 + pattern * 16;
            for (int i = 0; i < 16; i++) hash = (hash ^ cartridge.Mapper.PpuRead((ushort)(address + i))) * prime;
            return hash;
        }

        private void OnDisable()
        {
            SaveLastProfile();
            if (atlas != null) DestroyImmediate(atlas);
            if (ownsExternalProfile && profile != null) DestroyImmediate(profile);
        }
    }
}
