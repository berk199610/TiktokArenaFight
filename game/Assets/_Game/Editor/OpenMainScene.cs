using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Arena.EditorTools
{
    /// <summary>
    /// When the project is opened in the editor: open the Main scene (if nothing else is open) and switch the
    /// Game view to the portrait TikTok size, so pressing Play shows the game the way the stream will.
    /// </summary>
    [InitializeOnLoad]
    static class OpenMainScene
    {
        const string ScenePath = "Assets/Scenes/Main.unity";

        static OpenMainScene()
        {
            if (Application.isBatchMode) return;
            if (SessionState.GetBool("Arena.Opened", false)) return;
            SessionState.SetBool("Arena.Opened", true);
            EditorApplication.delayCall += () =>
            {
                var active = EditorSceneManager.GetActiveScene();
                if ((string.IsNullOrEmpty(active.path) || active.path.EndsWith("SampleScene.unity")) && System.IO.File.Exists(ScenePath))
                    EditorSceneManager.OpenScene(ScenePath);
                SelectPortraitGameView();
            };
        }

        [MenuItem("Arena/Oyun penceresini dikey yap (1080x1920)")]
        static void SelectPortraitGameView()
        {
            try
            {
                var asm = typeof(Editor).Assembly;
                var sizesType = asm.GetType("UnityEditor.GameViewSizes");
                var instance = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
                var group = sizesType.GetMethod("GetGroup").Invoke(instance, new object[] { (int)GameViewSizeGroupType.Standalone });
                var groupType = group.GetType();
                int count = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null);
                int index = -1;
                for (int i = 0; i < count; i++)
                {
                    var sz = groupType.GetMethod("GetGameViewSize").Invoke(group, new object[] { i });
                    if ((string)sz.GetType().GetProperty("baseText").GetValue(sz) == "TikTok 1080x1920") { index = i; break; }
                }
                if (index < 0) return;
                var gvType = asm.GetType("UnityEditor.GameView");
                var gv = EditorWindow.GetWindow(gvType, false, null, false);
                var m = gvType.GetMethod("SizeSelectionCallback", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                m?.Invoke(gv, new object[] { index, null });
            }
            catch (Exception e) { Debug.Log("[Arena] Game view boyutu seçilemedi (önemsiz): " + e.Message); }
        }
    }
}
