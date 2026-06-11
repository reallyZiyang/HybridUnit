using UnityEngine;

namespace Game.Play.Rendering.Runtime
{
    public sealed class BattleDrawMeshInstanceRenderHost : MonoBehaviour
    {
        [SerializeField] private int activeCount;
        [SerializeField] private string lastDrawCameraName;
        [SerializeField] private int lastDrawInstanceCount;

        public int ActiveCount => activeCount;
        public string LastDrawCameraName => lastDrawCameraName;
        public int LastDrawInstanceCount => lastDrawInstanceCount;

        public void Bind(BattleDrawMeshInstanceManager targetManager)
        {
            activeCount = targetManager?.ActiveCount ?? 0;
        }

        public void RecordDrawStats(int active, int drawn, string cameraName)
        {
            activeCount = active;
            lastDrawInstanceCount = drawn;
            if (drawn > 0)
            {
                lastDrawCameraName = cameraName;
            }
        }
    }
}
