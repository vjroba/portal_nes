using System;
using System.Collections.Generic;
using System.IO;
using PortalNes.Emulator.Ppu;
using UnityEngine;
using UnityEngine.Rendering;
namespace PortalNes.Rendering3D
{
    public sealed class NesSceneRenderer : MonoBehaviour
    {
        private const float PixelsPerDepthUnit = 8f;
        private const int MaximumRuleCacheEntries = 16384;
        private static readonly Bounds FixedSceneBounds = new Bounds(Vector3.zero,
            new Vector3(512f, 480f, 4096f));
        [SerializeField] private Transform worldRoot;
        [SerializeField, Min(0.001f)] private float worldScale = 0.01f;
        [SerializeField] private float spriteDepth = 0f;
        [SerializeField, Min(0f)] private float automaticSpriteThickness = 1f;
        [SerializeField, Tooltip("Raycasts the 8x8 opaque mask in a proxy box instead of drawing pixel geometry. " +
            "This is usually faster for Portalgraph multi-view rendering.")]
        private bool useShaderPixelExtrusion = true;
        [SerializeField, Tooltip("For both background and sprite Pixel Extrusion, uses the nearest non-matching interior pixel to color side walls.")]
        private bool ignorePixelExtrusionEdgeColor = true;
        [SerializeField] private Color pixelExtrusionIgnoredEdgeColor = Color.black;
        [SerializeField, Range(0f, 0.5f)] private float ignoredEdgeColorTolerance = 0.05f;
        private NesRenderProfile renderProfile;
        private NesRenderProfile runtimeRenderProfile;
        private string runtimeRenderProfilePath;
        [SerializeField, Tooltip("Keeps the top 40 NES pixels flat at HUD Depth. Disabled by default because this is game-specific.")]
        private bool flattenTopHud;
        [SerializeField] private float hudDepth = -0.15f;
        [SerializeField, Range(1, 64)] private int automaticBoxOpaquePixels = 48;
        [SerializeField, Min(0f)] private float automaticBackgroundThickness = 1f;
        [Header("Tile diagnostics")]
        [SerializeField] private bool showTileDebugOverlay;
        [SerializeField] private Camera debugCamera;
        [SerializeField, Range(0, 31)] private int debugTileX;
        [SerializeField, Range(0, 29)] private int debugTileY;
        private Texture2D backgroundTexture;
        private Texture2D spriteTexture;
        private Material backgroundMaterial;
        private Material spriteMaterial;
        private Material spriteCutoutMaterial;
        private Material backgroundVoxelMaterial;
        private Material spriteVoxelMaterial;
        private Mesh voxelProxyMesh;
        private Mesh screenMesh;
        private Mesh backgroundMesh;
        private Vector3[] backgroundVertices;
        private Vector2[] backgroundUv;
        private Mesh spriteMesh;
        private Vector3[] spriteVertices;
        private Vector2[] spriteUv;
        private Mesh extrusionMesh;
        private readonly List<Vector3> extrusionVertices = new List<Vector3>(8192);
        private readonly List<Vector2> extrusionUv = new List<Vector2>(8192);
        private readonly List<int> extrusionTriangles = new List<int>(12288);
        private long presentedFrame = -1;
        private bool hasBackdropColor;
        private Color32 currentBackdropColor;
        private readonly byte[] tilePatterns = new byte[TileMeshFactory.Columns * TileMeshFactory.Rows];
        private readonly byte[] tilePalettes = new byte[TileMeshFactory.Columns * TileMeshFactory.Rows];
        private readonly uint[] tileHashes = new uint[TileMeshFactory.Columns * TileMeshFactory.Rows];
        private readonly byte[] tileOpaqueCounts = new byte[TileMeshFactory.Columns * TileMeshFactory.Rows];
        private readonly float[] tileDepths = new float[TileMeshFactory.Columns * TileMeshFactory.Rows];
        private readonly string[] tileRuleNames = new string[TileMeshFactory.Columns * TileMeshFactory.Rows];
        private readonly RectInt[] pickerTileRects = new RectInt[TileMeshFactory.Columns * TileMeshFactory.Rows];
        private readonly byte[] spritePatterns = new byte[64];
        private readonly byte[] spritePalettes = new byte[64];
        private readonly uint[] spriteHashes = new uint[64];
        private readonly float[] spriteDepths = new float[64];
        private readonly string[] spriteRuleNames = new string[64];
        private readonly RectInt[] pickerSpriteRects = new RectInt[64];
        private readonly bool[] pickerSpriteVisible = new bool[64];
        private GUIStyle debugStyle;
        private readonly Dictionary<int, int> dynamicTileLookup = new Dictionary<int, int>(TileMeshFactory.MaximumTiles);
        private readonly short[] dynamicTileOriginX = new short[TileMeshFactory.MaximumTiles];
        private readonly short[] dynamicTileOriginY = new short[TileMeshFactory.MaximumTiles];
        private readonly byte[] dynamicTilePatterns = new byte[TileMeshFactory.MaximumTiles];
        private readonly byte[] dynamicTilePalettes = new byte[TileMeshFactory.MaximumTiles];
        private readonly uint[] dynamicTileHashes = new uint[TileMeshFactory.MaximumTiles];
        private readonly byte[] dynamicTileOpaque = new byte[TileMeshFactory.MaximumTiles];
        private readonly ulong[] dynamicTileMasks = new ulong[TileMeshFactory.MaximumTiles];
        private readonly ulong[] dynamicTileColor1Masks = new ulong[TileMeshFactory.MaximumTiles];
        private readonly ulong[] dynamicTileColor2Masks = new ulong[TileMeshFactory.MaximumTiles];
        private readonly ulong[] dynamicTileColor3Masks = new ulong[TileMeshFactory.MaximumTiles];
        private readonly NesGeometryType[] dynamicTileGeometry = new NesGeometryType[TileMeshFactory.MaximumTiles];
        private readonly float[] dynamicTileDepth = new float[TileMeshFactory.MaximumTiles];
        private readonly float[] dynamicTileBaseDepth = new float[TileMeshFactory.MaximumTiles];
        private readonly float[] dynamicTileThickness = new float[TileMeshFactory.MaximumTiles];
        private readonly bool[] dynamicTileVisible = new bool[TileMeshFactory.MaximumTiles];
        private readonly NesRenderRule[] dynamicTileRules = new NesRenderRule[TileMeshFactory.MaximumTiles];
        private readonly Dictionary<ulong, NesRenderRule> ruleMatchCache =
            new Dictionary<ulong, NesRenderRule>(1024);
        private int activeBackgroundTiles;
        private readonly List<MeshRenderer> shapedTilePool = new List<MeshRenderer>();
        private readonly List<MeshRenderer> shapedSpritePool = new List<MeshRenderer>();
        private readonly Dictionary<string, Mesh> generatedShapeMeshes = new Dictionary<string, Mesh>();
        private readonly Dictionary<Mesh, PixelInstanceBatch> backgroundPixelBatches =
            new Dictionary<Mesh, PixelInstanceBatch>();
        private readonly Dictionary<Mesh, PixelInstanceBatch> spritePixelBatches =
            new Dictionary<Mesh, PixelInstanceBatch>();
        private MaterialPropertyBlock pixelInstanceProperties;
        private readonly byte[] pixelSideSourceScratch = new byte[64];
        private static readonly int InstanceTexStId = Shader.PropertyToID("_InstanceTexST");
        private static readonly int InstanceMaskId = Shader.PropertyToID("_InstanceMask");
        private static readonly int IgnoredEdgeColorId = Shader.PropertyToID("_IgnoredEdgeColor");
        private static readonly int IgnoredEdgeToleranceId = Shader.PropertyToID("_IgnoredEdgeTolerance");
        private int shapedTilesUsed;
        private int shapedSpritesUsed;
        private MaterialPropertyBlock shapeProperties;

