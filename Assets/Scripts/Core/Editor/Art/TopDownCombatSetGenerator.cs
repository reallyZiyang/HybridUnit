#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class TopDownCombatSetGenerator
{
    private const string Root = "Assets/RawRes/TopDownCombatSet";
    private const string AtlasPath = Root + "/TopDownCombatSet_Atlas.png";
    private const string ManifestPath = Root + "/TopDownCombatSet_Atlas.json";
    private const string AnimationFolder = Root + "/Animations";
    private const string TurretPrefabFolder = Root + "/Prefabs/Turrets";
    private const string MonsterPrefabFolder = Root + "/Prefabs/Monsters";
    private const string ProjectilePrefabFolder = Root + "/Prefabs/Projectiles";
    private const string EffectPrefabFolder = Root + "/Prefabs/Effects";
    private const string GeneratedMarkerPath = Root + "/TopDownCombatSet.generated";

    [InitializeOnLoadMethod]
    private static void AutoGenerateIfMissing()
    {
        EditorApplication.delayCall += () =>
        {
            if (IsGeneratedUpToDate())
            {
                return;
            }

            if (!File.Exists(AtlasPath) || !File.Exists(ManifestPath))
            {
                return;
            }

            Generate();
        };
    }

    [MenuItem("Hybrid/Art/Generate TopDown Combat Set")]
    public static void Generate()
    {
        Directory.CreateDirectory(AnimationFolder);
        Directory.CreateDirectory(TurretPrefabFolder);
        Directory.CreateDirectory(MonsterPrefabFolder);
        Directory.CreateDirectory(ProjectilePrefabFolder);
        Directory.CreateDirectory(EffectPrefabFolder);

        ConfigureAtlas();
        Dictionary<string, Sprite> sprites = LoadSprites();

        CreateTurret("MachineGun", "turret_machine_base", "turret_machine_barrel", sprites, 0.12f, false);
        CreateTurret("Grenade", "turret_grenade_base", "turret_grenade_barrel", sprites, 0.34f, false);
        CreateTurret("Laser", "turret_laser_base", "turret_laser_barrel", sprites, 1.0f, false);
        CreateTurret("Gatling", "turret_gatling_base", "turret_gatling_barrel", sprites, 0.22f, true);

        CreateMonster("Melee", "monster_melee", sprites, true);
        CreateMonster("Ranged", "monster_ranged", sprites, false);

        CreateSingleSpritePrefab(ProjectilePrefabFolder + "/Projectile_MachineBullet.prefab", "Projectile_MachineBullet", "projectile_machine_bullet", sprites, 100);
        CreateSingleSpritePrefab(ProjectilePrefabFolder + "/Projectile_GrenadeShell.prefab", "Projectile_GrenadeShell", "projectile_grenade_shell", sprites, 100);
        CreateSingleSpritePrefab(ProjectilePrefabFolder + "/Projectile_LaserBeam.prefab", "Projectile_LaserBeam", "projectile_laser_beam", sprites, 100);
        CreateSingleSpritePrefab(ProjectilePrefabFolder + "/Projectile_GatlingBullet.prefab", "Projectile_GatlingBullet", "projectile_gatling_bullet", sprites, 100);
        CreateSingleSpritePrefab(ProjectilePrefabFolder + "/Projectile_RangedBolt.prefab", "Projectile_RangedBolt", "projectile_ranged_bolt", sprites, 100);

        CreateSingleSpritePrefab(EffectPrefabFolder + "/Effect_MachineHit.prefab", "Effect_MachineHit", "effect_machine_hit", sprites, 200);
        CreateSingleSpritePrefab(EffectPrefabFolder + "/Effect_GrenadeExplosion.prefab", "Effect_GrenadeExplosion", "effect_grenade_explosion", sprites, 200);
        CreateSingleSpritePrefab(EffectPrefabFolder + "/Effect_GatlingSpin.prefab", "Effect_GatlingSpin", "effect_gatling_spin", sprites, 200);
        CreateSingleSpritePrefab(EffectPrefabFolder + "/Effect_MeleeDash.prefab", "Effect_MeleeDash", "effect_melee_dash", sprites, 200);
        CreateSingleSpritePrefab(EffectPrefabFolder + "/Effect_MeleeHit.prefab", "Effect_MeleeHit", "effect_melee_hit", sprites, 200);
        CreateSingleSpritePrefab(EffectPrefabFolder + "/Effect_RangedCharge.prefab", "Effect_RangedCharge", "effect_ranged_charge", sprites, 200);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        File.WriteAllText(GeneratedMarkerPath, DateTime.Now.ToString("O"));
        AssetDatabase.ImportAsset(GeneratedMarkerPath);
        Debug.Log("Generated TopDown combat set.");
    }

    private static void ConfigureAtlas()
    {
        TextAsset manifestText = AssetDatabase.LoadAssetAtPath<TextAsset>(ManifestPath);
        TextureImporter importer = AssetImporter.GetAtPath(AtlasPath) as TextureImporter;
        if (manifestText == null || importer == null)
        {
            throw new InvalidOperationException("TopDown combat atlas or manifest not found.");
        }

        AtlasManifest manifest = JsonUtility.FromJson<AtlasManifest>(manifestText.text);
        if (manifest == null || manifest.sprites == null || manifest.sprites.Length == 0)
        {
            throw new InvalidOperationException("TopDown combat atlas manifest has no sprites.");
        }

        SpriteMetaData[] metas = new SpriteMetaData[manifest.sprites.Length];
        for (int i = 0; i < manifest.sprites.Length; i++)
        {
            AtlasSprite sprite = manifest.sprites[i];
            metas[i] = new SpriteMetaData
            {
                name = sprite.name,
                rect = new Rect(sprite.rect.x, sprite.rect.y, sprite.rect.width, sprite.rect.height),
                alignment = (int)SpriteAlignment.Custom,
                pivot = new Vector2(sprite.pivot.x, sprite.pivot.y)
            };
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 100f;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.spritesheet = metas;
        importer.SaveAndReimport();
    }

    private static Dictionary<string, Sprite> LoadSprites()
    {
        Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(AtlasPath);
        for (int i = 0; i < assets.Length; i++)
        {
            Sprite sprite = assets[i] as Sprite;
            if (sprite != null)
            {
                sprites[sprite.name] = sprite;
            }
        }

        return sprites;
    }

    private static void CreateTurret(string name, string baseSpriteName, string barrelSpriteName, Dictionary<string, Sprite> sprites, float attackDuration, bool gatling)
    {
        GameObject root = new GameObject("Turret_" + name);
        try
        {
            GameObject baseGo = CreateSpriteChild(root.transform, "base", sprites[baseSpriteName], 0);
            baseGo.transform.localPosition = Vector3.zero;

            GameObject pivot = new GameObject("weapon_pivot");
            pivot.transform.SetParent(root.transform, false);

            Transform barrelParent = pivot.transform;
            if (gatling)
            {
                GameObject spin = new GameObject("barrel_spin");
                spin.transform.SetParent(pivot.transform, false);
                barrelParent = spin.transform;
            }

            GameObject barrel = CreateSpriteChild(barrelParent, "barrel", sprites[barrelSpriteName], 10);
            barrel.transform.localPosition = Vector3.zero;

            AnimationClip idle = CreateTurretIdleClip(name);
            AnimationClip attack = gatling
                ? CreateGatlingAttackClip(name, attackDuration, GetGatlingBarrelFrames(barrelSpriteName, sprites))
                : CreateRecoilAttackClip(name, attackDuration);
            idle = SaveClip(idle, AnimationFolder + "/Turret_" + name + "_idle.anim");
            attack = SaveClip(attack, AnimationFolder + "/Turret_" + name + "_attack.anim");

            AttachAnimation(root, idle, true, new[] { "idle", "attack" }, idle, attack);

            PrefabUtility.SaveAsPrefabAsset(root, TurretPrefabFolder + "/Turret_" + name + ".prefab");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void CreateMonster(string name, string spriteName, Dictionary<string, Sprite> sprites, bool melee)
    {
        GameObject root = new GameObject("Monster_" + name);
        try
        {
            CreateSpriteChild(root.transform, "body", sprites[spriteName], 0);

            AnimationClip attack = melee ? CreateMeleeAttackClip() : CreateRangedAttackClip();
            attack = SaveClip(attack, AnimationFolder + "/Monster_" + name + "_attack.anim");

            AttachAnimation(root, attack, false, new[] { "attack" }, attack);

            PrefabUtility.SaveAsPrefabAsset(root, MonsterPrefabFolder + "/Monster_" + name + ".prefab");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void CreateSingleSpritePrefab(string prefabPath, string objectName, string spriteName, Dictionary<string, Sprite> sprites, int sortingOrder)
    {
        GameObject root = new GameObject(objectName);
        try
        {
            SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = sprites[spriteName];
            renderer.sortingOrder = sortingOrder;
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static GameObject CreateSpriteChild(Transform parent, string name, Sprite sprite, int sortingOrder)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(parent, false);
        SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = sortingOrder;
        return child;
    }

    private static AnimationClip CreateTurretIdleClip(string name)
    {
        AnimationClip clip = CreateClip("Turret_" + name + "_idle", 30f, true);
        SetCurve(clip, "base", typeof(Transform), "m_LocalScale.x", 0f, 1f, 0.6f, 1.015f, 1.2f, 1f);
        SetCurve(clip, "base", typeof(Transform), "m_LocalScale.y", 0f, 1f, 0.6f, 1.015f, 1.2f, 1f);
        return clip;
    }

    private static AnimationClip CreateRecoilAttackClip(string name, float duration)
    {
        AnimationClip clip = CreateClip("Turret_" + name + "_attack", 30f, false);
        float recoilTime = Mathf.Min(0.08f, duration * 0.35f);
        SetCurve(clip, "weapon_pivot/barrel", typeof(Transform), "m_LocalPosition.x", 0f, 0f, recoilTime, -0.08f, duration, 0f);
        SetCurve(clip, "weapon_pivot/barrel", typeof(Transform), "m_LocalScale.x", 0f, 1f, recoilTime, 0.96f, duration, 1f);
        return clip;
    }

    private static AnimationClip CreateGatlingAttackClip(string name, float duration, Sprite[] barrelFrames)
    {
        AnimationClip clip = CreateClip("Turret_" + name + "_attack", 30f, true);
        float recoilTime = Mathf.Min(0.05f, duration * 0.25f);
        SetCurve(clip, "weapon_pivot/barrel_spin", typeof(Transform), "m_LocalPosition.x", 0f, 0f, recoilTime, -0.04f, duration, 0f);
        SetSpriteFrames(clip, "weapon_pivot/barrel_spin/barrel", duration, barrelFrames);
        return clip;
    }

    private static AnimationClip CreateMeleeAttackClip()
    {
        AnimationClip clip = CreateClip("Monster_Melee_attack", 30f, false);
        SetCurve(clip, "body", typeof(Transform), "m_LocalPosition.x", 0f, 0f, 0.18f, -0.16f, 0.36f, 0.22f);
        AddKey(clip, "body", typeof(Transform), "m_LocalPosition.x", 0.5f, 0f);
        SetCurve(clip, "body", typeof(Transform), "m_LocalScale.x", 0f, 1f, 0.18f, 0.9f, 0.36f, 1.18f);
        AddKey(clip, "body", typeof(Transform), "m_LocalScale.x", 0.5f, 1f);
        SetCurve(clip, "body", typeof(Transform), "m_LocalScale.y", 0f, 1f, 0.18f, 1.08f, 0.36f, 0.92f);
        AddKey(clip, "body", typeof(Transform), "m_LocalScale.y", 0.5f, 1f);
        return clip;
    }

    private static AnimationClip CreateRangedAttackClip()
    {
        AnimationClip clip = CreateClip("Monster_Ranged_attack", 30f, false);
        SetCurve(clip, "body", typeof(Transform), "m_LocalScale.x", 0f, 1f, 0.28f, 1.18f, 0.45f, 0.96f);
        AddKey(clip, "body", typeof(Transform), "m_LocalScale.x", 0.65f, 1f);
        SetCurve(clip, "body", typeof(Transform), "m_LocalScale.y", 0f, 1f, 0.28f, 0.92f, 0.45f, 1.05f);
        AddKey(clip, "body", typeof(Transform), "m_LocalScale.y", 0.65f, 1f);
        return clip;
    }

    private static AnimationClip CreateClip(string name, float frameRate, bool loop)
    {
        AnimationClip clip = new AnimationClip
        {
            name = name,
            frameRate = frameRate,
            legacy = true
        };
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        return clip;
    }

    private static void SetCurve(AnimationClip clip, string path, Type type, string property, float t0, float v0, float t1, float v1, float t2, float v2)
    {
        AnimationCurve curve = new AnimationCurve(new Keyframe(t0, v0), new Keyframe(t1, v1), new Keyframe(t2, v2));
        SetAutoTangents(curve);
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, type, property), curve);
    }

    private static void SetSpriteFrames(AnimationClip clip, string path, float duration, Sprite[] frames)
    {
        if (frames == null || frames.Length == 0)
        {
            return;
        }

        const float frameTime = 1f / 30f;
        int keyCount = Mathf.Max(2, Mathf.CeilToInt(duration / frameTime) + 1);
        ObjectReferenceKeyframe[] keys = new ObjectReferenceKeyframe[keyCount];
        for (int i = 0; i < keyCount; i++)
        {
            keys[i] = new ObjectReferenceKeyframe
            {
                time = Mathf.Min(i * frameTime, duration),
                value = frames[i % frames.Length]
            };
        }

        keys[keys.Length - 1].time = duration;
        keys[keys.Length - 1].value = frames[0];
        AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve(path, typeof(SpriteRenderer), "m_Sprite"), keys);
    }

    private static void AddKey(AnimationClip clip, string path, Type type, string property, float time, float value)
    {
        EditorCurveBinding binding = EditorCurveBinding.FloatCurve(path, type, property);
        AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
        if (curve == null)
        {
            curve = new AnimationCurve();
        }
        curve.AddKey(time, value);
        SetAutoTangents(curve);
        AnimationUtility.SetEditorCurve(clip, binding, curve);
    }

    private static void SetAutoTangents(AnimationCurve curve)
    {
        for (int i = 0; i < curve.keys.Length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Auto);
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Auto);
        }
    }

    private static AnimationClip SaveClip(AnimationClip clip, string path)
    {
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(clip, path);
        AssetDatabase.ImportAsset(path);

        AnimationClip savedClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (savedClip == null)
        {
            throw new InvalidOperationException("Failed to save animation clip: " + path);
        }

        return savedClip;
    }

    private static Sprite[] GetGatlingBarrelFrames(string baseSpriteName, Dictionary<string, Sprite> sprites)
    {
        List<Sprite> frames = new List<Sprite>(3);
        AddSpriteFrame(baseSpriteName, sprites, frames);
        AddSpriteFrame(baseSpriteName + "_phase1", sprites, frames);
        AddSpriteFrame(baseSpriteName + "_phase2", sprites, frames);
        return frames.ToArray();
    }

    private static void AddSpriteFrame(string spriteName, Dictionary<string, Sprite> sprites, List<Sprite> frames)
    {
        if (sprites.TryGetValue(spriteName, out Sprite sprite))
        {
            frames.Add(sprite);
        }
    }

    private static bool IsGeneratedUpToDate()
    {
        if (!File.Exists(GeneratedMarkerPath)
            || !File.Exists(TurretPrefabFolder + "/Turret_MachineGun.prefab")
            || !File.Exists(TurretPrefabFolder + "/Turret_Gatling.prefab")
            || !File.Exists(MonsterPrefabFolder + "/Monster_Melee.prefab"))
        {
            return false;
        }

        AnimationClip gatlingAttack = AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimationFolder + "/Turret_Gatling_attack.anim");
        if (gatlingAttack == null)
        {
            return false;
        }

        EditorCurveBinding[] spriteBindings = AnimationUtility.GetObjectReferenceCurveBindings(gatlingAttack);
        for (int i = 0; i < spriteBindings.Length; i++)
        {
            EditorCurveBinding binding = spriteBindings[i];
            if (binding.path == "weapon_pivot/barrel_spin/barrel"
                && binding.type == typeof(SpriteRenderer)
                && binding.propertyName == "m_Sprite")
            {
                return true;
            }
        }

        return false;
    }

    private static void AttachAnimation(GameObject root, AnimationClip defaultClip, bool playAutomatically, string[] stateNames, params AnimationClip[] clips)
    {
        Animation animation = root.AddComponent<Animation>();
        animation.playAutomatically = playAutomatically;
        animation.clip = defaultClip;

        for (int i = 0; i < clips.Length; i++)
        {
            AnimationClip clip = clips[i];
            if (clip == null)
            {
                continue;
            }

            string stateName = stateNames != null && i < stateNames.Length && !string.IsNullOrEmpty(stateNames[i])
                ? stateNames[i]
                : clip.name;
            animation.AddClip(clip, stateName);
        }
    }

    [Serializable]
    private sealed class AtlasManifest
    {
        public string texture;
        public int width;
        public int height;
        public AtlasSprite[] sprites;
    }

    [Serializable]
    private sealed class AtlasSprite
    {
        public string name;
        public AtlasRect rect;
        public AtlasPivot pivot;
    }

    [Serializable]
    private sealed class AtlasRect
    {
        public float x;
        public float y;
        public float width;
        public float height;
    }

    [Serializable]
    private sealed class AtlasPivot
    {
        public float x;
        public float y;
    }
}
#endif
