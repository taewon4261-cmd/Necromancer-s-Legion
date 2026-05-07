#if UNITY_EDITOR
using Necromancer.Systems;
using UnityEditor;
using UnityEngine;

namespace Necromancer.Editor
{
    public static class MinionVfxPrefabGenerator
    {
        private const string VfxPath = "Assets/00.Necromancer/03.Prefabs/VFX";
        private const string MaterialPath = "Assets/00.Necromancer/02.Data/Generated/VFXMaterials";

        [MenuItem("Necromancer/Generate Minion Skill VFX Prefabs")]
        public static void GenerateAll()
        {
            EnsureFolders();

            Material additiveMaterial = GetOrCreateMaterial("M_VFX_Additive", "Particles/Standard Unlit", new Color(1f, 0.72f, 0.22f, 1f));
            Material lightningMaterial = GetOrCreateMaterial("M_VFX_Lightning", "Sprites/Default", new Color(0.45f, 0.85f, 1f, 1f));

            CreateParticlePrefab("VFX_SlamShockwave", additiveMaterial, ConfigureSlamShockwave, delay: 0.75f);
            CreateParticlePrefab("VFX_ChargeSlash", additiveMaterial, ConfigureChargeSlash, delay: 0.55f);
            CreateParticlePrefab("VFX_FireZone", additiveMaterial, ConfigureFireZone, delay: 3.2f);
            CreateLinePrefab("VFX_ChainLightning", lightningMaterial, delay: 0.35f);
            CreateParticlePrefab("VFX_MeteorImpact", additiveMaterial, ConfigureMeteorImpact, delay: 1.1f);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("<color=orange><b>[MinionVfxPrefabGenerator]</b></color> Minion skill VFX prefabs generated.");
        }

        private static void CreateParticlePrefab(string tag, Material material, System.Action<ParticleSystem> configure, float delay)
        {
            GameObject root = new GameObject(tag);
            ParticleSystem ps = root.AddComponent<ParticleSystem>();
            ParticleSystemRenderer renderer = root.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = 20;

            configure(ps);
            AddAutoRelease(root, tag, delay);
            SavePrefab(root, tag);
        }

        private static void CreateLinePrefab(string tag, Material material, float delay)
        {
            GameObject root = new GameObject(tag);
            LineRenderer line = root.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.positionCount = 6;
            line.useWorldSpace = false;
            line.loop = false;
            line.widthMultiplier = 0.08f;
            line.sortingOrder = 25;
            line.numCornerVertices = 1;
            line.numCapVertices = 2;
            line.startColor = new Color(0.35f, 0.85f, 1f, 0.95f);
            line.endColor = new Color(0.9f, 1f, 1f, 0.15f);
            line.SetPosition(0, new Vector3(-1.1f, 0.05f, 0f));
            line.SetPosition(1, new Vector3(-0.62f, 0.28f, 0f));
            line.SetPosition(2, new Vector3(-0.22f, -0.18f, 0f));
            line.SetPosition(3, new Vector3(0.18f, 0.20f, 0f));
            line.SetPosition(4, new Vector3(0.62f, -0.10f, 0f));
            line.SetPosition(5, new Vector3(1.1f, 0.06f, 0f));

            ParticleSystem spark = root.AddComponent<ParticleSystem>();
            ConfigureLightningSpark(spark);
            ParticleSystemRenderer renderer = root.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = 26;

            AddAutoRelease(root, tag, delay);
            SavePrefab(root, tag);
        }

        private static void ConfigureSlamShockwave(ParticleSystem ps)
        {
            var main = ps.main;
            main.duration = 0.35f;
            main.loop = false;
            main.startLifetime = 0.28f;
            main.startSpeed = 2.8f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.32f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.72f, 0.25f, 0.95f), new Color(1f, 0.38f, 0.08f, 0.45f));
            main.maxParticles = 48;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = true;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 32) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.18f;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.5f, 1f, 2.4f));
        }

        private static void ConfigureChargeSlash(ParticleSystem ps)
        {
            var main = ps.main;
            main.duration = 0.25f;
            main.loop = false;
            main.startLifetime = 0.22f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.8f, 3.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.24f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.86f, 0.4f, 0.9f), new Color(1f, 0.42f, 0.08f, 0.45f));
            main.maxParticles = 36;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = true;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 24) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 18f;
            shape.radius = 0.2f;
            shape.rotation = new Vector3(0f, 90f, 0f);
        }

        private static void ConfigureFireZone(ParticleSystem ps)
        {
            var main = ps.main;
            main.duration = 3f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.34f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.58f, 0.08f, 0.75f), new Color(1f, 0.14f, 0.02f, 0.35f));
            main.maxParticles = 80;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = true;

            var emission = ps.emission;
            emission.rateOverTime = 22f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 1.1f;
        }

        private static void ConfigureLightningSpark(ParticleSystem ps)
        {
            var main = ps.main;
            main.duration = 0.2f;
            main.loop = false;
            main.startLifetime = 0.18f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.4f, 3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.45f, 0.9f, 1f, 0.95f), new Color(1f, 1f, 1f, 0.25f));
            main.maxParticles = 20;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = true;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 14) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.55f;
        }

        private static void ConfigureMeteorImpact(ParticleSystem ps)
        {
            var main = ps.main;
            main.duration = 0.65f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 5.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.22f, 0.48f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.68f, 0.18f, 1f), new Color(1f, 0.1f, 0.02f, 0.42f));
            main.maxParticles = 96;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = true;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 64) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.35f;
        }

        private static void AddAutoRelease(GameObject root, string tag, float delay)
        {
            PoolObjectAutoRelease autoRelease = root.AddComponent<PoolObjectAutoRelease>();
            autoRelease.poolTag = tag;
            autoRelease.delay = delay;
        }

        private static void SavePrefab(GameObject root, string tag)
        {
            string path = $"{VfxPath}/{tag}.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }

        private static Material GetOrCreateMaterial(string fileName, string shaderName, Color tint)
        {
            string path = $"{MaterialPath}/{fileName}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;

            Shader shader = Shader.Find(shaderName);
            if (shader == null) shader = Shader.Find("Sprites/Default");

            material = new Material(shader)
            {
                color = tint
            };

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/00.Necromancer", "03.Prefabs");
            EnsureFolder("Assets/00.Necromancer/03.Prefabs", "VFX");
            EnsureFolder("Assets/00.Necromancer", "02.Data");
            EnsureFolder("Assets/00.Necromancer/02.Data", "Generated");
            EnsureFolder("Assets/00.Necromancer/02.Data/Generated", "VFXMaterials");
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, child);
        }
    }
}
#endif
