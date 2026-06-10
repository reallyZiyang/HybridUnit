using System;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using Game.Data.Configs;
using SimpleJSON;
using UniKit.Asset;
using UnityEngine;

namespace Game.Play.Adapters
{
    public static partial class API
    {
        private const string ConfigDummy = "global_tbglobal"; // 仅仅用于加载该组资源
        private const string ConfigPath = "Assets/Res/Data/Configs";
        private const string ConfigExtension = ".json";

        private static Tables sTables;

        public static Tables Tables
        {
            get
            {
#if UNITY_EDITOR
                if (sTables != null)
                    return sTables;

                Debug.LogWarning("[API] Configs.Tables is not initialized. Initializing now...");
                InitConfig().Forget();
#endif
                return sTables;
            }
        }

        public static async UniTask InitConfig()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                InitConfigsSync();
                return;
            }
#endif
            await InitConfigsAsync();
        }

# if UNITY_EDITOR
        public static T LoadConfig<T>(string path) where T : class
        {
            var file = Path.Combine(ConfigPath, path + ConfigExtension);
            if (!File.Exists(file))
            {
                Debug.LogError($"Config file not found: {file}");
                return null;
            }

            var text = File.ReadAllText(file);
            var node = JSONNode.Parse(text);
            return Activator.CreateInstance(typeof(T), new object[] { node }) as T;
        }

        [UnityEditor.MenuItem("Tools/热更配置")]
        public static void HotReloadConfig()
        {
            if (Application.isPlaying)
                InitConfigsSync();
        }
#endif

        private static void InitConfigsSync()
        {
            var files = Directory.GetFiles(ConfigPath, "*" + ConfigExtension, SearchOption.AllDirectories);
            var nodes = new Dictionary<string, JSONNode>();

            foreach (var file in files)
            {
                var text = File.ReadAllText(file);
                var node = JSONNode.Parse(text);
                nodes.Add(Path.GetFileNameWithoutExtension(file), node);
            }

            InitTables(nodes);
        }

        private static async UniTask InitConfigsAsync()
        {
            var assets = await AssetManager.LoadAssetListAsync<TextAsset>(ConfigDummy);
            var nodes = new Dictionary<string, JSONNode>();

            foreach (var asset in assets)
            {
                var node = JSONNode.Parse(asset.text);
                nodes.Add(Path.GetFileNameWithoutExtension(asset.name), node);
            }

            InitTables(nodes);
        }

        private static void InitTables(Dictionary<string, JSONNode> nodes)
        {
            sTables = new Tables(name =>
            {
                Debug.Log("[Initialize] Loading config: " + name);
                return nodes[name];
            });
        }
    }
}