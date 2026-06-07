#if UNITY_EDITOR
using System;
using System.IO;
using UnityEngine;

public static class ParticleAtlasPathUtility
{
    public static string AbsoluteToProjectPath(string absolutePath)
    {
        absolutePath = absolutePath.Replace('\\', '/');
        string dataPath = Application.dataPath.Replace('\\', '/');

        if (absolutePath.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase))
        {
            return "Assets" + absolutePath.Substring(dataPath.Length);
        }

        return absolutePath;
    }

    public static string ProjectPathToAbsolute(string projectPath)
    {
        projectPath = projectPath.Replace('\\', '/');
        if (Path.IsPathRooted(projectPath))
        {
            return projectPath;
        }

        string projectRoot = Directory.GetParent(Application.dataPath).FullName.Replace('\\', '/');
        return Path.Combine(projectRoot, projectPath).Replace('\\', '/');
    }

    public static string CombineProjectPath(string left, string right)
    {
        return Path.Combine(left, right).Replace('\\', '/');
    }

    public static string GetOutputBaseName(ParticleAtlasBakeSettings settings)
    {
        string rawName = string.IsNullOrWhiteSpace(settings.OutputName) ? settings.Prefab.name : settings.OutputName;
        foreach (char invalidChar in Path.GetInvalidFileNameChars())
        {
            rawName = rawName.Replace(invalidChar, '_');
        }

        return rawName;
    }
}
#endif
