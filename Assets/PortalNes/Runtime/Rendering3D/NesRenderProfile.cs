using UnityEngine;

namespace PortalNes.Rendering3D
{
    [CreateAssetMenu(menuName = "PortalNes/Render Profile")]
    public sealed class NesRenderProfile : ScriptableObject
    {
        public string RomSha256;

        [Tooltip("Use profile-wide defaults for non-empty background tiles that do not match an individual rule.")]
        public bool UseDefaultBackgroundSettings;

        public float DefaultBackgroundDepth;

        public NesGeometryType DefaultBackgroundGeometry = NesGeometryType.Flat;

        [Min(0f), Tooltip("Thickness used by unmatched background tiles when default background settings are enabled.")]
        public float DefaultBackgroundThickness = 1f;

        [Tooltip("Depth used by sprites that do not match an individual rule.")]
        public float DefaultSpriteDepth;

        [Tooltip("Geometry used by sprites that do not match an individual rule.")]
        public NesGeometryType DefaultSpriteGeometry = NesGeometryType.PixelExtrusion;

        [Min(0f), Tooltip("Thickness used by sprites without a positive per-rule Thickness override. One unit equals 8 NES pixels.")]
        public float DefaultSpriteThickness = 1f;

        public NesRenderRule[] Rules;
    }
}