        private sealed class PixelInstanceBatch
        {
            public readonly List<Matrix4x4> LocalMatrices = new List<Matrix4x4>(64);
            public readonly List<Matrix4x4> WorldMatrices = new List<Matrix4x4>(64);
            public readonly List<Vector4> TextureTransforms = new List<Vector4>(64);
            public readonly List<Vector4> Masks = new List<Vector4>(64);

            public void Clear()
            {
                LocalMatrices.Clear();
                WorldMatrices.Clear();
                TextureTransforms.Clear();
                Masks.Clear();
            }
        }
        public Transform WorldRoot => worldRoot;
        public Texture2D BackgroundTexture => backgroundTexture;
        public Texture2D SpriteTexture => spriteTexture;
        public Color32 CurrentBackdropColor => currentBackdropColor;
        public event Action<Color32> BackdropColorChanged;
        public NesRenderProfile RenderProfile => renderProfile;
        public bool HasRuntimeRenderProfile => runtimeRenderProfile != null;
        public string RuntimeRenderProfilePath => runtimeRenderProfilePath;
        public void LoadRenderProfileJson(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A profile path is required.", nameof(path));
            var loadedProfile = ScriptableObject.CreateInstance<NesRenderProfile>();
            loadedProfile.name = Path.GetFileNameWithoutExtension(path);
            try
            {
                JsonUtility.FromJsonOverwrite(File.ReadAllText(path), loadedProfile);
                if (loadedProfile.Rules == null) loadedProfile.Rules = Array.Empty<NesRenderRule>();
                if (loadedProfile.DefaultSpriteGeometry == NesGeometryType.CustomMesh)
                    loadedProfile.DefaultSpriteGeometry = NesGeometryType.PixelExtrusion;
                if (loadedProfile.DefaultBackgroundGeometry == NesGeometryType.CustomMesh)
                    loadedProfile.DefaultBackgroundGeometry = NesGeometryType.Flat;
                foreach (NesRenderRule rule in loadedProfile.Rules)
                {
                    if (rule == null) continue;
                    rule.SurfaceUnitWidth = Mathf.Max(1, rule.SurfaceUnitWidth);
                    rule.SurfaceUnitHeight = Mathf.Max(1, rule.SurfaceUnitHeight);
                    if (rule.Geometry == NesGeometryType.CustomMesh)
                    {
                        rule.Geometry = NesGeometryType.Flat;
                        rule.CustomMesh = null;
                    }
                }
            }
            catch
            {
                Destroy(loadedProfile);
                throw;
            }
            if (runtimeRenderProfile != null) Destroy(runtimeRenderProfile);
            runtimeRenderProfile = loadedProfile;
            runtimeRenderProfilePath = Path.GetFullPath(path);
            renderProfile = loadedProfile;
            InvalidateRuleCache();
        }

        public void InvalidateRuleCache()
        {
            ruleMatchCache.Clear();
        }
        public void SaveRuntimeRenderProfileJson()
        {
            if (runtimeRenderProfile == null || string.IsNullOrWhiteSpace(runtimeRenderProfilePath))
                throw new InvalidOperationException("No external render profile is currently loaded.");
            File.WriteAllText(runtimeRenderProfilePath, JsonUtility.ToJson(runtimeRenderProfile, true));
        }
        public int SelectedTileX => debugTileX;
        public int SelectedTileY => debugTileY;
        public byte SelectedTilePattern => tilePatterns[debugTileY * TileMeshFactory.Columns + debugTileX];
        public byte SelectedTilePalette => tilePalettes[debugTileY * TileMeshFactory.Columns + debugTileX];
        public uint SelectedTileHash => tileHashes[debugTileY * TileMeshFactory.Columns + debugTileX];
        public float SelectedTileDepth => tileDepths[debugTileY * TileMeshFactory.Columns + debugTileX];
        public byte GetTilePattern(int x, int y) => tilePatterns[y * TileMeshFactory.Columns + x];
        public byte GetTilePalette(int x, int y) => tilePalettes[y * TileMeshFactory.Columns + x];
        public uint GetTileHash(int x, int y) => tileHashes[y * TileMeshFactory.Columns + x];
        public float GetTileDepth(int x, int y) => tileDepths[y * TileMeshFactory.Columns + x];
        public RectInt GetPickerTileRect(int x, int y) =>
            pickerTileRects[y * TileMeshFactory.Columns + x];
        public byte GetSpritePattern(int index) => spritePatterns[Mathf.Clamp(index, 0, 63)];
        public byte GetSpritePalette(int index) => spritePalettes[Mathf.Clamp(index, 0, 63)];
        public uint GetSpriteHash(int index) => spriteHashes[Mathf.Clamp(index, 0, 63)];
        public float GetSpriteDepth(int index) => spriteDepths[Mathf.Clamp(index, 0, 63)];
        public RectInt GetPickerSpriteRect(int index) => pickerSpriteRects[Mathf.Clamp(index, 0, 63)];
        public bool IsPickerSpriteVisible(int index) => pickerSpriteVisible[Mathf.Clamp(index, 0, 63)];
        public string GetSpriteInfo(int index)
        {
            index = Mathf.Clamp(index, 0, 63);
            return $"Sprite OAM {index} Pattern=${spritePatterns[index]:X2} Palette={spritePalettes[index]} " +
                   $"Hash={spriteHashes[index]:X8} Depth={spriteDepths[index]:0.###} " +
                   $"Rule={spriteRuleNames[index] ?? "<automatic>"}";
        }
        public void SelectDebugTile(int x, int y)
        {
            debugTileX = Mathf.Clamp(x, 0, TileMeshFactory.Columns - 1);
            debugTileY = Mathf.Clamp(y, 0, TileMeshFactory.Rows - 1);
        }
        public string SelectedTileInfo
        {
            get
            {
                int index = debugTileY * TileMeshFactory.Columns + debugTileX;
                return $"Tile ({debugTileX},{debugTileY}) Pattern=${tilePatterns[index]:X2} " +
                       $"Palette={tilePalettes[index]} Hash={tileHashes[index]:X8} Opaque={tileOpaqueCounts[index]}/64 " +
                       $"Depth={tileDepths[index]:0.###} Rule={tileRuleNames[index] ?? "<automatic>"}";
            }
        }

