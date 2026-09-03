using PortalNes.UnityBridge;
using UnityEditor;
using UnityEngine;

namespace PortalNes.Editor
{
    [InitializeOnLoad]
    [CustomEditor(typeof(NesRunner))]
    public sealed class NesRunnerEditor : UnityEditor.Editor
    {
        static NesRunnerEditor()
        {
            NesRunner.RomPathPicker = currentPath =>
            {
                string directory = string.IsNullOrWhiteSpace(currentPath)
                    ? "" : System.IO.Path.GetDirectoryName(currentPath);
                return EditorUtility.OpenFilePanel("Choose iNES ROM", directory, "nes");
            };
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var runner = (NesRunner)target;
            if (GUILayout.Button("Choose local ROM..."))
            {
                string path = EditorUtility.OpenFilePanel("Choose iNES ROM", "", "nes");
                if (!string.IsNullOrEmpty(path))
                {
                    Undo.RecordObject(runner, "Choose NES ROM");
                    runner.RomPath = path;
                    EditorUtility.SetDirty(runner);
                }
            }
            using (new EditorGUI.DisabledScope(!Application.isPlaying || string.IsNullOrWhiteSpace(runner.RomPath)))
                if (GUILayout.Button("Load / Reset ROM")) runner.LoadRomFromPath(runner.RomPath);
            if (runner.IsFaulted)
                EditorGUILayout.HelpBox(runner.LastError, MessageType.Error);
            if (Application.isPlaying && runner.IsLoaded)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Live diagnostics", EditorStyles.boldLabel);
                EditorGUILayout.SelectableLabel(runner.GetDiagnostics(), EditorStyles.textArea, GUILayout.MinHeight(55));
                if (GUILayout.Button("Log Diagnostics")) Debug.Log($"PortalNes diagnostics: {runner.GetDiagnostics()}", runner);
                Repaint();
            }
        }
    }
}
