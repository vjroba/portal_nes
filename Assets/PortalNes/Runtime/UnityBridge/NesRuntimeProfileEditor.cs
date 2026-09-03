using System;
using System.Collections.Generic;
using System.Globalization;
using PortalNes.Rendering3D;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PortalNes.UnityBridge
{
    public sealed class NesRuntimeProfileEditor : MonoBehaviour
    {
        private static readonly NesGeometryType[] EditableGeometries =
        {
            NesGeometryType.Flat,
            NesGeometryType.Box,
            NesGeometryType.PixelExtrusion
        };
        private static readonly string[] EditableGeometryLabels =
            { "Flat", "Box", "Pixels" };
        private enum PickerTab { Defaults, Background, Sprites, Chr }
        private enum ChrPreviewMode { Live, Bank8K, Page1K }

        private NesRunner runner;
        private new NesSceneRenderer renderer;
        private bool visible;
        private bool previousPaused;
        private bool previousCursorVisible;
        private CursorLockMode previousCursorLock;
        private Rect windowRect = new Rect(16, 16, 1060, 840);
        private PickerTab tab;
        private int selectedTileX;
        private int selectedTileY;
        private int selectedSprite;
        private int selectedChr;
        private readonly HashSet<int> selectedTiles = new HashSet<int>();
        private readonly HashSet<int> selectedSprites = new HashSet<int>();
        private readonly HashSet<int> selectedChrs = new HashSet<int>();
        private bool selectionDragging;
        private Vector2 selectionDragStart;
        private Vector2 selectionDragCurrent;
        private PickerTab selectionDragTab;
        private NesElementType chrElementType;
        private int chrPalette = -1;
        private int chrPreviewBank = -1;
        private string chrPreviewBankText = "0";
        private ChrPreviewMode chrPreviewMode;
        private int chrPreviewPage;
        private string chrPreviewPageText = "0";
        private Texture2D chrAtlas;
        private GUISkin editorSkin;
        private Vector2 editorScroll;

        private byte selectedPattern;
        private byte selectedPalette;
        private uint selectedHash;
        private NesElementType selectedElement;
        private bool hasSelection;
        private string selectedDescription = "Select a tile.";
        private string ruleName = "";
        private string depthText = "0";
        private string thicknessText = "1";
        private bool usePixelBaseDepth;
        private string pixelBaseDepthText = "1";
        private string surfaceWidthText = "1";
        private string surfaceHeightText = "1";
        private NesGeometryType geometry;
        private bool matchTileHash;
        private bool matchAnyPattern;
        private bool hide;
        private int excludedColors;
        private string status = "";
        private string defaultSpriteDepthText = "0";
        private string defaultSpriteThicknessText = "1";
        private NesGeometryType defaultSpriteGeometry = NesGeometryType.PixelExtrusion;
        private bool useDefaultBackgroundSettings;
        private string defaultBackgroundDepthText = "0";
        private string defaultBackgroundThicknessText = "1";
        private NesGeometryType defaultBackgroundGeometry = NesGeometryType.Flat;

        public bool IsVisible => visible;

        public bool ContainsScreenPoint(Vector2 screenPoint)
        {
            if (!visible) return false;
            // Input System screen coordinates start at the bottom-left, while
            // IMGUI window coordinates start at the top-left.
            return windowRect.Contains(new Vector2(screenPoint.x, Screen.height - screenPoint.y));
        }

        public void Initialize(NesRunner owner)
        {
            runner = owner;
            renderer = owner != null ? owner.SceneRenderer : null;
        }

        public void HandleRomLoaded()
        {
            // The displayed frame and CHR atlas belong to the previous ROM until
            // the newly reset machine has presented its first frame.
            if (visible) SetVisible(false);
            selectedTiles.Clear();
            selectedSprites.Clear();
            selectedChrs.Clear();
            chrPreviewBank = -1;
            chrPreviewBankText = "0";
            chrPreviewMode = ChrPreviewMode.Live;
            chrPreviewPage = 0;
            chrPreviewPageText = "0";
            hasSelection = false;
            selectionDragging = false;
            selectedDescription = "Select one or more tiles.";
            ruleName = "";
            depthText = "0";
            thicknessText = "1";
            surfaceWidthText = "1";
            surfaceHeightText = "1";
            geometry = NesGeometryType.Flat;
            matchTileHash = false;
            matchAnyPattern = false;
            hide = false;
            excludedColors = 0;
            CaptureDefaultBackgroundSettings();
            CaptureDefaultSpriteSettings();
            status = "";
            renderer = runner != null ? runner.SceneRenderer : renderer;
            if (chrAtlas != null)
            {
                Destroy(chrAtlas);
                chrAtlas = null;
            }
        }

        private void Update()
        {
            if (Keyboard.current?.f3Key.wasPressedThisFrame == true) SetVisible(!visible);
            if (visible && Keyboard.current?.escapeKey.wasPressedThisFrame == true) SetVisible(false);
        }

        private void SetVisible(bool value)
        {
            if (visible == value) return;
            visible = value;
            if (visible)
            {
                previousPaused = runner != null && runner.EmulationPaused;
                if (runner != null) runner.EmulationPaused = true;
                previousCursorVisible = Cursor.visible;
                previousCursorLock = Cursor.lockState;
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
                renderer = runner != null ? runner.SceneRenderer : renderer;
            }
            else
            {
                if (runner != null) runner.EmulationPaused = previousPaused;
                Cursor.visible = previousCursorVisible;
                Cursor.lockState = previousCursorLock;
            }
        }

        private void OnDisable()
        {
            if (visible) SetVisible(false);
        }

        private void OnDestroy()
        {
            if (chrAtlas != null) Destroy(chrAtlas);
            if (editorSkin != null) Destroy(editorSkin);
        }

        private void OnGUI()
        {
            if (!visible) return;
            EnsureEditorSkin();
            GUI.skin = editorSkin;
            windowRect.width = Mathf.Max(680, Mathf.Min(1120, Screen.width - 32));
            windowRect.height = Mathf.Max(520, Mathf.Min(900, Screen.height - 32));
            windowRect = GUI.Window(GetInstanceID(), windowRect, DrawWindow, "PortalNes Runtime Profile Editor — F3/Esc to close");
        }

        private void EnsureEditorSkin()
        {
            if (editorSkin != null) return;
            editorSkin = Instantiate(GUI.skin);
            editorSkin.name = "PortalNes Large Runtime Editor Skin";
            editorSkin.label.fontSize = 15;
            editorSkin.button.fontSize = 15;
            editorSkin.textField.fontSize = 15;
            editorSkin.toggle.fontSize = 15;
            editorSkin.box.fontSize = 15;
            editorSkin.window.fontSize = 16;
        }

        private void DrawWindow(int id)
        {
            GUILayout.BeginHorizontal();
            DrawPickerPanel();
            DrawRulePanel();
            GUILayout.EndHorizontal();
            GUI.DragWindow(new Rect(0, 0, windowRect.width, 24));
        }

        private void DrawPickerPanel()
        {
            GUILayout.BeginVertical(GUILayout.Width(windowRect.width * .61f));
            int nextTab = GUILayout.Toolbar((int)tab,
                new[] { "Defaults", "Background", "Sprites", "CHR Tiles" });
            if (nextTab != (int)tab)
            {
                tab = (PickerTab)nextTab;
                status = "";
                if (tab == PickerTab.Chr && chrAtlas == null) BuildChrAtlas();
                if (tab == PickerTab.Defaults)
                {
                    CaptureDefaultBackgroundSettings();
                    CaptureDefaultSpriteSettings();
                }
                RefreshPrimarySelection();
            }

            if (renderer == null || runner == null || !runner.IsLoaded)
            {
                GUILayout.Label("Load a ROM before editing.");
                GUILayout.EndVertical();
                return;
            }

            if (tab == PickerTab.Defaults)
            {
                GUILayout.Space(12);
                GUILayout.Label("Profile-wide defaults", GUI.skin.box);
                GUILayout.Label("These settings apply only when no individual tile rule matches.");
                GUILayout.Label("Transparent background tiles remain empty.");
            }
            else if (tab == PickerTab.Background) DrawBackgroundPicker();
            else if (tab == PickerTab.Sprites) DrawSpritePicker();
            else DrawChrPicker();
            if (tab != PickerTab.Defaults)
            {
                GUILayout.Label("Click/drag to select. Ctrl+click/drag adds or removes.");
                GUILayout.Label(selectedDescription);
            }
            GUILayout.EndVertical();
        }

        private void DrawBackgroundPicker()
        {
            Texture2D texture = renderer.BackgroundTexture;
            if (texture == null) { GUILayout.Label("Background is not ready."); return; }
            Rect rect = GUILayoutUtility.GetAspectRect(256f / 240f, GUILayout.ExpandWidth(true));
            GUI.DrawTextureWithTexCoords(rect, texture, new Rect(0, 1, 1, -1));
            DrawBackgroundGrid(rect);
            HandleDragSelection(rect, PickerTab.Background, delegate(Rect selection, bool toggle)
            {
                if (!toggle) selectedTiles.Clear();
                for (int i = 0; i < TileMeshFactory.Columns * TileMeshFactory.Rows; i++)
                {
                    int x = i % TileMeshFactory.Columns, y = i / TileMeshFactory.Columns;
                    if (!selection.Overlaps(ToDisplayRect(rect, renderer.GetPickerTileRect(x, y)))) continue;
                    ToggleOrAdd(selectedTiles, i, toggle);
                    selectedTileX = x;
                    selectedTileY = y;
                }
                RefreshPrimarySelection();
            });
        }

        private void DrawBackgroundGrid(Rect display)
        {
            for (int i = 0; i < TileMeshFactory.Columns * TileMeshFactory.Rows; i++)
            {
                int x = i % TileMeshFactory.Columns, y = i / TileMeshFactory.Columns;
                RectInt pixels = renderer.GetPickerTileRect(x, y);
                if (pixels.width <= 0 || pixels.height <= 0) continue;
                bool selected = selectedTiles.Contains(i);
                DrawBorder(ToDisplayRect(display, pixels),
                    selected ? Color.yellow : new Color(1, 1, 1, .15f),
                    selected ? 2 : 1);
            }
        }

        private void DrawSpritePicker()
        {
            if (renderer.BackgroundTexture == null || renderer.SpriteTexture == null)
            {
                GUILayout.Label("Sprite layer is not ready."); return;
            }
            Rect rect = GUILayoutUtility.GetAspectRect(256f / 240f, GUILayout.ExpandWidth(true));
            GUI.DrawTextureWithTexCoords(rect, renderer.BackgroundTexture, new Rect(0, 1, 1, -1));
            GUI.DrawTextureWithTexCoords(rect, renderer.SpriteTexture, new Rect(0, 1, 1, -1));
            for (int i = 63; i >= 0; i--)
            {
                if (!renderer.IsPickerSpriteVisible(i)) continue;
                bool selected = selectedSprites.Contains(i);
                DrawBorder(ToDisplayRect(rect, renderer.GetPickerSpriteRect(i)),
                    selected ? Color.yellow : new Color(1, 1, 1, .35f),
                    selected ? 2 : 1);
            }
            HandleDragSelection(rect, PickerTab.Sprites, delegate(Rect selection, bool toggle)
            {
                if (!toggle) selectedSprites.Clear();
                for (int i = 0; i < 64; i++)
                {
                    if (!renderer.IsPickerSpriteVisible(i) ||
                        !selection.Overlaps(ToDisplayRect(rect, renderer.GetPickerSpriteRect(i)))) continue;
                    ToggleOrAdd(selectedSprites, i, toggle);
                    selectedSprite = i;
                }
                RefreshPrimarySelection();
            });
        }

        private void DrawChrPicker()
        {
            GUILayout.BeginHorizontal();
            ChrPreviewMode nextMode = (ChrPreviewMode)GUILayout.Toolbar((int)chrPreviewMode,
                new[] { "Live Layout", "8KB Banks", "1KB Pages" });
            if (nextMode != chrPreviewMode) SetChrPreviewMode(nextMode);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            int bankCount = GetChrPreviewBankCount();
            int itemCount = chrPreviewMode == ChrPreviewMode.Page1K
                ? GetChrPreviewPageCount() : bankCount;
            GUI.enabled = chrPreviewMode != ChrPreviewMode.Live && itemCount > 1;
            if (GUILayout.Button("<", GUILayout.Width(35)))
                SetChrPreviewItem(CurrentChrPreviewItem() - 1);
            GUILayout.Label(ChrPreviewLabel(), GUILayout.Width(190));
            if (GUILayout.Button(">", GUILayout.Width(35)))
                SetChrPreviewItem(CurrentChrPreviewItem() + 1);
            GUI.enabled = true;
            if (chrPreviewMode != ChrPreviewMode.Live)
            {
                GUILayout.Label(chrPreviewMode == ChrPreviewMode.Page1K ? "Page" : "Bank",
                    GUILayout.Width(38));
                string itemText = chrPreviewMode == ChrPreviewMode.Page1K
                    ? chrPreviewPageText : chrPreviewBankText;
                itemText = GUILayout.TextField(itemText, GUILayout.Width(40));
                if (chrPreviewMode == ChrPreviewMode.Page1K) chrPreviewPageText = itemText;
                else chrPreviewBankText = itemText;
                if (GUILayout.Button("Go", GUILayout.Width(40)) &&
                    int.TryParse(itemText, out int requestedItem))
                    SetChrPreviewItem(requestedItem);
            }
            if (GUILayout.Button("Refresh", GUILayout.Width(75))) BuildChrAtlas();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            NesElementType nextElementType = (NesElementType)GUILayout.Toolbar(
                (int)chrElementType, new[] { "Background", "Sprite" });
            if (nextElementType != chrElementType)
            {
                chrElementType = nextElementType;
                RefreshPrimarySelection();
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Palette (-1=Any)", GUILayout.Width(110));
            string paletteText = GUILayout.TextField(chrPalette.ToString(CultureInfo.InvariantCulture), GUILayout.Width(45));
            if (int.TryParse(paletteText, out int parsedPalette)) chrPalette = Mathf.Clamp(parsedPalette, -1, 3);
            GUILayout.EndHorizontal();
            if (chrAtlas == null) BuildChrAtlas();
            if (chrAtlas == null) { GUILayout.Label("CHR is not available."); return; }
            Rect rect = GUILayoutUtility.GetAspectRect(2f, GUILayout.ExpandWidth(true));
            GUI.DrawTexture(rect, chrAtlas, ScaleMode.StretchToFill, false);
            float cell = rect.width / 32f;
            foreach (int chr in selectedChrs)
            {
                int column = (chr / 256) * 16 + (chr & 15);
                int row = (chr & 255) / 16;
                DrawBorder(new Rect(rect.x + column * cell, rect.y + row * cell, cell, cell),
                    Color.yellow, 2);
            }
            HandleDragSelection(rect, PickerTab.Chr, delegate(Rect selection, bool toggle)
            {
                if (!toggle) selectedChrs.Clear();
                for (int row = 0; row < 16; row++)
                for (int column = 0; column < 32; column++)
                {
                    Rect cellRect = new Rect(rect.x + column * cell, rect.y + row * cell, cell, cell);
                    if (!selection.Overlaps(cellRect)) continue;
                    int chr = (column / 16) * 256 + row * 16 + column % 16;
                    if (chrPreviewMode == ChrPreviewMode.Page1K && chr >= 64) continue;
                    ToggleOrAdd(selectedChrs, chr, toggle);
                    selectedChr = chr;
                }
                RefreshPrimarySelection();
            });
        }

        private void DrawRulePanel()
        {
            GUILayout.BeginVertical(GUILayout.Width(windowRect.width * .37f));
            if (tab == PickerTab.Defaults)
            {
                editorScroll = GUILayout.BeginScrollView(editorScroll);
                DrawDefaultBackgroundSettings();
                DrawDefaultSpriteSettings();
                if (!string.IsNullOrWhiteSpace(status)) GUILayout.Label(status);
                GUILayout.EndScrollView();
                GUILayout.EndVertical();
                return;
            }
            GUILayout.Label("Rule", GUI.skin.box);
            editorScroll = GUILayout.BeginScrollView(editorScroll);
            GUILayout.Label("Name"); ruleName = GUILayout.TextField(ruleName);
            GUILayout.Label("Depth"); depthText = GUILayout.TextField(depthText);
            GUILayout.Label("Geometry");
            geometry = DrawGeometrySelection(geometry);
            if (geometry != NesGeometryType.Flat)
            {
                GUILayout.Label("Thickness"); thicknessText = GUILayout.TextField(thicknessText);
            }
            if (geometry == NesGeometryType.Box)
            {
                GUILayout.Label("Surface Unit Width"); surfaceWidthText = GUILayout.TextField(surfaceWidthText);
                GUILayout.Label("Surface Unit Height"); surfaceHeightText = GUILayout.TextField(surfaceHeightText);
            }
            if (geometry == NesGeometryType.PixelExtrusion)
            {
                if (selectedElement == NesElementType.Background)
                {
                    usePixelBaseDepth = GUILayout.Toggle(usePixelBaseDepth,
                        "Use Separate Rear / Base Depth");
                    GUI.enabled = usePixelBaseDepth;
                    GUILayout.Label("Rear / Base Depth");
                    pixelBaseDepthText = GUILayout.TextField(pixelBaseDepthText);
                    GUI.enabled = true;
                }
                GUILayout.Label("Pattern Colors Left At Base");
                GUILayout.BeginHorizontal();
                for (int color = 1; color <= 3; color++)
                {
                    bool current = (excludedColors & (1 << color)) != 0;
                    bool next = GUILayout.Toggle(current, $"Color {color}", "Button");
                    if (next) excludedColors |= 1 << color; else excludedColors &= ~(1 << color);
                }
                GUILayout.EndHorizontal();
            }
            matchTileHash = GUILayout.Toggle(matchTileHash, "Match Tile Contents");
            GUI.enabled = matchTileHash;
            matchAnyPattern = GUILayout.Toggle(matchAnyPattern,
                "Match Any Pattern Index (follow bank slots)");
            GUI.enabled = true;
            hide = GUILayout.Toggle(hide, "Hide");
            GUILayout.Space(8);
            GUI.enabled = hasSelection && renderer != null && renderer.RenderProfile != null;
            if (GUILayout.Button("Add / Update Selected Rules", GUILayout.Height(32))) SaveRules();
            GUI.enabled = true;
            if (!string.IsNullOrWhiteSpace(status)) GUILayout.Label(status);
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void DrawDefaultSpriteSettings()
        {
            GUILayout.Label("Default Sprite", GUI.skin.box);
            GUILayout.Label("Depth");
            defaultSpriteDepthText = GUILayout.TextField(defaultSpriteDepthText);
            GUILayout.Label("Geometry");
            defaultSpriteGeometry = DrawGeometrySelection(defaultSpriteGeometry);
            if (defaultSpriteGeometry != NesGeometryType.Flat)
            {
                GUILayout.Label("Thickness");
                defaultSpriteThicknessText = GUILayout.TextField(defaultSpriteThicknessText);
            }
            GUI.enabled = renderer != null && renderer.RenderProfile != null;
            if (GUILayout.Button("Save Default Sprite Settings")) SaveDefaultSpriteSettings();
            GUI.enabled = true;
            GUILayout.Space(8);
        }

        private void DrawDefaultBackgroundSettings()
        {
            GUILayout.Label("Default Background", GUI.skin.box);
            useDefaultBackgroundSettings = GUILayout.Toggle(useDefaultBackgroundSettings,
                "Use Default Background Settings");
            GUI.enabled = useDefaultBackgroundSettings;
            GUILayout.Label("Depth");
            defaultBackgroundDepthText = GUILayout.TextField(defaultBackgroundDepthText);
            GUILayout.Label("Geometry");
            defaultBackgroundGeometry = DrawGeometrySelection(defaultBackgroundGeometry);
            if (defaultBackgroundGeometry != NesGeometryType.Flat)
            {
                GUILayout.Label("Thickness");
                defaultBackgroundThicknessText = GUILayout.TextField(defaultBackgroundThicknessText);
            }
            GUI.enabled = renderer != null && renderer.RenderProfile != null &&
                defaultBackgroundGeometry != NesGeometryType.CustomMesh;
            if (GUILayout.Button("Save Default Background Settings")) SaveDefaultBackgroundSettings();
            GUI.enabled = true;
            GUILayout.Space(8);
        }

        private static NesGeometryType DrawGeometrySelection(NesGeometryType current)
        {
            int currentIndex = Array.IndexOf(EditableGeometries, current);
            // Experimental and asset-backed options are not editable in the
            // runtime UI. Treat legacy/default occurrences as Flat here.
            if (currentIndex < 0) currentIndex = 0;
            int selected = GUILayout.SelectionGrid(currentIndex, EditableGeometryLabels, 2);
            return selected >= 0 && selected < EditableGeometries.Length
                ? EditableGeometries[selected]
                : EditableGeometries[currentIndex];
        }

        private void CaptureDefaultBackgroundSettings()
        {
            NesRenderProfile profile = renderer != null ? renderer.RenderProfile : null;
            useDefaultBackgroundSettings = profile != null && profile.UseDefaultBackgroundSettings;
            defaultBackgroundDepthText = Format(profile != null ? profile.DefaultBackgroundDepth : 0f);
            defaultBackgroundThicknessText = Format(profile != null ? profile.DefaultBackgroundThickness : 1f);
            defaultBackgroundGeometry = profile != null
                ? profile.DefaultBackgroundGeometry : NesGeometryType.Flat;
        }

        private void SaveDefaultBackgroundSettings()
        {
            NesRenderProfile profile = renderer?.RenderProfile;
            if (profile == null) { status = "No ROM profile is loaded."; return; }
            if (!TryFloat(defaultBackgroundDepthText, out float depth) ||
                !TryFloat(defaultBackgroundThicknessText, out float thickness))
            {
                status = "Default background Depth or Thickness is invalid."; return;
            }
            if (defaultBackgroundGeometry == NesGeometryType.CustomMesh)
            {
                status = "Custom Mesh cannot be stored as a JSON background default."; return;
            }
            profile.UseDefaultBackgroundSettings = useDefaultBackgroundSettings;
            profile.DefaultBackgroundDepth = depth;
            profile.DefaultBackgroundGeometry = defaultBackgroundGeometry;
            profile.DefaultBackgroundThickness = defaultBackgroundGeometry == NesGeometryType.Flat
                ? 0f : Mathf.Max(0f, thickness);
            try
            {
                renderer.SaveRuntimeRenderProfileJson();
                RefreshRenderedProfile();
                status = "Saved default background settings.";
            }
            catch (Exception exception)
            {
                status = exception.Message;
            }
        }

        private void CaptureDefaultSpriteSettings()
        {
            NesRenderProfile profile = renderer != null ? renderer.RenderProfile : null;
            defaultSpriteDepthText = Format(profile != null ? profile.DefaultSpriteDepth : 0f);
            defaultSpriteThicknessText = Format(profile != null ? profile.DefaultSpriteThickness : 1f);
            defaultSpriteGeometry = profile != null
                ? profile.DefaultSpriteGeometry : NesGeometryType.PixelExtrusion;
        }

        private void SaveDefaultSpriteSettings()
        {
            NesRenderProfile profile = renderer?.RenderProfile;
            if (profile == null) { status = "No ROM profile is loaded."; return; }
            if (!TryFloat(defaultSpriteDepthText, out float depth) ||
                !TryFloat(defaultSpriteThicknessText, out float thickness))
            {
                status = "Default sprite Depth or Thickness is invalid."; return;
            }
            if (defaultSpriteGeometry == NesGeometryType.CustomMesh)
            {
                status = "Custom Mesh cannot be stored as a JSON sprite default."; return;
            }
            profile.DefaultSpriteDepth = depth;
            profile.DefaultSpriteGeometry = defaultSpriteGeometry;
            profile.DefaultSpriteThickness = defaultSpriteGeometry == NesGeometryType.Flat
                ? 0f : Mathf.Max(0f, thickness);
            try
            {
                renderer.SaveRuntimeRenderProfileJson();
                RefreshRenderedProfile();
                status = "Saved default sprite settings.";
            }
            catch (Exception exception)
            {
                status = exception.Message;
            }
        }

        private void CaptureSelection(byte pattern, byte palette, uint hash, NesElementType element, string description)
        {
            selectedPattern = pattern;
            selectedPalette = palette;
            selectedHash = hash;
            selectedElement = element;
            hasSelection = true;
            selectedDescription = description;
            NesRenderRule existing = FindApplicableRule(renderer.RenderProfile, pattern, palette, hash, element);
            ruleName = existing?.Name ?? $"{element} ${pattern:X2} Palette {(tab == PickerTab.Chr && chrPalette < 0 ? "Any" : palette.ToString())}";
            float defaultDepth = 0f;
            if (tab == PickerTab.Background)
                defaultDepth = renderer.GetTileDepth(selectedTileX, selectedTileY);
            else if (tab == PickerTab.Sprites)
                defaultDepth = renderer.GetSpriteDepth(selectedSprite);
            depthText = Format(existing?.Depth ?? defaultDepth);
            thicknessText = Format(existing?.Thickness > 0 ? existing.Thickness : 1f);
            usePixelBaseDepth = existing?.UsePixelExtrusionBaseDepth ?? false;
            pixelBaseDepthText = Format(existing?.PixelExtrusionBaseDepth ??
                ((existing?.Depth ?? defaultDepth) +
                 (existing?.Thickness > 0 ? existing.Thickness : 1f)));
            surfaceWidthText = (existing != null ? Mathf.Max(1, existing.SurfaceUnitWidth) : 1).ToString();
            surfaceHeightText = (existing != null ? Mathf.Max(1, existing.SurfaceUnitHeight) : 1).ToString();
            geometry = existing?.EffectiveGeometry ?? NesGeometryType.Flat;
            var cartridge = runner?.Machine?.Cartridge;
            bool dynamicChrDefault = cartridge != null &&
                (cartridge.HasChrRam || cartridge.ChrRom.Length > 8 * 1024);
            matchTileHash = existing?.MatchTileHash ?? dynamicChrDefault;
            matchAnyPattern = existing?.MatchAnyPattern ??
                (dynamicChrDefault ||
                 tab == PickerTab.Chr && chrPreviewMode == ChrPreviewMode.Page1K);
            hide = existing?.Hide ?? false;
            excludedColors = existing?.PixelExtrusionExcludedColorMask ?? 0;
            status = "";
        }

        private void SaveRules()
        {
            if (renderer?.RenderProfile == null) { status = "No ROM profile is loaded."; return; }
            if (!TryFloat(depthText, out float depth) || !TryFloat(thicknessText, out float thickness) ||
                !TryFloat(pixelBaseDepthText, out float pixelBaseDepth))
            {
                status = "Depth or Thickness is invalid."; return;
            }
            int.TryParse(surfaceWidthText, out int unitWidth);
            int.TryParse(surfaceHeightText, out int unitHeight);
            NesRenderProfile profile = renderer.RenderProfile;
            List<SelectionTarget> targets = GetSelectionTargets();
            foreach (SelectionTarget target in targets)
                SaveRule(profile, target, depth, thickness, pixelBaseDepth, unitWidth, unitHeight);
            try
            {
                renderer.SaveRuntimeRenderProfileJson();
                RefreshRenderedProfile();
                status = $"Saved {targets.Count} selected rule(s).";
            }
            catch (Exception exception)
            {
                status = exception.Message;
            }
        }

        private void SaveRule(NesRenderProfile profile, SelectionTarget target, float depth,
            float thickness, float pixelBaseDepth, int unitWidth, int unitHeight)
        {
            NesRenderRule rule = FindExactRule(profile, target.Pattern, target.Palette, target.Hash,
                matchTileHash, matchAnyPattern, target.Element);
            if (rule == null)
            {
                int count = profile.Rules?.Length ?? 0;
                var rules = new NesRenderRule[count + 1];
                rule = new NesRenderRule();
                rules[0] = rule;
                if (count > 0) Array.Copy(profile.Rules, 0, rules, 1, count);
                profile.Rules = rules;
            }
            string baseName = string.IsNullOrWhiteSpace(ruleName) ? target.Element.ToString() : ruleName;
            rule.Name = GetSelectionCount() > 1 ? $"{baseName} ${target.Pattern:X2}" : baseName;
            rule.PatternIndexMin = rule.PatternIndexMax = target.Pattern;
            rule.PaletteIndex = target.Palette;
            rule.MatchTileHash = matchTileHash;
            rule.MatchAnyPattern = matchTileHash && matchAnyPattern;
            rule.TileHash = matchTileHash ? target.Hash : 0;
            rule.ElementType = target.Element;
            rule.Depth = depth;
            rule.Geometry = geometry;
            rule.UseBoxMesh = geometry == NesGeometryType.Box;
            rule.Thickness = geometry == NesGeometryType.Flat ? 0 : Mathf.Max(0, thickness);
            rule.SurfaceUnitWidth = Mathf.Max(1, unitWidth);
            rule.SurfaceUnitHeight = Mathf.Max(1, unitHeight);
            rule.PixelExtrusionExcludedColorMask = geometry == NesGeometryType.PixelExtrusion
                ? excludedColors & 0x0E : 0;
            rule.UsePixelExtrusionBaseDepth = geometry == NesGeometryType.PixelExtrusion &&
                target.Element == NesElementType.Background && usePixelBaseDepth;
            rule.PixelExtrusionBaseDepth = rule.UsePixelExtrusionBaseDepth
                ? pixelBaseDepth : 0f;
            rule.Hide = hide;
        }

        private void RefreshRenderedProfile()
        {
            if (renderer == null || runner?.Machine == null) return;
            renderer.ReapplyProfile(runner.Machine.GetSceneSnapshot());
        }

        private void HandleDragSelection(Rect pickerRect, PickerTab pickerTab,
            Action<Rect, bool> applySelection)
        {
            Event e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && pickerRect.Contains(e.mousePosition))
            {
                selectionDragging = true;
                selectionDragTab = pickerTab;
                selectionDragStart = selectionDragCurrent = e.mousePosition;
                e.Use();
            }
            else if (selectionDragging && selectionDragTab == pickerTab &&
                e.type == EventType.MouseDrag && e.button == 0)
            {
                selectionDragCurrent = e.mousePosition;
                e.Use();
            }
            else if (selectionDragging && selectionDragTab == pickerTab &&
                e.type == EventType.MouseUp && e.button == 0)
            {
                selectionDragCurrent = e.mousePosition;
                Rect selection = NormalizedRect(selectionDragStart, selectionDragCurrent);
                if (selection.width < 3 && selection.height < 3)
                    selection = new Rect(selectionDragCurrent.x - 1, selectionDragCurrent.y - 1, 2, 2);
                bool toggle = e.control || e.command;
                applySelection(selection, toggle);
                selectionDragging = false;
                e.Use();
            }

            if (selectionDragging && selectionDragTab == pickerTab)
                DrawBorder(NormalizedRect(selectionDragStart, selectionDragCurrent), Color.cyan, 2);
        }

        private static Rect NormalizedRect(Vector2 a, Vector2 b) => Rect.MinMaxRect(
            Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y),
            Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));

        private static void ToggleOrAdd(HashSet<int> selection, int value, bool toggle)
        {
            if (toggle && selection.Contains(value)) selection.Remove(value);
            else selection.Add(value);
        }

        private void RefreshPrimarySelection()
        {
            if (tab == PickerTab.Defaults)
            {
                hasSelection = false;
                selectedDescription = "";
                status = "";
                return;
            }
            hasSelection = GetSelectionCount() > 0;
            if (!hasSelection)
            {
                selectedDescription = "Select one or more tiles.";
                status = "";
                return;
            }

            if (tab == PickerTab.Background)
            {
                selectedTileX = selectedTileY = 0;
                foreach (int index in selectedTiles)
                {
                    selectedTileX = index % TileMeshFactory.Columns;
                    selectedTileY = index / TileMeshFactory.Columns;
                    break;
                }
                byte pattern = renderer.GetTilePattern(selectedTileX, selectedTileY);
                byte palette = renderer.GetTilePalette(selectedTileX, selectedTileY);
                uint hash = renderer.GetTileHash(selectedTileX, selectedTileY);
                CaptureSelection(pattern, palette, hash, NesElementType.Background,
                    $"Background: {selectedTiles.Count} selected (primary ${pattern:X2}, palette {palette})");
            }
            else if (tab == PickerTab.Sprites)
            {
                selectedSprite = 0;
                foreach (int index in selectedSprites) { selectedSprite = index; break; }
                CaptureSelection(renderer.GetSpritePattern(selectedSprite),
                    renderer.GetSpritePalette(selectedSprite), renderer.GetSpriteHash(selectedSprite),
                    NesElementType.Sprite,
                    $"Sprites: {selectedSprites.Count} selected (primary #{selectedSprite})");
            }
            else
            {
                selectedChr = 0;
                foreach (int chr in selectedChrs) { selectedChr = chr; break; }
                int table = selectedChr / 256;
                int pattern = selectedChr & 255;
                uint hash = ComputeChrHash(table, pattern);
                CaptureSelection((byte)pattern, (byte)Mathf.Max(0, chrPalette), hash, chrElementType,
                    $"CHR: {selectedChrs.Count} selected (primary table {table}, ${pattern:X2})");
            }
        }

        private int GetSelectionCount()
        {
            if (tab == PickerTab.Defaults) return 0;
            if (tab == PickerTab.Background) return selectedTiles.Count;
            if (tab == PickerTab.Sprites) return selectedSprites.Count;
            return selectedChrs.Count;
        }

        private List<SelectionTarget> GetSelectionTargets()
        {
            var targets = new List<SelectionTarget>();
            var unique = new HashSet<string>();
            if (tab == PickerTab.Background)
            {
                foreach (int index in selectedTiles)
                {
                    int x = index % TileMeshFactory.Columns;
                    int y = index / TileMeshFactory.Columns;
                    AddUniqueTarget(targets, unique, renderer.GetTilePattern(x, y),
                        renderer.GetTilePalette(x, y), renderer.GetTileHash(x, y),
                        NesElementType.Background);
                }
            }
            else if (tab == PickerTab.Sprites)
            {
                foreach (int index in selectedSprites)
                    AddUniqueTarget(targets, unique, renderer.GetSpritePattern(index),
                        renderer.GetSpritePalette(index), renderer.GetSpriteHash(index),
                        NesElementType.Sprite);
            }
            else
            {
                foreach (int chr in selectedChrs)
                {
                    int table = chr / 256;
                    int pattern = chr & 255;
                    AddUniqueTarget(targets, unique, (byte)pattern, chrPalette,
                        ComputeChrHash(table, pattern), chrElementType);
                }
            }
            return targets;
        }

        private void AddUniqueTarget(List<SelectionTarget> targets, HashSet<string> unique,
            byte pattern, int palette, uint hash, NesElementType element)
        {
            string key = $"{(int)element}:{(matchTileHash && matchAnyPattern ? -1 : pattern)}:{palette}:{(matchTileHash ? hash : 0)}";
            if (unique.Add(key)) targets.Add(new SelectionTarget(pattern, palette, hash, element));
        }

        private readonly struct SelectionTarget
        {
            public readonly byte Pattern;
            public readonly int Palette;
            public readonly uint Hash;
            public readonly NesElementType Element;

            public SelectionTarget(byte pattern, int palette, uint hash, NesElementType element)
            {
                Pattern = pattern;
                Palette = palette;
                Hash = hash;
                Element = element;
            }
        }

        private void BuildChrAtlas()
        {
            if (runner?.Machine?.Cartridge?.Mapper == null) return;
            if (chrAtlas == null)
            {
                chrAtlas = new Texture2D(256, 128, TextureFormat.RGBA32, false)
                {
                    name = "PortalNes Runtime CHR Atlas",
                    filterMode = FilterMode.Point
                };
            }
            var colors = new Color32[256 * 128];
            Color32[] shades =
            {
                new Color32(20,20,20,255), new Color32(90,90,90,255),
                new Color32(170,170,170,255), new Color32(245,245,245,255)
            };
            for (int table = 0; table < 2; table++)
            for (int tile = 0; tile < 256; tile++)
            for (int row = 0; row < 8; row++)
            {
                int address = table * 0x1000 + tile * 16 + row;
                byte lo = ReadChrPreview(address);
                byte hi = ReadChrPreview(address + 8);
                int tileX = table * 16 + tile % 16, tileY = tile / 16;
                for (int x = 0; x < 8; x++)
                {
                    int bit = 7 - x;
                    int value = ((lo >> bit) & 1) | (((hi >> bit) & 1) << 1);
                    int px = tileX * 8 + x, py = 127 - (tileY * 8 + row);
                    colors[py * 256 + px] = shades[value];
                }
            }
            chrAtlas.SetPixels32(colors);
            chrAtlas.Apply(false, false);
        }

        private uint ComputeChrHash(int table, int pattern)
        {
            const uint basis = 2166136261u, prime = 16777619u;
            uint hash = basis;
            int address = table * 0x1000 + pattern * 16;
            for (int i = 0; i < 16; i++)
                hash = (hash ^ ReadChrPreview(address + i)) * prime;
            return hash;
        }

        private int GetChrPreviewBankCount()
        {
            int length = runner?.Machine?.Cartridge?.ChrRom?.Length ?? 0;
            return Mathf.Max(1, (length + 0x1FFF) / 0x2000);
        }

        private int GetChrPreviewPageCount()
        {
            int length = runner?.Machine?.Cartridge?.ChrRom?.Length ?? 0;
            return Mathf.Max(1, (length + 0x3FF) / 0x400);
        }

        private int CurrentChrPreviewItem() =>
            chrPreviewMode == ChrPreviewMode.Page1K ? chrPreviewPage : chrPreviewBank;

        private string ChrPreviewLabel()
        {
            if (chrPreviewMode == ChrPreviewMode.Live) return "Current PPU layout";
            if (chrPreviewMode == ChrPreviewMode.Page1K)
                return $"CHR 1KB Page {chrPreviewPage}/{GetChrPreviewPageCount() - 1}";
            return $"CHR 8KB Bank {chrPreviewBank}/{GetChrPreviewBankCount() - 1}";
        }

        private void SetChrPreviewMode(ChrPreviewMode mode)
        {
            chrPreviewMode = mode;
            if (mode == ChrPreviewMode.Live) chrPreviewBank = -1;
            else if (mode == ChrPreviewMode.Bank8K && chrPreviewBank < 0) chrPreviewBank = 0;
            if (mode == ChrPreviewMode.Page1K)
            {
                matchTileHash = true;
                matchAnyPattern = true;
            }
            ResetChrPreviewSelection();
        }

        private void SetChrPreviewItem(int item)
        {
            if (chrPreviewMode == ChrPreviewMode.Page1K)
            {
                int count = GetChrPreviewPageCount();
                chrPreviewPage = (item % count + count) % count;
                chrPreviewPageText = chrPreviewPage.ToString(CultureInfo.InvariantCulture);
            }
            else if (chrPreviewMode == ChrPreviewMode.Bank8K)
            {
                int count = GetChrPreviewBankCount();
                chrPreviewBank = (item % count + count) % count;
                chrPreviewBankText = chrPreviewBank.ToString(CultureInfo.InvariantCulture);
            }
            ResetChrPreviewSelection();
        }

        private void ResetChrPreviewSelection()
        {
            selectedChrs.Clear();
            hasSelection = false;
            selectedDescription = "Select one or more tiles.";
            BuildChrAtlas();
        }

        private byte ReadChrPreview(int ppuAddress)
        {
            var cartridge = runner?.Machine?.Cartridge;
            if (cartridge == null) return 0;
            if (chrPreviewMode == ChrPreviewMode.Live || cartridge.HasChrRam)
                return cartridge.Mapper.PpuRead((ushort)(ppuAddress & 0x1FFF));
            int address;
            if (chrPreviewMode == ChrPreviewMode.Page1K)
            {
                if ((ppuAddress & 0x1FFF) >= 0x400) return 0;
                address = chrPreviewPage * 0x400 + (ppuAddress & 0x3FF);
            }
            else address = chrPreviewBank * 0x2000 + (ppuAddress & 0x1FFF);
            return address < cartridge.ChrRom.Length ? cartridge.ChrRom[address] : (byte)0;
        }

        private static NesRenderRule FindApplicableRule(NesRenderProfile profile, byte pattern,
            byte palette, uint hash, NesElementType element)
        {
            if (profile?.Rules == null) return null;
            foreach (NesRenderRule rule in profile.Rules)
                if (Applies(rule, pattern, palette, hash, element) &&
                    rule.MatchTileHash && !rule.MatchAnyPattern) return rule;
            foreach (NesRenderRule rule in profile.Rules)
                if (Applies(rule, pattern, palette, hash, element) &&
                    rule.MatchTileHash && rule.MatchAnyPattern) return rule;
            foreach (NesRenderRule rule in profile.Rules)
                if (Applies(rule, pattern, palette, hash, element)) return rule;
            return null;
        }

        private static bool Applies(NesRenderRule rule, byte pattern, byte palette, uint hash,
            NesElementType element) =>
            rule != null && rule.ElementType == element &&
            (rule.MatchAnyPattern ||
             pattern >= rule.PatternIndexMin && pattern <= rule.PatternIndexMax) &&
            (rule.PaletteIndex < 0 || rule.PaletteIndex == palette) &&
            (!rule.MatchTileHash || rule.TileHash == hash);

        private static NesRenderRule FindExactRule(NesRenderProfile profile, byte pattern, int palette,
            uint hash, bool matchHash, bool matchAnyPattern, NesElementType element) =>
            profile?.Rules == null ? null : Array.Find(profile.Rules, rule => rule != null &&
            rule.ElementType == element && rule.MatchAnyPattern == (matchHash && matchAnyPattern) &&
            (rule.MatchAnyPattern || (rule.PatternIndexMin == pattern &&
             rule.PatternIndexMax == pattern)) && rule.PaletteIndex == palette &&
            rule.MatchTileHash == matchHash && (!matchHash || rule.TileHash == hash));

        private static Vector2Int ScreenPixelAt(Rect rect, Vector2 position) => new Vector2Int(
            Mathf.Clamp((int)((position.x - rect.x) / rect.width * 256), 0, 255),
            Mathf.Clamp((int)((position.y - rect.y) / rect.height * 240), 0, 239));

        private static Rect ToDisplayRect(Rect display, RectInt pixels) => new Rect(
            display.x + pixels.x / 256f * display.width,
            display.y + pixels.y / 240f * display.height,
            pixels.width / 256f * display.width,
            pixels.height / 240f * display.height);

        private static void DrawBorder(Rect rect, Color color, float width)
        {
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, width), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, color, 0, 0);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - width, rect.width, width), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, color, 0, 0);
            GUI.DrawTexture(new Rect(rect.x, rect.y, width, rect.height), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, color, 0, 0);
            GUI.DrawTexture(new Rect(rect.xMax - width, rect.y, width, rect.height), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, color, 0, 0);
        }

        private static string Format(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
        private static bool TryFloat(string text, out float value) =>
            float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}
