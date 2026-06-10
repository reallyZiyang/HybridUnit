using System;
using Cysharp.Threading.Tasks;
using UniKit.Asset;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Play.Adapters
{
    public static partial class API
    {
        public struct Assets
        {
            public static UniTask<GameObject> InstantiateAsync(string key, Transform parent = null)
            {
                return AssetManager.InstantiateAsync(key, parent);
            }

            public static UniTask<GameObject> InstantiateAsync(string key, Vector3 position,
                Quaternion rotation,
                Transform parent = null)
            {
                return AssetManager.InstantiateAsync(key, position, rotation, parent);
            }

            public static void InstantiateDelegate(string key,
                Action<string, GameObject> onSucceeded = null,
                Transform parent = null)
            {
                AssetManager.InstantiateDelegate(key, parent, onSucceeded);
            }

            public static void InstantiateDelegate(string key,
                Vector3 position,
                Quaternion rotation,
                Transform parent = null,
                Action<string, GameObject> onSucceeded = null,
                Action<string> onFailed = null)
            {
                AssetManager.InstantiateDelegate(key, position, rotation, parent, onSucceeded, onFailed);
            }

            public static UniTask<GameObject> LoadAssetAsync(string key)
            {
                return AssetManager.LoadAssetAsync<GameObject>(key);
            }

            public static UniTask<T> LoadAssetAsync<T>(string key)
                where T : UnityEngine.Object
            {
                return AssetManager.LoadAssetAsync<T>(key);
            }

            public static void LoadAsset(string key,
                Action<string, GameObject> onSucceeded = null,
                Action<string> onFailed = null)
            {
                AssetManager.LoadAssetDelegate(key, onSucceeded, onFailed);
            }

            public static void LoadAsset<T>(string key,
                Action<string, T> onSucceeded = null,
                Action<string> onFailed = null)
                where T : UnityEngine.Object
            {
                AssetManager.LoadAssetDelegate<T>(key, onSucceeded, onFailed);
            }

            public static void LoadScene(string key,
                Action<Scene> onSucceeded = null,
                Action<float> onProgress = null,
                LoadSceneMode loadMode = LoadSceneMode.Single)
            {
                AssetManager.LoadSceneDelegate(key, onSucceeded, loadMode: loadMode);
            }

            public static void UnloadScene(string key,
                Action<string> onSucceeded = null,
                Action<string> onFailed = null)
            {
                AssetManager.UnloadSceneDelegate(key, onSucceeded, onFailed);
            }
        }
    }
}