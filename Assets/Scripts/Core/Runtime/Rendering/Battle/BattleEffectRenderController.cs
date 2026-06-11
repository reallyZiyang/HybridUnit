using UnityEngine;

namespace Game.Play.Battle.Rendering
{
    public sealed class BattleEffectRenderController : MonoBehaviour
    {
        [SerializeField] private BakedSequencePlayer sequence;
        [SerializeField] private ParticleSystem particle;

        private void Awake()
        {
            EnsurePlayers();
        }

        public void Play()
        {
            EnsurePlayers();
            if (sequence != null)
            {
                sequence.Play();
                return;
            }

            if (particle != null)
            {
                particle.Play(true);
            }
        }

        public void Stop()
        {
            if (sequence != null)
            {
                sequence.Stop();
            }

            if (particle != null)
            {
                particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private void EnsurePlayers()
        {
            if (sequence == null)
            {
                sequence = GetComponentInChildren<BakedSequencePlayer>(true);
            }

            if (particle == null)
            {
                particle = GetComponentInChildren<ParticleSystem>(true);
            }
        }
    }
}
