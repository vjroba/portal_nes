using System;
using UnityEngine;

namespace PortalNes.Rendering3D
{
    public enum NesElementType { Background, Sprite }
    public enum NesGeometryType { Flat, Box, BeveledBox, Cylinder, PixelExtrusion, CustomMesh }

    [Serializable]
    public sealed class NesRenderRule
    {
        public string Name;
        public int PatternIndexMin, PatternIndexMax;
        public int PaletteIndex = -1;
        [Tooltip("When enabled, this rule only matches the exact 8x8 CHR pixel pattern. Use this for bank-switched CHR ROM or CHR RAM.")]
        public bool MatchTileHash;
        [Tooltip("When enabled, Pattern Index is ignored and the rule follows matching CHR contents across bank slots. Requires Match Tile Hash.")]
        public bool MatchAnyPattern;
        public uint TileHash;
        [Range(0, 14), Tooltip("For background Pixel Extrusion, pattern colors selected by bits 1-3 remain on the base layer instead of being extruded.")]
        public int PixelExtrusionExcludedColorMask;
        [Tooltip("For background Pixel Extrusion, place the non-extruded rear/base pixels at an explicit Depth independently of Thickness.")]
        public bool UsePixelExtrusionBaseDepth;
        [Tooltip("Rear/base plane Depth used when Use Pixel Extrusion Base Depth is enabled.")]
        public float PixelExtrusionBaseDepth;
        public NesElementType ElementType;
        public float Depth, Thickness;
        [Min(1)] public int SurfaceUnitWidth = 1;
        [Min(1)] public int SurfaceUnitHeight = 1;
        public bool UseBoxMesh, Billboard, Hide, Hud;
        public NesGeometryType Geometry;
        [Range(0f, 0.49f)] public float Bevel = 0.12f;
        [Range(3, 32)] public int CylinderSegments = 12;
        public Mesh CustomMesh;
        public Vector3 GeometryOffset;
        public Vector3 GeometryRotation;
        public Vector3 GeometryScale = Vector3.one;

        public NesGeometryType EffectiveGeometry => Geometry == NesGeometryType.Flat && UseBoxMesh
            ? NesGeometryType.Box : Geometry;
    }
}
