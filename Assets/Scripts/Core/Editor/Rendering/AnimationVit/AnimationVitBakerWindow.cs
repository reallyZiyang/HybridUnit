#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public sealed class AnimationVitBakerWindow : EditorWindow
{
    private const string DefaultSettingsAssetPath = AnimationVitBakeSettings.DefaultOutputFolder + "/AnimationVitBakeSettings.asset";

    [SerializeField] private AnimationVitBakeSettings settings;
    private Vector2 scroll;

    [MenuItem("烘培工具/Animation图集烘培")]
    public static void Open()
    {
        AnimationVitBakerWindow window = GetWindow<AnimationVitBakerWindow>();
        window.titleContent = new GUIContent("Animation VIT Baker");
        window.minSize = new Vector2(480f, 560f);
        window.Show();
    }

    private void OnEnable()
    {
        EnsureSettings();
    }

    private void OnGUI()
    {
        EnsureSettings();

        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUI.BeginChangeCheck();
        DrawSource();
        DrawBakeSettings();
        DrawOutput();
        DrawSummary();

        using (new EditorGUI.DisabledScope(!AnimationVitBaker.CanBake(settings)))
        {
            if (GUILayout.Button("Bake Animation VIT", GUILayout.Height(34f)))
            {
                Bake();
            }
        }

        if (EditorGUI.EndChangeCheck())
        {
            SaveSettings();
        }

        EditorGUILayout.EndScrollView();
    }

    private void EnsureSettings()
    {
        if (settings != null)
        {
            return;
        }

        settings = AssetDatabase.LoadAssetAtPath<AnimationVitBakeSettings>(DefaultSettingsAssetPath);
        if (settings != null)
        {
            return;
        }

        System.IO.Directory.CreateDirectory(ParticleAtlasPathUtility.ProjectPathToAbsolute(AnimationVitBakeSettings.DefaultOutputFolder));
        AssetDatabase.Refresh();

        settings = CreateInstance<AnimationVitBakeSettings>();
        settings.name = System.IO.Path.GetFileNameWithoutExtension(DefaultSettingsAssetPath);
        AssetDatabase.CreateAsset(settings, DefaultSettingsAssetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(DefaultSettingsAssetPath);
    }

    private void SaveSettings()
    {
        if (settings == null)
        {
            return;
        }

        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
    }

    private void DrawSource()
    {
        EditorGUILayout.LabelField("Source", EditorStyles.boldLabel);
        settings.SourceRoot = (GameObject)EditorGUILayout.ObjectField("Source Root", settings.SourceRoot, typeof(GameObject), true);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Read Clips From Source"))
        {
            settings.Clips = ReadClipsFromSource(settings.SourceRoot);
        }
        if (GUILayout.Button("Clear Clips"))
        {
            settings.Clips = Array.Empty<AnimationClip>();
        }
        EditorGUILayout.EndHorizontal();

        int clipCount = settings.Clips != null ? settings.Clips.Length : 0;
        int nextCount = Mathf.Max(0, EditorGUILayout.IntField("Clip Count", clipCount));
        if (settings.Clips == null || nextCount != settings.Clips.Length)
        {
            Array.Resize(ref settings.Clips, nextCount);
        }

        for (int i = 0; i < settings.Clips.Length; i++)
        {
            settings.Clips[i] = (AnimationClip)EditorGUILayout.ObjectField("Clip " + i, settings.Clips[i], typeof(AnimationClip), false);
        }
    }

    private void DrawBakeSettings()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Bake Settings", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        settings.ResolutionPreset = (AnimationVitBakeResolutionPreset)EditorGUILayout.EnumPopup(
            new GUIContent("Resolution Preset", "Controls the baked runtime source texture max size. VIT texture size is determined by vertex count and frame count."),
            settings.ResolutionPreset);
        if (EditorGUI.EndChangeCheck())
        {
            settings.ApplyResolutionPreset();
        }

        using (new EditorGUI.DisabledScope(settings.ResolutionPreset != AnimationVitBakeResolutionPreset.Custom))
        {
            settings.SourceTextureMaxSize = Mathf.Max(1, EditorGUILayout.IntField("Source Texture Max Size", settings.SourceTextureMaxSize));
        }

        EditorGUI.BeginChangeCheck();
        settings.FrameRatePreset = (AnimationVitBakeFrameRatePreset)EditorGUILayout.EnumPopup("Frame Rate Preset", settings.FrameRatePreset);
        if (EditorGUI.EndChangeCheck())
        {
            settings.ApplyFrameRatePreset();
        }

        using (new EditorGUI.DisabledScope(settings.FrameRatePreset != AnimationVitBakeFrameRatePreset.Custom))
        {
            settings.FrameRate = Mathf.Max(1, EditorGUILayout.IntField("Frame Rate", settings.FrameRate));
        }
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
        settings.OutputName = EditorGUILayout.TextField(new GUIContent("File Name", "Leave empty to use source root name."), settings.OutputName);
    }

    private void DrawSummary()
    {
        EditorGUILayout.Space(12f);
        int validClipCount = CountValidClips();
        int totalFrames = EstimateTotalFrames();
        int rendererCount = settings.SourceRoot != null ? settings.SourceRoot.GetComponentsInChildren<SpriteRenderer>(true).Length : 0;
        MessageType messageType = settings.SourceRoot == null || validClipCount == 0 ? MessageType.Warning : MessageType.Info;
        EditorGUILayout.HelpBox(
            string.Format(
                "SpriteRenderers: {0}\nSelected Clips: {1}\nEstimated Frames: {2}\nVIT FPS: {3}\nV1 Limits: SpriteRenderer only, one source texture, stable topology and UV, fixed draw order",
                rendererCount,
                validClipCount,
                totalFrames,
                settings.FrameRate),
            messageType);
    }

    private void Bake()
    {
        try
        {
            AnimationVitBakeResult result = AnimationVitBaker.Bake(settings);
            EditorUtility.DisplayDialog(
                "Animation VIT Baker",
                "Animation VIT baked:\n" + result.AssetPath + "\nClips: " + result.ClipCount + "\nFrames: " + result.TotalFrameCount + "\nVertices: " + result.VertexCount,
                "OK");
            Debug.Log("Animation VIT baked: " + result.AssetPath + ", clips: " + result.ClipCount + ", frames: " + result.TotalFrameCount + ", vertices: " + result.VertexCount);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Animation VIT Baker", exception.Message, "OK");
        }
    }

    private AnimationClip[] ReadClipsFromSource(GameObject sourceRoot)
    {
        if (sourceRoot == null)
        {
            return Array.Empty<AnimationClip>();
        }

        List<AnimationClip> clips = new List<AnimationClip>();

        Animation animation = sourceRoot.GetComponent<Animation>();
        if (animation != null)
        {
            foreach (AnimationState state in animation)
            {
                AddClip(clips, state != null ? state.clip : null);
            }
        }

        Animator animator = sourceRoot.GetComponent<Animator>();
        RuntimeAnimatorController controller = animator != null ? animator.runtimeAnimatorController : null;
        if (controller != null)
        {
            AnimationClip[] controllerClips = controller.animationClips;
            for (int i = 0; i < controllerClips.Length; i++)
            {
                AddClip(clips, controllerClips[i]);
            }
        }

        AnimatorController editorController = controller as AnimatorController;
        if (editorController != null)
        {
            for (int layerIndex = 0; layerIndex < editorController.layers.Length; layerIndex++)
            {
                CollectClipsFromStateMachine(clips, editorController.layers[layerIndex].stateMachine);
            }
        }

        return clips.ToArray();
    }

    private static void CollectClipsFromStateMachine(List<AnimationClip> clips, AnimatorStateMachine stateMachine)
    {
        if (stateMachine == null)
        {
            return;
        }

        ChildAnimatorState[] states = stateMachine.states;
        for (int i = 0; i < states.Length; i++)
        {
            Motion motion = states[i].state != null ? states[i].state.motion : null;
            CollectClipsFromMotion(clips, motion);
        }

        ChildAnimatorStateMachine[] childStateMachines = stateMachine.stateMachines;
        for (int i = 0; i < childStateMachines.Length; i++)
        {
            CollectClipsFromStateMachine(clips, childStateMachines[i].stateMachine);
        }
    }

    private static void CollectClipsFromMotion(List<AnimationClip> clips, Motion motion)
    {
        AnimationClip clip = motion as AnimationClip;
        if (clip != null)
        {
            AddClip(clips, clip);
            return;
        }

        BlendTree blendTree = motion as BlendTree;
        if (blendTree == null)
        {
            return;
        }

        ChildMotion[] children = blendTree.children;
        for (int i = 0; i < children.Length; i++)
        {
            CollectClipsFromMotion(clips, children[i].motion);
        }
    }

    private static void AddClip(List<AnimationClip> clips, AnimationClip clip)
    {
        if (clip != null && !clips.Contains(clip))
        {
            clips.Add(clip);
        }
    }

    private int CountValidClips()
    {
        if (settings.Clips == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < settings.Clips.Length; i++)
        {
            if (settings.Clips[i] != null)
            {
                count++;
            }
        }

        return count;
    }

    private int EstimateTotalFrames()
    {
        if (settings.Clips == null)
        {
            return 0;
        }

        int total = 0;
        int frameRate = Mathf.Max(1, settings.FrameRate);
        for (int i = 0; i < settings.Clips.Length; i++)
        {
            AnimationClip clip = settings.Clips[i];
            if (clip != null)
            {
                total += Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(0.001f, clip.length) * frameRate));
            }
        }

        return total;
    }
}
#endif
