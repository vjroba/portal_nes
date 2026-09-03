using System.IO;
using PortalNes.Rendering3D;
using UnityEditor;
using UnityEngine;

namespace PortalNes.Editor
{
    internal static class NesRenderProfileJsonMenu
    {
        [MenuItem("Assets/PortalNes/Export Render Profile JSON", true)]
        private static bool CanExport() => Selection.activeObject is NesRenderProfile;

        [MenuItem("Assets/PortalNes/Export Render Profile JSON")]
        private static void Export()
        {
            var profile = Selection.activeObject as NesRenderProfile;
            if (profile == null) return;
            string path = EditorUtility.SaveFilePanel("Export PortalNes Render Profile",
                "", profile.name + ".nesprof", "nesprof");
            if (string.IsNullOrWhiteSpace(path)) return;
            File.WriteAllText(path, JsonUtility.ToJson(profile, true));
            Debug.Log($"Exported PortalNes render profile to '{path}'.", profile);
        }
    }
}
