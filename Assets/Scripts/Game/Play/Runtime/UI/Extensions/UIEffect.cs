using System;
using Coffee.UIExtensions;
using Game.Play.Adapters;
using UniKit.UI.Core;
using UnityEditor;
using UnityEngine;
using static Coffee.UIExtensions.UIParticle;

namespace Game.UI
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(UIParticle))]
    public class UIEffect : UINode
    {
        [SerializeField]
        private bool m_AutoLoad;
        [SerializeField]
        private string m_EffectName;

        private string m_CurLoadingEffect;
        private GameObject m_Effect;
        private UIParticle m_UIParticle;

        private void Awake()
        {
            m_UIParticle = gameObject.GetOrAddComponent<UIParticle>();
        }

        private void Start()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (!string.IsNullOrEmpty(m_EffectName))
            {
                Load(m_EffectName);
            }
        }

        public void Load(string name, Action callback = null)
        {
            // 检查当前对象是否是预制体，如果是预制体则不加载特效，避免修改预制体
            if (gameObject.scene.name == null || gameObject.scene.name == "")
            {
                Debug.LogWarning("当前对象是预制体，不加载特效");
                return;
            }

#if UNITY_EDITOR
            if (PrefabUtility.IsPartOfPrefabAsset(gameObject))
            {
                Debug.LogWarning("当前对象是预制体，不加载特效");
                callback?.Invoke();
                return;
            }
#endif

            if (name.Equals(m_CurLoadingEffect))
            {
                m_UIParticle.RefreshParticles();
                callback?.Invoke();
                return;
            }


            m_CurLoadingEffect = name;
            API.Assets.InstantiateDelegate(name, (key, particle) =>
            {
                if (!m_CurLoadingEffect.Equals(name) || !this || !gameObject)
                {
                    GameObject.Destroy(particle);
                    return;
                }
                m_UIParticle.RefreshParticles();
                callback?.Invoke();
                m_Effect = particle;
            }, parent: transform);
        }

        public void SetScale(float scale)
        {
            m_UIParticle.scale = scale;
        }

        public void UseCustomParticle(bool use = true)
        {
            m_UIParticle.useCustomView = use;
        }

        public void SetScaleMode(AutoScalingMode mode)
        {
            m_UIParticle.autoScalingMode = mode;
        }

        public void Play()
        {
            m_UIParticle?.Play();
        }

        public void Stop()
        {
            m_UIParticle?.Stop();
        }

        public void Clear()
        {
            m_UIParticle?.Clear();
        }

        public void RefreshParticles()
        {
            if (m_UIParticle)
            {
                m_UIParticle.RefreshParticles();
            }
        }

        public bool HasEffect()
        {
            return m_Effect != null;
        }

        public void Destroy()
        {
            m_CurLoadingEffect = string.Empty;
            if (!m_Effect) return;
            GameObject.Destroy(m_Effect);
            m_Effect = null;
        }
    }
}