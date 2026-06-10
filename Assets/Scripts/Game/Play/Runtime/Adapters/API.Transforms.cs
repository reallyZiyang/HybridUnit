using UnityEngine;

namespace Game.Play.Adapters
{
    public static partial class API
    {
        public struct Transforms
        {
            private static Camera m_MainCamera;
            private static Camera m_UICamera;
            private static Canvas m_UICanvas;
            private static Canvas m_UIWorldCanvas;
            
            /// <summary>
            /// 主相机
            /// </summary>
            public static Camera MainCamera
            {
                get
                {
                    if (m_MainCamera == null)
                    {
                        m_MainCamera = Camera.main;
                    }
                    return m_MainCamera;
                }
            }

            /// <summary>
            /// UGUI相机
            /// </summary>
            public static Camera UICamera
            {
                get
                {
                    if (m_UICamera == null)
                    {
                        m_UICamera = GameObject.Find("UI Root/UI Camera").GetComponent<Camera>();
                    }
                    return m_UICamera;
                }
            }

            public static Canvas UICanvas
            {
                get
                {
                    if (m_UICanvas == null)
                    {
                        m_UICanvas = GameObject.Find("UI Root/UI Canvas").GetComponent<Canvas>();
                    }
                    return m_UICanvas;
                }
            }

            public static Canvas UIWorldCanvas
            {
                get
                {
                    if (m_UIWorldCanvas == null)
                    {
                        m_UIWorldCanvas = GameObject.Find("UI Root/UI World Canvas").GetComponent<Canvas>();
                    }
                    return m_UIWorldCanvas;
                }
            }

            public static Camera GetRenderCamera(Transform target)
            {
                var canvas = target.gameObject.GetComponentInParent<Canvas>();

                if (canvas == null)
                {
                    if (target as RectTransform)
                    {
                        return UICamera;
                    }
                    else
                    {
                        return Camera.main;
                    }
                }

                if (canvas.renderMode == RenderMode.WorldSpace)
                {
                    return Camera.main;
                }
                {
                    return canvas.worldCamera;
                }
            }

            /// <summary>
            /// RectTransform 转换为世界坐标
            /// </summary>
            /// <param name="rectTransform"></param>
            /// <returns></returns>
            public static Rect GetWorldRect(RectTransform rectTransform)
            {
                var corners = new Vector3[4];
                rectTransform.GetWorldCorners(corners);
                var width = Mathf.Abs(Vector2.Distance(corners[0], corners[3]));
                var height = Mathf.Abs(Vector2.Distance(corners[0], corners[1]));
                return new Rect(corners[0], new Vector2(width, height));
            }

            /// <summary>
            /// RectTransform 转换为屏幕坐标
            /// </summary>
            /// <param name="rectTransform"></param>
            /// <returns></returns>
            public static Rect GetLocalRect(RectTransform rectTransform)
            {
                var corners = new Vector3[4];
                rectTransform.GetWorldCorners(corners);
                for (var i = 0; i < corners.Length; i++)
                {
                    corners[i] = WorldToLocalPoint(rectTransform, corners[i]);
                }
                var width = Mathf.Abs(Vector2.Distance(corners[0], corners[3]));
                var height = Mathf.Abs(Vector2.Distance(corners[0], corners[1]));
                return new Rect(corners[0], new Vector2(width, height));
            }

            #region 世界坐标转UI坐标

            /// <summary>
            /// 世界坐标转换为UI坐标
            /// </summary>
            /// <param name="worldPoint"></param>
            /// <returns></returns>
            public static Vector3 WorldToGUIPoint(Vector3 worldPoint)
            {
                var camera = Camera.main;
                if (camera == null)
                {
                    return Vector3.zero;
                }
                Vector2 screenPoint = camera.WorldToScreenPoint(worldPoint);
                return ScreenPointToLocalPointInRectangle(UICanvas.transform as RectTransform, screenPoint);
            }

            /// <summary>
            /// 世界坐标转换为UI坐标
            /// </summary>
            /// <param name="worldPoint"></param>
            /// <returns></returns>
            public static Vector3 WorldToGUIPoint(Camera camera, Vector3 worldPoint)
            {
                Vector2 screenPoint = camera.WorldToScreenPoint(worldPoint);
                return ScreenPointToLocalPointInRectangle(UICanvas.transform as RectTransform, screenPoint);
            }

            /// <summary>
            /// 世界坐标转换为屏幕坐标
            /// </summary>
            /// <param name="worldPoint"></param>
            /// <returns></returns>
            public static Vector3 WorldToLocalPoint(Vector3 worldPoint)
            {
                return WorldToLocalPoint(UICamera, UICanvas.transform as RectTransform, worldPoint);
            }

