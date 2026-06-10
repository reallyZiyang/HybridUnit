using System;
using Game.Play.Adapters;
using UnityEngine;

namespace Game.Play.UI.Extensions
{
    [Flags]
    public enum PreferredDirection
    {
        Null = 0,
        Bottom = 2 << 1,
        Top = 2 << 2,
        Left = 2 << 3,
        Right = 2 << 4,
    }

    public static class AutoAnchorExtension
    {
        public static void SetAutoAnchor(this MonoBehaviour target, Vector2 screenPos,
            PreferredDirection preferredDirection = PreferredDirection.Top | PreferredDirection.Left)
        {
            target.GetOrAddComponent<AutoAnchor>().ShowAtPosition(screenPos, preferredDirection);
        }
    }
    
    public class AutoAnchor : MonoBehaviour
    {
        [SerializeField] private Vector2 m_Offset = new Vector2(0, 10);  // 偏移量
        
        [SerializeField] private PreferredDirection m_PreferredDirection = PreferredDirection.Top | PreferredDirection.Left;
        
        public void ShowAtPosition(Vector2 screenPos, PreferredDirection preferredDirection)
        {
            m_PreferredDirection = preferredDirection;
            Vector2 anchoredPos = GetOptimalPosition(screenPos);
            (transform as RectTransform).anchoredPosition = anchoredPos;
        }
        
        private Vector2 GetOptimalPosition(Vector2 screenPos)
        {
            var canvas = API.Transforms.UICanvas;
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            Camera uiCamera = API.Transforms.UICamera;
            
            // 转换点击位置到Canvas本地坐标
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screenPos, uiCamera, out Vector2 localPos);
            
            var rectTrans = transform as RectTransform;
            Vector2 popupSize = rectTrans.rect.size;
            Rect canvasRectBounds = canvasRect.rect;
            
            // 检查各个方向的可用空间
            float spaceAbove = (canvasRectBounds.yMax - localPos.y) - popupSize.y;
            float spaceBelow = (localPos.y - canvasRectBounds.yMin) - popupSize.y;
            float spaceLeft = (localPos.x - canvasRectBounds.xMin) - popupSize.x;
            float spaceRight = (canvasRectBounds.xMax - localPos.x) - popupSize.x;
            
            Vector2 finalPos = localPos;
    
            if ((m_PreferredDirection & PreferredDirection.Bottom) != 0)
            {
                if (spaceBelow > 0)
                    finalPos.y -= popupSize.y / 2 + m_Offset.y;  // 显示在下方
                else if (spaceAbove > 0)
                    finalPos.y += popupSize.y / 2 + m_Offset.y;  // 显示在上方
            }
    
            if ((m_PreferredDirection & PreferredDirection.Top) != 0)
            {
                if (spaceAbove > 0)
                    finalPos.y += popupSize.y / 2 + m_Offset.y;  // 显示在上方
                else if (spaceBelow > 0)
                    finalPos.y -= popupSize.y / 2 + m_Offset.y;  // 显示在下方
            }
    
            if ((m_PreferredDirection & PreferredDirection.Left) != 0)
            {
                if (spaceLeft > 0)
                    finalPos.x -= popupSize.x / 2 + m_Offset.x;  // 显示在左边
                else if (spaceRight > 0)
                    finalPos.x += popupSize.x / 2 + m_Offset.x;  // 显示在右边
            }
    
            if ((m_PreferredDirection & PreferredDirection.Right) != 0)
            {
                if (spaceRight > 0)
                    finalPos.x += popupSize.x / 2 + m_Offset.x;  // 显示在右边
                else if (spaceLeft > 0)
                    finalPos.x -= popupSize.x / 2 + m_Offset.x;  // 显示在左边
            }
            
            // 水平方向居中调整
            finalPos.x = Mathf.Clamp(
                finalPos.x,
                canvasRectBounds.xMin + popupSize.x / 2,
                canvasRectBounds.xMax - popupSize.x / 2
            );
            
            // 垂直方向调整
            finalPos.y = Mathf.Clamp(
                finalPos.y,
                canvasRectBounds.yMin + popupSize.y / 2,
                canvasRectBounds.yMax - popupSize.y / 2
            );
            
            return finalPos;
        }
    }
}