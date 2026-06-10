using UnityEngine;

namespace Game.Play.UI.Extensions
{
    public static class UIUtilities
    {
        /// <summary>
        /// 把 tips 放在 target 附近，并自动做屏幕边界适配
        /// </summary>
        public static void PlaceTips(
            RectTransform target,
            Canvas canvas,
            RectTransform tips,
            float screenPadding = 0f)
        {
            var targetCanvas = target.GetComponentInParent<Canvas>();
            var targetCanvasMode = targetCanvas.renderMode;
            var targetCam = targetCanvas.worldCamera;

            var corners = new Vector3[4];
            target.GetWorldCorners(corners);

            var useWorldToScreen = targetCanvasMode != RenderMode.ScreenSpaceOverlay;

            Vector3 bl, tl, tr, br;
            if (useWorldToScreen)
            {
                bl = RectTransformUtility.WorldToScreenPoint(targetCam, corners[0]);
                tl = RectTransformUtility.WorldToScreenPoint(targetCam, corners[1]);
                tr = RectTransformUtility.WorldToScreenPoint(targetCam, corners[2]);
                br = RectTransformUtility.WorldToScreenPoint(targetCam, corners[3]);
            }
            else
            {
                bl = corners[0];
                tl = corners[1];
                tr = corners[2];
                br = corners[3];
            }

            var tLeft = bl.x;
            var tRight = br.x;
            var tTop = tl.y;
            var tBottom = bl.y;

            var tipsSize = tips.rect.size * tips.localScale;
            var width = tipsSize.x;
            var height = tipsSize.y;

            var left = tLeft;
            var top = tBottom;

            if (left + width > Screen.width - screenPadding)
            {
                left = tRight - width;
            }

            if (top - height < screenPadding)
            {
                top = tTop + height;
            }

            // clamp 防止翻转后仍溢出
            left = Mathf.Clamp(left, screenPadding, Screen.width - screenPadding - width);
            top = Mathf.Clamp(top, screenPadding + height, Screen.height - screenPadding);

            var pivot = tips.pivot;
            var centerX = left + width * pivot.x;
            var centerY = top - height * (1 - pivot.y);

            var screenPoint = new Vector2(centerX, centerY);

            var canvasRect = canvas.transform as RectTransform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenPoint,
                canvas.worldCamera,
                out var localPoint);

            tips.anchoredPosition = localPoint;
        }
    }
}