        private void Awake()
        {
            pixelInstanceProperties = new MaterialPropertyBlock();
            if (worldRoot == null)
            {
                var root = new GameObject("NesWorldRoot");
                root.transform.SetParent(transform, false);
                worldRoot = root.transform;
            }
            worldRoot.localScale = Vector3.one * worldScale;
            screenMesh = TileMeshFactory.CreateScreenQuad("NES Layer Quad");
            backgroundMesh = TileMeshFactory.CreateTileGrid("NES Background Tile Grid", out backgroundVertices, out backgroundUv);
            spriteMesh = TileMeshFactory.CreateSpriteGrid("NES OAM Sprite Grid", out spriteVertices, out spriteUv);
            extrusionMesh = TileMeshFactory.CreateCompactExtrusionMesh("NES Tile Extrusions");
            voxelProxyMesh = TileMeshFactory.CreateUnitBox("NES Shader Pixel Extrusion Proxy");
            backgroundTexture = CreateTexture("NES Background Layer");
            spriteTexture = CreateTexture("NES Sprite Layer");
            backgroundMaterial = CreateMaterial(backgroundTexture, true, true);
            spriteMaterial = CreateMaterial(spriteTexture, true);
            spriteCutoutMaterial = CreateMaterial(spriteTexture, true, true);
            backgroundVoxelMaterial = CreateVoxelMaterial(backgroundTexture);
            spriteVoxelMaterial = CreateVoxelMaterial(spriteTexture);
            backgroundMaterial.enableInstancing = true;
            spriteCutoutMaterial.enableInstancing = true;
            backgroundVoxelMaterial.enableInstancing = true;
            spriteVoxelMaterial.enableInstancing = true;
            backgroundMaterial.SetVector(InstanceTexStId, new Vector4(1, 1, 0, 0));
            spriteCutoutMaterial.SetVector(InstanceTexStId, new Vector4(1, 1, 0, 0));
            CreateLayer("Background Tiles", 0, backgroundMaterial, backgroundMesh);
            CreateLayer("Background Tile Sides", 0, backgroundMaterial, extrusionMesh);
            CreateLayer("Sprites", 0, spriteMaterial, spriteMesh);
        }

        public void Present(PpuSceneSnapshot snapshot)
        {
            if (snapshot == null || snapshot.FrameNumber == presentedFrame) return;
            presentedFrame = snapshot.FrameNumber;
            ClearPixelInstanceBatches(backgroundPixelBatches);
            ClearPixelInstanceBatches(spritePixelBatches);
            PresentBackdropColor(snapshot.BackdropColor);
            backgroundTexture.SetPixelData(snapshot.BackgroundPixels, 0);
            backgroundTexture.Apply(false, false);
            UpdateBackgroundDepths(snapshot);
            spriteTexture.SetPixelData(snapshot.SpritePixels, 0);
            spriteTexture.Apply(false, false);
            UpdateSprites(snapshot);
        }

        public void ReapplyProfile(PpuSceneSnapshot snapshot)
        {
            // Runtime profile editing pauses emulation, so Present would normally
            // reject the unchanged frame number. Rebuild the current frame while
            // preserving, rather than advancing, temporal sprite history.
            if (snapshot == null) return;
            InvalidateRuleCache();
            ClearPixelInstanceBatches(backgroundPixelBatches);
            ClearPixelInstanceBatches(spritePixelBatches);
            PresentBackdropColor(snapshot.BackdropColor);
            backgroundTexture.SetPixelData(snapshot.BackgroundPixels, 0);
            backgroundTexture.Apply(false, false);
            UpdateBackgroundDepths(snapshot);
            spriteTexture.SetPixelData(snapshot.SpritePixels, 0);
            spriteTexture.Apply(false, false);
            UpdateSprites(snapshot);
        }

        private void PresentBackdropColor(uint packedColor)
        {
            var color = new Color32((byte)packedColor, (byte)(packedColor >> 8),
                (byte)(packedColor >> 16), (byte)(packedColor >> 24));
            if (hasBackdropColor && currentBackdropColor.r == color.r && currentBackdropColor.g == color.g &&
                currentBackdropColor.b == color.b && currentBackdropColor.a == color.a) return;
            hasBackdropColor = true;
            currentBackdropColor = color;
            BackdropColorChanged?.Invoke(color);
        }

