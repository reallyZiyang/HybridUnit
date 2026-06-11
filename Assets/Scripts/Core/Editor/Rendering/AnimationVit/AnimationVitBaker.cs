#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class AnimationVitBaker
{
    private const float UvTolerance = 0.00001f;

    public static bool CanBake(AnimationVitBakeSettings settings)
    {
        return settings != null
            && settings.SourceRoot != null
            && settings.Clips != null
            && settings.Clips.Length > 0
            && settings.FrameRate > 0
            && !string.IsNullOrWhiteSpace(settings.OutputFolder);
    }

    public static AnimationVitBakeResult Bake(AnimationVitBakeSettings settings)
    {
        Validate(settings);

        AnimationClip[] clips = ResolveClips(settings.Clips);
        int[] frameCounts = CalculateFrameCounts(clips, settings.FrameRate);
        int totalFrameCount = 0;
        for (int i = 0; i < frameCounts.Length; i++)
        {
            totalFrameCount += frameCounts[i];
        }

        Texture2D sourceTexture = GetSingleSourceTexture(settings.SourceRoot);
        Texture2D runtimeSourceTexture = CreateRuntimeSourceTexture(sourceTexture, settings);

        AnimationVitSample firstSample;
        Bounds combinedBounds;
        Color[] positionPixels;
        Color32[] colorPixels;
        BakedAnimationVitClip[] bakedClips = new BakedAnimationVitClip[clips.Length];
        int vertexCount;

        using (AnimationVitMeshSampler sampler = new AnimationVitMeshSampler(settings.SourceRoot))
        {
            firstSample = sampler.Sample(clips[0], 0f);
            ValidateFirstSample(firstSample);

            vertexCount = firstSample.Vertices.Length;
            ValidateTextureSize(vertexCount, totalFrameCount);
            positionPixels = new Color[vertexCount * totalFrameCount];
            colorPixels = new Color32[vertexCount * totalFrameCount];
            combinedBounds = firstSample.Bounds;

            int frameCursor = 0;
            for (int clipIndex = 0; clipIndex < clips.Length; clipIndex++)
            {
                AnimationClip clip = clips[clipIndex];
                int clipFrameCount = frameCounts[clipIndex];
                bakedClips[clipIndex] = new BakedAnimationVitClip
                {
                    name = clip.name,
                    startFrame = frameCursor,
                    frameCount = clipFrameCount,
                    duration = clipFrameCount / (float)settings.FrameRate,
                    loop = true
                };

                for (int frameIndex = 0; frameIndex < clipFrameCount; frameIndex++)
                {
                    float sampleTime = frameIndex / (float)settings.FrameRate;
                    AnimationVitSample sample = sampler.Sample(clip, sampleTime);
                    ValidateFrameTopology(firstSample, sample, clip.name, frameIndex);
                    WriteFramePixels(sample, positionPixels, colorPixels, vertexCount, frameCursor + frameIndex);
                    combinedBounds.Encapsulate(sample.Bounds);
                }

                frameCursor += clipFrameCount;
            }
        }

        Mesh mesh = CreateMesh(firstSample, combinedBounds);
        Texture2D positionTexture = CreatePositionTexture(vertexCount, totalFrameCount, positionPixels);
        Texture2D colorTexture = CreateColorTexture(vertexCount, totalFrameCount, colorPixels);
        Material material = CreateMaterial(runtimeSourceTexture, positionTexture, colorTexture);
        BakedAnimationVitAsset asset = CreateAssetObject(mesh, material, runtimeSourceTexture, positionTexture, colorTexture, settings.FrameRate, vertexCount, totalFrameCount, combinedBounds, bakedClips);

        string assetPath = SaveAsset(settings, asset, mesh, runtimeSourceTexture, sourceTexture, positionTexture, colorTexture, material);
        return new AnimationVitBakeResult(assetPath, vertexCount, totalFrameCount, bakedClips.Length);
    }

    private static void Validate(AnimationVitBakeSettings settings)
    {
        if (!CanBake(settings))
        {
            throw new InvalidOperationException("Please choose SourceRoot, at least one AnimationClip, output folder, and a positive frame rate.");
        }

        if (!settings.OutputFolder.StartsWith("Assets", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Output folder must be inside this Unity project Assets folder.");
        }

        if (settings.SourceTextureMaxSize <= 0)
        {
            throw new InvalidOperationException("Source Texture Max Size must be positive.");
        }
    }

    private static AnimationClip[] ResolveClips(AnimationClip[] clips)
    {
        List<AnimationClip> resolved = new List<AnimationClip>(clips.Length);
        for (int i = 0; i < clips.Length; i++)
        {
            AnimationClip clip = clips[i];
            if (clip == null)
            {
                continue;
            }

            if (!resolved.Contains(clip))
            {
                resolved.Add(clip);
            }
        }

        if (resolved.Count == 0)
        {
            throw new InvalidOperationException("No valid AnimationClip selected.");
        }

        return resolved.ToArray();
    }

    private static int[] CalculateFrameCounts(AnimationClip[] clips, int frameRate)
    {
        int[] frameCounts = new int[clips.Length];
        for (int i = 0; i < clips.Length; i++)
        {
            frameCounts[i] = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(0.001f, clips[i].length) * frameRate));
        }

        return frameCounts;
    }

    private static Texture2D GetSingleSourceTexture(GameObject sourceRoot)
    {
        SpriteRenderer[] renderers = sourceRoot.GetComponentsInChildren<SpriteRenderer>(true);
        Texture2D sourceTexture = null;
        for (int i = 0; i < renderers.Length; i++)
        {
            Sprite sprite = renderers[i].sprite;
            if (sprite == null)
            {
                continue;
            }

            Texture2D texture = sprite.texture;
            if (texture == null)
            {
                continue;
            }

            if (sourceTexture == null)
            {
                sourceTexture = texture;
                continue;
            }

            if (sourceTexture != texture)
            {
                throw new InvalidOperationException("Animation VIT v1 only supports one source texture. Pack sprites into one atlas or use Sequence Atlas for sprite-swap animations.");
            }
        }

        if (sourceTexture == null)
        {
            throw new InvalidOperationException("SourceRoot has no SpriteRenderer with a valid Sprite texture.");
        }

        return sourceTexture;
    }

    private static Texture2D CreateRuntimeSourceTexture(Texture2D sourceTexture, AnimationVitBakeSettings settings)
    {
        int maxSize = Mathf.Clamp(settings.SourceTextureMaxSize, 1, SystemInfo.maxTextureSize);
        int sourceWidth = sourceTexture.width;
        int sourceHeight = sourceTexture.height;
        int sourceMaxSide = Mathf.Max(sourceWidth, sourceHeight);
        if (sourceMaxSide <= maxSize)
        {
            sourceTexture.wrapMode = TextureWrapMode.Clamp;
            return sourceTexture;
        }

        float scale = maxSize / (float)sourceMaxSide;
        int targetWidth = Mathf.Max(1, Mathf.RoundToInt(sourceWidth * scale));
        int targetHeight = Mathf.Max(1, Mathf.RoundToInt(sourceHeight * scale));
        RenderTexture previous = RenderTexture.active;
        RenderTexture renderTexture = RenderTexture.GetTemporary(targetWidth, targetHeight, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
        Texture2D copy = new Texture2D(targetWidth, targetHeight, TextureFormat.RGBA32, false, false)
        {
            name = sourceTexture.name + "_RuntimeAtlas",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        try
        {
            Graphics.Blit(sourceTexture, renderTexture);
            RenderTexture.active = renderTexture;
            copy.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0, false);
            copy.Apply(false, true);
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(renderTexture);
        }

        return copy;
    }

    private static void ValidateFirstSample(AnimationVitSample sample)
    {
        if (sample.Vertices == null || sample.Vertices.Length == 0 || sample.Triangles == null || sample.Triangles.Length == 0)
        {
            throw new InvalidOperationException("First sampled frame produced an empty mesh.");
        }

        if (sample.Uvs == null || sample.Uvs.Length != sample.Vertices.Length || sample.Colors == null || sample.Colors.Length != sample.Vertices.Length)
        {
            throw new InvalidOperationException("First sampled frame has invalid vertex stream sizes.");
        }
    }

    private static void ValidateTextureSize(int vertexCount, int totalFrameCount)
    {
        int maxTextureSize = SystemInfo.maxTextureSize;
        if (vertexCount > maxTextureSize || totalFrameCount > maxTextureSize)
        {
            throw new InvalidOperationException(string.Format("VIT texture size {0}x{1} exceeds max texture size {2}. Reduce renderers, clips, duration, or frame rate.", vertexCount, totalFrameCount, maxTextureSize));
        }
    }

    private static void ValidateFrameTopology(AnimationVitSample reference, AnimationVitSample sample, string clipName, int frameIndex)
    {
        if (sample.Vertices.Length != reference.Vertices.Length)
        {
            throw new InvalidOperationException(string.Format("Topology changed in clip {0} frame {1}: vertex count {2} != {3}. Sprite swaps, null sprites, or dynamic renderer lists are not supported by Animation VIT v1.", clipName, frameIndex, sample.Vertices.Length, reference.Vertices.Length));
        }

        if (sample.Triangles.Length != reference.Triangles.Length)
        {
            throw new InvalidOperationException(string.Format("Topology changed in clip {0} frame {1}: triangle index count {2} != {3}.", clipName, frameIndex, sample.Triangles.Length, reference.Triangles.Length));
        }

        for (int i = 0; i < reference.Triangles.Length; i++)
        {
            if (sample.Triangles[i] != reference.Triangles[i])
            {
                throw new InvalidOperationException(string.Format("Topology changed in clip {0} frame {1}: triangle index mismatch at {2}.", clipName, frameIndex, i));
            }
        }

        for (int i = 0; i < reference.Uvs.Length; i++)
        {
            if ((sample.Uvs[i] - reference.Uvs[i]).sqrMagnitude > UvTolerance * UvTolerance)
            {
                throw new InvalidOperationException(string.Format("UV changed in clip {0} frame {1}: vertex {2}. Sprite swaps are not supported by Animation VIT v1.", clipName, frameIndex, i));
            }
        }
    }

    private static void WriteFramePixels(AnimationVitSample sample, Color[] positionPixels, Color32[] colorPixels, int vertexCount, int frameIndex)
    {
        int rowOffset = frameIndex * vertexCount;
        for (int vertexIndex = 0; vertexIndex < vertexCount; vertexIndex++)
        {
            Vector3 vertex = sample.Vertices[vertexIndex];
            positionPixels[rowOffset + vertexIndex] = new Color(vertex.x, vertex.y, vertex.z, 1f);
            colorPixels[rowOffset + vertexIndex] = sample.Colors[vertexIndex];
        }
    }

    private static Mesh CreateMesh(AnimationVitSample sample, Bounds bounds)
    {
        Vector3[] vertices = new Vector3[sample.Vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i] = Vector3.zero;
        }

        Mesh mesh = new Mesh
        {
            name = "Animation VIT Mesh"
        };
        if (vertices.Length > 65535)
        {
            mesh.indexFormat = IndexFormat.UInt32;
        }

        mesh.vertices = vertices;
        mesh.uv = sample.Uvs;
        mesh.triangles = sample.Triangles;
        mesh.bounds = bounds;
        return mesh;
    }

    private static Texture2D CreatePositionTexture(int vertexCount, int totalFrameCount, Color[] pixels)
    {
        Texture2D texture = new Texture2D(vertexCount, totalFrameCount, TextureFormat.RGBAHalf, false, true)
        {
            name = "Animation VIT Positions",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private static Texture2D CreateColorTexture(int vertexCount, int totalFrameCount, Color32[] pixels)
    {
        Texture2D texture = new Texture2D(vertexCount, totalFrameCount, TextureFormat.RGBA32, false, false)
        {
            name = "Animation VIT Colors",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private static Material CreateMaterial(Texture2D sourceTexture, Texture2D positionTexture, Texture2D colorTexture)
    {
        Shader shader = Shader.Find("Hybrid/Animation VIT");
        if (shader == null)
        {
            throw new InvalidOperationException("Shader not found: Hybrid/Animation VIT");
        }

        Material material = new Material(shader)
        {
            name = "Animation VIT Material",
            enableInstancing = true
        };
        material.SetTexture("_MainTex", sourceTexture);
        material.SetTexture("_PositionTex", positionTexture);
        material.SetTexture("_ColorTex", colorTexture);
        material.SetVector("_RenderTrans", new Vector4(0f, 0f, 1f, 1f));
        material.SetVector("_RenderRotation", new Vector4(1f, 0f, 0f, 0f));
        return material;
    }

    private static BakedAnimationVitAsset CreateAssetObject(Mesh mesh, Material material, Texture2D sourceTexture, Texture2D positionTexture, Texture2D colorTexture, int frameRate, int vertexCount, int totalFrameCount, Bounds bounds, BakedAnimationVitClip[] clips)
    {
        BakedAnimationVitAsset asset = ScriptableObject.CreateInstance<BakedAnimationVitAsset>();
        asset.mesh = mesh;
        asset.material = material;
        asset.sourceTexture = sourceTexture;
        asset.positionTexture = positionTexture;
        asset.colorTexture = colorTexture;
        asset.frameRate = frameRate;
        asset.vertexCount = vertexCount;
        asset.totalFrameCount = totalFrameCount;
        asset.bounds = bounds;
        asset.renderOffset = Vector2.zero;
        asset.renderScale = Vector2.one;
        asset.renderRotationDeg = 0f;
        asset.clips = clips;
        return asset;
    }

    private static string SaveAsset(AnimationVitBakeSettings settings, BakedAnimationVitAsset asset, Mesh mesh, Texture2D runtimeSourceTexture, Texture2D originalSourceTexture, Texture2D positionTexture, Texture2D colorTexture, Material material)
    {
        string fileBaseName = GetOutputBaseName(settings);
        string outputDirectoryAbsolute = ParticleAtlasPathUtility.ProjectPathToAbsolute(settings.OutputFolder);
        Directory.CreateDirectory(outputDirectoryAbsolute);

        string assetPath = ParticleAtlasPathUtility.CombineProjectPath(settings.OutputFolder, fileBaseName + "_AnimationVit.asset");
        AssetDatabase.DeleteAsset(assetPath);

        Texture2D persistentSourceTexture = SaveOrResolveRuntimeSourceTexture(settings, runtimeSourceTexture, originalSourceTexture);
        asset.sourceTexture = persistentSourceTexture;
        material.SetTexture("_MainTex", persistentSourceTexture);

        AssetDatabase.CreateAsset(asset, assetPath);

        mesh.name = fileBaseName + "_Mesh";
        positionTexture.name = fileBaseName + "_PositionVIT";
        colorTexture.name = fileBaseName + "_ColorVIT";
        material.name = fileBaseName + "_Material";
        AssetDatabase.AddObjectToAsset(mesh, asset);
        AssetDatabase.AddObjectToAsset(positionTexture, asset);
        AssetDatabase.AddObjectToAsset(colorTexture, asset);
        AssetDatabase.AddObjectToAsset(material, asset);
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(assetPath);
        return assetPath;
    }

    private static Texture2D SaveOrResolveRuntimeSourceTexture(AnimationVitBakeSettings settings, Texture2D runtimeSourceTexture, Texture2D originalSourceTexture)
    {
        if (runtimeSourceTexture == null)
        {
            throw new InvalidOperationException("Animation VIT runtime source texture is null.");
        }

        string runtimeAssetPath = AssetDatabase.GetAssetPath(runtimeSourceTexture);
        bool isPersistentAsset = !string.IsNullOrEmpty(runtimeAssetPath) && EditorUtility.IsPersistent(runtimeSourceTexture);
        bool needsRuntimeCopy = runtimeSourceTexture != originalSourceTexture || !isPersistentAsset;
        if (!needsRuntimeCopy)
        {
            runtimeSourceTexture.wrapMode = TextureWrapMode.Clamp;
            return runtimeSourceTexture;
        }

        string textureProjectPath = SaveSharedRuntimeSourceTexture(settings, runtimeSourceTexture);

        Texture2D importedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(textureProjectPath);
        if (importedTexture == null)
        {
            throw new InvalidOperationException("Failed to import Animation VIT runtime source texture: " + textureProjectPath);
        }

        importedTexture.wrapMode = TextureWrapMode.Clamp;
        return importedTexture;
    }

    private static string SaveSharedRuntimeSourceTexture(AnimationVitBakeSettings settings, Texture2D sourceTexture)
    {
        Texture2D readableTexture = CopyTextureToReadable(sourceTexture);
        try
        {
            byte[] pngBytes = readableTexture.EncodeToPNG();
            if (pngBytes == null || pngBytes.Length == 0)
            {
                throw new InvalidOperationException("Failed to encode Animation VIT runtime source texture as PNG.");
            }

            string hash = ComputeRuntimeAtlasHash(pngBytes);
            string textureProjectPath = ParticleAtlasPathUtility.CombineProjectPath(settings.OutputFolder, "AnimationVit_RuntimeAtlas_" + hash + ".png");
            string textureAbsolutePath = ParticleAtlasPathUtility.ProjectPathToAbsolute(textureProjectPath);
            string textureDirectory = Path.GetDirectoryName(textureAbsolutePath);
            if (!string.IsNullOrEmpty(textureDirectory))
            {
                Directory.CreateDirectory(textureDirectory);
            }

            if (!File.Exists(textureAbsolutePath))
            {
                File.WriteAllBytes(textureAbsolutePath, pngBytes);
                AssetDatabase.ImportAsset(textureProjectPath);
            }

            ConfigureRuntimeSourceTextureImporter(textureProjectPath);
            return textureProjectPath;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(readableTexture);
        }
    }

    private static string ComputeRuntimeAtlasHash(byte[] pngBytes)
    {
        using (SHA256 sha = SHA256.Create())
        {
            byte[] hashBytes = sha.ComputeHash(pngBytes);
            return BitConverter.ToString(hashBytes, 0, 8).Replace("-", string.Empty).ToLowerInvariant();
        }
    }

    private static Texture2D CopyTextureToReadable(Texture2D sourceTexture)
    {
        RenderTexture previous = RenderTexture.active;
        RenderTexture renderTexture = RenderTexture.GetTemporary(sourceTexture.width, sourceTexture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
        Texture2D readableTexture = new Texture2D(sourceTexture.width, sourceTexture.height, TextureFormat.RGBA32, false, false)
        {
            name = sourceTexture.name + "_ReadableCopy",
            filterMode = sourceTexture.filterMode,
            wrapMode = TextureWrapMode.Clamp
        };

        try
        {
            Graphics.Blit(sourceTexture, renderTexture);
            RenderTexture.active = renderTexture;
            readableTexture.ReadPixels(new Rect(0, 0, sourceTexture.width, sourceTexture.height), 0, 0, false);
            readableTexture.Apply(false, false);
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(renderTexture);
        }

        return readableTexture;
    }

    private static void ConfigureRuntimeSourceTextureImporter(string textureProjectPath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(textureProjectPath) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        importer.textureType = TextureImporterType.Default;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.sRGBTexture = true;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.SaveAndReimport();
    }

    private static string GetOutputBaseName(AnimationVitBakeSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.OutputName))
        {
            return settings.OutputName.Trim();
        }

        return settings.SourceRoot != null ? settings.SourceRoot.name : "Animation";
    }
}
#endif
