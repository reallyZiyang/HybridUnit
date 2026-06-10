using System.Collections.Generic;
using UniKit.Asset;
using UniKit.Asset.Pooling;
using UnityEngine;

namespace Game.Play.Battle.Rendering
{
    public sealed class GameObjectBattleRenderWorld : IBattleRenderWorld
    {
        private sealed class RenderEntry
        {
            public int handle;
            public string key;
            public Vector2 position;
            public float angleDeg;
            public string pendingAction;
            public GameObject gameObject;
            public Component spineVit;
            public Component sequence;
            public ParticleSystem particle;
            public bool visible = true;
        }

        private readonly Dictionary<int, RenderEntry> entries = new();
        private int nextHandle = 1;

        public int SpawnUnit(string renderKey, Vector2 position)
        {
            return Spawn(renderKey, position, 0f);
        }

        public int SpawnProjectile(string projectileKey, Vector2 position, float angleDeg)
        {
            return Spawn(projectileKey, position, angleDeg);
        }

        public void PlayAction(int renderHandle, string actionName)
        {
            if (!entries.TryGetValue(renderHandle, out RenderEntry entry))
            {
                return;
            }

            entry.pendingAction = actionName;
            if (entry.spineVit != null && TryInvokePlay(entry.spineVit, actionName))
            {
                return;
            }

            if (entry.sequence != null && TryInvokePlay(entry.sequence, null))
            {
                return;
            }
            else if (entry.particle != null)
            {
                entry.particle.Play(true);
            }
        }

        public void SetPosition(int renderHandle, Vector2 position)
        {
            if (!entries.TryGetValue(renderHandle, out RenderEntry entry))
            {
                return;
            }

            entry.position = position;
            if (entry.gameObject != null)
            {
                entry.gameObject.transform.position = new Vector3(position.x, position.y, entry.gameObject.transform.position.z);
            }
        }

        public void SetRotation(int renderHandle, float angleDeg)
        {
            if (!entries.TryGetValue(renderHandle, out RenderEntry entry))
            {
                return;
            }

            entry.angleDeg = angleDeg;
            if (entry.gameObject != null)
            {
                entry.gameObject.transform.rotation = Quaternion.Euler(0f, 0f, angleDeg);
            }
        }

        public void SetVisible(int renderHandle, bool visible)
        {
            if (!entries.TryGetValue(renderHandle, out RenderEntry entry))
            {
                return;
            }

            entry.visible = visible;
            if (entry.gameObject != null)
            {
                entry.gameObject.SetActive(visible);
            }
        }

        public void Despawn(int renderHandle)
        {
            if (!entries.TryGetValue(renderHandle, out RenderEntry entry))
            {
                return;
            }

            Release(entry);
            entries.Remove(renderHandle);
        }

        public void Tick(float deltaTime)
        {
            foreach (RenderEntry entry in entries.Values)
            {
                if (entry.gameObject == null)
                {
                    TryBind(entry);
                }
            }
        }

        public void Clear()
        {
            foreach (RenderEntry entry in entries.Values)
            {
                Release(entry);
            }

            entries.Clear();
        }

        private int Spawn(string key, Vector2 position, float angleDeg)
        {
            int handle = nextHandle++;
            RenderEntry entry = new()
            {
                handle = handle,
                key = key,
                position = position,
                angleDeg = angleDeg
            };
            entries.Add(handle, entry);
            TryBind(entry);
            return handle;
        }

        private void TryBind(RenderEntry entry)
        {
            if (string.IsNullOrEmpty(entry.key))
            {
                return;
            }

            AssetPool pool = AssetPoolObjects.Instance.Find(entry.key);
            if (pool == null)
            {
                AssetPoolObjects.Instance.CreatePool(entry.key);
                return;
            }

            if (!pool.isLoading)
            {
                return;
            }

            AssetReference reference = pool.Get();
            if (reference == null)
            {
                return;
            }

            GameObject go = reference.gameObject;
            entry.gameObject = go;
            entry.spineVit = FindComponentByTypeName(go, "BakedSpineVitPlayer");
            entry.sequence = FindComponentByTypeName(go, "BakedSequencePlayer");
            entry.particle = go.GetComponentInChildren<ParticleSystem>(true);

            go.transform.position = new Vector3(entry.position.x, entry.position.y, go.transform.position.z);
            go.transform.rotation = Quaternion.Euler(0f, 0f, entry.angleDeg);
            go.SetActive(entry.visible);

            if (!string.IsNullOrEmpty(entry.pendingAction))
            {
                PlayAction(entry.handle, entry.pendingAction);
            }
        }

        private static void Release(RenderEntry entry)
        {
            if (entry.gameObject == null)
            {
                return;
            }

            entry.gameObject.Dispose();
            entry.gameObject = null;
            entry.spineVit = null;
            entry.sequence = null;
            entry.particle = null;
        }

        private static Component FindComponentByTypeName(GameObject go, string typeName)
        {
            MonoBehaviour[] components = go.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < components.Length; i++)
            {
                MonoBehaviour component = components[i];
                if (component != null && component.GetType().Name == typeName)
                {
                    return component;
                }
            }

            return null;
        }

        private static bool TryInvokePlay(Component component, string actionName)
        {
            System.Type type = component.GetType();
            if (actionName != null)
            {
                System.Reflection.MethodInfo methodWithName = type.GetMethod("Play", new[] { typeof(string) });
                if (methodWithName != null)
                {
                    methodWithName.Invoke(component, new object[] { actionName });
                    return true;
                }
            }

            System.Reflection.MethodInfo method = type.GetMethod("Play", System.Type.EmptyTypes);
            if (method == null)
            {
                return false;
            }

            method.Invoke(component, null);
            return true;
        }
    }
}
