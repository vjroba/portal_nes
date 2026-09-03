using System; using System.IO; using PortalNes.Emulator.Cartridge; using UnityEditor; using UnityEngine;
namespace PortalNes.Editor
{
    public sealed class NesRomInspectorWindow : EditorWindow
    {
        private Cartridge cartridge; private string status = "Select an iNES ROM. It will not be copied into the project.";
        [MenuItem("PortalNes/ROM Inspector")] private static void Open() => GetWindow<NesRomInspectorWindow>("NES ROM Inspector");
        private void OnGUI() { EditorGUILayout.HelpBox(status, MessageType.Info); if (GUILayout.Button("Open ROM...")) LoadRom(); if (cartridge == null) return; EditorGUILayout.LabelField("Mapper", cartridge.MapperNumber.ToString()); EditorGUILayout.LabelField("PRG ROM", $"{cartridge.PrgRom.Length / 1024} KB"); EditorGUILayout.LabelField("CHR ROM", $"{cartridge.ChrRom.Length / 1024} KB"); EditorGUILayout.LabelField("Mirroring", cartridge.Mirroring.ToString()); EditorGUILayout.LabelField("Battery RAM", cartridge.HasBatteryBackedRam ? "Yes" : "No"); }
        private void LoadRom() { string path = EditorUtility.OpenFilePanel("Open iNES ROM", "", "nes"); if (string.IsNullOrEmpty(path)) return; try { cartridge = INesLoader.Load(File.ReadAllBytes(path)); status = $"Loaded {Path.GetFileName(path)}"; } catch (Exception e) { cartridge = null; status = e.Message; Debug.LogError($"PortalNes ROM load failed: {e.Message}"); } }
    }
}
