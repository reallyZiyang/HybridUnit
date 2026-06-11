using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Play.Battle.Rendering
{
    internal static class BattleRenderObjectUtility
    {
        public static void DestroyObject(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(target);
            }
            else
            {
                Object.DestroyImmediate(target);
            }
        }

        public static int SecondsToMilliseconds(float seconds)
        {
            return seconds > 0f ? Mathf.CeilToInt(seconds * 1000f) : 0;
        }
    }
}
