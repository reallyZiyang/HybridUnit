using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Game.Play.Editor.Extensions
{
    public class CaptureExtensions : MonoBehaviour
    {
        [MenuItem("Tools/Capture/GameObject Path #g")]
        private static void GenerateGameObjectPath()
        {
            var go = Selection.activeTransform.gameObject;
            var path = GetGameObjectPath(go);
            GUIUtility.systemCopyBuffer = path;
            Debug.Log(go.name + ": " + path);
        }

        [MenuItem("Tools/Capture/GameObject Path #g", true)]
        private static bool ValidateGenerateGameObjectPath()
        {
            return Selection.activeTransform != null;
        }

        private static string GetGameObjectPath(GameObject obj)
        {
            var path = "/" + obj.name;
            while (obj.transform.parent != null)
            {
                obj = obj.transform.parent.gameObject;
                path = "/" + obj.name + path;
            }

            var uiPrefix = path.IndexOf("ViewRoot/", StringComparison.Ordinal);
            if (uiPrefix > 0)
            {
                var subIdx = uiPrefix + 9;
                return path.Substring(subIdx, path.Length - subIdx);
            }
            else
            {
                return path.Substring(1);
            }
        }

        [MenuItem("Tools/Capture/GameObject Position")]
        private static void CapturePosition()
        {
            var locations = new List<string>();
            foreach (var obj in Selection.gameObjects)
            {
                var position = obj.transform.position;
                locations.Add(string.Format("[{0},{1},{2}]",
                    Math.Round(position.x, 2), Math.Round(position.y, 2), Math.Round(position.z, 2)));
            }

            var locationStr = $"[{string.Join(",", locations)}]";
            Debug.Log(locationStr);
            GUIUtility.systemCopyBuffer = locationStr;
        }

        [MenuItem("Tools/Capture/GameObject Position", true)]
        private static bool ValidateCapturePosition()
        {
            return Selection.gameObjects != null && Selection.gameObjects.Length > 0;
        }

        [MenuItem("Tools/Capture/GameObject Rotaion")]
        private static void CaptureRotation()
        {
            var locations = new List<string>();
            foreach (var obj in Selection.gameObjects)
            {
                var rotation = obj.transform.localRotation.eulerAngles;
                locations.Add(string.Format("[{0},{1},{2}]",
                    Math.Round(rotation.x, 2), Math.Round(rotation.y, 2), Math.Round(rotation.z, 2)));
            }

            var locationStr = $"[{string.Join(",", locations)}]";
            Debug.Log(locationStr);
            GUIUtility.systemCopyBuffer = locationStr;
        }

        [MenuItem("Tools/Capture/GameObject Rotation", true)]
        private static bool ValidateCaptureRotation()
        {
            return Selection.gameObjects != null && Selection.gameObjects.Length > 0;
        }

        [MenuItem("Tools/Capture/Objects Name")]
        private static void CaptureObjectNames()
        {
            var objects = Selection.objects;
            var names = new List<string>();
            foreach (var obj in objects)
            {
                names.Add(obj.name);
            }

            var namesStr = string.Join("\n", names);
            Debug.Log(namesStr);
            GUIUtility.systemCopyBuffer = namesStr;
        }

        [MenuItem("Tools/Capture/Objects Name", true)]
        private static bool ValidateCaptureObjectNames()
        {
            return Selection.objects.Length > 0;
        }

        [MenuItem("Tools/Capture/Objects Sort")]
        private static void SortChildrenByName()
        {
            foreach (var obj in Selection.gameObjects)
            {
                var children = new List<Transform>();
                for (var i = obj.transform.childCount - 1; i >= 0; i--)
                {
                    var child = obj.transform.GetChild(i);
                    children.Add(child);
                    child.parent = null;
                }

                children.Sort((t1, t2) => string.Compare(t1.name, t2.name, StringComparison.Ordinal));
                foreach (var child in children)
                {
                    child.parent = obj.transform;
                }
            }
        }
    }
}