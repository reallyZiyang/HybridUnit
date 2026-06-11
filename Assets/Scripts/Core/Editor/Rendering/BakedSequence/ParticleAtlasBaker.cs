#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ParticleAtlasBaker
{
    public static bool CanBake(ParticleAtlasBakeSettings settings)
    {
        return settings != null
            && settings.Prefab != null
            && ParticleAtlasBakeUtility.GetBakeDuration(settings) > 0f
            && settings.FrameRate > 0
            && settings.FrameWidth > 0
            && settings.FrameHeight > 0
            && settings.FrameRectPadding >= 0
            && settings.MaxAtlasSize > 0
            && !string.IsNullOrWhiteSpace(settings.OutputFolder);
    }

    public static ParticleAtlasBakeResult Bake(ParticleAtlasBakeSettings settings)
    {
        Validate(settings);
        return BakeInternal(settings);
    }

    private static void Validate(ParticleAtlasBakeSettings settings)
    {
        if (settings.Prefab == null)
        {
            throw new InvalidOperationException("Please choose a prefab.");
        }

        string prefabPath = AssetDatabase.GetAssetPath(settings.Prefab);
        if (string.IsNullOrEmpty(prefabPath))
        {
            throw new InvalidOperationException("The source object must be a project asset prefab.");
        }

        if (settings.Prefab.GetComponentInChildren<ParticleSystem>(true) == null)
        {
            throw new InvalidOperationException("The selected prefab does not contain any ParticleSystem.");
        }

        ParticleAtlasLayout layout = ParticleAtlasLayoutUtility.CalculateLayout(settings, ParticleAtlasLayoutUtility.CalculateFrameCount(settings));
        if (!settings.TrimFrameRects && (layout.AtlasWidth > settings.MaxAtlasSize || layout.AtlasHeight > settings.MaxAtlasSize))
        {
            throw new InvalidOperationException(
                string.Format("Atlas size {0}x{1} is larger than Max Atlas Size {2}. Reduce frame count, frame size, or columns.", layout.AtlasWidth, layout.AtlasHeight, settings.MaxAtlasSize));
        }

        if (!settings.OutputFolder.StartsWith("Assets", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Output folder must be inside this Unity project Assets folder.");
        }
    }

    private static ParticleAtlasBakeResult BakeInternal(ParticleAtlasBakeSettings settings)
    {
        float bakeDuration = ParticleAtlasBakeUtility.GetBakeDuration(settings);
        int requestedFrameCount = ParticleAtlasLayoutUtility.CalculateFrameCount(settings);

        Scene previousActiveScene = SceneManager.GetActiveScene();
        Scene bakeScene = default;
        Camera bakeCamera = null;
        RenderTexture renderTexture = null;
        Texture2D frameTexture = null;
        Texture2D atlas = null;

        try
        {
            bakeScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            EditorSceneManager.SetActiveScene(bakeScene);

            GameObject instance = CreatePrefabInstance(settings, bakeScene);
            ApplyPrefabTransform(instance, settings);
            ApplyBakeLayer(instance, settings);
            ApplyRendererFilter(instance, settings);
            PrepareParticleRenderersForBake(instance);
            if (settings.UseBakeParticleMaterial)
            {
                ParticleAtlasBakeMaterialUtility.ApplyBakeParticleMaterials(instance);
            }

            bakeCamera = CreateCamera(settings, bakeScene);
            if (settings.AddDirectionalLight)
            {
                CreateLight(bakeScene, settings);
            }

            renderTexture = CreateRenderTexture(settings);
            frameTexture = new Texture2D(settings.FrameWidth, settings.FrameHeight, TextureFormat.RGBA32, false, false);
            bakeCamera.targetTexture = renderTexture;

            ParticleSystem[] rootParticleSystems = ParticleAtlasBakeUtility.GetRootParticleSystems(instance);
            ParticleAtlasBakeUtility.PrepareParticles(instance, rootParticleSystems, settings);
            if (settings.AutoFrameCamera)
            {
                AutoFrameCamera(instance, rootParticleSystems, bakeCamera, settings, bakeDuration);
                ParticleAtlasBakeUtility.PrepareParticles(instance, rootParticleSystems, settings);
            }

            BakedFrameSet frames = RenderFrames(settings, bakeCamera, renderTexture, frameTexture, rootParticleSystems, requestedFrameCount, bakeDuration);
            int outputStartFrame = settings.TrimEmptyHead && frames.FirstVisibleFrame >= 0 ? frames.FirstVisibleFrame : 0;
            int outputEndFrame = settings.TrimEmptyTail && frames.LastVisibleFrame >= 0 ? frames.LastVisibleFrame : requestedFrameCount - 1;
            if (outputEndFrame < outputStartFrame)
            {
                outputStartFrame = 0;
                outputEndFrame = Mathf.Max(0, requestedFrameCount - 1);
            }

            int outputFrameCount = outputEndFrame - outputStartFrame + 1;
            frames = ApplyLoopSeamBlend(settings, frames, outputStartFrame, outputFrameCount);
            float effectiveDuration = outputFrameCount / (float)settings.FrameRate;
            ParticleAtlasPacking packing = CreatePacking(settings, frames.FrameRects, outputStartFrame, outputFrameCount);
            if (packing.Layout.AtlasWidth > settings.MaxAtlasSize || packing.Layout.AtlasHeight > settings.MaxAtlasSize)
            {
                throw new InvalidOperationException(
                    string.Format("Atlas size {0}x{1} is larger than Max Atlas Size {2}. Reduce frame count, frame size, or columns.", packing.Layout.AtlasWidth, packing.Layout.AtlasHeight, settings.MaxAtlasSize));
            }

            atlas = PackAtlas(settings, frames.FramePixels, frames.FrameRects, outputStartFrame, packing);

            string atlasProjectPath = SaveAtlas(settings, atlas);
            if (settings.ConfigureTextureImporter)
            {
                ConfigureImporter(atlasProjectPath, settings);
            }

            string metadataProjectPath = null;
            if (settings.GenerateMetadata)
            {
                metadataProjectPath = SaveMetadata(settings, packing, requestedFrameCount, outputStartFrame, outputFrameCount, bakeDuration, effectiveDuration, frames);
            }

            string sequenceAssetProjectPath = null;
            if (settings.GenerateSequenceAsset)
            {
                sequenceAssetProjectPath = SaveSequenceAsset(settings, atlasProjectPath, metadataProjectPath);
            }

            AssetDatabase.Refresh();
            return new ParticleAtlasBakeResult(atlasProjectPath, metadataProjectPath, sequenceAssetProjectPath, requestedFrameCount, outputFrameCount, frames.VisiblePixelCount, frames.FirstVisibleFrame, frames.LastVisibleFrame);
        }
        finally
        {
            if (renderTexture != null)
            {
                renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(renderTexture);
            }

            if (frameTexture != null)
            {
                UnityEngine.Object.DestroyImmediate(frameTexture);
            }

            if (atlas != null)
            {
                UnityEngine.Object.DestroyImmediate(atlas);
            }

            if (previousActiveScene.IsValid())
            {
                EditorSceneManager.SetActiveScene(previousActiveScene);
            }

            if (bakeScene.IsValid())
            {
                EditorSceneManager.CloseScene(bakeScene, true);
            }
        }
    }

    private static BakedFrameSet RenderFrames(ParticleAtlasBakeSettings settings, Camera bakeCamera, RenderTexture renderTexture, Texture2D frameTexture, ParticleSystem[] rootParticleSystems, int requestedFrameCount, float bakeDuration)
    {
        Color32[][] framePixels = new Color32[requestedFrameCount][];
        RectInt[] frameRects = new RectInt[requestedFrameCount];
        int visiblePixelCount = 0;
        int firstVisibleFrame = -1;
        int lastVisibleFrame = -1;

        for (int frameIndex = 0; frameIndex < requestedFrameCount; frameIndex++)
        {
            float sampleTime = Mathf.Min(bakeDuration, frameIndex / (float)settings.FrameRate);
            EditorUtility.DisplayProgressBar("Particle Atlas Baker", "Rendering frame " + (frameIndex + 1) + " / " + requestedFrameCount, frameIndex / (float)requestedFrameCount);

            ParticleAtlasBakeUtility.SampleParticles(rootParticleSystems, sampleTime);
            SceneView.RepaintAll();
            RenderFrame(bakeCamera, renderTexture, frameTexture);

            Color32[] pixels = ParticleAtlasTextureUtility.ReadFramePixels(frameTexture, settings.AlphaFromColor);
            int frameVisiblePixels = ParticleAtlasTextureUtility.CountVisiblePixels(pixels);
            RectInt frameRect = settings.TrimFrameRects
                ? ParticleAtlasTextureUtility.CalculateVisibleRect(pixels, settings.FrameWidth, settings.FrameHeight, settings.FrameRectPadding)
                : new RectInt(0, 0, settings.FrameWidth, settings.FrameHeight);
            framePixels[frameIndex] = pixels;
            frameRects[frameIndex] = frameRect;
            visiblePixelCount += frameVisiblePixels;
            if (firstVisibleFrame < 0 && frameVisiblePixels > 0)
            {
                firstVisibleFrame = frameIndex;
            }
            if (frameVisiblePixels > 0)
            {
                lastVisibleFrame = frameIndex;
            }
        }

        return new BakedFrameSet(framePixels, frameRects, visiblePixelCount, firstVisibleFrame, lastVisibleFrame);
    }

    private static BakedFrameSet ApplyLoopSeamBlend(ParticleAtlasBakeSettings settings, BakedFrameSet frames, int outputStartFrame, int outputFrameCount)
    {
        if (!settings.Loop || !settings.LoopBlend || outputFrameCount < 2)
        {
            return frames;
        }

        int blendFrames = Mathf.Clamp(settings.LoopBlendFrames, 0, outputFrameCount / 2);
        if (blendFrames <= 0)
        {
            return frames;
        }

        for (int i = 0; i < blendFrames; i++)
        {
            int tailFrame = outputStartFrame + outputFrameCount - blendFrames + i;
            int headFrame = outputStartFrame + i;
            float t = i / (float)(blendFrames * 1.5f);

            BlendFramePixels(frames.FramePixels[tailFrame], frames.FramePixels[0], t);
        }

        return RecalculateFrameSet(settings, frames);
    }

    private static void BlendFramePixels(Color32[] targetPixels, Color32[] sourcePixels, float t)
    {
        if (targetPixels == null || sourcePixels == null)
        {
            return;
        }

        int pixelCount = Mathf.Min(targetPixels.Length, sourcePixels.Length);
        for (int pixelIndex = 0; pixelIndex < pixelCount; pixelIndex++)
        {
            targetPixels[pixelIndex] = LerpPremultiplied(targetPixels[pixelIndex], sourcePixels[pixelIndex], t);
        }
    }

    private static Color32 LerpPremultiplied(Color32 a, Color32 b, float t)
    {
        float alphaA = a.a / 255f;
        float alphaB = b.a / 255f;
        float outAlpha = Mathf.Lerp(alphaA, alphaB, t);

        Vector3 colorA = new Vector3(a.r, a.g, a.b) * alphaA;
        Vector3 colorB = new Vector3(b.r, b.g, b.b) * alphaB;
        Vector3 outColor = Vector3.Lerp(colorA, colorB, t);

        if (outAlpha > 0.0001f)
        {
            outColor /= outAlpha;
        }

        return new Color32(
            (byte)Mathf.Clamp(Mathf.RoundToInt(outColor.x), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(outColor.y), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(outColor.z), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(outAlpha * 255f), 0, 255));
    }

    private static BakedFrameSet RecalculateFrameSet(ParticleAtlasBakeSettings settings, BakedFrameSet frames)
    {
        RectInt[] frameRects = new RectInt[frames.FramePixels.Length];
        int visiblePixelCount = 0;
        int firstVisibleFrame = -1;
        int lastVisibleFrame = -1;

        for (int frameIndex = 0; frameIndex < frames.FramePixels.Length; frameIndex++)
        {
            Color32[] pixels = frames.FramePixels[frameIndex];
            int frameVisiblePixels = ParticleAtlasTextureUtility.CountVisiblePixels(pixels);
            frameRects[frameIndex] = settings.TrimFrameRects
                ? ParticleAtlasTextureUtility.CalculateVisibleRect(pixels, settings.FrameWidth, settings.FrameHeight, settings.FrameRectPadding)
                : new RectInt(0, 0, settings.FrameWidth, settings.FrameHeight);

            visiblePixelCount += frameVisiblePixels;
            if (firstVisibleFrame < 0 && frameVisiblePixels > 0)
            {
                firstVisibleFrame = frameIndex;
            }
            if (frameVisiblePixels > 0)
            {
                lastVisibleFrame = frameIndex;
            }
        }

        return new BakedFrameSet(frames.FramePixels, frameRects, visiblePixelCount, firstVisibleFrame, lastVisibleFrame);
    }

    private static ParticleAtlasPacking CreatePacking(ParticleAtlasBakeSettings settings, RectInt[] sourceRects, int outputStartFrame, int outputFrameCount)
    {
        if (!settings.TrimFrameRects)
        {
            ParticleAtlasLayout gridLayout = ParticleAtlasLayoutUtility.CalculateLayout(settings, outputFrameCount);
            RectInt[] gridRects = new RectInt[outputFrameCount];
            for (int frameIndex = 0; frameIndex < outputFrameCount; frameIndex++)
            {
                int column = frameIndex % gridLayout.Columns;
                int row = frameIndex / gridLayout.Columns;
                int x = column * settings.FrameWidth;
                int y = settings.FirstFrameTopLeft ? gridLayout.AtlasHeight - ((row + 1) * settings.FrameHeight) : row * settings.FrameHeight;
                if (settings.TrimFrameRects)
                {
                    RectInt sourceRect = sourceRects[outputStartFrame + frameIndex];
                    gridRects[frameIndex] = new RectInt(x + sourceRect.x, y + sourceRect.y, sourceRect.width, sourceRect.height);
                }
                else
                {
                    gridRects[frameIndex] = new RectInt(x, y, settings.FrameWidth, settings.FrameHeight);
                }
            }

            return new ParticleAtlasPacking(gridLayout, gridRects, false);
        }

        return CreateTightPacking(settings, sourceRects, outputStartFrame, outputFrameCount);
    }

    private static ParticleAtlasPacking CreateTightPacking(ParticleAtlasBakeSettings settings, RectInt[] sourceRects, int outputStartFrame, int outputFrameCount)
    {
        int maxWidth = 1;
        for (int i = 0; i < outputFrameCount; i++)
        {
            RectInt rect = sourceRects[outputStartFrame + i];
            maxWidth = Mathf.Max(maxWidth, rect.width);
        }

        int minCandidateWidth = Mathf.NextPowerOfTwo(maxWidth);
        int maxCandidateWidth = settings.MaxAtlasSize;
        TightPackingCandidate best = default;
        TightPackingCandidate bestAllowed = default;
        bool hasBest = false;
        bool hasBestAllowed = false;
        float maxAspect = Mathf.Max(1f, settings.MaxAtlasAspect);

        for (int candidateWidth = minCandidateWidth; candidateWidth <= maxCandidateWidth; candidateWidth *= 2)
        {
            TightPackingCandidate candidate = PackTightWithWidth(settings, sourceRects, outputStartFrame, outputFrameCount, candidateWidth);
            if (!candidate.Valid || candidate.AtlasHeight > settings.MaxAtlasSize)
            {
                continue;
            }

            bool better = IsBetterTightPacking(candidate, best);
            if (!hasBest || better)
            {
                best = candidate;
                hasBest = true;
            }

            float aspect = candidate.AtlasWidth / (float)Mathf.Max(1, candidate.AtlasHeight);
            float normalizedAspect = Mathf.Max(aspect, 1f / Mathf.Max(0.001f, aspect));
            if (normalizedAspect <= maxAspect && (!hasBestAllowed || IsBetterTightPacking(candidate, bestAllowed)))
            {
                bestAllowed = candidate;
                hasBestAllowed = true;
            }

            if (candidateWidth > settings.MaxAtlasSize / 2)
            {
                break;
            }
        }

        TightPackingCandidate selected = hasBestAllowed ? bestAllowed : best;
        if (!hasBest)
        {
            ParticleAtlasLayout fallbackLayout = ParticleAtlasLayoutUtility.CalculateLayout(settings, outputFrameCount);
            RectInt[] fallbackRects = new RectInt[outputFrameCount];
            for (int frameIndex = 0; frameIndex < outputFrameCount; frameIndex++)
            {
                int column = frameIndex % fallbackLayout.Columns;
                int row = frameIndex / fallbackLayout.Columns;
                int x = column * settings.FrameWidth;
                int y = settings.FirstFrameTopLeft ? fallbackLayout.AtlasHeight - ((row + 1) * settings.FrameHeight) : row * settings.FrameHeight;
                fallbackRects[frameIndex] = new RectInt(x, y, settings.FrameWidth, settings.FrameHeight);
            }

            return new ParticleAtlasPacking(fallbackLayout, fallbackRects, false);
        }

        ParticleAtlasLayout layout = new ParticleAtlasLayout(outputFrameCount, 0, 0, selected.UsedWidth, selected.UsedHeight, selected.AtlasWidth, selected.AtlasHeight);
        return new ParticleAtlasPacking(layout, selected.AtlasRects, true);
    }

    private static TightPackingCandidate PackTightWithWidth(ParticleAtlasBakeSettings settings, RectInt[] sourceRects, int outputStartFrame, int outputFrameCount, int atlasWidth)
    {
        RectInt[] atlasRects = new RectInt[outputFrameCount];
        int cursorX = 0;
        int cursorY = 0;
        int shelfHeight = 0;
        int usedWidth = 0;

        for (int frameIndex = 0; frameIndex < outputFrameCount; frameIndex++)
        {
            RectInt sourceRect = sourceRects[outputStartFrame + frameIndex];
            if (sourceRect.width <= 0 || sourceRect.height <= 0)
            {
                atlasRects[frameIndex] = new RectInt(0, 0, 0, 0);
                continue;
            }

            if (sourceRect.width > atlasWidth)
            {
                return TightPackingCandidate.Invalid();
            }

            if (cursorX > 0 && cursorX + sourceRect.width > atlasWidth)
            {
                cursorY += shelfHeight;
                cursorX = 0;
                shelfHeight = 0;
            }

            atlasRects[frameIndex] = new RectInt(cursorX, cursorY, sourceRect.width, sourceRect.height);
            cursorX += sourceRect.width;
            shelfHeight = Mathf.Max(shelfHeight, sourceRect.height);
            usedWidth = Mathf.Max(usedWidth, cursorX);
        }

        int usedHeight = cursorY + shelfHeight;
        int atlasHeight = Mathf.NextPowerOfTwo(Mathf.Max(1, usedHeight));
        int finalAtlasWidth = Mathf.NextPowerOfTwo(Mathf.Max(1, usedWidth));

        if (settings.FirstFrameTopLeft)
        {
            for (int frameIndex = 0; frameIndex < outputFrameCount; frameIndex++)
            {
                RectInt rect = atlasRects[frameIndex];
                if (rect.width <= 0 || rect.height <= 0)
                {
                    continue;
                }

                atlasRects[frameIndex] = new RectInt(rect.x, atlasHeight - rect.y - rect.height, rect.width, rect.height);
            }
        }

        return new TightPackingCandidate(true, finalAtlasWidth, atlasHeight, usedWidth, usedHeight, atlasRects);
    }

    private static bool IsBetterTightPacking(TightPackingCandidate candidate, TightPackingCandidate current)
    {
        long candidateArea = (long)candidate.AtlasWidth * candidate.AtlasHeight;
        long currentArea = (long)current.AtlasWidth * current.AtlasHeight;
        int candidateWaste = candidate.AtlasWidth * candidate.AtlasHeight - candidate.UsedWidth * candidate.UsedHeight;
        int currentWaste = current.AtlasWidth * current.AtlasHeight - current.UsedWidth * current.UsedHeight;
        float candidateAspectScore = Mathf.Abs(Mathf.Log(candidate.AtlasWidth / (float)Mathf.Max(1, candidate.AtlasHeight), 2f));
        float currentAspectScore = Mathf.Abs(Mathf.Log(current.AtlasWidth / (float)Mathf.Max(1, current.AtlasHeight), 2f));

        return candidateArea < currentArea
            || candidateArea == currentArea && candidateWaste < currentWaste
            || candidateArea == currentArea && candidateWaste == currentWaste && candidateAspectScore < currentAspectScore;
    }

    private static Texture2D PackAtlas(ParticleAtlasBakeSettings settings, Color32[][] framePixels, RectInt[] sourceRects, int outputStartFrame, ParticleAtlasPacking packing)
    {
        ParticleAtlasLayout layout = packing.Layout;
        Texture2D atlas = new Texture2D(layout.AtlasWidth, layout.AtlasHeight, TextureFormat.RGBA32, false, false);
        ParticleAtlasTextureUtility.FillTexture(atlas, new Color32(0, 0, 0, 0));

        for (int frameIndex = 0; frameIndex < layout.FrameCount; frameIndex++)
        {
            int sourceFrameIndex = outputStartFrame + frameIndex;
            if (packing.PackFrameRects)
            {
                ParticleAtlasTextureUtility.CopyFrameRectToAtlas(framePixels[sourceFrameIndex], atlas, sourceRects[sourceFrameIndex], packing.AtlasRects[frameIndex], settings.FrameWidth);
            }
            else
            {
                ParticleAtlasTextureUtility.CopyFrameToAtlas(framePixels[sourceFrameIndex], atlas, frameIndex, settings, layout);
            }
        }

        atlas.Apply(false, false);
        return atlas;
    }

    private static string SaveAtlas(ParticleAtlasBakeSettings settings, Texture2D atlas)
    {
        string fileBaseName = ParticleAtlasPathUtility.GetOutputBaseName(settings);
        string outputDirectoryAbsolute = ParticleAtlasPathUtility.ProjectPathToAbsolute(settings.OutputFolder);
        Directory.CreateDirectory(outputDirectoryAbsolute);

        string atlasProjectPath = ParticleAtlasPathUtility.CombineProjectPath(settings.OutputFolder, fileBaseName + ".png");
        string atlasAbsolutePath = ParticleAtlasPathUtility.ProjectPathToAbsolute(atlasProjectPath);
        File.WriteAllBytes(atlasAbsolutePath, atlas.EncodeToPNG());
        AssetDatabase.ImportAsset(atlasProjectPath);
        return atlasProjectPath;
    }

    private static string SaveMetadata(ParticleAtlasBakeSettings settings, ParticleAtlasPacking packing, int requestedFrameCount, int outputStartFrame, int outputFrameCount, float bakeDuration, float effectiveDuration, BakedFrameSet frames)
    {
        ParticleAtlasLayout layout = packing.Layout;
        string fileBaseName = ParticleAtlasPathUtility.GetOutputBaseName(settings);
        string jsonProjectPath = ParticleAtlasPathUtility.CombineProjectPath(settings.OutputFolder, fileBaseName + ".json");
        string jsonAbsolutePath = ParticleAtlasPathUtility.ProjectPathToAbsolute(jsonProjectPath);
        ParticleAtlasMetadata metadata = new ParticleAtlasMetadata
        {
            prefab = AssetDatabase.GetAssetPath(settings.Prefab),
            rendererNameFilter = settings.RendererNameFilter,
            loop = settings.Loop,
            loopBlend = settings.Loop && settings.LoopBlend,
            loopBlendFrames = settings.Loop && settings.LoopBlend ? Mathf.Clamp(settings.LoopBlendFrames, 0, outputFrameCount / 2) : 0,
            duration = bakeDuration,
            effectiveDuration = effectiveDuration,
            resolutionPreset = settings.ResolutionPreset.ToString(),
            frameRatePreset = settings.FrameRatePreset.ToString(),
            frameRate = settings.FrameRate,
            requestedFrameCount = requestedFrameCount,
            frameCount = outputFrameCount,
            frameWidth = settings.FrameWidth,
            frameHeight = settings.FrameHeight,
            columns = layout.Columns,
            rows = layout.Rows,
            maxAtlasAspect = settings.MaxAtlasAspect,
            atlasWidth = layout.AtlasWidth,
            atlasHeight = layout.AtlasHeight,
            usedAtlasWidth = layout.UsedAtlasWidth,
            usedAtlasHeight = layout.UsedAtlasHeight,
            powerOfTwoAtlas = true,
            firstFrameTopLeft = settings.FirstFrameTopLeft,
            alphaFromColor = settings.AlphaFromColor,
            useBakeParticleMaterial = settings.UseBakeParticleMaterial,
            trimEmptyHead = settings.TrimEmptyHead,
            trimEmptyTail = settings.TrimEmptyTail,
            trimFrameRects = settings.TrimFrameRects,
            packFrameRects = packing.PackFrameRects,
            frameRectPadding = settings.FrameRectPadding,
            outputStartFrame = outputStartFrame,
            trimmedHeadFrameCount = outputStartFrame,
            trimmedFrameCount = requestedFrameCount - outputFrameCount,
            firstVisibleFrame = frames.FirstVisibleFrame,
            lastVisibleFrame = frames.LastVisibleFrame,
            autoFrameCamera = settings.AutoFrameCamera,
            bakeLayer = Mathf.Clamp(settings.BakeLayer, 0, 31),
            forceRandomSeed = settings.ForceRandomSeed,
            randomSeed = settings.RandomSeed,
            frameRects = CreateFrameMetadata(settings, layout, frames.FrameRects, packing.AtlasRects, outputStartFrame, outputFrameCount)
        };

        File.WriteAllText(jsonAbsolutePath, JsonUtility.ToJson(metadata, true));
        AssetDatabase.ImportAsset(jsonProjectPath);
        return jsonProjectPath;
    }

    private static string SaveSequenceAsset(ParticleAtlasBakeSettings settings, string atlasProjectPath, string metadataProjectPath)
    {
        string fileBaseName = ParticleAtlasPathUtility.GetOutputBaseName(settings);
        string materialProjectPath = ParticleAtlasPathUtility.CombineProjectPath(settings.OutputFolder, fileBaseName + ".mat");
        string assetProjectPath = ParticleAtlasPathUtility.CombineProjectPath(settings.OutputFolder, fileBaseName + "_Sequence.asset");

        Texture2D atlasTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(atlasProjectPath);
        TextAsset metadataAsset = string.IsNullOrEmpty(metadataProjectPath) ? null : AssetDatabase.LoadAssetAtPath<TextAsset>(metadataProjectPath);
        Material material = LoadOrCreateSequenceMaterial(materialProjectPath, atlasTexture);

        BakedSequenceAsset asset = AssetDatabase.LoadAssetAtPath<BakedSequenceAsset>(assetProjectPath);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<BakedSequenceAsset>();
            AssetDatabase.CreateAsset(asset, assetProjectPath);
        }

        asset.atlas = atlasTexture;
        asset.metadataJson = metadataAsset;
        asset.material = material;
        asset.playOnEnable = true;
        asset.loop = settings.Loop;
        asset.speed = 1f;
        asset.displayScale = 1f;
        asset.color = Color.white;
        asset.skipEmptyFrames = true;
        asset.flipU = false;
        asset.flipV = false;

        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(assetProjectPath);
        return assetProjectPath;
    }

    private static Material LoadOrCreateSequenceMaterial(string materialProjectPath, Texture2D atlasTexture)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialProjectPath);
        if (material == null)
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

            material = new Material(shader)
            {
                name = Path.GetFileNameWithoutExtension(materialProjectPath),
                enableInstancing = true
            };
            AssetDatabase.CreateAsset(material, materialProjectPath);
        }

        material.enableInstancing = true;
        if (atlasTexture != null)
        {
            SetTextureIfExists(material, "_MainTex", atlasTexture);
            SetTextureIfExists(material, "_BaseMap", atlasTexture);
        }
        SetColorIfExists(material, "_Tint", Color.white);
        SetColorIfExists(material, "_BaseColor", Color.white);
        SetColorIfExists(material, "_Color", Color.white);

        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(materialProjectPath);
        return material;
    }

    private static void SetTextureIfExists(Material material, string propertyName, Texture texture)
    {
        if (material != null && material.HasProperty(propertyName))
        {
            material.SetTexture(propertyName, texture);
        }
    }

    private static void SetColorIfExists(Material material, string propertyName, Color color)
    {
        if (material != null && material.HasProperty(propertyName))
        {
            material.SetColor(propertyName, color);
        }
    }

    private static ParticleAtlasFrameRect[] CreateFrameMetadata(ParticleAtlasBakeSettings settings, ParticleAtlasLayout layout, RectInt[] sourceRects, RectInt[] atlasRects, int outputStartFrame, int outputFrameCount)
    {
        ParticleAtlasFrameRect[] metadata = new ParticleAtlasFrameRect[outputFrameCount];
        for (int frameIndex = 0; frameIndex < outputFrameCount; frameIndex++)
        {
            RectInt sourceRect = sourceRects[outputStartFrame + frameIndex];
            RectInt atlasRect = atlasRects[frameIndex];

            metadata[frameIndex] = new ParticleAtlasFrameRect
            {
                frame = frameIndex,
                sourceX = sourceRect.x,
                sourceY = sourceRect.y,
                sourceWidth = sourceRect.width,
                sourceHeight = sourceRect.height,
                atlasX = atlasRect.x,
                atlasY = atlasRect.y,
                atlasWidth = atlasRect.width,
                atlasHeight = atlasRect.height,
                uvX = layout.AtlasWidth > 0 ? atlasRect.x / (float)layout.AtlasWidth : 0f,
                uvY = layout.AtlasHeight > 0 ? atlasRect.y / (float)layout.AtlasHeight : 0f,
                uvWidth = layout.AtlasWidth > 0 ? atlasRect.width / (float)layout.AtlasWidth : 0f,
                uvHeight = layout.AtlasHeight > 0 ? atlasRect.height / (float)layout.AtlasHeight : 0f,
                quadOffsetX = (sourceRect.x + sourceRect.width * 0.5f - settings.FrameWidth * 0.5f) / Mathf.Max(1, settings.FrameWidth),
                quadOffsetY = (sourceRect.y + sourceRect.height * 0.5f - settings.FrameHeight * 0.5f) / Mathf.Max(1, settings.FrameHeight),
                quadWidth = sourceRect.width / (float)Mathf.Max(1, settings.FrameWidth),
                quadHeight = sourceRect.height / (float)Mathf.Max(1, settings.FrameHeight)
            };
        }

        return metadata;
    }

    private static GameObject CreatePrefabInstance(ParticleAtlasBakeSettings settings, Scene bakeScene)
    {
        GameObject instance = PrefabUtility.InstantiatePrefab(settings.Prefab, bakeScene) as GameObject;
        if (instance == null)
        {
            instance = UnityEngine.Object.Instantiate(settings.Prefab);
            SceneManager.MoveGameObjectToScene(instance, bakeScene);
        }

        instance.name = settings.Prefab.name + "_BakeInstance";
        return instance;
    }

    private static void ApplyBakeLayer(GameObject instance, ParticleAtlasBakeSettings settings)
    {
        int bakeLayer = Mathf.Clamp(settings.BakeLayer, 0, 31);
        Transform[] transforms = instance.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            transforms[i].gameObject.layer = bakeLayer;
        }
    }

    private static void ApplyRendererFilter(GameObject instance, ParticleAtlasBakeSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.RendererNameFilter))
        {
            return;
        }

        string filter = settings.RendererNameFilter.Trim();
        ParticleSystemRenderer[] renderers = instance.GetComponentsInChildren<ParticleSystemRenderer>(true);
        int matchedCount = 0;
        for (int i = 0; i < renderers.Length; i++)
        {
            ParticleSystemRenderer particleRenderer = renderers[i];
            if (particleRenderer == null)
            {
                continue;
            }

            bool matched = string.Equals(particleRenderer.gameObject.name, filter, StringComparison.OrdinalIgnoreCase);
            particleRenderer.enabled = particleRenderer.enabled && matched;
            if (matched)
            {
                matchedCount++;
            }
        }

        if (matchedCount == 0)
        {
            throw new InvalidOperationException("Renderer Filter did not match any ParticleSystemRenderer GameObject: " + filter);
        }
    }

    private static void PrepareParticleRenderersForBake(GameObject instance)
    {
        ParticleSystemRenderer[] renderers = instance.GetComponentsInChildren<ParticleSystemRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            ParticleSystemRenderer particleRenderer = renderers[i];
            if (particleRenderer == null)
            {
                continue;
            }

            // 离屏逐帧烘培需要确定性的 CPU 粒子网格路径，避免 GPU 粒子 instancing 缓冲把旧帧数据带进当前帧。
            particleRenderer.enableGPUInstancing = false;
        }
    }

    private static void ApplyPrefabTransform(GameObject instance, ParticleAtlasBakeSettings settings)
    {
        instance.transform.position = settings.PrefabPosition;
        instance.transform.rotation = Quaternion.Euler(settings.PrefabEulerAngles);
        instance.transform.localScale = settings.PrefabScale;
    }

    private static Camera CreateCamera(ParticleAtlasBakeSettings settings, Scene bakeScene)
    {
        GameObject cameraObject = new GameObject("ParticleAtlasBakeCamera");
        SceneManager.MoveGameObjectToScene(cameraObject, bakeScene);
        cameraObject.transform.position = settings.CameraPosition;
        cameraObject.transform.rotation = Quaternion.Euler(settings.CameraEulerAngles);

        Camera camera = cameraObject.AddComponent<Camera>();
        camera.enabled = false;
        camera.cullingMask = 1 << Mathf.Clamp(settings.BakeLayer, 0, 31);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = settings.TransparentBackground ? new Color(0f, 0f, 0f, 0f) : settings.BackgroundColor;
        camera.orthographic = settings.Orthographic;
        camera.orthographicSize = Mathf.Max(0.001f, settings.OrthographicSize);
        camera.fieldOfView = Mathf.Clamp(settings.FieldOfView, 1f, 179f);
        camera.nearClipPlane = 0.01f;
        camera.farClipPlane = 1000f;
        camera.allowHDR = false;
        camera.allowMSAA = settings.AntiAliasing > 1;
        return camera;
    }

    private static void CreateLight(Scene bakeScene, ParticleAtlasBakeSettings settings)
    {
        GameObject lightObject = new GameObject("ParticleAtlasBakeLight");
        SceneManager.MoveGameObjectToScene(lightObject, bakeScene);
        lightObject.layer = Mathf.Clamp(settings.BakeLayer, 0, 31);
        lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1f;
        light.cullingMask = 1 << Mathf.Clamp(settings.BakeLayer, 0, 31);
    }

    private static RenderTexture CreateRenderTexture(ParticleAtlasBakeSettings settings)
    {
        RenderTexture texture = new RenderTexture(settings.FrameWidth, settings.FrameHeight, 24, RenderTextureFormat.ARGB32)
        {
            antiAliasing = Mathf.Max(1, settings.AntiAliasing),
            useMipMap = false,
            autoGenerateMips = false,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        texture.Create();
        return texture;
    }

    private static void AutoFrameCamera(GameObject instance, ParticleSystem[] rootParticleSystems, Camera camera, ParticleAtlasBakeSettings settings, float bakeDuration)
    {
        ParticleAtlasBakeUtility.SampleParticles(rootParticleSystems, Mathf.Max(0.001f, bakeDuration * 0.5f));
        Bounds bounds;
        if (!ParticleAtlasBakeUtility.TryGetRendererBounds(instance, out bounds))
        {
            return;
        }

        Vector3 forward = camera.transform.forward;
        float distance = Mathf.Max(10f, bounds.extents.magnitude + 10f);
        camera.transform.position = bounds.center - forward * distance;
        camera.nearClipPlane = 0.01f;
        camera.farClipPlane = distance + bounds.extents.magnitude + 100f;

        if (camera.orthographic)
        {
            float verticalSize = Mathf.Max(bounds.extents.y, bounds.extents.x / Mathf.Max(0.001f, camera.aspect));
            camera.orthographicSize = Mathf.Max(0.01f, verticalSize * 1.25f);
        }
    }

    private static void RenderFrame(Camera camera, RenderTexture renderTexture, Texture2D frameTexture)
    {
        RenderTexture previous = RenderTexture.active;
        try
        {
            RenderTexture.active = renderTexture;
            // 每帧显式清空 RT，避免 additive 粒子把上一帧内容累积进当前帧。
            GL.Clear(true, true, camera.backgroundColor);
            camera.Render();
            frameTexture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0, false);
            frameTexture.Apply(false, false);
        }
        finally
        {
            RenderTexture.active = previous;
        }
    }

    private static void ConfigureImporter(string projectPath, ParticleAtlasBakeSettings settings)
    {
        TextureImporter importer = AssetImporter.GetAtPath(projectPath) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        importer.textureType = TextureImporterType.Default;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = settings.TransparentBackground;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.SaveAndReimport();
    }

    private readonly struct ParticleAtlasPacking
    {
        public ParticleAtlasPacking(ParticleAtlasLayout layout, RectInt[] atlasRects, bool packFrameRects)
        {
            Layout = layout;
            AtlasRects = atlasRects;
            PackFrameRects = packFrameRects;
        }

        public ParticleAtlasLayout Layout { get; }
        public RectInt[] AtlasRects { get; }
        public bool PackFrameRects { get; }
    }

    private readonly struct TightPackingCandidate
    {
        public TightPackingCandidate(bool valid, int atlasWidth, int atlasHeight, int usedWidth, int usedHeight, RectInt[] atlasRects)
        {
            Valid = valid;
            AtlasWidth = atlasWidth;
            AtlasHeight = atlasHeight;
            UsedWidth = usedWidth;
            UsedHeight = usedHeight;
            AtlasRects = atlasRects;
        }

        public bool Valid { get; }
        public int AtlasWidth { get; }
        public int AtlasHeight { get; }
        public int UsedWidth { get; }
        public int UsedHeight { get; }
        public RectInt[] AtlasRects { get; }

        public static TightPackingCandidate Invalid()
        {
            return new TightPackingCandidate(false, 0, 0, 0, 0, null);
        }
    }

    private readonly struct BakedFrameSet
    {
        public BakedFrameSet(Color32[][] framePixels, RectInt[] frameRects, int visiblePixelCount, int firstVisibleFrame, int lastVisibleFrame)
        {
            FramePixels = framePixels;
            FrameRects = frameRects;
            VisiblePixelCount = visiblePixelCount;
            FirstVisibleFrame = firstVisibleFrame;
            LastVisibleFrame = lastVisibleFrame;
        }

        public Color32[][] FramePixels { get; }
        public RectInt[] FrameRects { get; }
        public int VisiblePixelCount { get; }
        public int FirstVisibleFrame { get; }
        public int LastVisibleFrame { get; }
    }
}
#endif
