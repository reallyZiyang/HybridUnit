#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public sealed class SequenceAtlasBakerWindow : EditorWindow
{
    private const string DefaultSettingsAssetPath = ParticleAtlasBakeSettings.DefaultOutputFolder + "/ParticleAtlasBakeSettings.asset";

    [SerializeField] private ParticleAtlasBakeSettings settings;
    private Vector2 scroll;

    [MenuItem("烘培工具/粒子图集烘培")]
    public static void Open()
    {
        SequenceAtlasBakerWindow window = GetWindow<SequenceAtlasBakerWindow>();
        window.titleContent = new GUIContent("Particle Atlas Baker");
        window.minSize = new Vector2(460f, 600f);
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
        DrawParticleSampling();
        DrawOutput();
        DrawRendering();
        DrawPrefabTransform();

        EditorGUILayout.Space(12f);
        DrawSummary();

        using (new EditorGUI.DisabledScope(!ParticleAtlasBaker.CanBake(settings)))
        {
            if (GUILayout.Button("Bake Particle Atlas", GUILayout.Height(34f)))
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

        settings = AssetDatabase.LoadAssetAtPath<ParticleAtlasBakeSettings>(DefaultSettingsAssetPath);
        if (settings != null)
        {
            return;
        }

        System.IO.Directory.CreateDirectory(ParticleAtlasPathUtility.ProjectPathToAbsolute(ParticleAtlasBakeSettings.DefaultOutputFolder));
        AssetDatabase.Refresh();

        settings = CreateInstance<ParticleAtlasBakeSettings>();
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
        settings.Prefab = (GameObject)EditorGUILayout.ObjectField("Prefab", settings.Prefab, typeof(GameObject), false);
        DrawRendererFilter();
    }

    private void DrawRendererFilter()
    {
        string[] values = GetRendererFilterValues(settings.Prefab);
        string[] labels = new string[values.Length];
        labels[0] = "All Particle Renderers";
        for (int i = 1; i < labels.Length; i++)
        {
            labels[i] = values[i];
        }

        int currentIndex = Array.IndexOf(values, settings.RendererNameFilter);
        if (currentIndex < 0)
        {
            currentIndex = 0;
            settings.RendererNameFilter = string.Empty;
        }

        using (new EditorGUI.DisabledScope(settings.Prefab == null || values.Length <= 1))
        {
            int nextIndex = EditorGUILayout.Popup(
                new GUIContent("Renderer Filter", "Choose one ParticleSystemRenderer from the selected prefab, or bake all renderers."),
                currentIndex,
                labels);
            settings.RendererNameFilter = values[Mathf.Clamp(nextIndex, 0, values.Length - 1)];
        }
    }

    private static string[] GetRendererFilterValues(GameObject prefab)
    {
        List<string> values = new List<string> { string.Empty };
        if (prefab == null)
        {
            return values.ToArray();
        }

        ParticleSystemRenderer[] renderers = prefab.GetComponentsInChildren<ParticleSystemRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            ParticleSystemRenderer particleRenderer = renderers[i];
            if (particleRenderer == null || particleRenderer.gameObject == null)
            {
                continue;
            }

            string rendererName = particleRenderer.gameObject.name;
            if (!string.IsNullOrWhiteSpace(rendererName) && !values.Contains(rendererName))
            {
                values.Add(rendererName);
            }
        }

        return values.ToArray();
    }

    private void DrawBakeSettings()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Bake Settings", EditorStyles.boldLabel);
        settings.Loop = EditorGUILayout.Toggle(new GUIContent("Loop", "Uses the source ParticleSystem duration and enables loop/prewarm on the bake instance."), settings.Loop);
        using (new EditorGUI.DisabledScope(settings.Loop))
        {
            settings.Duration = EditorGUILayout.FloatField("Duration", settings.Duration);
        }
        if (settings.Loop)
        {
            float bakeDuration = ParticleAtlasBakeUtility.GetBakeDuration(settings);
            EditorGUILayout.LabelField("Particle Duration", bakeDuration.ToString("0.###") + "s");
            settings.LoopBlend = EditorGUILayout.Toggle(new GUIContent("Loop Blend", "Crossfades the last few frames into the first few frames to soften the loop seam."), settings.LoopBlend);
            using (new EditorGUI.DisabledScope(!settings.LoopBlend))
            {
                settings.LoopBlendFrames = EditorGUILayout.IntField(new GUIContent("Loop Blend Frames", "Number of tail frames blended toward the start of the sequence."), settings.LoopBlendFrames);
            }
        }
        else
        {
            settings.LoopBlend = false;
        }

        EditorGUI.BeginChangeCheck();
        settings.ResolutionPreset = (ParticleAtlasBakeResolutionPreset)EditorGUILayout.EnumPopup("Resolution Preset", settings.ResolutionPreset);
        if (EditorGUI.EndChangeCheck())
        {
            settings.ApplyResolutionPreset();
        }

        using (new EditorGUI.DisabledScope(settings.ResolutionPreset != ParticleAtlasBakeResolutionPreset.Custom))
        {
            settings.FrameWidth = EditorGUILayout.IntField("Frame Width", settings.FrameWidth);
            settings.FrameHeight = EditorGUILayout.IntField("Frame Height", settings.FrameHeight);
        }

        EditorGUI.BeginChangeCheck();
        settings.FrameRatePreset = (ParticleAtlasBakeFrameRatePreset)EditorGUILayout.EnumPopup("Frame Rate Preset", settings.FrameRatePreset);
        if (EditorGUI.EndChangeCheck())
        {
            settings.ApplyFrameRatePreset();
        }

        using (new EditorGUI.DisabledScope(settings.FrameRatePreset != ParticleAtlasBakeFrameRatePreset.Custom))
        {
            settings.FrameRate = EditorGUILayout.IntField("Frame Rate", settings.FrameRate);
        }

        settings.Columns = EditorGUILayout.IntField(new GUIContent("Columns", "0 means automatic power-of-two area optimization."), settings.Columns);
        using (new EditorGUI.DisabledScope(settings.Columns > 0))
        {
            settings.MaxAtlasAspect = EditorGUILayout.FloatField(new GUIContent("Max Atlas Aspect", "Automatic layout avoids atlases wider or taller than this aspect ratio when possible."), settings.MaxAtlasAspect);
        }
        settings.MaxAtlasSize = EditorGUILayout.IntField("Max Atlas Size", settings.MaxAtlasSize);
        EditorGUILayout.LabelField("Power Of Two Atlas", "Always enabled");
        settings.FirstFrameTopLeft = EditorGUILayout.Toggle("First Frame Top Left", settings.FirstFrameTopLeft);
        settings.AlphaFromColor = EditorGUILayout.Toggle(new GUIContent("Alpha From RGB", "Useful for additive particle shaders that render color but leave alpha at 0."), settings.AlphaFromColor);
        settings.UseBakeParticleMaterial = EditorGUILayout.Toggle(new GUIContent("Use Bake Material", "Temporarily replaces legacy particle materials on the bake instance with a render-pipeline-compatible unlit particle material."), settings.UseBakeParticleMaterial);
        settings.TrimEmptyHead = EditorGUILayout.Toggle(new GUIContent("Trim Empty Head", "Removes leading fully empty frames so looping effects start from the first visible frame."), settings.TrimEmptyHead);
        settings.TrimEmptyTail = EditorGUILayout.Toggle(new GUIContent("Trim Empty Tail", "Removes trailing fully empty frames before packing the atlas."), settings.TrimEmptyTail);
        settings.TrimFrameRects = EditorGUILayout.Toggle(new GUIContent("Trim Frame Rects", "Records each frame's visible pixel rect so runtime can draw a tight quad instead of a full frame quad."), settings.TrimFrameRects);
        using (new EditorGUI.DisabledScope(!settings.TrimFrameRects))
        {
            settings.FrameRectPadding = EditorGUILayout.IntField(new GUIContent("Rect Padding", "Extra pixels added around each visible frame rect to avoid bilinear clipping."), settings.FrameRectPadding);
        }
        settings.AntiAliasing = EditorGUILayout.IntPopup("Anti Aliasing", settings.AntiAliasing, new[] { "1x", "2x", "4x", "8x" }, new[] { 1, 2, 4, 8 });
    }

    private void DrawParticleSampling()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Particle Sampling", EditorStyles.boldLabel);
        settings.ForceRandomSeed = EditorGUILayout.Toggle("Force Random Seed", settings.ForceRandomSeed);
        using (new EditorGUI.DisabledScope(!settings.ForceRandomSeed))
        {
            long seed = EditorGUILayout.LongField("Random Seed", settings.RandomSeed);
            settings.RandomSeed = ParticleAtlasBakeUtility.ClampToUInt(seed);
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
        settings.OutputName = EditorGUILayout.TextField(new GUIContent("File Name", "Leave empty to use prefab name."), settings.OutputName);
        settings.GenerateMetadata = EditorGUILayout.Toggle("Generate Metadata JSON", settings.GenerateMetadata);
        settings.GenerateSequenceAsset = EditorGUILayout.Toggle("Generate Sequence Asset", settings.GenerateSequenceAsset);
        settings.ConfigureTextureImporter = EditorGUILayout.Toggle("Configure Importer", settings.ConfigureTextureImporter);
    }

    private void DrawRendering()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Rendering", EditorStyles.boldLabel);
        settings.TransparentBackground = EditorGUILayout.Toggle("Transparent Background", settings.TransparentBackground);
        using (new EditorGUI.DisabledScope(settings.TransparentBackground))
        {
            settings.BackgroundColor = EditorGUILayout.ColorField("Background Color", settings.BackgroundColor);
        }
        settings.Orthographic = EditorGUILayout.Toggle("Orthographic Camera", settings.Orthographic);
        if (settings.Orthographic)
        {
            settings.OrthographicSize = EditorGUILayout.FloatField("Orthographic Size", settings.OrthographicSize);
        }
        else
        {
            settings.FieldOfView = EditorGUILayout.FloatField("Field Of View", settings.FieldOfView);
        }
        settings.AutoFrameCamera = EditorGUILayout.Toggle(new GUIContent("Auto Frame Camera", "Simulates the effect once and frames all particle renderer bounds before baking."), settings.AutoFrameCamera);
        settings.BakeLayer = EditorGUILayout.IntSlider(new GUIContent("Bake Layer", "Temporary layer used by the bake instance and camera culling mask. Choose a layer not used by visible scene objects."), settings.BakeLayer, 0, 31);
        settings.CameraPosition = EditorGUILayout.Vector3Field("Camera Position", settings.CameraPosition);
        settings.CameraEulerAngles = EditorGUILayout.Vector3Field("Camera Rotation", settings.CameraEulerAngles);
        settings.AddDirectionalLight = EditorGUILayout.Toggle("Add Directional Light", settings.AddDirectionalLight);
    }

    private void DrawPrefabTransform()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Prefab Transform", EditorStyles.boldLabel);
        settings.PrefabPosition = EditorGUILayout.Vector3Field("Position", settings.PrefabPosition);
        settings.PrefabEulerAngles = EditorGUILayout.Vector3Field("Rotation", settings.PrefabEulerAngles);
        settings.PrefabScale = EditorGUILayout.Vector3Field("Scale", settings.PrefabScale);
    }

    private void DrawSummary()
    {
        float bakeDuration = ParticleAtlasBakeUtility.GetBakeDuration(settings);
        ParticleAtlasLayout layout = ParticleAtlasLayoutUtility.CalculateLayout(settings, ParticleAtlasLayoutUtility.CalculateFrameCount(settings));
        MessageType messageType = layout.AtlasWidth > settings.MaxAtlasSize || layout.AtlasHeight > settings.MaxAtlasSize ? MessageType.Warning : MessageType.Info;
        string prefix = settings.TrimFrameRects ? "Fixed-cell estimate before tight rect packing.\n" : string.Empty;
        EditorGUILayout.HelpBox(
            prefix + string.Format(
                "Duration: {0:0.###}s\nFrames: {1}\nLayout: {2} x {3}\nUsed: {4} x {5}\nAtlas: {6} x {7}\nPOT Waste: {8:0.##}%",
                bakeDuration,
                layout.FrameCount,
                layout.Columns,
                layout.Rows,
                layout.UsedAtlasWidth,
                layout.UsedAtlasHeight,
                layout.AtlasWidth,
                layout.AtlasHeight,
                layout.WastePercent),
            messageType);
    }

    private void Bake()
    {
        try
        {
            ParticleAtlasBakeResult result = ParticleAtlasBaker.Bake(settings);
            if (result.VisiblePixelCount == 0)
            {
                string warning = "Atlas baked but no visible pixels were detected. Check particle material shader compatibility, camera framing, and particle emission settings.";
                EditorUtility.DisplayDialog("Particle Atlas Baker", warning + "\n" + result.AtlasProjectPath, "OK");
                Debug.LogWarning(warning + " Path: " + result.AtlasProjectPath);
            }
            else
            {
                string assetLine = string.IsNullOrEmpty(result.SequenceAssetProjectPath) ? string.Empty : "\nAsset: " + result.SequenceAssetProjectPath;
                EditorUtility.DisplayDialog("Particle Atlas Baker", "Atlas baked:\n" + result.AtlasProjectPath + assetLine + "\nFrames: " + result.OutputFrameCount + " / " + result.RequestedFrameCount + "\nVisible pixels: " + result.VisiblePixelCount + "\nFirst visible frame: " + result.FirstVisibleFrame + "\nLast visible frame: " + result.LastVisibleFrame, "OK");
                Debug.Log("Particle atlas baked: " + result.AtlasProjectPath + assetLine + ", frames: " + result.OutputFrameCount + " / " + result.RequestedFrameCount + ", visible pixels: " + result.VisiblePixelCount + ", first visible frame: " + result.FirstVisibleFrame + ", last visible frame: " + result.LastVisibleFrame);
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Particle Atlas Baker", exception.Message, "OK");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }
}
#endif
