using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityToolbarExtender;

namespace Game.Play.Editor.Extensions
{
    internal static class ToolbarStyles
    {
        public static readonly GUIStyle kCommandButtonStyle;

        static ToolbarStyles()
        {
            kCommandButtonStyle = new GUIStyle("Command")
            {
                fontSize = 12,
                fixedHeight = 20,
                alignment = TextAnchor.MiddleCenter,
                imagePosition = ImagePosition.ImageAbove,
                fontStyle = FontStyle.Bold
            };
        }
    }

    [InitializeOnLoad]
    public static class SceneExtensions
    {
        static SceneExtensions()
        {
            ToolbarExtender.LeftToolbarGUI.Add(OnToolbarGUI);
        }

        private static void OnToolbarGUI()
        {
            GUILayout.FlexibleSpace();

            if (GUILayout.Button(new GUIContent("G", "Start Game"), ToolbarStyles.kCommandButtonStyle))
            {
                SceneHelper.SwitchScene("Assets/Scenes/Game.unity");
            }

            if (GUILayout.Button(new GUIContent("U", "Start UI"), ToolbarStyles.kCommandButtonStyle))
            {
                SceneHelper.SwitchScene("Assets/Scenes/GameUI.unity");
            }
        }
    }

    internal static class SceneHelper
    {
        private static string s_SceneToOpen;

        public static void SwitchScene(string scenePath)
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(scenePath);
            }
        }

        public static void StartScene(string sceneName)
        {
            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
            }

            s_SceneToOpen = sceneName;
            EditorApplication.update += OnUpdate;
        }

        private static void OnUpdate()
        {
            if (s_SceneToOpen == null ||
                EditorApplication.isPlaying || EditorApplication.isPaused ||
                EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            EditorApplication.update -= OnUpdate;

            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                // need to get scene via search because the path to the scene
                // file contains the package version so it'll change over time
                string[] guids = AssetDatabase.FindAssets("t:scene " + s_SceneToOpen, null);
                if (guids.Length == 0)
                {
                    Debug.LogWarning("Couldn't find scene file");
                }
                else
                {
                    string scenePath = AssetDatabase.GUIDToAssetPath(guids[0]);
                    EditorSceneManager.OpenScene(scenePath);
                    EditorApplication.isPlaying = true;
                }
            }

            s_SceneToOpen = null;
        }
    }
}