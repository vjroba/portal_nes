using System;
using System.Collections.Generic;
using PortalNes.Rendering3D;
using UnityEditor;
using UnityEngine;

namespace PortalNes.Editor
{
    [CustomEditor(typeof(NesSceneRenderer))]
    public sealed class NesSceneRendererEditor : UnityEditor.Editor
    {
        private int loadedPattern = -1;
        private int loadedPalette = -1;
        private uint loadedTileHash;
        private NesElementType loadedElementType = (NesElementType)(-1);
        private string ruleName;
        private float ruleDepth;
        private float ruleThickness;
        private int ruleSurfaceUnitWidth = 1;
        private int ruleSurfaceUnitHeight = 1;
        private bool ruleUseBox;
        private bool ruleHide;
        private bool ruleMatchTileContents = true;
        private int ruleExcludedPatternColors;
        private NesGeometryType ruleGeometry;
        private float ruleBevel = .12f;
        private int ruleCylinderSegments = 12;
        private Mesh ruleCustomMesh;
        private Vector3 ruleGeometryOffset;
        private Vector3 ruleGeometryRotation;
        private Vector3 ruleGeometryScale = Vector3.one;
        private readonly bool[] selectedScreenTiles = new bool[TileMeshFactory.Columns * TileMeshFactory.Rows];
        private int dragStart = -1;
        private int dragCurrent = -1;
        private NesElementType pickerElementType;
        private int selectedSpriteIndex;
        private bool hasLatchedSprite;
        private byte latchedSpritePattern;
        private byte latchedSpritePalette;
        private uint latchedSpriteHash;
        private float latchedSpriteDepth;
        private RectInt latchedSpriteRect;
        private string latchedSpriteInfo;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var renderer = (NesSceneRenderer)target;
            EditorGUILayout.Space();
            pickerElementType = (NesElementType)GUILayout.Toolbar((int)pickerElementType,
                new[] { "Background", "Sprites" });
            if (pickerElementType == NesElementType.Background) DrawTilePicker(renderer);
            else DrawSpritePicker(renderer);
            EditorGUILayout.HelpBox(pickerElementType == NesElementType.Background
                ? renderer.SelectedTileInfo : latchedSpriteInfo ?? "Click a visible sprite to select it.", MessageType.Info);
            DrawSelectedTileRuleEditor(renderer);
            if (renderer.RenderProfile == null)
                EditorGUILayout.HelpBox("Assign a Render Profile before adding a rule.", MessageType.Warning);
            else if (!EditorApplication.isPlaying)
                EditorGUILayout.HelpBox("Enter Play Mode so the selected tile contains captured PPU data.", MessageType.None);
            if (EditorApplication.isPlaying) Repaint();
        }

