using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Play.UI.Extensions
{
    /// <summary>
    /// 更多事件传递请自已拓展
    /// </summary>
    public class UIPointerEventPass : MonoBehaviour, IPointerClickHandler
    {
        [Header("排除区域")]
        [SerializeField] private List<RectTransform> excludeAreas = new();  // 点击这些区域不传递

        private EventSystem eventSystem;

        public void AddExcludeArea(RectTransform area)
        {
            excludeAreas.Add(area);
        }
        
        private void Start()
        {
            eventSystem = EventSystem.current;
        }
        
        public void OnPointerClick(PointerEventData eventData)
        {
            PassPointerEvent(eventData, EventTriggerType.PointerClick);
        }
        
        private void PassPointerEvent(PointerEventData eventData, EventTriggerType eventTriggerType)
        {
            if (IsPointerOverExcludeArea(eventData.position))
                return;
            
            Component firstInteractiveUI = GetFirstInteractiveUI(eventData.position);
            if (firstInteractiveUI)
                ExecuteEvent(firstInteractiveUI, eventTriggerType, eventData);
        }
        
        private Component GetFirstInteractiveUI(Vector2 screenPosition)
        {
            if (eventSystem == null) return null;
        
            PointerEventData eventData = new PointerEventData(eventSystem);
            eventData.position = screenPosition;
        
            List<RaycastResult> results = new List<RaycastResult>();
            eventSystem.RaycastAll(eventData, results);
        
            foreach (var result in results)
            {
                GameObject go = result.gameObject;
            
                // 跳过自身
                if (go == gameObject) continue;
            
                // 检查是否是交互式UI元素
                if (IsInteractiveUI(go, out var component))
                {
                    return component;
                }
            }
        
            return null;
        }

        private bool IsPointerOverExcludeArea(Vector2 screenPosition)
        {
            if (excludeAreas == null || excludeAreas.Count == 0) return false;
            
            foreach (var area in excludeAreas)
            {
                if (area == null) continue;
                
                if (RectTransformUtility.RectangleContainsScreenPoint(area, screenPosition))
                {
                    return true;
                }
            }
            
            return false;
        }
        
        private bool IsInteractiveUI(GameObject go, out Component component)
        {
            component = null;
            
            if (go.TryGetComponent<Selectable>(out var selectable))
            {
                component = selectable;
                return true;
            }

            if (go.TryGetComponent<ScrollRect>(out var scrollRect))
            {
                component = scrollRect;
                return true;
            }

            if (go.TryGetComponent<EventTrigger>(out var eventTrigger))
            {
                component = eventTrigger;
                return true;
            }

            return false;
        }
        
        private void ExecuteEvent(Component targetUI, EventTriggerType eventType, PointerEventData eventData)
        {
            if (targetUI is Selectable selectable)
                ExecuteSelectable(selectable, eventType);
            else if (targetUI is ScrollRect scrollRect)
                ExecuteScrollRect(scrollRect, eventType, eventData);
            else
                ExecuteTrigger(targetUI as EventTrigger, eventType, eventData);
        }

        private void ExecuteTrigger(EventTrigger targetUI, EventTriggerType eventType, PointerEventData eventData)
        {
            if (targetUI)
            {
                foreach (var entry in targetUI.triggers)
                {
                    if (entry.eventID == eventType)
                    {
                        entry.callback?.Invoke(eventData);
                        break;
                    }
                } 
            }
        }

        private void ExecuteSelectable(Selectable targetUI, EventTriggerType eventType)
        {
            switch (eventType)
            {
                case EventTriggerType.PointerClick:
                    if (targetUI is Button btn)
                        btn.onClick?.Invoke();
                    else if (targetUI is Toggle toggle)
                        toggle.isOn = !toggle.isOn;
                    break;
            }
        }

        private void ExecuteScrollRect(ScrollRect targetUI, EventTriggerType eventType, PointerEventData eventData)
        {
            
        }
        
    }
}