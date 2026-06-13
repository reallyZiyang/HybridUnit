using Game.Play.Battle.Runtime;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Play.Battle.Rendering
{
    internal sealed class DrawMeshBattlefieldBoundaryRenderer
    {
        private const int BoundaryRenderQueue = 2900;

        private Mesh mesh;
        private Material material;
        private BattlefieldBoundaryConfig config;
        private bool enabled;

        public void SetBoundary(BattlefieldBoundaryConfig value)
        {
            config = value;
            enabled = BattlefieldBoundary.IsEnabled(config);
            if (!enabled)
            {
                ReleaseMesh();
                return;
            }

            RebuildMesh();
        }

        public void Draw()
        {
            if (!enabled || mesh == null || material == null)
            {
                return;
            }

            Graphics.DrawMesh(
                mesh,
                Matrix4x4.identity,
                material,
                0,
                null,
                0,
                null,
                ShadowCastingMode.Off,
                false);
        }

        public void Clear()
        {
            ReleaseMesh();
            if (material != null)
            {
                BattleRenderObjectUtility.DestroyObject(material);
                material = null;
            }

            enabled = false;
            config = default;
        }

        private void RebuildMesh()
        {
            ReleaseMesh();
            EnsureMaterial();

            Rect rect = BattlefieldBoundary.GetRect(config);
            Vector3[] vertices =
            {
                new(rect.xMin, rect.yMin, 0f),
                new(rect.xMax, rect.yMin, 0f),
                new(rect.xMax, rect.yMax, 0f),
                new(rect.xMin, rect.yMax, 0f)
            };
            int[] triangles = { 0, 2, 1, 0, 3, 2 };

            mesh = new Mesh
            {
                name = "Battlefield Boundary Fill",
                hideFlags = HideFlags.DontSave,
                vertices = vertices,
                triangles = triangles
            };
            mesh.bounds = new Bounds(rect.center, new Vector3(rect.width, rect.height, 1f));
            mesh.RecalculateNormals();
        }

        private void EnsureMaterial()
        {
            if (material != null)
            {
                return;
            }

            Shader shader = Shader.Find("Hybrid/Battle DrawMesh Instance Unlit")
                ?? Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Sprites/Default")
                ?? Shader.Find("Standard");
            if (shader == null)
            {
                return;
            }

            material = new Material(shader)
            {
                name = "Battlefield Boundary Fill Material",
                hideFlags = HideFlags.DontSave,
                enableInstancing = false,
                renderQueue = BoundaryRenderQueue
            };
            SetMaterialColor(material, BattlefieldBoundary.FillColor);
        }

        private static void SetMaterialColor(Material target, Color color)
        {
            target.renderQueue = BoundaryRenderQueue;
            if (target.HasProperty("_BaseColor"))
            {
                target.SetColor("_BaseColor", color);
            }

            if (target.HasProperty("_Color"))
            {
                target.SetColor("_Color", color);
            }

            if (target.HasProperty("_InstanceColor"))
            {
                target.SetColor("_InstanceColor", Color.white);
            }
        }

        private void ReleaseMesh()
        {
            if (mesh == null)
            {
                return;
            }

            BattleRenderObjectUtility.DestroyObject(mesh);
            mesh = null;
        }
    }
}
