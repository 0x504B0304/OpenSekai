using System;
using Sekai.MusicScoreMaker.Ingame.Views;
using UnityEditor;
using UnityEngine;

// Bakes the static editor chrome (EditorActionDock) into the music score maker prefab and
// removes the legacy toolbar buttons that the runtime dock used to hide. Run once whenever
// the dock layout changes:
//   Unity.exe -batchmode -quit -projectPath <project> -executeMethod EditorChromePrefabBuilder.Rebuild
public static class EditorChromePrefabBuilder
{
    private const string PrefabPath = "Assets/Resources/screen/prefabs/ScreenLayerMusicScoreMaker.prefab";
    private static readonly string[] LegacyObjects =
    {
        "ZoomTimelineButtons",
        "TimeSliderValueButtons",
        "OnTestPlayEventButton",
        "QuickSaveMusicScoreButton"
    };

    [MenuItem("OpenSekai/Editor/Bake Static Editor Chrome")]
    public static void Rebuild()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var view = root.GetComponentInChildren<MusicScoreMakerView>(true);
            if (view == null) throw new InvalidOperationException("MusicScoreMakerView not found in " + PrefabPath);
            // The screen prefab has no Canvas; the scene supplies it. The root is a full-screen
            // stretch rect, so bake the dock under it and use that rect for layout.
            var rootRect = (RectTransform)root.transform;

            var viewSerialized = new SerializedObject(view);
            var toolWindow = viewSerialized.FindProperty("_toolWindowObject").objectReferenceValue as GameObject;
            Transform tools = toolWindow != null ? toolWindow.transform : null;

            foreach (string name in LegacyObjects)
            {
                for (Transform target = FindDeep(root.transform, name); target != null; target = FindDeep(root.transform, name))
                    UnityEngine.Object.DestroyImmediate(target.gameObject);
            }

            Transform dockTransform = root.transform.Find("EditorActionDock");
            if (dockTransform == null)
            {
                var dockObject = new GameObject("EditorActionDock", typeof(RectTransform));
                dockObject.transform.SetParent(root.transform, false);
                dockTransform = dockObject.transform;
            }
            var dock = dockTransform.GetComponent<EditorActionDock>();
            if (dock == null) dock = dockTransform.gameObject.AddComponent<EditorActionDock>();
            // The prefab has no canvas at bake time, so the root rect is empty. Lay the dock
            // out against the reference resolution so its baked anchors are already correct
            // before the runtime relayout pass.
            Vector2 savedMin = rootRect.anchorMin, savedMax = rootRect.anchorMax, savedSize = rootRect.sizeDelta;
            rootRect.anchorMin = rootRect.anchorMax = new Vector2(.5f, .5f);
            rootRect.sizeDelta = new Vector2(1920f, 1080f);
            try { dock.Build(rootRect, 1f, tools, null, null, null); }
            finally { rootRect.anchorMin = savedMin; rootRect.anchorMax = savedMax; rootRect.sizeDelta = savedSize; }

            viewSerialized.FindProperty("_editorActionDock").objectReferenceValue = dock;
            viewSerialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(view);
            Debug.Log("[EditorChromePrefabBuilder] Baked static dock: buttons=" + dockTransform.childCount + " tools=" + (tools != null));
        }
        finally
        {
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
        }
        Debug.Log("[EditorChromePrefabBuilder] Rebuilt " + PrefabPath);
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDeep(root.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }
}