        private static Texture2D CreateTexture(string name)
        {
            return new Texture2D(PpuFrameBuffer.Width, PpuFrameBuffer.Height, TextureFormat.RGBA32, false)
            { name = name, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        }

        private static Material CreateMaterial(Texture texture, bool transparent, bool cullBack = false)
        {
            Shader shader = cullBack ? Resources.Load<Shader>("PortalNesTransparentCutout") :
                Shader.Find(transparent ? "Sprites/Default" : "Unlit/Texture");
            if (cullBack && shader == null) shader = Shader.Find("PortalNes/Transparent Cutout");
            if (shader == null)
                throw new System.InvalidOperationException("Required PortalNes rendering shader was not included in the player build.");
            var material = new Material(shader) { mainTexture = texture };
            return material;
        }

        private Material CreateVoxelMaterial(Texture texture)
        {
            Material template = Resources.Load<Material>("PortalNesVoxelExtrusionMaterial");
            Shader shader = template != null ? template.shader :
                Resources.Load<Shader>("PortalNesVoxelExtrusion");
            if (shader == null) shader = Shader.Find("PortalNes/Voxel Extrusion");
            if (template == null && shader == null)
                throw new InvalidOperationException("PortalNes voxel extrusion shader was not included.");
            var material = template != null ? new Material(template) : new Material(shader);
            material.mainTexture = texture;
            material.enableInstancing = true;
            material.SetColor(IgnoredEdgeColorId, pixelExtrusionIgnoredEdgeColor);
            material.SetFloat(IgnoredEdgeToleranceId, ignoredEdgeColorTolerance);
            return material;
        }

        private MeshRenderer CreateLayer(string layerName, float depth, Material material, Mesh mesh = null)
        {
            var layer = new GameObject(layerName);
            layer.transform.SetParent(worldRoot, false);
            layer.transform.localPosition = new Vector3(0, 0, depth);
            layer.AddComponent<MeshFilter>().sharedMesh = mesh != null ? mesh : screenMesh;
            MeshRenderer renderer = layer.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            return renderer;
        }

        private void UpdateBackgroundDepths(PpuSceneSnapshot snapshot)
        {
            shapedTilesUsed = 0;
            extrusionVertices.Clear();
            extrusionUv.Clear();
            extrusionTriangles.Clear();
            // Keep a screen-cell view only for the clickable diagnostics UI.
            for (int tileY = 0; tileY < TileMeshFactory.Rows; tileY++)
            for (int tileX = 0; tileX < TileMeshFactory.Columns; tileX++)
            {
                int opaque = 0;
                int center = (tileY * 8 + 4) * PpuFrameBuffer.Width + tileX * 8 + 4;
                int centerX = tileX * 8 + 4, centerY = tileY * 8 + 4;
                int tileOriginX = centerX - snapshot.BackgroundTileLocalX[center];
                int tileOriginY = centerY - snapshot.BackgroundTileLocalY[center];
                int clippedLeft = Mathf.Max(0, tileOriginX);
                int clippedTop = Mathf.Max(0, tileOriginY);
                int clippedRight = Mathf.Min(PpuFrameBuffer.Width, tileOriginX + 8);
                int clippedBottom = Mathf.Min(PpuFrameBuffer.Height, tileOriginY + 8);
                for (int py = 0; py < 8; py++)
                for (int px = 0; px < 8; px++)
                    opaque += snapshot.BackgroundOpaque[(tileY * 8 + py) * PpuFrameBuffer.Width + tileX * 8 + px];

                // Unmatched tiles share one neutral plane.  Their amount of
                // opaque ink is a visual property, not reliable depth data.
                float depth = flattenTopHud && tileY < 5 ? hudDepth : 0f;
                byte pattern = snapshot.BackgroundPattern[center];
                byte palette = snapshot.BackgroundPalette[center];
                uint tileHash = snapshot.BackgroundTileHash[center];
                NesRenderRule rule = FindRule(pattern, palette, tileHash);
                if (rule != null) depth = rule.Hide ? 100f : rule.Depth;
                else if (opaque > 0 && renderProfile != null && renderProfile.UseDefaultBackgroundSettings)
                    depth = renderProfile.DefaultBackgroundDepth;
                int tileIndex = tileY * TileMeshFactory.Columns + tileX;
                pickerTileRects[tileIndex] = new RectInt(clippedLeft, clippedTop,
                    Mathf.Max(0, clippedRight - clippedLeft), Mathf.Max(0, clippedBottom - clippedTop));
                tilePatterns[tileIndex] = pattern;
                tilePalettes[tileIndex] = palette;
                tileHashes[tileIndex] = tileHash;
                tileOpaqueCounts[tileIndex] = (byte)opaque;
                tileDepths[tileIndex] = depth;
                tileRuleNames[tileIndex] = rule?.Name;
            }

            // Reconstruct actual PPU tile instances. Origins may be negative at a
            // clipped edge and may differ by scanline at a split-screen boundary.
            dynamicTileLookup.Clear();
            int tileCount = 0;
            for (int y = 0; y < PpuFrameBuffer.Height; y++)
            for (int x = 0; x < PpuFrameBuffer.Width; x++)
            {
                int pixel = y * PpuFrameBuffer.Width + x;
                int originX = x - snapshot.BackgroundTileLocalX[pixel];
                int originY = y - snapshot.BackgroundTileLocalY[pixel];
                int key = ((originY + 8) << 10) | (originX + 8);
                if (!dynamicTileLookup.TryGetValue(key, out int tileIndex))
                {
                    if (tileCount >= TileMeshFactory.MaximumTiles) continue;
                    tileIndex = tileCount++;
                    dynamicTileLookup.Add(key, tileIndex);
                    dynamicTileOriginX[tileIndex] = (short)originX;
                    dynamicTileOriginY[tileIndex] = (short)originY;
                    dynamicTilePatterns[tileIndex] = snapshot.BackgroundPattern[pixel];
                    dynamicTilePalettes[tileIndex] = snapshot.BackgroundPalette[pixel];
                    dynamicTileHashes[tileIndex] = snapshot.BackgroundTileHash[pixel];
                    dynamicTileOpaque[tileIndex] = 0;
                    dynamicTileMasks[tileIndex] = 0;
                    dynamicTileColor1Masks[tileIndex] = 0;
                    dynamicTileColor2Masks[tileIndex] = 0;
                    dynamicTileColor3Masks[tileIndex] = 0;
                }
                if (snapshot.BackgroundOpaque[pixel] != 0 && dynamicTileOpaque[tileIndex] < 64)
                {
                    dynamicTileOpaque[tileIndex]++;
                    int localX = snapshot.BackgroundTileLocalX[pixel];
                    int localY = snapshot.BackgroundTileLocalY[pixel];
                    ulong pixelBit = 1UL << (localY * 8 + localX);
                    dynamicTileMasks[tileIndex] |= pixelBit;
                    switch (snapshot.BackgroundPatternColor[pixel])
                    {
                        case 1: dynamicTileColor1Masks[tileIndex] |= pixelBit; break;
                        case 2: dynamicTileColor2Masks[tileIndex] |= pixelBit; break;
                        case 3: dynamicTileColor3Masks[tileIndex] |= pixelBit; break;
                    }
                }
            }

            // Resolve every tile first so adjacent boxes can omit their shared,
            // otherwise visible, internal faces.
            for (int tileIndex = 0; tileIndex < tileCount; tileIndex++)
            {
                int originX = dynamicTileOriginX[tileIndex], originY = dynamicTileOriginY[tileIndex];
                int left = Mathf.Max(0, originX), top = Mathf.Max(0, originY);
                int right = Mathf.Min(256, originX + 8), bottom = Mathf.Min(240, originY + 8);
                int opaque = dynamicTileOpaque[tileIndex];
                byte pattern = dynamicTilePatterns[tileIndex], palette = dynamicTilePalettes[tileIndex];
                uint tileHash = dynamicTileHashes[tileIndex];
                float depth = flattenTopHud && originY < 40 ? hudDepth : 0f;
                NesRenderRule rule = FindRule(pattern, palette, tileHash);
                if (rule != null) depth = rule.Hide ? 100f : rule.Depth;
                else if (opaque > 0 && renderProfile != null && renderProfile.UseDefaultBackgroundSettings)
                    depth = renderProfile.DefaultBackgroundDepth;
                float scaledDepth = depth * PixelsPerDepthUnit;
                bool visible = right > left && bottom > top && (rule == null || !rule.Hide);
                NesGeometryType geometry = rule?.EffectiveGeometry ??
                    (opaque > 0 && renderProfile != null && renderProfile.UseDefaultBackgroundSettings
                        ? renderProfile.DefaultBackgroundGeometry
                        : ((!flattenTopHud || originY >= 40) && opaque >= automaticBoxOpaquePixels
                            ? NesGeometryType.Box : NesGeometryType.Flat));
                dynamicTileRules[tileIndex] = rule;
                dynamicTileGeometry[tileIndex] = geometry;
                dynamicTileDepth[tileIndex] = scaledDepth;
                float defaultThickness = renderProfile != null && renderProfile.UseDefaultBackgroundSettings
                    ? renderProfile.DefaultBackgroundThickness : automaticBackgroundThickness;
                float thickness = rule != null && rule.Thickness > 0
                    ? rule.Thickness : defaultThickness;
                dynamicTileThickness[tileIndex] = Mathf.Max(0f, thickness) * PixelsPerDepthUnit;
                // Colors excluded from Pixel Extrusion stay on its rear/base
                // plane. This keeps them behind the raised pixels without
                // incorrectly falling back to absolute Depth 0.
                dynamicTileBaseDepth[tileIndex] = geometry == NesGeometryType.PixelExtrusion &&
                    rule != null && rule.UsePixelExtrusionBaseDepth
                        ? rule.PixelExtrusionBaseDepth * PixelsPerDepthUnit
                        : scaledDepth + dynamicTileThickness[tileIndex];
                dynamicTileVisible[tileIndex] = visible;
            }

            for (int tileIndex = 0; tileIndex < tileCount; tileIndex++)
            {
                int originX = dynamicTileOriginX[tileIndex], originY = dynamicTileOriginY[tileIndex];
                int left = Mathf.Max(0, originX), top = Mathf.Max(0, originY);
                int right = Mathf.Min(256, originX + 8), bottom = Mathf.Min(240, originY + 8);
                NesRenderRule rule = dynamicTileRules[tileIndex];
                NesGeometryType geometry = dynamicTileGeometry[tileIndex];
                float scaledDepth = dynamicTileDepth[tileIndex];
                float scaledThickness = dynamicTileThickness[tileIndex];
                bool visible = dynamicTileVisible[tileIndex];
                bool shaped = geometry == NesGeometryType.BeveledBox || geometry == NesGeometryType.Cylinder ||
                              geometry == NesGeometryType.PixelExtrusion || geometry == NesGeometryType.CustomMesh;
                TileMeshFactory.SetTileQuad(backgroundVertices, backgroundUv, tileIndex,
                    left, top, right, bottom, scaledDepth, visible && !shaped);

                bool useBox = geometry == NesGeometryType.Box;
                int surfaceUnitWidth = rule != null ? Mathf.Max(1, rule.SurfaceUnitWidth) : 1;
                int surfaceUnitHeight = rule != null ? Mathf.Max(1, rule.SurfaceUnitHeight) : 1;
                if (visible && useBox)
                    TileMeshFactory.AppendTileExtrusion(extrusionVertices, extrusionUv, extrusionTriangles,
                        left, top, right, bottom, scaledDepth, scaledThickness,
                        !HasMatchingBoxNeighbor(tileIndex, originX - 8, originY),
                        !HasMatchingBoxNeighbor(tileIndex, originX + 8, originY),
                        !HasMatchingBoxNeighbor(tileIndex, originX, originY - 8),
                        !HasMatchingBoxNeighbor(tileIndex, originX, originY + 8),
                        originX, originY, surfaceUnitWidth, surfaceUnitHeight);
                ulong shapeMask = ApplyExcludedPatternColors(tileIndex, rule, geometry);
                ulong baseMask = geometry == NesGeometryType.PixelExtrusion
                    ? dynamicTileMasks[tileIndex] & ~shapeMask : 0;
                if (visible && baseMask != 0)
                    TileMeshFactory.AppendPixelFaces(extrusionVertices, extrusionUv, extrusionTriangles,
                        baseMask, originX, originY, dynamicTileBaseDepth[tileIndex]);
                if (visible && shaped && shapeMask != 0)
                {
                    if (geometry == NesGeometryType.PixelExtrusion && CanInstancePixelExtrusion(
                            rule, originX, originY))
                        QueuePixelExtrusion(backgroundPixelBatches, rule, shapeMask,
                            originX, originY, scaledDepth, scaledThickness,
                            snapshot.BackgroundPixels);
                    else if (geometry == NesGeometryType.PixelExtrusion && CanCompactPixelExtrusion(rule))
                        AppendCompactPixelExtrusion(shapeMask, originX, originY, scaledDepth,
                            scaledThickness, snapshot.BackgroundPixels);
                    else
                        PresentShapedTile(rule, geometry, shapeMask,
                            left, top, right, bottom, originX, originY, scaledDepth,
                            scaledThickness / PixelsPerDepthUnit, snapshot.BackgroundPixels);
                }
            }
            for (int i = tileCount; i < activeBackgroundTiles; i++)
            {
                TileMeshFactory.SetTileQuad(backgroundVertices, backgroundUv, i, 0, 0, 0, 0, 0, false);
            }
            activeBackgroundTiles = tileCount;
            for (int i = shapedTilesUsed; i < shapedTilePool.Count; i++) shapedTilePool[i].gameObject.SetActive(false);
            backgroundMesh.vertices = backgroundVertices;
            backgroundMesh.uv = backgroundUv;
            backgroundMesh.RecalculateBounds();
            extrusionMesh.Clear(false);
            extrusionMesh.SetVertices(extrusionVertices);
            extrusionMesh.SetUVs(0, extrusionUv);
            extrusionMesh.SetTriangles(extrusionTriangles, 0, false);
            extrusionMesh.bounds = FixedSceneBounds;
        }

        private ulong ApplyExcludedPatternColors(int tileIndex, NesRenderRule rule, NesGeometryType geometry)
        {
            ulong mask = dynamicTileMasks[tileIndex];
            if (geometry != NesGeometryType.PixelExtrusion || rule == null) return mask;
            int excluded = rule.PixelExtrusionExcludedColorMask;
            if ((excluded & (1 << 1)) != 0) mask &= ~dynamicTileColor1Masks[tileIndex];
            if ((excluded & (1 << 2)) != 0) mask &= ~dynamicTileColor2Masks[tileIndex];
            if ((excluded & (1 << 3)) != 0) mask &= ~dynamicTileColor3Masks[tileIndex];
            return mask;
        }

        private void AppendCompactPixelExtrusion(ulong mask, int originX, int originY,
            float frontDepth, float thickness, uint[] pixels)
        {
            byte[] sideSources = ignorePixelExtrusionEdgeColor
                ? BuildSideColorSources(pixels, originX, originY, mask, pixelSideSourceScratch) : null;
            TileMeshFactory.AppendPixelExtrusion(extrusionVertices, extrusionUv, extrusionTriangles,
                mask, originX, originY, frontDepth, thickness, sideSources);
        }

        private static bool CanCompactPixelExtrusion(NesRenderRule rule)
        {
            return rule == null || (rule.GeometryOffset == Vector3.zero &&
                rule.GeometryRotation == Vector3.zero && rule.GeometryScale == Vector3.one);
        }

        private static bool CanInstancePixelExtrusion(NesRenderRule rule, int originX, int originY)
        {
            return SystemInfo.supportsInstancing && CanCompactPixelExtrusion(rule) &&
                originX >= 0 && originX <= 248 && originY >= 0 && originY <= 232;
        }

        private void QueuePixelExtrusion(Dictionary<Mesh, PixelInstanceBatch> batches,
            NesRenderRule rule, ulong mask, int originX, int originY,
            float frontDepth, float thickness, uint[] pixels)
        {
            Mesh mesh;
            if (useShaderPixelExtrusion)
            {
                mesh = voxelProxyMesh;
            }
            else
            {
                byte[] sideSources = ignorePixelExtrusionEdgeColor
                    ? BuildSideColorSources(pixels, originX, originY, mask,
                        pixelSideSourceScratch) : null;
                mesh = ResolveShapeMesh(rule, NesGeometryType.PixelExtrusion, mask, sideSources);
            }
            if (mesh == null) return;
            if (!batches.TryGetValue(mesh, out PixelInstanceBatch batch))
            {
                batch = new PixelInstanceBatch();
                batches.Add(mesh, batch);
            }
            Matrix4x4 local = Matrix4x4.TRS(
                new Vector3(originX - 124, 116 - originY, frontDepth),
                Quaternion.identity, new Vector3(8, 8, Mathf.Max(.001f, thickness)));
            // Keep the instance transform local. ScreenCenter and other
            // Portalgraph transforms may move while emulation is paused, so a
            // world matrix captured during Present would leave shader-based
            // Pixel Extrusions behind until the next emulated frame.
            batch.LocalMatrices.Add(local);
            batch.TextureTransforms.Add(new Vector4(
                8f / PpuFrameBuffer.Width, -8f / PpuFrameBuffer.Height,
                originX / (float)PpuFrameBuffer.Width,
                (originY + 8f) / PpuFrameBuffer.Height));
            batch.Masks.Add(PackMask(mask));
        }

        private static Vector4 PackMask(ulong mask)
        {
            return new Vector4(
                (ushort)mask, (ushort)(mask >> 16),
                (ushort)(mask >> 32), (ushort)(mask >> 48));
        }

        private static void ClearPixelInstanceBatches(Dictionary<Mesh, PixelInstanceBatch> batches)
        {
            foreach (PixelInstanceBatch batch in batches.Values) batch.Clear();
        }

        private void LateUpdate()
        {
            DrawPixelInstanceBatches(backgroundPixelBatches,
                useShaderPixelExtrusion ? backgroundVoxelMaterial : backgroundMaterial);
            DrawPixelInstanceBatches(spritePixelBatches,
                useShaderPixelExtrusion ? spriteVoxelMaterial : spriteCutoutMaterial);
        }

        private void DrawPixelInstanceBatches(Dictionary<Mesh, PixelInstanceBatch> batches,
            Material material)
        {
            if (material == null || !SystemInfo.supportsInstancing) return;
            foreach (KeyValuePair<Mesh, PixelInstanceBatch> pair in batches)
            {
                PixelInstanceBatch batch = pair.Value;
                if (batch.LocalMatrices.Count == 0) continue;
                batch.WorldMatrices.Clear();
                Matrix4x4 rootMatrix = worldRoot.localToWorldMatrix;
                for (int i = 0; i < batch.LocalMatrices.Count; i++)
                    batch.WorldMatrices.Add(rootMatrix * batch.LocalMatrices[i]);
                pixelInstanceProperties.Clear();
                pixelInstanceProperties.SetVectorArray(InstanceTexStId, batch.TextureTransforms);
                pixelInstanceProperties.SetVectorArray(InstanceMaskId, batch.Masks);
                Graphics.DrawMeshInstanced(pair.Key, 0, material, batch.WorldMatrices,
                    pixelInstanceProperties, ShadowCastingMode.Off, true, gameObject.layer);
            }
        }

        private bool HasMatchingBoxNeighbor(int tileIndex, int originX, int originY)
        {
            int key = ((originY + 8) << 10) | (originX + 8);
            if (!dynamicTileLookup.TryGetValue(key, out int neighbor)) return false;
            return dynamicTileVisible[neighbor] && dynamicTileGeometry[neighbor] == NesGeometryType.Box &&
                   Mathf.Abs(dynamicTileDepth[neighbor] - dynamicTileDepth[tileIndex]) < 0.0001f &&
                   Mathf.Abs(dynamicTileThickness[neighbor] - dynamicTileThickness[tileIndex]) < 0.0001f;
        }

        private void PresentShapedTile(NesRenderRule rule, NesGeometryType geometry, ulong opaqueMask,
            int left, int top, int right, int bottom, int sourceX, int sourceY,
            float scaledDepth, float thickness, uint[] pixels)
        {
            byte[] sideSources = geometry == NesGeometryType.PixelExtrusion && ignorePixelExtrusionEdgeColor
                ? BuildSideColorSources(pixels, sourceX, sourceY, opaqueMask) : null;
            Mesh mesh = ResolveShapeMesh(rule, geometry, opaqueMask, sideSources);
            if (mesh == null) return;
            MeshRenderer renderer;
            if (shapedTilesUsed < shapedTilePool.Count) renderer = shapedTilePool[shapedTilesUsed];
            else
            {
                var go = new GameObject("Profile Tile Shape");
                go.transform.SetParent(worldRoot, false);
                go.AddComponent<MeshFilter>();
                renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = backgroundMaterial;
                shapedTilePool.Add(renderer);
            }
            shapedTilesUsed++;
            renderer.gameObject.SetActive(true);
            renderer.GetComponent<MeshFilter>().sharedMesh = mesh;
            Transform t = renderer.transform;
            Vector3 offset = rule?.GeometryOffset ?? Vector3.zero;
            Vector3 scale = rule?.GeometryScale ?? Vector3.one;
            t.localPosition = new Vector3((left + right) * .5f - 128 + offset.x,
                120 - (top + bottom) * .5f + offset.y, scaledDepth + offset.z * PixelsPerDepthUnit);
            t.localRotation = Quaternion.Euler(rule?.GeometryRotation ?? Vector3.zero);
            t.localScale = new Vector3((right - left) * scale.x, (bottom - top) * scale.y,
                Mathf.Max(.001f, thickness * PixelsPerDepthUnit * scale.z));
            if (shapeProperties == null) shapeProperties = new MaterialPropertyBlock();
            shapeProperties.Clear();
            shapeProperties.SetVector("_MainTex_ST", new Vector4((right - left) / 256f,
                -(bottom - top) / 240f, left / 256f, bottom / 240f));
            renderer.SetPropertyBlock(shapeProperties);
        }

        private byte[] BuildSideColorSources(uint[] pixels, int originX, int originY, ulong mask,
            byte[] reusable = null)
        {
            byte[] sources = reusable != null && reusable.Length == 64 ? reusable : new byte[64];
            Color32 ignored = pixelExtrusionIgnoredEdgeColor;
            float tolerance = ignoredEdgeColorTolerance * 255f;
            float toleranceSquared = tolerance * tolerance;
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
            {
                int local = y * 8 + x;
                sources[local] = (byte)local;
                if ((mask & (1UL << local)) == 0) continue;
                int best = local, bestDistance = int.MaxValue;
                for (int candidateY = 0; candidateY < 8; candidateY++)
                for (int candidateX = 0; candidateX < 8; candidateX++)
                {
                    int candidate = candidateY * 8 + candidateX;
                    if ((mask & (1UL << candidate)) == 0) continue;
                    int screenX = originX + candidateX, screenY = originY + candidateY;
                    if (screenX < 0 || screenX >= 256 || screenY < 0 || screenY >= 240) continue;
                    uint color = pixels[screenY * 256 + screenX];
                    if ((color >> 24) == 0) continue;
                    float dr = (byte)color - ignored.r;
                    float dg = (byte)(color >> 8) - ignored.g;
                    float db = (byte)(color >> 16) - ignored.b;
                    if (dr * dr + dg * dg + db * db <= toleranceSquared) continue;
                    int distance = Mathf.Abs(candidateX - x) + Mathf.Abs(candidateY - y);
                    if (distance >= bestDistance) continue;
                    best = candidate; bestDistance = distance;
                }
                sources[local] = (byte)best;
            }
            return sources;
        }

        private Mesh ResolveShapeMesh(NesRenderRule rule, NesGeometryType geometry, ulong opaqueMask,
            byte[] sideColorSources = null)
        {
            if (geometry == NesGeometryType.CustomMesh) return rule?.CustomMesh;
            ulong sourceHash = 1469598103934665603UL;
            if (sideColorSources != null)
                for (int i = 0; i < sideColorSources.Length; i++)
                    sourceHash = (sourceHash ^ sideColorSources[i]) * 1099511628211UL;
            string key = geometry == NesGeometryType.PixelExtrusion ? $"Pixels:{opaqueMask:X16}:{sourceHash:X16}" : geometry == NesGeometryType.Cylinder
                ? $"Cylinder:{Mathf.Clamp(rule?.CylinderSegments ?? 12, 3, 32)}"
                : $"Bevel:{Mathf.Clamp(rule?.Bevel ?? .12f, .001f, .49f):0.###}";
            if (generatedShapeMeshes.TryGetValue(key, out Mesh mesh)) return mesh;
            mesh = geometry == NesGeometryType.PixelExtrusion ? TileMeshFactory.CreatePixelExtrusion(opaqueMask, sideColorSources) : geometry == NesGeometryType.Cylinder
                ? TileMeshFactory.CreateCylinder(rule?.CylinderSegments ?? 12)
                : TileMeshFactory.CreateBeveledPrism(rule?.Bevel ?? .12f);
            generatedShapeMeshes.Add(key, mesh);
            return mesh;
        }

        private NesRenderRule FindRule(byte pattern, byte palette, uint tileHash)
        {
            return FindRule(pattern, palette, tileHash, NesElementType.Background);
        }

        private void OnGUI()
        {
            if (!showTileDebugOverlay || worldRoot == null) return;
            Camera cameraToUse = debugCamera != null ? debugCamera : Camera.main;
            if (cameraToUse == null) return;
            if (debugStyle == null)
                debugStyle = new GUIStyle(GUI.skin.label) { fontSize = 9, alignment = TextAnchor.MiddleCenter };

            for (int y = 0; y < TileMeshFactory.Rows; y++)
            for (int x = 0; x < TileMeshFactory.Columns; x++)
            {
                int index = y * TileMeshFactory.Columns + x;
                if (tileOpaqueCounts[index] == 0) continue;
                Vector3 localCenter = new Vector3(x * 8 - 124, 116 - y * 8, tileDepths[index] * PixelsPerDepthUnit);
                Vector3 screen = cameraToUse.WorldToScreenPoint(worldRoot.TransformPoint(localCenter));
                if (screen.z <= 0) continue;
                debugStyle.normal.textColor = index == debugTileY * TileMeshFactory.Columns + debugTileX
                    ? Color.yellow : Color.white;
                GUI.Label(new Rect(screen.x - 18, Screen.height - screen.y - 7, 36, 14),
                    $"{tilePatterns[index]:X2}:{tilePalettes[index]}", debugStyle);
            }

            GUI.Box(new Rect(8, 8, 390, 24), SelectedTileInfo);
        }

        private void UpdateSprites(PpuSceneSnapshot snapshot)
        {
            shapedSpritesUsed = 0;
            for (int i = 0; i < 64; i++)
            {
                int o = i * 4;
                byte[] spriteOam = snapshot.RenderedSpriteValid[i] != 0
                    ? snapshot.RenderedSpriteOam : snapshot.Oam;
                int y = spriteOam[o] + 1;
                int x = spriteOam[o + 3];
                byte pattern = spriteOam[o + 1];
                byte palette = (byte)(spriteOam[o + 2] & 3);
                float depth = renderProfile != null ? renderProfile.DefaultSpriteDepth : spriteDepth;
                NesRenderRule rule = FindRule(pattern, palette, snapshot.SpriteTileHashes[i], NesElementType.Sprite);
                bool visible = y < 240 && (rule == null || !rule.Hide);
                if (rule != null) depth = rule.Depth;
                spritePatterns[i] = pattern;
                spritePalettes[i] = palette;
                spriteHashes[i] = snapshot.SpriteTileHashes[i];
                spriteDepths[i] = depth;
                spriteRuleNames[i] = rule?.Name;
                int right = Mathf.Min(256, x + 8), bottom = Mathf.Min(240, y + snapshot.SpriteHeight);
                pickerSpriteRects[i] = new RectInt(Mathf.Max(0, x), Mathf.Max(0, y),
                    Mathf.Max(0, right - Mathf.Max(0, x)), Mathf.Max(0, bottom - Mathf.Max(0, y)));
                pickerSpriteVisible[i] = visible && x < 256 && right > 0 && bottom > 0;
                NesGeometryType geometry = rule?.EffectiveGeometry ??
                    (renderProfile != null ? renderProfile.DefaultSpriteGeometry : NesGeometryType.PixelExtrusion);
                bool shaped = geometry != NesGeometryType.Flat;
                // Keep the authoritative rasterized sprite as the front face.
                // The shaped mesh adds depth, but must not make a sprite vanish
                // when NES scanline sprite limits affect the 3D mask.
                TileMeshFactory.SetSpriteQuad(spriteVertices, spriteUv, i, x, y, snapshot.SpriteHeight,
                    depth * PixelsPerDepthUnit - (shaped ? 0.01f : 0f), visible);
                if (visible && shaped)
                {
                    float thickness = rule != null && rule.Thickness > 0
                        ? rule.Thickness : ResolveDefaultSpriteThickness();
                    if (geometry == NesGeometryType.PixelExtrusion &&
                        CanInstancePixelExtrusion(rule, x, y))
                        QueuePixelExtrusion(spritePixelBatches, rule,
                            snapshot.SpriteOpaqueMasks[i], x, y, depth * PixelsPerDepthUnit,
                            thickness * PixelsPerDepthUnit, snapshot.SpritePixels);
                    else
                        PresentShapedSprite(rule, geometry, snapshot.SpriteOpaqueMasks[i], x, y,
                            depth * PixelsPerDepthUnit, thickness, snapshot.SpritePixels);
                    if (snapshot.SpriteHeight == 16 && snapshot.SpriteLowerOpaqueMasks[i] != 0)
                    {
                        if (geometry == NesGeometryType.PixelExtrusion &&
                            CanInstancePixelExtrusion(rule, x, y + 8))
                            QueuePixelExtrusion(spritePixelBatches, rule,
                                snapshot.SpriteLowerOpaqueMasks[i], x, y + 8,
                                depth * PixelsPerDepthUnit, thickness * PixelsPerDepthUnit,
                                snapshot.SpritePixels);
                        else
                            PresentShapedSprite(rule, geometry, snapshot.SpriteLowerOpaqueMasks[i], x, y + 8,
                                depth * PixelsPerDepthUnit, thickness, snapshot.SpritePixels);
                    }
                }
            }
            for (int i = shapedSpritesUsed; i < shapedSpritePool.Count; i++)
                shapedSpritePool[i].gameObject.SetActive(false);
            spriteMesh.vertices = spriteVertices;
            spriteMesh.uv = spriteUv;
            spriteMesh.RecalculateBounds();
        }

        private float ResolveDefaultSpriteThickness()
        {
            return renderProfile != null && renderProfile.DefaultSpriteThickness > 0f
                ? renderProfile.DefaultSpriteThickness
                : automaticSpriteThickness;
        }

        private void PresentShapedSprite(NesRenderRule rule, NesGeometryType geometry, ulong opaqueMask,
            int x, int y, float scaledDepth, float thickness, uint[] pixels)
        {
            byte[] sideSources = geometry == NesGeometryType.PixelExtrusion && ignorePixelExtrusionEdgeColor
                ? BuildSideColorSources(pixels, x, y, opaqueMask) : null;
            Mesh mesh = ResolveShapeMesh(rule, geometry, opaqueMask, sideSources);
            if (mesh == null) return;
            MeshRenderer renderer;
            if (shapedSpritesUsed < shapedSpritePool.Count) renderer = shapedSpritePool[shapedSpritesUsed];
            else
            {
                var go = new GameObject("Profile Sprite Shape");
                go.transform.SetParent(worldRoot, false);
                go.AddComponent<MeshFilter>();
                renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = spriteCutoutMaterial;
                shapedSpritePool.Add(renderer);
            }
            shapedSpritesUsed++;
            renderer.gameObject.SetActive(true);
            renderer.GetComponent<MeshFilter>().sharedMesh = mesh;
            Transform t = renderer.transform;
            Vector3 offset = rule?.GeometryOffset ?? Vector3.zero;
            Vector3 scale = rule?.GeometryScale ?? Vector3.one;
            t.localPosition = new Vector3(x - 124 + offset.x, 116 - y + offset.y,
                scaledDepth + offset.z * PixelsPerDepthUnit);
            t.localRotation = Quaternion.Euler(rule?.GeometryRotation ?? Vector3.zero);
            t.localScale = new Vector3(8 * scale.x, 8 * scale.y,
                Mathf.Max(.001f, thickness * PixelsPerDepthUnit * scale.z));
            if (shapeProperties == null) shapeProperties = new MaterialPropertyBlock();
            shapeProperties.Clear();
            shapeProperties.SetVector("_MainTex_ST", new Vector4(8 / 256f, -8 / 240f,
                x / 256f, (y + 8) / 240f));
            renderer.SetPropertyBlock(shapeProperties);
        }

        private NesRenderRule FindRule(byte pattern, byte palette, uint tileHash, NesElementType elementType)
        {
            ulong cacheKey = tileHash | ((ulong)pattern << 32) | ((ulong)palette << 40) |
                             ((ulong)elementType << 48);
            if (ruleMatchCache.TryGetValue(cacheKey, out NesRenderRule cachedRule))
                return cachedRule;

            NesRenderRule matchedRule = null;
            if (renderProfile == null || renderProfile.Rules == null)
                return CacheRuleMatch(cacheKey, null);
            foreach (NesRenderRule rule in renderProfile.Rules)
                if (RuleMatches(rule, pattern, palette, tileHash, elementType) &&
                    rule.MatchTileHash && !rule.MatchAnyPattern)
                {
                    matchedRule = rule;
                    break;
                }
            if (matchedRule == null)
                foreach (NesRenderRule rule in renderProfile.Rules)
                    if (RuleMatches(rule, pattern, palette, tileHash, elementType) &&
                        rule.MatchTileHash && rule.MatchAnyPattern)
                    {
                        matchedRule = rule;
                        break;
                    }
            if (matchedRule == null)
                foreach (NesRenderRule rule in renderProfile.Rules)
                    if (RuleMatches(rule, pattern, palette, tileHash, elementType))
                    {
                        matchedRule = rule;
                        break;
                    }
            return CacheRuleMatch(cacheKey, matchedRule);
        }

        private NesRenderRule CacheRuleMatch(ulong key, NesRenderRule rule)
        {
            // Dynamic CHR games can continuously introduce new hashes. A fixed
            // upper bound prevents a long session from growing this cache forever.
            if (ruleMatchCache.Count >= MaximumRuleCacheEntries)
                ruleMatchCache.Clear();
            ruleMatchCache[key] = rule;
            return rule;
        }

        private static bool RuleMatches(NesRenderRule rule, byte pattern, byte palette, uint tileHash,
            NesElementType elementType)
        {
            return rule != null && rule.ElementType == elementType &&
                (rule.MatchAnyPattern ||
                 pattern >= rule.PatternIndexMin && pattern <= rule.PatternIndexMax) &&
                (rule.PaletteIndex < 0 || rule.PaletteIndex == palette) &&
                (!rule.MatchTileHash || rule.TileHash == tileHash);
        }

        private void OnDestroy()
        {
            if (screenMesh != null) Destroy(screenMesh);
            if (backgroundMesh != null) Destroy(backgroundMesh);
            if (spriteMesh != null) Destroy(spriteMesh);
            if (extrusionMesh != null) Destroy(extrusionMesh);
            if (voxelProxyMesh != null) Destroy(voxelProxyMesh);
            foreach (Mesh mesh in generatedShapeMeshes.Values) if (mesh != null) Destroy(mesh);
            if (backgroundTexture != null) Destroy(backgroundTexture);
            if (spriteTexture != null) Destroy(spriteTexture);
            if (backgroundMaterial != null) Destroy(backgroundMaterial);
            if (spriteMaterial != null) Destroy(spriteMaterial);
            if (spriteCutoutMaterial != null) Destroy(spriteCutoutMaterial);
            if (backgroundVoxelMaterial != null) Destroy(backgroundVoxelMaterial);
            if (spriteVoxelMaterial != null) Destroy(spriteVoxelMaterial);
            if (runtimeRenderProfile != null) Destroy(runtimeRenderProfile);
        }
    }
}
