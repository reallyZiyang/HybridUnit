using System;
using Cysharp.Threading.Tasks;

namespace Game.Play.Adapters
{
    public static partial class API
    {
        public static void Fork(Action callback)
        {
            WaitForFrame(callback).Forget();
        }

        public static void DelayInvoke(float delay, Action action, bool ignoreTimeScale = false)
        {
            WaitForSeconds(delay, action, ignoreTimeScale).Forget();
        }

        private static async UniTask WaitForFrame(Action callback)
        {
            await UniTask.NextFrame();
            callback?.Invoke();
        }

        private static async UniTask WaitForSeconds(float delay, Action action, bool ignoreTimeScale = false)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(delay), ignoreTimeScale: ignoreTimeScale);
            action?.Invoke();
        }
    }
}