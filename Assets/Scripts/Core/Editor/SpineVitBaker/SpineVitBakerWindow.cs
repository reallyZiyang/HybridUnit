#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Spine;
using Spine.Unity;
using UnityEditor;
using UnityEngine;

public sealed class SpineVitBakerWindow : EditorWindow
{
    private readonly SpineVitBakeSettings settings = new SpineVitBakeSettings();
    private Vector2 scroll;
    private SkeletonDataAsset cachedSkeletonDataAsset;
    private string[] skinNames = Array.Empty<string>();
    private string[] animationNames = Array.Empty<string>();
    private bool[] selectedAnimations = Array.Empty<bool>();

    [MenuItem("Tools/Rendering/Spine VIT Baker")]
    public static void Open()
    {
        SpineVitBakerWindow window = GetWindow<SpineVitBakerWindow>();
        window.titleContent = new GUIContent("Spine VIT Baker");
        window.minSize = new Vector2(480f, 640f);
        window.Show();
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        DrawSource();
        DrawBakeSettings();
        DrawOutput();
        DrawSummary();

        using (new EditorGUI.DisabledScope(!SpineVitBaker.CanBake(settings)))
        {
            if (GUILayout.Button("Bake Spine VIT", GUILayout.Height(34f)))
            {
                Bake();
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawSource()
    {
        EditorGUILayout.LabelField("Source", EditorStyles.boldLabel);
        settings.SkeletonDataAsset = (SkeletonDataAsset)EditorGUILayout.ObjectField("Skeleton Data", settings.SkeletonDataAsset, typeof(SkeletonDataAsset), false);
        if (settings.SkeletonDataAsset != cachedSkeletonDataAsset)
        {
            RefreshSkeletonData();
        }

        using (new EditorGUI.DisabledScope(settings.SkeletonDataAsset == null || skinNames.Length == 0))
        {
            int currentSkinIndex = Mathf.Max(0, Array.IndexOf(skinNames, settings.SkinName));
            int nextSkinIndex = EditorGUILayout.Popup("Skin", currentSkinIndex, skinNames);
            if (nextSkinIndex >= 0 && nextSkinIndex < skinNames.Length)
            {
                settings.SkinName = skinNames[nextSkinIndex];
            }
        }
    }

    private void DrawBakeSettings()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Bake Settings", EditorStyles.boldLabel);
        settings.FrameRate = EditorGUILayout.IntPopup("Frame Rate", settings.FrameRate, new[] { "15", "20", "30", "Custom" }, new[] { 15, 20, 30, settings.FrameRate });
        settings.FrameRate = Mathf.Max(1, EditorGUILayout.IntField("Custom Frame Rate", settings.FrameRate));

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Animations", EditorStyles.boldLabel);
        if (animationNames.Length == 0)
        {
            EditorGUILayout.HelpBox("No animations found.", MessageType.Info);
            settings.AnimationNames = Array.Empty<string>();
            return;
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Select All"))
        {
            SetAllAnimationsSelected(true);
        }
        if (GUILayout.Button("Clear"))
        {
            SetAllAnimationsSelected(false);
        }
        EditorGUILayout.EndHorizontal();

        for (int i = 0; i < animationNames.Length; i++)
        {
            selectedAnimations[i] = EditorGUILayout.ToggleLeft(animationNames[i], selectedAnimations[i]);
        }

        settings.AnimationNames = GetSelectedAnimations();
    }

    private void DrawOutput()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        settings.OutputFolder = EditorGUILayout.TextField("Folder", settings.OutputFolder);
        if (GUILayout.Button("Browse", GUILayout.Width(72f)))
        {
            string absolute = EditorUtility.OpenFolderPanel("Choose Output Folder", Application.dataPath, string.Empty);
            if (!string.IsNullOrEmpty(absolute))
            {
                settings.OutputFolder = ParticleAtlasPathUtility.AbsoluteToProjectPath(absolute);
            }
        }
        EditorGUILayout.EndHorizontal();
        settings.OutputName = EditorGUILayout.TextField(new GUIContent("File Name", "Leave empty to use skeleton and skin name."), settings.OutputName);
    }

    private void DrawSummary()
    {
        EditorGUILayout.Space(12f);
        int selectedCount = settings.AnimationNames != null ? settings.AnimationNames.Length : 0;
        int totalFrames = EstimateTotalFrames();
        MessageType messageType = settings.SkeletonDataAsset == null || selectedCount == 0 ? MessageType.Warning : MessageType.Info;
        EditorGUILayout.HelpBox(
            string.Format(
                "Selected Clips: {0}\nEstimated Frames: {1}\nVIT FPS: {2}\nV1 Limits: fixed draw order, stable topology, one atlas page",
                selectedCount,
                totalFrames,
                settings.FrameRate),
            messageType);
    }

    private void Bake()
    {
        try
        {
            SpineVitBakeResult result = SpineVitBaker.Bake(settings);
            EditorUtility.DisplayDialog(
                "Spine VIT Baker",
                "Spine VIT baked:\n" + result.AssetPath + "\nClips: " + result.ClipCount + "\nFrames: " + result.TotalFrameCount + "\nVertices: " + result.VertexCount,
                "OK");
            Debug.Log("Spine VIT baked: " + result.AssetPath + ", clips: " + result.ClipCount + ", frames: " + result.TotalFrameCount + ", vertices: " + result.VertexCount);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Spine VIT Baker", exception.Message, "OK");
        }
    }

    private void RefreshSkeletonData()
    {
        cachedSkeletonDataAsset = settings.SkeletonDataAsset;
        skinNames = Array.Empty<string>();
        animationNames = Array.Empty<string>();
        selectedAnimations = Array.Empty<bool>();
        settings.AnimationNames = Array.Empty<string>();
        settings.SkinName = string.Empty;

        if (settings.SkeletonDataAsset == null)
        {
            return;
        }

        SkeletonData skeletonData = settings.SkeletonDataAsset.GetSkeletonData(false);
        if (skeletonData == null)
        {
            return;
        }

        List<string> skins = new List<string>();
        for (int i = 0; i < skeletonData.Skins.Count; i++)
        {
            Skin skin = skeletonData.Skins.Items[i];
            if (skin != null)
            {
                skins.Add(skin.Name);
            }
        }

        if (skins.Count == 0)
        {
            skins.Add(string.Empty);
        }

        skinNames = skins.ToArray();
        settings.SkinName = skinNames[0];

        List<string> animations = new List<string>();
        for (int i = 0; i < skeletonData.Animations.Count; i++)
        {
            Spine.Animation animation = skeletonData.Animations.Items[i];
            if (animation != null)
            {
                animations.Add(animation.Name);
            }
        }

        animationNames = animations.ToArray();
        selectedAnimations = new bool[animationNames.Length];
        if (selectedAnimations.Length > 0)
        {
            selectedAnimations[0] = true;
        }

        settings.AnimationNames = GetSelectedAnimations();
    }

    private void SetAllAnimationsSelected(bool selected)
    {
        for (int i = 0; i < selectedAnimations.Length; i++)
        {
            selectedAnimations[i] = selected;
        }
        settings.AnimationNames = GetSelectedAnimations();
    }

    private string[] GetSelectedAnimations()
    {
        List<string> selected = new List<string>();
        for (int i = 0; i < animationNames.Length; i++)
        {
            if (selectedAnimations[i])
            {
                selected.Add(animationNames[i]);
            }
        }

        return selected.ToArray();
    }

    private int EstimateTotalFrames()
    {
        if (settings.SkeletonDataAsset == null || settings.AnimationNames == null)
        {
            return 0;
        }

        SkeletonData skeletonData = settings.SkeletonDataAsset.GetSkeletonData(true);
        if (skeletonData == null)
        {
            return 0;
        }

        int total = 0;
        for (int i = 0; i < settings.AnimationNames.Length; i++)
        {
            Spine.Animation animation = skeletonData.FindAnimation(settings.AnimationNames[i]);
            if (animation != null)
            {
                total += Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(0.001f, animation.Duration) * Mathf.Max(1, settings.FrameRate)));
            }
        }

        return total;
    }
}
#endif