        private void DrawSelectedTileRuleEditor(NesSceneRenderer renderer)
        {
            if (renderer.RenderProfile == null || !EditorApplication.isPlaying) return;
            if (pickerElementType == NesElementType.Sprite && !hasLatchedSprite) return;
            byte selectedPattern = pickerElementType == NesElementType.Background
                ? renderer.SelectedTilePattern : latchedSpritePattern;
            byte selectedPalette = pickerElementType == NesElementType.Background
                ? renderer.SelectedTilePalette : latchedSpritePalette;
            uint selectedHash = pickerElementType == NesElementType.Background
                ? renderer.SelectedTileHash : latchedSpriteHash;
            if (loadedPattern != selectedPattern || loadedPalette != selectedPalette ||
                loadedTileHash != selectedHash || loadedElementType != pickerElementType)
                LoadSelectedRule(renderer);

            EditorGUILayout.Space();
            int selectedCount = pickerElementType == NesElementType.Background ? CountSelectedScreenTiles() : 1;
            EditorGUILayout.LabelField($"Selected {pickerElementType} Rule ({Mathf.Max(1, selectedCount)} item(s))", EditorStyles.boldLabel);
            if (pickerElementType == NesElementType.Background && selectedCount > 0 && GUILayout.Button("Clear Multi-Selection"))
            {
                Array.Clear(selectedScreenTiles, 0, selectedScreenTiles.Length);
                selectedCount = 0;
            }
            ruleName = EditorGUILayout.TextField("Rule Name", ruleName);
            ruleDepth = EditorGUILayout.FloatField("Depth", ruleDepth);
            ruleGeometry = (NesGeometryType)EditorGUILayout.EnumPopup("Geometry", ruleGeometry);
            ruleUseBox = ruleGeometry == NesGeometryType.Box;
            using (new EditorGUI.DisabledScope(ruleGeometry == NesGeometryType.Flat))
                ruleThickness = EditorGUILayout.FloatField("Thickness", ruleThickness);
            if (ruleGeometry == NesGeometryType.Box)
            {
                ruleSurfaceUnitWidth = EditorGUILayout.IntSlider("Surface Unit Width", ruleSurfaceUnitWidth, 1, 8);
                ruleSurfaceUnitHeight = EditorGUILayout.IntSlider("Surface Unit Height", ruleSurfaceUnitHeight, 1, 8);
            }
            if (ruleGeometry == NesGeometryType.BeveledBox)
                ruleBevel = EditorGUILayout.Slider("Bevel", ruleBevel, .001f, .49f);
            if (ruleGeometry == NesGeometryType.Cylinder)
                ruleCylinderSegments = EditorGUILayout.IntSlider("Cylinder Segments", ruleCylinderSegments, 3, 32);
            if (ruleGeometry == NesGeometryType.CustomMesh)
                ruleCustomMesh = (Mesh)EditorGUILayout.ObjectField("Custom Mesh", ruleCustomMesh, typeof(Mesh), false);
            if (ruleGeometry == NesGeometryType.PixelExtrusion)
                ruleExcludedPatternColors = DrawExcludedPatternColors(ruleExcludedPatternColors);
            if (ruleGeometry != NesGeometryType.Flat)
            {
                ruleGeometryOffset = EditorGUILayout.Vector3Field("Geometry Offset", ruleGeometryOffset);
                ruleGeometryRotation = EditorGUILayout.Vector3Field("Geometry Rotation", ruleGeometryRotation);
                ruleGeometryScale = EditorGUILayout.Vector3Field("Geometry Scale", ruleGeometryScale);
            }
            ruleHide = EditorGUILayout.Toggle("Hide", ruleHide);
            ruleMatchTileContents = EditorGUILayout.Toggle(
                new GUIContent("Match Tile Contents", "Prevents the rule matching different graphics that reuse this pattern number after CHR bank switches or CHR RAM updates."),
                ruleMatchTileContents);
            if (GUILayout.Button("Add / Update Rules For Selected Tiles"))
            {
                if (pickerElementType == NesElementType.Background) UpsertBackgroundRules(renderer);
                else UpsertSpriteRule(renderer);
            }
        }

        private void LoadSelectedRule(NesSceneRenderer renderer)
        {
            loadedElementType = pickerElementType;
            loadedPattern = pickerElementType == NesElementType.Background
                ? renderer.SelectedTilePattern : latchedSpritePattern;
            loadedPalette = pickerElementType == NesElementType.Background
                ? renderer.SelectedTilePalette : latchedSpritePalette;
            loadedTileHash = pickerElementType == NesElementType.Background
                ? renderer.SelectedTileHash : latchedSpriteHash;
            NesRenderRule existing = FindApplicableRule(renderer.RenderProfile, (byte)loadedPattern,
                (byte)loadedPalette, loadedTileHash, pickerElementType);
            ruleName = existing?.Name ?? $"{pickerElementType} ${loadedPattern:X2} Palette {loadedPalette}";
            ruleDepth = existing?.Depth ?? (pickerElementType == NesElementType.Background
                ? renderer.SelectedTileDepth : latchedSpriteDepth);
            ruleThickness = existing?.Thickness ?? 0.18f;
            ruleSurfaceUnitWidth = existing != null ? Mathf.Max(1, existing.SurfaceUnitWidth) : 1;
            ruleSurfaceUnitHeight = existing != null ? Mathf.Max(1, existing.SurfaceUnitHeight) : 1;
            ruleUseBox = existing?.UseBoxMesh ?? false;
            ruleGeometry = existing?.EffectiveGeometry ?? NesGeometryType.Flat;
            ruleHide = existing?.Hide ?? false;
            ruleMatchTileContents = existing?.MatchTileHash ?? true;
            ruleExcludedPatternColors = existing?.PixelExtrusionExcludedColorMask ?? 0;
            ruleBevel = existing?.Bevel ?? .12f;
            ruleCylinderSegments = existing?.CylinderSegments ?? 12;
            ruleCustomMesh = existing?.CustomMesh;
            ruleGeometryOffset = existing?.GeometryOffset ?? Vector3.zero;
            ruleGeometryRotation = existing?.GeometryRotation ?? Vector3.zero;
            ruleGeometryScale = existing?.GeometryScale ?? Vector3.one;
        }

