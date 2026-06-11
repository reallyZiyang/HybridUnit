using System;
using System.Collections.Generic;
using UniKit.Asset;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Play.Battle.Rendering
{
    internal sealed class BattleRenderAssetLoader
    {
        private const int InitialPendingCapacity = 16;

        private readonly Dictionary<string, LoadRecord> records = new(64);
        private readonly Func<int, string, BattleRenderEntry> resolveEntry;
        private readonly Action<BattleRenderEntry, BattleRenderAssetBase> bind;

        public BattleRenderAssetLoader(
            Func<int, string, BattleRenderEntry> resolveEntry,
            Action<BattleRenderEntry, BattleRenderAssetBase> bind)
        {
            this.resolveEntry = resolveEntry;
            this.bind = bind;
        }

        public void Request(BattleRenderEntry entry)
        {
            if (entry == null || entry.assetRequestStarted || entry.assetRequestCompleted || string.IsNullOrEmpty(entry.key))
            {
                return;
            }

            entry.assetRequestStarted = true;
            string key = entry.key;
            if (!records.TryGetValue(key, out LoadRecord record))
            {
                record = new LoadRecord(key);
                records.Add(key, record);
            }

            if (record.completed)
            {
                CompleteEntryFromRecord(entry, record);
                return;
            }

            record.AddPending(entry.handle);
            if (record.loading)
            {
                return;
            }

            if (AssetManager.ContainsAsset(key))
            {
                Object cachedAsset = AssetManager.GetAsset<Object>(key);
                CompleteRecord(record, cachedAsset as BattleRenderAssetBase);
                return;
            }

            record.loading = true;
            AssetManager.LoadAssetDelegate<BattleRenderAssetBase>(
                key,
                (_, asset) => CompleteRecord(record, asset),
                _ => FailRecord(record));
        }

        public void ClearPending()
        {
            foreach (LoadRecord record in records.Values)
            {
                record.pendingCount = 0;
            }
        }

        private void CompleteRecord(LoadRecord record, BattleRenderAssetBase asset)
        {
            record.loading = false;
            record.completed = true;
            record.failed = asset == null;
            record.asset = asset;
            FlushPending(record);
        }

        private void FailRecord(LoadRecord record)
        {
            record.loading = false;
            record.completed = true;
            record.failed = true;
            record.asset = null;
            FlushPending(record);
        }

        private void FlushPending(LoadRecord record)
        {
            for (int i = 0; i < record.pendingCount; i++)
            {
                BattleRenderEntry entry = resolveEntry?.Invoke(record.pendingHandles[i], record.key);
                if (entry == null)
                {
                    continue;
                }

                CompleteEntryFromRecord(entry, record);
            }

            record.pendingCount = 0;
        }

        private void CompleteEntryFromRecord(BattleRenderEntry entry, LoadRecord record)
        {
            entry.assetRequestCompleted = true;
            if (record.failed || record.asset == null || entry.instanceHandle.IsValid)
            {
                return;
            }

            bind?.Invoke(entry, record.asset);
        }

        private sealed class LoadRecord
        {
            public readonly string key;
            public BattleRenderAssetBase asset;
            public bool loading;
            public bool completed;
            public bool failed;
            public int[] pendingHandles;
            public int pendingCount;

            public LoadRecord(string key)
            {
                this.key = key;
                pendingHandles = new int[InitialPendingCapacity];
            }

            public void AddPending(int handle)
            {
                if (pendingCount >= pendingHandles.Length)
                {
                    Array.Resize(ref pendingHandles, pendingHandles.Length * 2);
                }

                pendingHandles[pendingCount++] = handle;
            }
        }
    }
}
