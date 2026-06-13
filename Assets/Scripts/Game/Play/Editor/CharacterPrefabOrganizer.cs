#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;
using Object = UnityEngine.Object;

namespace Game.Play.Editor
{
    public static class CharacterPrefabOrganizer
    {
        private const string MenuPath = "Tools/Character Prefabs/Organize Selected Prefabs";
        private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".png",
            ".jpg",
            ".jpeg",
            ".tga",
            ".psd",
            ".bmp",
            ".gif"
        };

        [MenuItem(MenuPath)]
        private static void OrganizeSelectedPrefabs()
        {
            List<PrefabPlan> plans = BuildPlansFromSelection(out List<string> skipped);
            if (plans.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "Character Prefab Organizer",
                    "No prefab assets selected.\n\n" + FormatSkipped(skipped),
                    "OK");
                return;
            }

            string preview = BuildPreview(plans, skipped);
            if (!EditorUtility.DisplayDialog("Character Prefab Organizer", preview, "Organize", "Cancel"))
            {
                return;
            }

            List<Result> results = new();
            foreach (PrefabPlan plan in plans)
            {
                results.Add(Organize(plan));
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string report = BuildReport(results, skipped);
            Debug.Log(report);
            EditorUtility.DisplayDialog("Character Prefab Organizer", report, "OK");
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateOrganizeSelectedPrefabs()
        {
            return Selection.objects != null && Selection.objects.Length > 0;
        }

        private static List<PrefabPlan> BuildPlansFromSelection(out List<string> skipped)
        {
            skipped = new List<string>();
            List<PrefabPlan> plans = new();
            HashSet<string> seenPaths = new(StringComparer.Ordinal);

            foreach (Object selected in Selection.objects)
            {
                string path = AssetDatabase.GetAssetPath(selected);
                if (string.IsNullOrEmpty(path))
                {
                    skipped.Add($"{selected.name}: not an asset");
                    continue;
                }

                if (AssetDatabase.IsValidFolder(path))
                {
                    skipped.Add($"{path}: folder selection is not processed");
                    continue;
                }

                if (!string.Equals(Path.GetExtension(path), ".prefab", StringComparison.OrdinalIgnoreCase))
                {
                    skipped.Add($"{path}: not a prefab");
                    continue;
                }

                if (!seenPaths.Add(path))
                {
                    continue;
                }

                plans.Add(BuildPlan(path));
            }

            return plans;
        }

        private static PrefabPlan BuildPlan(string prefabPath)
        {
            string prefabName = Path.GetFileNameWithoutExtension(prefabPath);
            string prefabFolder = Path.GetDirectoryName(prefabPath)?.Replace('\\', '/') ?? "Assets";
            string outputFolder = CombineProjectPath(prefabFolder, prefabName);

            GameObject root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(prefabPath);
                List<GameObject> inactiveChildren = CollectInactiveChildren(root);
                HashSet<Sprite> directSprites = CollectDirectSprites(root, inactiveChildren);
                int existingImages = CountImageAssets(outputFolder);
                int currentUsedImages = CountImagesUsedBySprites(outputFolder, directSprites);

                return new PrefabPlan
                {
                    prefabPath = prefabPath,
                    prefabName = prefabName,
                    outputFolder = outputFolder,
                    inactiveChildren = inactiveChildren.Count,
                    directSprites = directSprites.Count,
                    existingImages = existingImages,
                    currentUsedImages = currentUsedImages
                };
            }
            finally
            {
                if (root != null)
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        private static Result Organize(PrefabPlan plan)
        {
            Result result = new()
            {
                prefabPath = plan.prefabPath,
                outputFolder = plan.outputFolder
            };

            GameObject root = null;
            try
            {
                EnsureFolder(plan.outputFolder);

                root = PrefabUtility.LoadPrefabContents(plan.prefabPath);
                List<GameObject> inactiveChildren = CollectInactiveChildren(root);
                result.deletedInactiveObjects = inactiveChildren.Count;
                for (int i = 0; i < inactiveChildren.Count; i++)
                {
                    Object.DestroyImmediate(inactiveChildren[i]);
                }

                SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
                Dictionary<Sprite, Sprite> spriteMap = new();
                for (int i = 0; i < renderers.Length; i++)
                {
                    Sprite sprite = renderers[i].sprite;
                    if (sprite == null)
                    {
                        continue;
                    }

                    if (!spriteMap.TryGetValue(sprite, out Sprite localSprite))
                    {
                        localSprite = EnsureLocalSprite(sprite, plan.outputFolder, result);
                        spriteMap.Add(sprite, localSprite);
                    }

                    if (localSprite != null)
                    {
                        renderers[i].sprite = localSprite;
                    }
                }

                if (result.errors.Count > 0)
                {
                    result.failed = true;
                    return result;
                }

                PrefabUtility.SaveAsPrefabAsset(root, plan.prefabPath);
                result.savedPrefab = true;

                HashSet<Sprite> usedLocalSprites = CollectDirectSprites(root, null);
                result.directSprites = usedLocalSprites.Count;
                DeleteUnusedImages(plan.outputFolder, usedLocalSprites, result);
                CreateOrUpdateAtlas(plan.outputFolder, plan.prefabName, usedLocalSprites, result);
                DeleteOldAtlases(plan.outputFolder, plan.prefabName, result);

                return result;
            }
            catch (Exception exception)
            {
                result.failed = true;
                result.errors.Add(exception.Message);
                Debug.LogException(exception);
                return result;
            }
            finally
            {
                if (root != null)
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        private static List<GameObject> CollectInactiveChildren(GameObject root)
        {
            List<GameObject> inactiveChildren = new();
            if (root == null)
            {
                return inactiveChildren;
            }

            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform transform = transforms[i];
                if (transform == root.transform)
                {
                    continue;
                }

                if (transform.gameObject.activeSelf && !HasInactiveParent(transform, root.transform))
                {
                    continue;
                }

                if (!transform.gameObject.activeSelf && !HasInactiveParent(transform.parent, root.transform))
                {
                    inactiveChildren.Add(transform.gameObject);
                }
            }

            return inactiveChildren;
        }

        private static bool HasInactiveParent(Transform transform, Transform root)
        {
            while (transform != null && transform != root)
            {
                if (!transform.gameObject.activeSelf)
                {
                    return true;
                }

                transform = transform.parent;
            }

            return false;
        }

        private static HashSet<Sprite> CollectDirectSprites(GameObject root, List<GameObject> excludedObjects)
        {
            HashSet<GameObject> excluded = excludedObjects != null ? new HashSet<GameObject>(excludedObjects) : null;
            HashSet<Sprite> sprites = new();
            SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (excluded != null && HasExcludedParent(renderers[i].transform, root.transform, excluded))
                {
                    continue;
                }

                if (renderers[i].sprite != null)
                {
                    sprites.Add(renderers[i].sprite);
                }
            }

            return sprites;
        }

        private static bool HasExcludedParent(Transform transform, Transform root, HashSet<GameObject> excluded)
        {
            while (transform != null && transform != root)
            {
                if (excluded.Contains(transform.gameObject))
                {
                    return true;
                }

                transform = transform.parent;
            }

            return false;
        }

        private static Sprite EnsureLocalSprite(Sprite sourceSprite, string outputFolder, Result result)
        {
            string sourcePath = AssetDatabase.GetAssetPath(sourceSprite);
            if (string.IsNullOrEmpty(sourcePath))
            {
                result.errors.Add($"Sprite has no asset path: {sourceSprite.name}");
                return null;
            }

            if (IsPathInsideFolder(sourcePath, outputFolder))
            {
                return sourceSprite;
            }

            string destinationPath = ResolveDestinationPath(sourceSprite, sourcePath, outputFolder);
            if (!File.Exists(ProjectPathToAbsolute(destinationPath)))
            {
                if (!AssetDatabase.CopyAsset(sourcePath, destinationPath))
                {
                    result.errors.Add($"Failed to copy sprite asset: {sourcePath} -> {destinationPath}");
                    return null;
                }

                result.copiedImages++;
                AssetDatabase.ImportAsset(destinationPath, ImportAssetOptions.ForceSynchronousImport);
            }

            Sprite localSprite = FindMatchingSprite(sourceSprite, destinationPath);
            if (localSprite == null)
            {
                result.errors.Add($"Could not match copied sprite '{sourceSprite.name}' in {destinationPath}");
            }

            return localSprite;
        }

        private static string ResolveDestinationPath(Sprite sourceSprite, string sourcePath, string outputFolder)
        {
            string fileName = Path.GetFileName(sourcePath);
            string destinationPath = CombineProjectPath(outputFolder, fileName);
            if (!File.Exists(ProjectPathToAbsolute(destinationPath)))
            {
                return destinationPath;
            }

            Sprite existing = FindMatchingSprite(sourceSprite, destinationPath);
            if (existing != null)
            {
                return destinationPath;
            }

            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sourceSprite, out string guid, out long _);
            string suffix = string.IsNullOrEmpty(guid) ? "copy" : guid.Substring(0, Mathf.Min(8, guid.Length));
            string baseName = Path.GetFileNameWithoutExtension(sourcePath);
            string extension = Path.GetExtension(sourcePath);
            destinationPath = CombineProjectPath(outputFolder, $"{baseName}_{suffix}{extension}");
            int index = 2;
            while (File.Exists(ProjectPathToAbsolute(destinationPath)) && FindMatchingSprite(sourceSprite, destinationPath) == null)
            {
                destinationPath = CombineProjectPath(outputFolder, $"{baseName}_{suffix}_{index}{extension}");
                index++;
            }

            return destinationPath;
        }

        private static Sprite FindMatchingSprite(Sprite sourceSprite, string assetPath)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            List<Sprite> candidates = new();
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Sprite sprite)
                {
                    candidates.Add(sprite);
                }
            }

            if (candidates.Count == 0)
            {
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                if (sprite != null)
                {
                    candidates.Add(sprite);
                }
            }

            if (candidates.Count == 1)
            {
                return candidates[0];
            }

            Sprite match = null;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (!string.Equals(candidates[i].name, sourceSprite.name, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!SameRect(candidates[i].rect, sourceSprite.rect))
                {
                    continue;
                }

                if (match != null)
                {
                    return null;
                }

                match = candidates[i];
            }

            return match;
        }

        private static bool SameRect(Rect a, Rect b)
        {
            return Mathf.Approximately(a.x, b.x)
                && Mathf.Approximately(a.y, b.y)
                && Mathf.Approximately(a.width, b.width)
                && Mathf.Approximately(a.height, b.height);
        }

        private static void DeleteUnusedImages(string outputFolder, HashSet<Sprite> usedSprites, Result result)
        {
            HashSet<string> usedPaths = new(StringComparer.Ordinal);
            foreach (Sprite sprite in usedSprites)
            {
                string path = AssetDatabase.GetAssetPath(sprite);
                if (!string.IsNullOrEmpty(path))
                {
                    usedPaths.Add(path);
                }
            }

            string absoluteFolder = ProjectPathToAbsolute(outputFolder);
            if (!Directory.Exists(absoluteFolder))
            {
                return;
            }

            string[] files = Directory.GetFiles(absoluteFolder);
            for (int i = 0; i < files.Length; i++)
            {
                string extension = Path.GetExtension(files[i]);
                if (!ImageExtensions.Contains(extension))
                {
                    continue;
                }

                string projectPath = AbsoluteToProjectPath(files[i]);
                if (usedPaths.Contains(projectPath))
                {
                    result.keptImages++;
                    continue;
                }

                if (AssetDatabase.DeleteAsset(projectPath))
                {
                    result.deletedImages++;
                }
            }
        }

        private static void CreateOrUpdateAtlas(string outputFolder, string prefabName, HashSet<Sprite> sprites, Result result)
        {
            string atlasPath = CombineProjectPath(outputFolder, $"{prefabName}.spriteatlasv2");
            SpriteAtlas atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath);
            if (atlas == null)
            {
                atlas = new SpriteAtlas();
                AssetDatabase.CreateAsset(atlas, atlasPath);
            }

            Object[] existingPackables = atlas.GetPackables();
            if (existingPackables.Length > 0)
            {
                SpriteAtlasExtensions.Remove(atlas, existingPackables);
            }

            Object[] packables = new Object[sprites.Count];
            int index = 0;
            foreach (Sprite sprite in sprites)
            {
                packables[index++] = sprite;
            }

            if (packables.Length > 0)
            {
                SpriteAtlasExtensions.Add(atlas, packables);
            }

            SpriteAtlasPackingSettings packingSettings = atlas.GetPackingSettings();
            packingSettings.enableRotation = false;
            packingSettings.enableTightPacking = false;
            packingSettings.padding = 8;
            atlas.SetPackingSettings(packingSettings);
            atlas.SetIncludeInBuild(true);

            EditorUtility.SetDirty(atlas);
            result.atlasPath = atlasPath;
            result.atlasPackables = sprites.Count;
        }

        private static void DeleteOldAtlases(string outputFolder, string prefabName, Result result)
        {
            string absoluteFolder = ProjectPathToAbsolute(outputFolder);
            if (!Directory.Exists(absoluteFolder))
            {
                return;
            }

            string expectedName = $"{prefabName}.spriteatlasv2";
            string[] files = Directory.GetFiles(absoluteFolder);
            for (int i = 0; i < files.Length; i++)
            {
                string extension = Path.GetExtension(files[i]);
                if (!string.Equals(extension, ".spriteatlas", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(extension, ".spriteatlasv2", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (string.Equals(Path.GetFileName(files[i]), expectedName, StringComparison.Ordinal))
                {
                    continue;
                }

                string projectPath = AbsoluteToProjectPath(files[i]);
                if (AssetDatabase.DeleteAsset(projectPath))
                {
                    result.deletedOldAtlases++;
                }
            }
        }

        private static int CountImageAssets(string outputFolder)
        {
            string absoluteFolder = ProjectPathToAbsolute(outputFolder);
            if (!Directory.Exists(absoluteFolder))
            {
                return 0;
            }

            int count = 0;
            string[] files = Directory.GetFiles(absoluteFolder);
            for (int i = 0; i < files.Length; i++)
            {
                if (ImageExtensions.Contains(Path.GetExtension(files[i])))
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountImagesUsedBySprites(string outputFolder, HashSet<Sprite> sprites)
        {
            HashSet<string> usedPaths = new(StringComparer.Ordinal);
            foreach (Sprite sprite in sprites)
            {
                string path = AssetDatabase.GetAssetPath(sprite);
                if (IsPathInsideFolder(path, outputFolder))
                {
                    usedPaths.Add(path);
                }
            }

            return usedPaths.Count;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private static bool IsPathInsideFolder(string path, string folder)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(folder))
            {
                return false;
            }

            string normalizedPath = path.Replace('\\', '/');
            string normalizedFolder = folder.TrimEnd('/').Replace('\\', '/') + "/";
            return normalizedPath.StartsWith(normalizedFolder, StringComparison.Ordinal);
        }

        private static string CombineProjectPath(string left, string right)
        {
            return $"{left.TrimEnd('/')}/{right.TrimStart('/')}".Replace('\\', '/');
        }

        private static string ProjectPathToAbsolute(string projectPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            return Path.GetFullPath(Path.Combine(projectRoot, projectPath));
        }

        private static string AbsoluteToProjectPath(string absolutePath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            string fullPath = Path.GetFullPath(absolutePath);
            string relative = Path.GetRelativePath(projectRoot, fullPath);
            return relative.Replace('\\', '/');
        }

        private static string BuildPreview(List<PrefabPlan> plans, List<string> skipped)
        {
            StringBuilder builder = new();
            builder.AppendLine($"Prefabs to organize: {plans.Count}");
            builder.AppendLine();
            for (int i = 0; i < plans.Count; i++)
            {
                PrefabPlan plan = plans[i];
                int staleImages = Mathf.Max(0, plan.existingImages - plan.currentUsedImages);
                builder.AppendLine($"{plan.prefabPath}");
                builder.AppendLine($"  inactive children: {plan.inactiveChildren}");
                builder.AppendLine($"  direct sprites after cleanup: {plan.directSprites}");
                builder.AppendLine($"  existing images: {plan.existingImages}, stale now: {staleImages}");
            }

            string skippedText = FormatSkipped(skipped);
            if (!string.IsNullOrEmpty(skippedText))
            {
                builder.AppendLine();
                builder.AppendLine(skippedText);
            }

            return builder.ToString();
        }

        private static string BuildReport(List<Result> results, List<string> skipped)
        {
            StringBuilder builder = new();
            int success = 0;
            for (int i = 0; i < results.Count; i++)
            {
                if (!results[i].failed)
                {
                    success++;
                }
            }

            builder.AppendLine($"Character prefab organizer finished. Success: {success}/{results.Count}");
            builder.AppendLine();
            for (int i = 0; i < results.Count; i++)
            {
                Result result = results[i];
                builder.AppendLine(result.failed ? $"FAILED {result.prefabPath}" : $"OK {result.prefabPath}");
                builder.AppendLine($"  inactive deleted: {result.deletedInactiveObjects}");
                builder.AppendLine($"  direct sprites: {result.directSprites}");
                builder.AppendLine($"  copied images: {result.copiedImages}");
                builder.AppendLine($"  kept images: {result.keptImages}, deleted images: {result.deletedImages}");
                builder.AppendLine($"  atlas packables: {result.atlasPackables}");
                if (result.deletedOldAtlases > 0)
                {
                    builder.AppendLine($"  deleted old atlases: {result.deletedOldAtlases}");
                }

                for (int j = 0; j < result.errors.Count; j++)
                {
                    builder.AppendLine($"  error: {result.errors[j]}");
                }
            }

            string skippedText = FormatSkipped(skipped);
            if (!string.IsNullOrEmpty(skippedText))
            {
                builder.AppendLine();
                builder.AppendLine(skippedText);
            }

            return builder.ToString();
        }

        private static string FormatSkipped(List<string> skipped)
        {
            if (skipped == null || skipped.Count == 0)
            {
                return string.Empty;
            }

            StringBuilder builder = new();
            builder.AppendLine("Skipped:");
            for (int i = 0; i < skipped.Count; i++)
            {
                builder.AppendLine($"  {skipped[i]}");
            }

            return builder.ToString();
        }

        private struct PrefabPlan
        {
            public string prefabPath;
            public string prefabName;
            public string outputFolder;
            public int inactiveChildren;
            public int directSprites;
            public int existingImages;
            public int currentUsedImages;
        }

        private sealed class Result
        {
            public string prefabPath;
            public string outputFolder;
            public string atlasPath;
            public int deletedInactiveObjects;
            public int directSprites;
            public int copiedImages;
            public int keptImages;
            public int deletedImages;
            public int deletedOldAtlases;
            public int atlasPackables;
            public bool savedPrefab;
            public bool failed;
            public readonly List<string> errors = new();
        }
    }
}
#endif