        private void DrawTilePicker(NesSceneRenderer renderer)
        {
            if (!EditorApplication.isPlaying || renderer.BackgroundTexture == null) return;
            EditorGUILayout.LabelField("Live Background Tile Picker", EditorStyles.boldLabel);
            Rect rect = GUILayoutUtility.GetAspectRect(256f / 240f, GUILayout.MaxHeight(360));
            GUI.DrawTextureWithTexCoords(rect, renderer.BackgroundTexture, new Rect(0, 1, 1, -1));

            Color gridColor = new Color(1, 1, 1, 0.12f);
            var drawnRects = new HashSet<RectInt>();
            for (int index = 0; index < selectedScreenTiles.Length; index++)
            {
                int x = index % TileMeshFactory.Columns, y = index / TileMeshFactory.Columns;
                RectInt tilePixels = renderer.GetPickerTileRect(x, y);
                if (tilePixels.width <= 0 || tilePixels.height <= 0 || !drawnRects.Add(tilePixels)) continue;
                DrawBorder(PickerRect(rect, tilePixels), gridColor, 1);
            }

            for (int index = 0; index < selectedScreenTiles.Length; index++)
            {
                if (!selectedScreenTiles[index]) continue;
                int x = index % TileMeshFactory.Columns, y = index / TileMeshFactory.Columns;
                EditorGUI.DrawRect(PickerRect(rect, renderer.GetPickerTileRect(x, y)),
                    new Color(1, 0.8f, 0, 0.22f));
            }

            Rect selected = PickerRect(rect,
                renderer.GetPickerTileRect(renderer.SelectedTileX, renderer.SelectedTileY));
            DrawBorder(selected, Color.yellow, 2);

            Event current = Event.current;
            if (current.button != 0) return;
            if (current.type == EventType.MouseDown && rect.Contains(current.mousePosition))
            {
                dragStart = dragCurrent = ScreenTileAt(renderer, rect, current.mousePosition);
                current.Use();
            }
            else if (current.type == EventType.MouseDrag && dragStart >= 0)
            {
                dragCurrent = ScreenTileAt(renderer, rect, current.mousePosition); current.Use(); Repaint();
            }
            else if (current.type == EventType.MouseUp && dragStart >= 0)
            {
                dragCurrent = ScreenTileAt(renderer, rect, current.mousePosition);
                ApplyScreenSelection(current.control || current.command);
                int tileX = dragCurrent % TileMeshFactory.Columns, tileY = dragCurrent / TileMeshFactory.Columns;
                Undo.RecordObject(renderer, "Select PortalNes Tiles");
                renderer.SelectDebugTile(tileX, tileY);
                EditorUtility.SetDirty(renderer);
                dragStart = dragCurrent = -1; current.Use(); Repaint();
            }
        }

        private static int ScreenTileAt(NesSceneRenderer renderer, Rect rect, Vector2 position)
        {
            int pixelX = Mathf.Clamp(Mathf.FloorToInt((position.x - rect.x) / rect.width * 256), 0, 255);
            int pixelY = Mathf.Clamp(Mathf.FloorToInt((position.y - rect.y) / rect.height * 240), 0, 239);
            for (int index = 0; index < TileMeshFactory.Columns * TileMeshFactory.Rows; index++)
            {
                RectInt tile = renderer.GetPickerTileRect(index % TileMeshFactory.Columns,
                    index / TileMeshFactory.Columns);
                if (tile.Contains(new Vector2Int(pixelX, pixelY))) return index;
            }
            int fallbackX = pixelX / 8, fallbackY = pixelY / 8;
            return fallbackY * TileMeshFactory.Columns + fallbackX;
        }

        private void DrawSpritePicker(NesSceneRenderer renderer)
        {
            if (!EditorApplication.isPlaying || renderer.BackgroundTexture == null || renderer.SpriteTexture == null) return;
            EditorGUILayout.LabelField("Live Sprite Picker", EditorStyles.boldLabel);
            Rect rect = GUILayoutUtility.GetAspectRect(256f / 240f, GUILayout.MaxHeight(360));
            GUI.DrawTextureWithTexCoords(rect, renderer.BackgroundTexture, new Rect(0, 1, 1, -1));
            GUI.DrawTextureWithTexCoords(rect, renderer.SpriteTexture, new Rect(0, 1, 1, -1));
            for (int i = 63; i >= 0; i--)
            {
                if (!renderer.IsPickerSpriteVisible(i)) continue;
                Rect spriteRect = PickerRect(rect, renderer.GetPickerSpriteRect(i));
                DrawBorder(spriteRect, new Color(1, 1, 1, .35f), 1);
            }
            if (hasLatchedSprite) DrawBorder(PickerRect(rect, latchedSpriteRect), Color.yellow, 2);
            Event current = Event.current;
            if (current.type != EventType.MouseDown || current.button != 0 || !rect.Contains(current.mousePosition)) return;
            for (int i = 0; i < 64; i++)
            {
                if (!renderer.IsPickerSpriteVisible(i) ||
                    !PickerRect(rect, renderer.GetPickerSpriteRect(i)).Contains(current.mousePosition)) continue;
                LatchSprite(renderer, i);
                current.Use(); Repaint();
                break;
            }
        }

