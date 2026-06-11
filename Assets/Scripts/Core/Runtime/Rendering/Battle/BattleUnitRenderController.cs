using UnityEngine;

namespace Game.Play.Battle.Rendering
{
    public sealed class BattleUnitRenderController : MonoBehaviour
    {
        [SerializeField] private BakedAnimationVitPlayer player;
        [SerializeField] private string idleAction = "idle";
        [SerializeField] private string hitAction = "hit";
        [SerializeField] private string deadAction = "dead";

        private bool returnToIdleOnComplete;
        private bool dead;
        private BakedAnimationVitPlayer subscribedPlayer;

        private void Awake()
        {
            EnsurePlayer();
        }

        private void Update()
        {
            if (returnToIdleOnComplete && !dead && player != null && !player.IsPlaying)
            {
                PlayIdle();
            }
        }

        private void OnDestroy()
        {
            UnsubscribePlayer();
        }

        public float PlayAction(string actionName)
        {
            EnsurePlayer();
            if (player == null)
            {
                return 0f;
            }

            returnToIdleOnComplete = false;
            dead = false;
            return player.Play(string.IsNullOrEmpty(actionName) ? idleAction : actionName, false);
        }

        public float PlayIdle()
        {
            EnsurePlayer();
            returnToIdleOnComplete = false;
            dead = false;
            return player != null ? player.Play(idleAction, true) : 0f;
        }

        public float PlayHit()
        {
            EnsurePlayer();
            if (player == null || dead)
            {
                return 0f;
            }

            returnToIdleOnComplete = true;
            return player.Play(hitAction, false);
        }

        public float PlayDead()
        {
            EnsurePlayer();
            returnToIdleOnComplete = false;
            dead = true;
            return player != null ? player.Play(deadAction, false) : 0f;
        }

        public void SetAlpha(float alpha)
        {
            EnsurePlayer();
            if (player == null)
            {
                return;
            }

            Color color = player.InstanceColor;
            color.a = Mathf.Clamp01(alpha);
            player.SetInstanceColor(color);
        }

        private void EnsurePlayer()
        {
            if (player == null)
            {
                player = GetComponentInChildren<BakedAnimationVitPlayer>(true);
            }

            if (subscribedPlayer != player)
            {
                UnsubscribePlayer();
                subscribedPlayer = player;
                if (subscribedPlayer != null)
                {
                    subscribedPlayer.Completed += OnPlayerCompleted;
                }
            }
        }

        private void OnPlayerCompleted(string clipName)
        {
            if (!returnToIdleOnComplete || dead || clipName != hitAction)
            {
                return;
            }

            PlayIdle();
        }

        private void UnsubscribePlayer()
        {
            if (subscribedPlayer != null)
            {
                subscribedPlayer.Completed -= OnPlayerCompleted;
                subscribedPlayer = null;
            }
        }
    }
}
