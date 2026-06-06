#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Spine;
using Spine.Unity;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class SpineVitBaker
{
    private const float UvTolerance = 0.00001f;

    public static bool CanBake(SpineVitBakeSettings settings)
    {
        return settings != null
            && settings.SkeletonDataAsset != null
            && settings.AnimationNames != null
            && settings.AnimationNames.Length > 0
            && settings.FrameRate > 0
            && !string.IsNullOrWhiteSpace(settings.OutputFolder);
    }

    public static SpineVitBakeResult Bake(SpineVitBakeSettings settings)
    {
        Validate(settings);

        SkeletonData skeletonData = settings.SkeletonDataAsset.GetSkeletonData(false);
        Material sourceMaterial = GetSingleSourceMaterial(settings.SkeletonDataAsset);
        Texture2D sourceTexture = sourceMaterial.mainTexture as Texture2D;
        if (sourceTexture == null)
        {
            throw new InvalidOperationException("Spine source material must use a Texture2D main texture.");
        }
        Texture2D runtimeSourceTexture = CreateRuntimeSourceTexture(sourceTexture, settings);

        Spine.Animation[] animations = ResolveAnimations(skeletonData, settings.AnimationNames);
        ValidateAnimations(animations);
        int[] frameCounts = CalculateFrameCounts(animations, settings.FrameRate);
        int totalFrameCount = 0;
        for (int i = 0; i < frameCounts.Length; i++)
        {
            totalFrameCount += frameCounts[i];
        }

        SpineVitMeshSampler sampler = new SpineVitMeshSampler(settings.SkeletonDataAsset, settings.SkinName, sourceMaterial);
        SpineVitSample firstSample = sampler.Sample(animations[0], 0f, true);
        ValidateFirstSample(firstSample);

        int vertexCount = firstSample.Vertices.Length;
        ValidateTextureSize(vertexCount, totalFrameCount);
        Color[] positionPixels = new Color[vertexCount * totalFrameCount];
        Color32[] colorPixels = new Color32[vertexCount * totalFrameCount];
        BakedSpineVitClip[] clips = new BakedSpineVitClip[animations.Length];
        Bounds combinedBounds = firstSample.Bounds;

        int frameCursor = 0;
        for (int clipIndex = 0; clipIndex < animations.Length; clipIndex++)
        {
            Spine.Animation animation = animations[clipIndex];
            int clipFrameCount = frameCounts[clipIndex];
            clips[clipIndex] = new BakedSpineVitClip
            {
                name = animation.Name,
                startFrame = frameCursor,
                frameCount = clipFrameCount,
                duration = clipFrameCount / (float)settings.FrameRate,
                loop = true
            };

            for (int frameIndex = 0; frameIndex < clipFrameCount; frameIndex++)
            {
                float sampleTime = frameIndex / (float)settings.FrameRate;
                SpineVitSample sample = sampler.Sample(animation, sampleTime, true);
                ValidateFrameTopology(firstSample, sample, animation.Name, frameIndex);
                WriteFramePixels(sample, positionPixels, colorPixels, vertexCount, frameCursor + frameIndex);
                combinedBounds.Encapsulate(sample.Bounds);
            }

            frameCursor += clipFrameCount;
        }

        Mesh mesh = CreateMesh(firstSample, combinedBounds);
        Texture2D positionTexture = CreatePositionTexture(vertexCount, totalFrameCount, positionPixels);
        Texture2D colorTexture = CreateColorTexture(vertexCount, totalFrameCount, colorPixels);
        Material material = CreateMaterial(runtimeSourceTexture, positionTexture, colorTexture);
        BakedSpineVitAsset asset = CreateAssetObject(mesh, material, runtimeSourceTexture, positionTexture, colorTexture, settings.FrameRate, vertexCount, totalFrameCount, combinedBounds, clips);

        string assetPath = SaveAsset(settings, asset, mesh, runtimeSourceTexture, sourceTexture, positionTexture, colorTexture, material);
        return new SpineVitBakeResult(assetPath, vertexCount, totalFrameCount, clips.Length);
    }

    private static void Validate(SpineVitBakeSettings settings)
    {
        if (!CanBake(settings))
        {
            throw new InvalidOperationException("Please choose SkeletonDataAsset, at least one animation, output folder, and a positive frame rate.");
        }

        if (!settings.OutputFolder.StartsWith("Assets", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Output folder must be inside this Unity project Assets folder.");
        }

        if (settings.SourceTextureMaxSize <= 0)
        {
            throw new InvalidOperationException("Source Atlas Max Size must be positive.");
        }

        SkeletonData skeletonData = settings.SkeletonDataAsset.GetSkeletonData(false);
        if (skeletonData == null)
        {
            throw new InvalidOperationException("SkeletonDataAsset failed to load SkeletonData.");
        }

        if (!string.IsNullOrEmpty(settings.SkinName) && skeletonData.FindSkin(settings.SkinName) == null)
        {
            throw new InvalidOperationException("Skin not found: " + settings.SkinName);
        }
    }

    private static Material GetSingleSourceMaterial(SkeletonDataAsset skeletonDataAsset)
    {
        if (skeletonDataAsset.atlasAssets == null || skeletonDataAsset.atlasAssets.Length != 1)
        {
            throw new InvalidOperationException("Spine VIT v1 only supports exactly one atlas asset.");
        }

        AtlasAssetBase atlasAsset = skeletonDataAsset.atlasAssets[0];
        if (atlasAsset == null || atlasAsset.MaterialCount != 1)
        {
            throw new InvalidOperationException("Spine VIT v1 only supports exactly one atlas material/page.");
        }

        Material material = atlasAsset.PrimaryMaterial;
        if (material == null)
        {
            throw new InvalidOperationException("Spine atlas primary material is missing.");
        }

        return material;
    }

    private static Texture2D CreateRuntimeSourceTexture(Texture2D sourceTexture, SpineVitBakeSettings settings)
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

    private static Spine.Animation[] ResolveAnimations(SkeletonData skeletonData, string[] animationNames)
    {
        List<Spine.Animation> animations = new List<Spine.Animation>(animationNames.Length);
        for (int i = 0; i < animationNames.Length; i++)
        {
            string animationName = animationNames[i];
            Spine.Animation animation = skeletonData.FindAnimation(animationName);
            if (animation == null)
            {
                throw new InvalidOperationException("Animation not found: " + animationName);
            }

            animations.Add(animation);
        }

        return animations.ToArray();
    }

    private static void ValidateAnimations(Spine.Animation[] animations)
    {
        for (int animationIndex = 0; animationIndex < animations.Length; animationIndex++)
        {
            Spine.Animation animation = animations[animationIndex];
            for (int timelineIndex = 0; timelineIndex < animation.Timelines.Count; timelineIndex++)
            {
                if (animation.Timelines.Items[timelineIndex] is DrawOrderTimeline)
                {
                    throw new InvalidOperationException("Spine VIT v1 does not support draw order timelines: " + animation.Name);
                }
            }
        }
    }

    private static int[] CalculateFrameCounts(Spine.Animation[] animations, int frameRate)
    {
        int[] frameCounts = new int[animations.Length];
        for (int i = 0; i < animations.Length; i++)
        {
            frameCounts[i] = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(0.001f, animations[i].Duration) * frameRate));
        }

        return frameCounts;
    }

    private static void ValidateFirstSample(SpineVitSample sample)
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
            throw new InvalidOperationException(string.Format("VIT texture size {0}x{1} exceeds max texture size {2}. Reduce vertices, animations, duration, or frame rate.", vertexCount, totalFrameCount, maxTextureSize));
        }
    }

    private static void ValidateFrameTopology(SpineVitSample reference, SpineVitSample sample, string animationName, int frameIndex)
    {
        if (sample.Vertices.Length != reference.Vertices.Length)
        {
            throw new InvalidOperationException(string.Format("Topology changed in animation {0} frame {1}: vertex count {2} != {3}.", animationName, frameIndex, sample.Vertices.Length, reference.Vertices.Length));
        }

        if (sample.Triangles.Length != reference.Triangles.Length)
        {
            throw new InvalidOperationException(string.Format("Topology changed in animation {0} frame {1}: triangle index count {2} != {3}.", animationName, frameIndex, sample.Triangles.Length, reference.Triangles.Length));
        }

        for (int i = 0; i < reference.Triangles.Length; i++)
        {
            if (sample.Triangles[i] != reference.Triangles[i])
            {
                throw new InvalidOperationException(string.Format("Topology changed in animation {0} frame {1}: triangle index mismatch at {2}.", animationName, frameIndex, i));
            }
        }

        for (int i = 0; i < reference.Uvs.Length; i++)
        {
            if ((sample.Uvs[i] - reference.Uvs[i]).sqrMagnitude > UvTolerance * UvTolerance)
            {
                throw new InvalidOperationException(string.Format("UV changed in animation {0} frame {1}: vertex {2}. Attachment swaps or sequence UVs are not supported in Spine VIT v1.", animationName, frameIndex, i));
            }
        }
    }

    private static void WriteFramePixels(SpineVitSample sample, Color[] positionPixels, Color32[] colorPixels, int vertexCount, int frameIndex)
    {
        int rowOffset = frameIndex * vertexCount;
        for (int vertexIndex = 0; vertexIndex < vertexCount; vertexIndex++)
        {
            Vector3 vertex = sample.Vertices[vertexIndex];
            // positionTexture 每行是一帧，每列是一个顶点，alpha 作为可见性标记预留。
            positionPixels[rowOffset + vertexIndex] = new Color(vertex.x, vertex.y, vertex.z, 1f);
            colorPixels[rowOffset + vertexIndex] = sample.Colors[vertexIndex];
        }
    }

    private static Mesh CreateMesh(SpineVitSample sample, Bounds bounds)
    {
        Vector3[] vertices = new Vector3[sample.Vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i] = Vector3.zero;
        }

        Mesh mesh = new Mesh
        {
            name = "Spine VIT Mesh"
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
            name = "Spine VIT Positions",
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
            name = "Spine VIT Colors",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private static Material CreateMaterial(Texture2D sourceTexture, Texture2D positionTexture, Texture2D colorTexture)
    {
        Shader shader = Shader.Find("Hybrid/Spine VIT");
        if (shader == null)
        {
            throw new InvalidOperationException("Shader not found: Hybrid/Spine VIT");
        }

        Material material = new Material(shader)
        {
            name = "Spine VIT Material",
            enableInstancing = true
        };
        material.SetTexture("_MainTex", sourceTexture);
        material.SetTexture("_PositionTex", positionTexture);
        material.SetTexture("_ColorTex", colorTexture);
        return material;
    }

    private static BakedSpineVitAsset CreateAssetObject(Mesh mesh, Material material, Texture2D sourceTexture, Texture2D positionTexture, Texture2D colorTexture, int frameRate, int vertexCount, int totalFrameCount, Bounds bounds, BakedSpineVitClip[] clips)
    {
        BakedSpineVitAsset asset = ScriptableObject.CreateInstance<BakedSpineVitAsset>();
        asset.mesh = mesh;
        asset.material = material;
        asset.sourceTexture = sourceTexture;
        asset.positionTexture = positionTexture;
        asset.colorTexture = colorTexture;
        asset.frameRate = frameRate;
        asset.vertexCount = vertexCount;
        asset.totalFrameCount = totalFrameCount;
        asset.bounds = bounds;
        asset.clips = clips;
        return asset;
    }

    private static string SaveAsset(SpineVitBakeSettings settings, BakedSpineVitAsset asset, Mesh mesh, Texture2D runtimeSourceTexture, Texture2D originalSourceTexture, Texture2D positionTexture, Texture2D colorTexture, Material material)
    {
        string fileBaseName = GetOutputBaseName(settings);
        string outputDirectoryAbsolute = ParticleAtlasPathUtility.ProjectPathToAbsolute(settings.OutputFolder);
        Directory.CreateDirectory(outputDirectoryAbsolute);

        string assetPath = ParticleAtlasPathUtility.CombineProjectPath(settings.OutputFolder, fileBaseName + "_SpineVit.asset");
        AssetDatabase.DeleteAsset(assetPath);
        AssetDatabase.CreateAsset(asset, assetPath);

        mesh.name = fileBaseName + "_Mesh";
        if (runtimeSourceTexture != originalSourceTexture)
        {
            runtimeSourceTexture.name = fileBaseName + "_RuntimeAtlas";
        }
        positionTexture.name = fileBaseName + "_PositionVIT";
        colorTexture.name = fileBaseName + "_ColorVIT";
        material.name = fileBaseName + "_Material";
        AssetDatabase.AddObjectToAsset(mesh, asset);
        if (runtimeSourceTexture != originalSourceTexture)
        {
            AssetDatabase.AddObjectToAsset(runtimeSourceTexture, asset);
        }
        AssetDatabase.AddObjectToAsset(positionTexture, asset);
        AssetDatabase.AddObjectToAsset(colorTexture, asset);
        AssetDatabase.AddObjectToAsset(material, asset);
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(assetPath);
        return assetPath;
    }

    private static string GetOutputBaseName(SpineVitBakeSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.OutputName))
        {
            return settings.OutputName.Trim();
        }

        string skeletonName = settings.SkeletonDataAsset != null ? settings.SkeletonDataAsset.name : "Spine";
        string skinName = string.IsNullOrEmpty(settings.SkinName) ? "default" : settings.SkinName;
        return skeletonName + "_" + skinName;
    }
}
#endif