        private void LatchSprite(NesSceneRenderer renderer, int index)
        {
            selectedSpriteIndex = index;
            latchedSpritePattern = renderer.GetSpritePattern(index);
            latchedSpritePalette = renderer.GetSpritePalette(index);
            latchedSpriteHash = renderer.GetSpriteHash(index);
            latchedSpriteDepth = renderer.GetSpriteDepth(index);
            latchedSpriteRect = renderer.GetPickerSpriteRect(index);
            latchedSpriteInfo = renderer.GetSpriteInfo(index) + " (selection locked)";
            hasLatchedSprite = true;
            loadedElementType = (NesElementType)(-1);
        }

        private static Rect PickerRect(Rect displayRect, RectInt pixelRect)
        {
            return new Rect(displayRect.x + pixelRect.x / 256f * displayRect.width,
                displayRect.y + pixelRect.y / 240f * displayRect.height,
                pixelRect.width / 256f * displayRect.width,
                pixelRect.height / 240f * displayRect.height);
        }

        private void ApplyScreenSelection(bool additive)
        {
            if (!additive) Array.Clear(selectedScreenTiles, 0, selectedScreenTiles.Length);
            int x0 = dragStart % TileMeshFactory.Columns, y0 = dragStart / TileMeshFactory.Columns;
            int x1 = dragCurrent % TileMeshFactory.Columns, y1 = dragCurrent / TileMeshFactory.Columns;
            for (int y = Mathf.Min(y0, y1); y <= Mathf.Max(y0, y1); y++)
            for (int x = Mathf.Min(x0, x1); x <= Mathf.Max(x0, x1); x++)
            {
                int index = y * TileMeshFactory.Columns + x;
                selectedScreenTiles[index] = additive ? !selectedScreenTiles[index] : true;
            }
        }

