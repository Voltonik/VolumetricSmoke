using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode, ImageEffectAllowedInSceneView]
public class Raymarcher : MonoBehaviour {
    public static Raymarcher Instance;

    [Header("Editor preview")]
    public Material editorMaterial;
    public Bounds editorBounds;

    [Header("Main")]
    public float cloudScale = 1;
    public float densityMultiplier = 2.8f;
    public float densityOffset = 13.23f;
    public int marchSteps = 8;
    public int lightmarchSteps = 8;
    public float rayOffset = 50;
    public Texture2D blueNoise;
    [ColorUsage(true, true)] public Color scatterColor;
    public float brightness = 0.426f;
    public float transmitThreshold = 0.535f;
    public float inScatterMultiplier = 0.611f;
    public float outScatterMultiplier = 0.447f;
    public float forwardScattering = 0.686f;
    public float backwardScattering = 0.564f;
    public float scatterMultiplier = 1;
    public Vector3 cloudSpeed = new Vector3(0.2f, 0f, 0.1f);

    private List<VoxelSphere> voxelSpheres = new List<VoxelSphere>();
    private bool isEven;

    private void OnEnable() {
        Instance = this;
    }

    private void OnDisable() {
        voxelSpheres.Clear();
    }

    private void OnDrawGizmos() {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(editorBounds.center, editorBounds.size);
    }

    public void AddVoxelSphere(VoxelSphere voxelSphere) {
        voxelSpheres.Add(voxelSphere);
        isEven = voxelSpheres.Count % 2 == 0;
    }

    public void RemoveVoxelSphere(VoxelSphere voxelSphere) {
        voxelSpheres.Remove(voxelSphere);
        isEven = voxelSpheres.Count % 2 == 0;
    }

    private void SetSphereProps(Material material, Bounds bounds) {
        material.SetVector("boundsMin", bounds.min - Vector3.one * 0.2f);
        material.SetVector("boundsMax", bounds.max + Vector3.one * 0.2f);

        material.SetFloat("scale", cloudScale);
        material.SetFloat("densityMultiplier", densityMultiplier);
        material.SetFloat("densityOffset", densityOffset);

        material.SetInt("marchSteps", marchSteps);
        material.SetInt("lightmarchSteps", lightmarchSteps);
        material.SetFloat("rayOffset", rayOffset);
        material.SetTexture("BlueNoise", blueNoise);

        material.SetVector("scatterColor", scatterColor);
        material.SetFloat("brightness", brightness);
        material.SetFloat("transmitThreshold", transmitThreshold);
        material.SetFloat("inScatterMultiplier", inScatterMultiplier);
        material.SetFloat("outScatterMultiplier", outScatterMultiplier);
        material.SetFloat("forwardScatter", forwardScattering);
        material.SetFloat("backwardScatter", backwardScattering);
        material.SetFloat("scatterMultiplier", scatterMultiplier);

        material.SetVector("cloudSpeed", cloudSpeed);
    }

    private void OnRenderImage(RenderTexture src, RenderTexture dest) {
        if (!Application.isPlaying) {
            if (editorMaterial == null) {
                Graphics.Blit(src, dest);

                return;
            }

            SetSphereProps(editorMaterial, editorBounds);

            Graphics.Blit(src, dest, editorMaterial);

            return;
        }

        RenderTexture tempSrc = RenderTexture.GetTemporary(src.width, src.height, src.depth, src.format);
        RenderTexture tempDst = RenderTexture.GetTemporary(src.width, src.height, src.depth, src.format);

        Graphics.Blit(src, tempSrc);

        for (int i = 0; i < voxelSpheres.Count; i++) {
            var sphere = voxelSpheres[i];
            var mat = sphere.Material;

            mat.SetBuffer("voxelBuffer", sphere.Voxels);
            mat.SetInteger("voxelsCount", sphere.Voxels.count);

            SetSphereProps(mat, sphere.Bounds);

            if (i % 2 == 0)
                Graphics.Blit(tempSrc, tempDst, mat);
            else
                Graphics.Blit(tempDst, tempSrc, mat);
        }

        Graphics.Blit(isEven ? tempSrc : tempDst, dest);

        RenderTexture.ReleaseTemporary(tempSrc);
        RenderTexture.ReleaseTemporary(tempDst);
    }
}