using System;
using DG.Tweening;
using UniKit.Framework.Timer;
using UniKit.UI.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Play.UI.View.Common.Loading
{
    public partial class LoadingView : UIView
    {
        private Timer m_TimeoutTimer;
        
        protected override void OnShow()
        {
            m_Mask.SetActive(false);
            m_Progress.SetActive(false);
        }

        public void SetProgressActive(bool value)
        {
            bool prevIsHidden = !m_Progress.gameObject.activeSelf;
            m_Progress.SetActive(value);
            if (prevIsHidden && value)
                m_SldValue.value = 0;
        }

        public void SetProgress(float value, float duration = 0.25f, Action callback = null)
        {
            SetProgressActive(true);
            m_SldValue.DOKill();

            if (callback != null)
            {
                m_SldValue.DOValue(value, duration).OnComplete(() => callback()).SetUpdate(true);
            }
            else
            {
                m_SldValue.DOValue(value, duration).SetUpdate(true);
            }
        }

        public void ShowMask(string tips = null)
        {
            m_TxtMask.text = string.IsNullOrEmpty(tips) ? tips : string.Empty;
            m_Mask.SetActive(true);
        }

        public void HideMask()
        {
            m_Mask.SetActive(false);
        }

        public void ShowTimeoutMask(string tips = null, float time = 1)
        {
            m_TxtMask.text = string.Empty;
            var maskImg = m_Mask.GetComponent<Image>();
            maskImg.color = Color.clear;
            m_Mask.SetActive(true);
            m_ImgLoading.SetActive(false);
            m_TimeoutTimer = this.RegisterTimer(time, () =>
            {
                m_TxtMask.text = string.IsNullOrEmpty(tips) ? tips : string.Empty;
                m_ImgLoading.SetActive(true);
                maskImg.color = new Color(0, 0, 0, .3f);
            });
        }

        public void HideTimeoutMask()
        {
            m_Mask.SetActive(false);
            m_ImgLoading.SetActive(true);
            m_TimeoutTimer?.Cancel();
            m_TimeoutTimer = null;
            var maskImg = m_Mask.GetComponent<Image>();
            maskImg.color = new Color(0, 0, 0, .3f);
        }
    }
}