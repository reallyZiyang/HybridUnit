#if UNITY_EDITOR
using UnityEngine;

public static class ParticleAtlasBakeUtility
{
    public static uint ClampToUInt(long value)
    {
        if (value < 0L)
        {
            return 0U;
        }

        if (value > uint.MaxValue)
        {
            return uint.MaxValue;
        }

        return (uint)value;
    }

    public static ParticleSystem[] GetRootParticleSystems(GameObject instance)
    {
        ParticleSystem[] allParticleSystems = instance.GetComponentsInChildren<ParticleSystem>(true);
        int rootCount = 0;

        for (int i = 0; i < allParticleSystems.Length; i++)
        {
            ParticleSystem particleSystem = allParticleSystems[i];
            if (particleSystem != null && !HasParentParticleSystem(particleSystem))
            {
                rootCount++;
            }
        }

        ParticleSystem[] roots = new ParticleSystem[rootCount];
        int rootIndex = 0;
        for (int i = 0; i < allParticleSystems.Length; i++)
        {
            ParticleSystem particleSystem = allParticleSystems[i];
            if (particleSystem != null && !HasParentParticleSystem(particleSystem))
            {
                roots[rootIndex] = particleSystem;
                rootIndex++;
            }
        }

        return roots;
    }

    public static void PrepareParticles(GameObject instance, ParticleSystem[] rootParticleSystems, ParticleAtlasBakeSettings settings)
    {
        ParticleSystem[] allParticleSystems = instance.GetComponentsInChildren<ParticleSystem>(true);

        ApplyLoopSettings(allParticleSystems, settings);

        if (settings.ForceRandomSeed)
        {
            for (int i = 0; i < allParticleSystems.Length; i++)
            {
                ParticleSystem particleSystem = allParticleSystems[i];
                if (particleSystem == null)
                {
                    continue;
                }

                particleSystem.useAutoRandomSeed = false;
                particleSystem.randomSeed = unchecked(settings.RandomSeed + (uint)i);
            }
        }

        for (int i = 0; i < rootParticleSystems.Length; i++)
        {
            ParticleSystem particleSystem = rootParticleSystems[i];
            if (particleSystem == null)
            {
                continue;
            }

            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particleSystem.Clear(true);
        }
    }

    public static void SampleParticles(ParticleSystem[] rootParticleSystems, float sampleTime)
    {
        for (int i = 0; i < rootParticleSystems.Length; i++)
        {
            ParticleSystem particleSystem = rootParticleSystems[i];
            if (particleSystem == null)
            {
                continue;
            }

            particleSystem.Simulate(sampleTime, true, true, false);
        }
    }

    public static float GetBakeDuration(ParticleAtlasBakeSettings settings)
    {
        if (settings == null)
        {
            return 0f;
        }

        if (!settings.Loop || settings.Prefab == null)
        {
            return settings.Duration;
        }

        float duration = GetMaxParticleDuration(settings.Prefab);
        return duration > 0f ? duration : settings.Duration;
    }

    public static bool TryGetRendererBounds(GameObject instance, out Bounds bounds)
    {
        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
        bounds = default;
        bool hasBounds = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled)
            {
                continue;
            }

            Bounds rendererBounds = renderer.bounds;
            if (rendererBounds.size == Vector3.zero)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = rendererBounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(rendererBounds);
            }
        }

        return hasBounds;
    }

    private static bool HasParentParticleSystem(ParticleSystem particleSystem)
    {
        Transform current = particleSystem.transform.parent;
        while (current != null)
        {
            if (current.GetComponent<ParticleSystem>() != null)
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private static void ApplyLoopSettings(ParticleSystem[] particleSystems, ParticleAtlasBakeSettings settings)
    {
        if (settings == null || !settings.Loop)
        {
            return;
        }

        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem particleSystem = particleSystems[i];
            if (particleSystem == null)
            {
                continue;
            }

            ParticleSystem.MainModule main = particleSystem.main;
            main.loop = true;
            main.prewarm = true;
        }
    }

    private static float GetMaxParticleDuration(GameObject prefab)
    {
        ParticleSystem[] particleSystems = prefab.GetComponentsInChildren<ParticleSystem>(true);
        float duration = 0f;

        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem particleSystem = particleSystems[i];
            if (particleSystem == null)
            {
                continue;
            }

            duration = Mathf.Max(duration, particleSystem.main.duration);
        }

        return duration;
    }
}
#endif