            /// <summary>
            /// 世界坐标转换为屏幕坐标
            /// </summary>
            /// <param name="rect"></param>
            /// <param name="worldPoint"></param>
            /// <returns></returns>
            public static Vector3 WorldToLocalPoint(RectTransform rect, Vector3 worldPoint)
            {
                return WorldToLocalPoint(UICamera, rect, worldPoint);
            }

            /// <summary>
            /// 世界坐标转换为屏幕坐标
            /// </summary>
            /// <param name="camera"></param>
            /// <param name="rect"></param>
            /// <param name="worldPoint"></param>
            /// <returns></returns>
            public static Vector3 WorldToLocalPoint(Camera camera, RectTransform rect, Vector3 worldPoint)
            {
                var screenPoint = WorldToScreenPoint(camera, worldPoint);
                return ScreenPointToLocalPointInRectangle(rect, screenPoint);
            }

            /// <summary>
            /// 世界坐标转换为屏幕坐标
            /// </summary>
            /// <param name="rect"></param>
            /// <param name="worldPoint"></param>
            /// <returns></returns>
            public static Vector3 WorldToRenderPoint(Transform target, RectTransform rect, Vector3 worldPoint)
            {
                var camera = GetRenderCamera(target);
                var screenPoint = WorldToScreenPoint(camera, worldPoint);
                return ScreenPointToLocalPointInRectangle(rect, screenPoint);
            }

            #endregion

            #region 世界坐标转屏幕坐标

            /// <summary>
            /// 世界坐标转屏幕坐标
            /// </summary>
            /// <param name="position"></param>
            /// <returns></returns>
            public static Vector2 WorldToScreenPoint(Vector3 worldPoint)
            {
                return RectTransformUtility.WorldToScreenPoint(Camera.main, worldPoint);
            }

            /// <summary>
            /// 世界坐标转屏幕坐标
            /// </summary>
            /// <param name="camera"></param>
            /// <param name="position"></param>
            /// <returns></returns>
            public static Vector2 WorldToScreenPoint(Camera camera, Vector3 worldPoint)
            {
                return RectTransformUtility.WorldToScreenPoint(camera, worldPoint);
            }

            #endregion

            #region 屏幕坐标转世界坐标

            /// <summary>
            /// 屏幕坐标转世界坐标
            /// </summary>
            /// <param name="rect"></param>
            /// <param name="screenPoint"></param>
            /// <param name="worldPoint"></param>
            /// <returns></returns>
            public static Vector3 ScreenPointToWorldPoint(RectTransform rect, Vector2 screenPoint)
            {
                return ScreenPointToWorldPoint(rect, screenPoint, UICamera);
            }

            /// <summary>
            /// 屏幕坐标转世界坐标
            /// </summary>
            /// <param name="rect"></param>
            /// <param name="screenPoint"></param>
            /// <param name="cam"></param>
            /// <param name="worldPoint"></param>
            /// <returns></returns>
            public static Vector3 ScreenPointToWorldPoint(RectTransform rect, Vector2 screenPoint, Camera camera)
            {
                Vector3 worldPoint;
                if (RectTransformUtility.ScreenPointToWorldPointInRectangle(rect, screenPoint, camera, out worldPoint))
                {
                    return worldPoint;
                }
                else
                {
                    return Vector3.zero;
                }
            }

            #endregion

            #region 屏幕坐标转UGUI坐标

            /// <summary>
            /// 屏幕坐标转某个RectTransform下的localPosition坐标
            /// </summary>
            /// <param name="rect"></param>
            /// <param name="screenPoint"></param>
            /// <param name="localPoint"></param>
            /// <returns></returns>
            public static Vector2 ScreenPointToLocalPointInRectangle(Vector2 screenPoint)
            {
                return ScreenPointToLocalPointInRectangle(UICanvas.transform as RectTransform, screenPoint, UICamera);
            }

            /// <summary>
            /// 屏幕坐标转某个RectTransform下的localPosition坐标
            /// </summary>
            /// <param name="rect"></param>
            /// <param name="screenPoint"></param>
            /// <param name="localPoint"></param>
            /// <returns></returns>
            public static Vector2 ScreenPointToLocalPointInRectangle(RectTransform rect, Vector2 screenPoint)
            {
                return ScreenPointToLocalPointInRectangle(rect, screenPoint, UICamera);
            }

            /// <summary>
            /// 屏幕坐标转某个RectTransform下的localPosition坐标
            /// </summary>
            /// <param name="rect"></param>
            /// <param name="screenPoint"></param>
            /// <param name="cam"></param>
            /// <param name="localPoint"></param>
            /// <returns></returns>
            public static Vector2 ScreenPointToLocalPointInRectangle(RectTransform rect, Vector2 screenPoint, Camera camera)
            {
                Vector2 localPoint;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screenPoint, camera, out localPoint))
                {
                    return localPoint;
                }
                else
                {
                    return Vector2.zero;
                }
            }

            #endregion
        }
    }
}