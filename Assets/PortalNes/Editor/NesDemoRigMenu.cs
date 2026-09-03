using PortalNes.UnityBridge;
using PortalNes.Rendering3D;
using UnityEditor;
using UnityEngine;

namespace PortalNes.Editor
{
    public static class NesDemoRigMenu
    {
        [MenuItem("PortalNes/Create 3D Demo Rig")]
        private static void Create3DRig()
        {
            var root = new GameObject("PortalNes 3D Demo");
            Undo.RegisterCreatedObjectUndo(root, "Create PortalNes 3D Demo");
            var runner = root.AddComponent<NesRunner>();
            var input = root.AddComponent<NesInputProvider>();
            var renderer3D = root.AddComponent<NesSceneRenderer>();

            var runnerObject = new SerializedObject(runner);
            runnerObject.FindProperty("sceneRenderer").objectReferenceValue = renderer3D;
            runnerObject.FindProperty("inputProvider").objectReferenceValue = input;
            runnerObject.FindProperty("displayMode").enumValueIndex = (int)NesDisplayMode.Scene3D;
            runnerObject.ApplyModifiedPropertiesWithoutUndo();

            Selection.activeGameObject = root;
            EditorGUIUtility.PingObject(root);
            Debug.Log("Created PortalNes 3D Demo Rig. Assign a local ROM in the NesRunner inspector and enter Play Mode.");
        }

        [MenuItem("PortalNes/Create 2D Demo Rig")]
        private static void CreateRig()
        {
            var root = new GameObject("PortalNes 2D Demo");
            Undo.RegisterCreatedObjectUndo(root, "Create PortalNes 2D Demo");
            var runner = root.AddComponent<NesRunner>();
            var input = root.AddComponent<NesInputProvider>();
            var presenter = root.AddComponent<NesTextureRenderer>();

            var screen = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Undo.RegisterCreatedObjectUndo(screen, "Create NES Screen");
            screen.name = "NES 256x240 Screen";
            screen.transform.SetParent(root.transform, false);
            screen.transform.localScale = new Vector3(256f / 240f, -1f, 1f);
            var collider = screen.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);

            var presenterObject = new SerializedObject(presenter);
            presenterObject.FindProperty("targetRenderer").objectReferenceValue = screen.GetComponent<Renderer>();
            presenterObject.ApplyModifiedPropertiesWithoutUndo();

            var runnerObject = new SerializedObject(runner);
            runnerObject.FindProperty("textureRenderer").objectReferenceValue = presenter;
            runnerObject.FindProperty("inputProvider").objectReferenceValue = input;
            runnerObject.ApplyModifiedPropertiesWithoutUndo();

            Selection.activeGameObject = root;
            EditorGUIUtility.PingObject(root);
            Debug.Log("Created PortalNes 2D Demo Rig. Choose a local ROM in the NesRunner inspector, enable Load Rom On Start, then enter Play Mode.");
        }
    }
}
