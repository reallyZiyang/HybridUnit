using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Game.Play.Editor
{
    public static class EditorUtilities
    {
        public static T FindScriptableObject<T>() where T : ScriptableObject
        {
            var guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            if (guids.Length == 0)
                return null;

            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }

        public static List<T> FindPrefabsWithScript<T>()
        {
            var result = new List<T>();
            var guids = AssetDatabase.FindAssets("t:Prefab");

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!prefab) continue;

                var comp = prefab.GetComponentInChildren<T>(true);
                if (comp != null)
                {
                    result.Add(comp);
                }
            }

            return result;
        }
    }
}