        private static void DrawBorder(Rect rect, Color color, float width)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, width), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - width, rect.width, width), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, width, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - width, rect.y, width, rect.height), color);
        }

        private void UpsertBackgroundRules(NesSceneRenderer renderer)
        {
            NesRenderProfile profile = renderer.RenderProfile;
            Undo.RecordObject(profile, "Add or Update PortalNes Render Rules");
            var handled = new HashSet<ulong>();
            bool anySelected = CountSelectedScreenTiles() > 0;
            for (int index = 0; index < selectedScreenTiles.Length; index++)
            {
                if (anySelected && !selectedScreenTiles[index]) continue;
                int x = anySelected ? index % TileMeshFactory.Columns : renderer.SelectedTileX;
                int y = anySelected ? index / TileMeshFactory.Columns : renderer.SelectedTileY;
                byte pattern = renderer.GetTilePattern(x, y), palette = renderer.GetTilePalette(x, y);
                uint tileHash = renderer.GetTileHash(x, y);
                ulong key = ((ulong)tileHash << 16) | ((ulong)pattern << 8) | palette;
                if (handled.Add(key)) UpsertOne(profile, pattern, palette, tileHash,
                    anySelected ? null : ruleName, NesElementType.Background);
                if (!anySelected) break;
            }
            SaveProfile(renderer, profile);
        }

        private static void SaveProfile(NesSceneRenderer renderer, NesRenderProfile profile)
        {
            renderer.InvalidateRuleCache();
            EditorUtility.SetDirty(profile);
            if (renderer.HasRuntimeRenderProfile)
            {
                renderer.SaveRuntimeRenderProfileJson();
                Debug.Log($"Saved PortalNes render profile to '{renderer.RuntimeRenderProfilePath}'.", renderer);
            }
            else
            {
                AssetDatabase.SaveAssetIfDirty(profile);
                EditorGUIUtility.PingObject(profile);
            }
        }

        private void UpsertSpriteRule(NesSceneRenderer renderer)
        {
            NesRenderProfile profile = renderer.RenderProfile;
            Undo.RecordObject(profile, "Add or Update PortalNes Sprite Rule");
            UpsertOne(profile, latchedSpritePattern, latchedSpritePalette, latchedSpriteHash,
                ruleName, NesElementType.Sprite);
            SaveProfile(renderer, profile);
        }

        private void UpsertOne(NesRenderProfile profile, byte pattern, byte palette, uint tileHash,
            string customName, NesElementType elementType)
        {
            NesRenderRule rule = FindExactRule(profile, pattern, palette, tileHash,
                ruleMatchTileContents, elementType);
            if (rule == null)
            {
                int count = profile.Rules?.Length ?? 0;
                var rules = new NesRenderRule[count + 1];
                rule = new NesRenderRule();
                rules[0] = rule;
                if (count > 0) Array.Copy(profile.Rules, 0, rules, 1, count);
                profile.Rules = rules;
            }
            rule.Name = string.IsNullOrWhiteSpace(customName)
                ? $"{elementType} ${pattern:X2} Palette {palette}" : customName;
            rule.PatternIndexMin = pattern;
            rule.PatternIndexMax = pattern;
            rule.PaletteIndex = palette;
            rule.MatchTileHash = ruleMatchTileContents;
            rule.TileHash = ruleMatchTileContents ? tileHash : 0;
            rule.PixelExtrusionExcludedColorMask = ruleGeometry == NesGeometryType.PixelExtrusion
                ? ruleExcludedPatternColors & 0x0E : 0;
            rule.ElementType = elementType;
            rule.Depth = ruleDepth;
            rule.Geometry = ruleGeometry;
            rule.Thickness = ruleGeometry != NesGeometryType.Flat ? Mathf.Max(0, ruleThickness) : 0;
            rule.SurfaceUnitWidth = Mathf.Max(1, ruleSurfaceUnitWidth);
            rule.SurfaceUnitHeight = Mathf.Max(1, ruleSurfaceUnitHeight);
            rule.UseBoxMesh = ruleGeometry == NesGeometryType.Box;
            rule.Bevel = ruleBevel; rule.CylinderSegments = ruleCylinderSegments; rule.CustomMesh = ruleCustomMesh;
            rule.GeometryOffset = ruleGeometryOffset; rule.GeometryRotation = ruleGeometryRotation;
            rule.GeometryScale = ruleGeometryScale;
            rule.Hide = ruleHide;
        }

        private int CountSelectedScreenTiles()
        {
            int count = 0; for (int i = 0; i < selectedScreenTiles.Length; i++) if (selectedScreenTiles[i]) count++;
            return count;
        }

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

        private static NesRenderRule FindExactRule(NesRenderProfile profile, byte pattern, byte palette,
            uint tileHash, bool matchTileHash, NesElementType elementType)
        {
            return profile.Rules == null ? null : Array.Find(profile.Rules, rule => rule != null &&
                rule.ElementType == elementType && rule.PatternIndexMin == pattern &&
                rule.PatternIndexMax == pattern && rule.PaletteIndex == palette &&
                !rule.MatchAnyPattern &&
                rule.MatchTileHash == matchTileHash && (!matchTileHash || rule.TileHash == tileHash));
        }

        private static NesRenderRule FindApplicableRule(NesRenderProfile profile, byte pattern,
            byte palette, uint tileHash, NesElementType elementType)
        {
            if (profile?.Rules == null) return null;
            foreach (NesRenderRule rule in profile.Rules)
                if (RuleApplies(rule, pattern, palette, tileHash, elementType) &&
                    rule.MatchTileHash && !rule.MatchAnyPattern)
                    return rule;
            foreach (NesRenderRule rule in profile.Rules)
                if (RuleApplies(rule, pattern, palette, tileHash, elementType) &&
                    rule.MatchTileHash && rule.MatchAnyPattern)
                    return rule;
            foreach (NesRenderRule rule in profile.Rules)
                if (RuleApplies(rule, pattern, palette, tileHash, elementType)) return rule;
            return null;
        }

        private static bool RuleApplies(NesRenderRule rule, byte pattern, byte palette,
            uint tileHash, NesElementType elementType)
        {
            return rule != null && rule.ElementType == elementType &&
                (rule.MatchAnyPattern ||
                 pattern >= rule.PatternIndexMin && pattern <= rule.PatternIndexMax) &&
                (rule.PaletteIndex < 0 || rule.PaletteIndex == palette) &&
                (!rule.MatchTileHash || rule.TileHash == tileHash);
        }
    }
}
