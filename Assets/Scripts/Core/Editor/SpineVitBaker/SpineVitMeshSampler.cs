#if UNITY_EDITOR
using System;
using Spine;
using Spine.Unity;
using UnityEngine;

public sealed class SpineVitMeshSampler
{
    private readonly Skeleton skeleton;
    private readonly MeshGenerator meshGenerator;
    private readonly SkeletonRendererInstruction instruction;
    private readonly Mesh scratchMesh;
    private readonly Material sourceMaterial;

    public SpineVitMeshSampler(SkeletonDataAsset skeletonDataAsset, string skinName, Material sourceMaterial)
    {
        SkeletonData skeletonData = skeletonDataAsset.GetSkeletonData(false);
        if (skeletonData == null)
        {
            throw new InvalidOperationException("SkeletonDataAsset failed to load SkeletonData.");
        }

        skeleton = new Skeleton(skeletonData);
        if (!string.IsNullOrEmpty(skinName))
        {
            Skin skin = skeletonData.FindSkin(skinName);
            if (skin == null)
            {
                throw new InvalidOperationException("Skin not found: " + skinName);
            }

            skeleton.SetSkin(skin);
        }

        skeleton.SetSlotsToSetupPose();
        skeleton.UpdateWorldTransform();

        this.sourceMaterial = sourceMaterial;
        instruction = new SkeletonRendererInstruction();
        meshGenerator = new MeshGenerator
        {
            settings =
            {
                useClipping = true,
                immutableTriangles = false,
                pmaVertexColors = false,
                addNormals = false,
                calculateTangents = false
            }
        };
        scratchMesh = new Mesh { name = "Spine VIT Scratch Mesh" };
    }

    public SpineVitSample Sample(Spine.Animation animation, float sampleTime, bool loop)
    {
        skeleton.SetToSetupPose();
        animation.Apply(skeleton, 0f, sampleTime, loop, null, 1f, MixBlend.Setup, MixDirection.In);
        skeleton.UpdateWorldTransform();

        // 使用 spine-unity 官方 MeshGenerator，保证烘培结果和 SkeletonRenderer 的网格生成规则一致。
        MeshGenerator.GenerateSingleSubmeshInstruction(instruction, skeleton, sourceMaterial);
        if (instruction.submeshInstructions.Count != 1)
        {
            throw new InvalidOperationException("Spine VIT v1 only supports a single submesh/material.");
        }

        if (instruction.hasActiveClipping)
        {
            throw new InvalidOperationException("Spine VIT v1 does not support clipping attachments because topology can change per frame.");
        }

        meshGenerator.Begin();
        meshGenerator.BuildMeshWithArrays(instruction, true);
        meshGenerator.FillVertexData(scratchMesh);
        meshGenerator.FillTriangles(scratchMesh);

        Vector3[] vertices = scratchMesh.vertices;
        Vector2[] uvs = scratchMesh.uv;
        Color32[] colors = scratchMesh.colors32;
        int[] triangles = scratchMesh.GetTriangles(0);
        return new SpineVitSample(vertices, uvs, colors, triangles, scratchMesh.bounds);
    }
}

public readonly struct SpineVitSample
{
    public SpineVitSample(Vector3[] vertices, Vector2[] uvs, Color32[] colors, int[] triangles, Bounds bounds)
    {
        Vertices = vertices;
        Uvs = uvs;
        Colors = colors;
        Triangles = triangles;
        Bounds = bounds;
    }

    public Vector3[] Vertices { get; }
    public Vector2[] Uvs { get; }
    public Color32[] Colors { get; }
    public int[] Triangles { get; }
    public Bounds Bounds { get; }
}
#endif
