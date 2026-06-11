#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.U2D;

public sealed class AtlasRenderAssetBakerWindow : EditorWindow
{
    private const string DefaultOutputFolder = "Assets/BakedSequences/AtlasRenderAssets";
    private const string SharedMaterialFolder = "Assets/BakedSequences/AtlasRenderAssets";
    private const string DefaultMaterialName = "AtlasRenderAsset_Material.mat";

    [SerializeField] private SpriteAtlas spriteAtlas;
    [SerializeField] private string outputFolder = DefaultOutputFolder;
    [SerializeField] private Vector2 defaultSize = Vector2.one;
    [SerializeField] private Color defaultColor = Color.white;
    [SerializeField] private bool overwriteExisting = true;
    [SerializeField] private bool useSpritePixelsPerUnit = true;

    private Vector2 scroll;
    private List<Sprite> previewSprites;
    private string previewAtlasPath;

    [MenuItem("Hybrid/Rendering/Atlas Render Asset Baker")]
    public static void Open()
    {
        AtlasRenderAssetBakerWindow window = GetWindow<AtlasRenderAssetBakerWindow>();
        window.titleContent = new GUIContent("Atlas Render Asset Baker");
        window.minSize = new Vector2(460f, 520f);
        window.Show();
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUI.BeginChangeCheck();
        DrawSource();
        DrawOutput();
        DrawDefaults();
        if (EditorGUI.EndChangeCheck())
        {
            RefreshPreview();
        }

        EditorGUILayout.Space(10f);
        DrawPreview();

        using (new EditorGUI.DisabledScope(!CanBake()))
        {
            if (GUILayout.Button("Generate Atlas Render Assets", GUILayout.Height(34f)))
            {
                Bake();
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawSource()
    {
        EditorGUILayout.LabelField("Source", EditorStyles.boldLabel);
        spriteAtlas = (SpriteAtlas)EditorGUILayout.ObjectField("Sprite Atlas", spriteAtlas, typeof(SpriteAtlas), false);
    }

    private void DrawOutput()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        outputFolder = EditorGUILayout.TextField("Folder", outputFolder);
        if (GUILayout.Button("Browse", GUILayout.Width(72f)))
        {
            string absolute = EditorUtility.OpenFolderPanel("Choose Output Folder", Application.dataPath, string.Empty);
            if (!string.IsNullOrEmpty(absolute))
            {
                outputFolder = ParticleAtlasPathUtility.AbsoluteToProjectPath(absolute);
            }
        }
        EditorGUILayout.EndHorizontal();

        overwriteExisting = EditorGUILayout.Toggle("Overwrite Existing", overwriteExisting);
    }

    private void DrawDefaults()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Asset Defaults", EditorStyles.boldLabel);
        useSpritePixelsPerUnit = EditorGUILayout.Toggle(new GUIContent("Use Sprite PPU Size", "When enabled, each asset size uses sprite.rect / sprite.pixelsPerUnit."), useSpritePixelsPerUnit);
        using (new EditorGUI.DisabledScope(useSpritePixelsPerUnit))
        {
            defaultSize = EditorGUILayout.Vector2Field("Default Size", defaultSize);
        }

        defaultColor = EditorGUILayout.ColorField("Default Color", defaultColor);
    }

    private void DrawPreview()
    {
        List<Sprite> sprites = GetSprites();
        MessageType messageType = sprites.Count > 0 ? MessageType.Info : MessageType.Warning;
        string message = sprites.Count > 0
            ? "Sprites: " + sprites.Count + "\nOutput: " + outputFolder
            : "Select a SpriteAtlas asset.";
        EditorGUILayout.HelpBox(message, messageType);

        if (sprites.Count == 0)
        {
            return;
        }

        int maxPreview = Mathf.Min(20, sprites.Count);
        EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
        for (int i = 0; i < maxPreview; i++)
        {
            Sprite sprite = sprites[i];
            Vector4 uvRect = CalculateUvRect(sprite);
            EditorGUILayout.LabelField(sprite.name, string.Format("uv=({0:0.###},{1:0.###},{2:0.###},{3:0.###}) size={4}", uvRect.x, uvRect.y, uvRect.z, uvRect.w, CalculateSize(sprite)));
        }

        if (sprites.Count > maxPreview)
        {
            EditorGUILayout.LabelField("...", (sprites.Count - maxPreview) + " more");
        }
    }

    private bool CanBake()
    {
        return spriteAtlas != null
            && !string.IsNullOrWhiteSpace(outputFolder)
            && outputFolder.StartsWith("Assets", StringComparison.Ordinal)
            && GetSprites().Count > 0;
    }

    private void Bake()
    {
        try
        {
            List<Sprite> sprites = GetSprites();
            Directory.CreateDirectory(ParticleAtlasPathUtility.ProjectPathToAbsolute(outputFolder));
            AssetDatabase.Refresh();

            Material runtimeMaterial = LoadOrCreateMaterial();
            int generated = 0;
            for (int i = 0; i < sprites.Count; i++)
            {
                if (CreateOrUpdateAsset(sprites[i], runtimeMaterial))
                {
                    generated++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Atlas Render Asset Baker", "Generated: " + generated + "\nFolder: " + outputFolder, "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Atlas Render Asset Baker", exception.Message, "OK");
        }
    }

    private bool CreateOrUpdateAsset(Sprite sprite, Material runtimeMaterial)
    {
        string assetPath = ParticleAtlasPathUtility.CombineProjectPath(outputFolder, SanitizeFileName(sprite.name) + "_Atlas.asset");
        AtlasRenderAsset asset = AssetDatabase.LoadAssetAtPath<AtlasRenderAsset>(assetPath);
        if (asset == null)
        {
            asset = CreateInstance<AtlasRenderAsset>();
            AssetDatabase.CreateAsset(asset, assetPath);
        }
        else if (!overwriteExisting)
        {
            return false;
        }

        asset.spriteAtlas = spriteAtlas;
        asset.spriteName = sprite.name;
        asset.sprite = sprite;
        asset.atlas = sprite.texture;
        asset.material = runtimeMaterial;
        asset.uvRect = CalculateUvRect(sprite);
        asset.size = CalculateSize(sprite);
        asset.color = defaultColor;
        asset.renderOffset = Vector2.zero;
        asset.renderScale = Vector2.one;
        asset.renderRotationDeg = 0f;

        EditorUtility.SetDirty(asset);
        return true;
    }

    private Material LoadOrCreateMaterial()
    {
        Directory.CreateDirectory(ParticleAtlasPathUtility.ProjectPathToAbsolute(SharedMaterialFolder));
        string materialPath = ParticleAtlasPathUtility.CombineProjectPath(SharedMaterialFolder, DefaultMaterialName);
        Material runtimeMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (runtimeMaterial == null)
        {
            Shader shader = Shader.Find("Hybrid/Baked Effect Atlas");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            }
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            runtimeMaterial = new Material(shader)
            {
                name = Path.GetFileNameWithoutExtension(materialPath),
                enableInstancing = true
            };
            AssetDatabase.CreateAsset(runtimeMaterial, materialPath);
        }

        runtimeMaterial.enableInstancing = true;
        Texture texture = GetMaterialPreviewTexture();
        if (runtimeMaterial.HasProperty("_MainTex"))
        {
            runtimeMaterial.SetTexture("_MainTex", texture);
        }
        if (runtimeMaterial.HasProperty("_BaseMap"))
        {
            runtimeMaterial.SetTexture("_BaseMap", texture);
        }
        if (runtimeMaterial.HasProperty("_Tint"))
        {
            runtimeMaterial.SetColor("_Tint", Color.white);
        }

        EditorUtility.SetDirty(runtimeMaterial);
        return runtimeMaterial;
    }

    private List<Sprite> GetSprites()
    {
        string atlasPath = spriteAtlas != null ? AssetDatabase.GetAssetPath(spriteAtlas) : string.Empty;
        if (previewSprites != null && previewAtlasPath == atlasPath)
        {
            return previewSprites;
        }

        previewAtlasPath = atlasPath;
        previewSprites = new List<Sprite>();
        if (string.IsNullOrEmpty(atlasPath))
        {
            return previewSprites;
        }

        int spriteCount = spriteAtlas.spriteCount;
        Sprite[] sprites = new Sprite[spriteCount];
        spriteAtlas.GetSprites(sprites);
        for (int i = 0; i < sprites.Length; i++)
        {
            Sprite sprite = sprites[i];
            if (sprite != null)
            {
                sprite.name = TrimCloneSuffix(sprite.name);
                previewSprites.Add(sprite);
            }
        }

        previewSprites.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        return previewSprites;
    }

    private void RefreshPreview()
    {
        previewSprites = null;
        previewAtlasPath = null;
    }

    private Vector4 CalculateUvRect(Sprite sprite)
    {
        Vector2[] uvs = sprite.uv;
        if (uvs == null || uvs.Length == 0)
        {
            return new Vector4(0f, 0f, 1f, 1f);
        }

        float minX = uvs[0].x;
        float minY = uvs[0].y;
        float maxX = uvs[0].x;
        float maxY = uvs[0].y;
        for (int i = 1; i < uvs.Length; i++)
        {
            Vector2 uv = uvs[i];
            minX = Mathf.Min(minX, uv.x);
            minY = Mathf.Min(minY, uv.y);
            maxX = Mathf.Max(maxX, uv.x);
            maxY = Mathf.Max(maxY, uv.y);
        }

        return new Vector4(minX, minY, Mathf.Max(0f, maxX - minX), Mathf.Max(0f, maxY - minY));
    }

    private Vector2 CalculateSize(Sprite sprite)
    {
        if (!useSpritePixelsPerUnit)
        {
            return defaultSize == Vector2.zero ? Vector2.one : defaultSize;
        }

        float ppu = Mathf.Max(1f, sprite.pixelsPerUnit);
        Rect rect = sprite.rect;
        return new Vector2(Mathf.Max(0.0001f, rect.width / ppu), Mathf.Max(0.0001f, rect.height / ppu));
    }

    private static string SanitizeFileName(string rawName)
    {
        string safeName = string.IsNullOrWhiteSpace(rawName) ? "AtlasSprite" : rawName;
        foreach (char invalidChar in Path.GetInvalidFileNameChars())
        {
            safeName = safeName.Replace(invalidChar, '_');
        }

        return safeName;
    }

    private Texture GetMaterialPreviewTexture()
    {
        List<Sprite> sprites = GetSprites();
        return sprites.Count > 0 && sprites[0] != null ? sprites[0].texture : null;
    }

    private static string TrimCloneSuffix(string spriteName)
    {
        const string cloneSuffix = "(Clone)";
        if (!string.IsNullOrEmpty(spriteName) && spriteName.EndsWith(cloneSuffix, StringComparison.Ordinal))
        {
            return spriteName.Substring(0, spriteName.Length - cloneSuffix.Length);
        }

        return spriteName;
    }
}
#endif
