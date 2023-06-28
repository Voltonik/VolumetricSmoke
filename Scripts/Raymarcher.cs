using System.Collections.Generic;
using MyBox;
using UnityEngine;

[ExecuteInEditMode, ImageEffectAllowedInSceneView]
public class Raymarcher : MonoBehaviour {
    public static Raymarcher Instance;

    public Material Material;
    public Bounds GlobalBounds;
    public Vector3Int VoxelResolution;

    public bool Debug;

    [Header("Main")]
    public float radius = 10;
    public float cloudScale = 1;
    public float densityMultiplier = 2.8f;
    public float densityOffset = 13.23f;
    public float densityFalloff = 1;
    public int marchSteps = 8;
    public int lightmarchSteps = 8;
    [Range(0.01f, 0.1f)] public float stepSize = 0.05f;
    [Range(0.01f, 1.0f)] public float lightStepSize = 0.25f;
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

    public List<VoxelSphere.VoxelData> GlobalVoxels = new List<VoxelSphere.VoxelData>();
    public int LatestSphereID;

    private ComputeBuffer m_voxelsGridBuffer;


    public Vector3 offset;

    [System.Serializable]
    public struct VoxelCell {
        public int Occupied;

        public static int SIZE = System.Runtime.InteropServices.Marshal.SizeOf(typeof(VoxelCell));
    }

    public VoxelCell[] VoxelsGrid;

    private void OnEnable() {
        Instance = this;
    }

    private void OnDrawGizmos() {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(GlobalBounds.center, GlobalBounds.size);
    }

    public void UpdateGridSize() {
        VoxelResolution.x = Mathf.CeilToInt(GlobalBounds.size.x);
        VoxelResolution.y = Mathf.CeilToInt(GlobalBounds.size.y);
        VoxelResolution.z = Mathf.CeilToInt(GlobalBounds.size.z);

        VoxelsGrid = new VoxelCell[VoxelResolution.x * VoxelResolution.y * VoxelResolution.z];

        m_voxelsGridBuffer?.Release();

        m_voxelsGridBuffer = new ComputeBuffer(VoxelsGrid.Length, VoxelCell.SIZE);

        foreach (var voxel in GlobalVoxels)
            UpdateVoxelGrid(voxel, -1, 1, false);

        m_voxelsGridBuffer.SetData(VoxelsGrid);

        Material.SetBuffer("voxelGrid", m_voxelsGridBuffer);
        Material.SetVector("VoxelResolution", VoxelResolution.ToVector3());

        Material.SetVector("boundsMin", GlobalBounds.min);
        Material.SetVector("boundsMax", GlobalBounds.max);
        Material.SetVector("boundsExtent", GlobalBounds.size);
    }

    public void UpdateVoxelGrid(VoxelSphere.VoxelData voxel, float furthestVoxel, float normalizedTime, bool apply = true) {
        // TODO: improve
        Vector3 voxelPos = (voxel.Center + voxel.LocalPosition) - GlobalBounds.min;

        Vector3Int boxPos = new Vector3Int((int)voxelPos.x - 1, (int)voxelPos.y, (int)voxelPos.z);

        int yOffset = boxPos.y * VoxelResolution.x;
        int zOffset = boxPos.z * VoxelResolution.x * VoxelResolution.y;

        VoxelsGrid[boxPos.x + yOffset + zOffset].Occupied = 1;

        if (apply) {
            m_voxelsGridBuffer.SetData(VoxelsGrid);

            Material.SetBuffer("voxelGrid", m_voxelsGridBuffer);
            Material.SetVector("_SmokeOrigin", voxel.Center);
            Material.SetFloat("normalizedTime", normalizedTime);
            Material.SetFloat("maxRadius", Mathf.Sqrt(furthestVoxel));
        }
    }

    // Downsamples the texture to a quarter resolution.
    private void DownSample4x(RenderTexture source, RenderTexture dest) {
        float off = 1.0f;
        Graphics.BlitMultiTap(source, dest, Material,
            new Vector2(-off, -off),
            new Vector2(-off, off),
            new Vector2(off, off),
            new Vector2(off, -off)
        );
    }

    private void OnRenderImage(RenderTexture src, RenderTexture dest) {
        if (Debug || Material == null || GlobalBounds.size.x == 0 || GlobalBounds.size.y == 0 || GlobalBounds.size.z == 0) {
            Graphics.Blit(src, dest);

            return;
        }
        UpdateGridSize();

        RenderTexture buffer = RenderTexture.GetTemporary(src.width / 4, src.height / 4, 0);

        DownSample4x(src, buffer);

        Material.SetFloat("_DensityFalloff", densityFalloff);
        Material.SetFloat("_Radius", radius);

        Material.SetFloat("scale", cloudScale);
        Material.SetFloat("densityMultiplier", densityMultiplier);
        Material.SetFloat("densityOffset", densityOffset);

        Material.SetInt("marchSteps", marchSteps);
        Material.SetInt("lightmarchSteps", lightmarchSteps);
        Material.SetFloat("stepSize", stepSize);
        Material.SetFloat("lightStepSize", lightStepSize);
        Material.SetFloat("rayOffset", rayOffset);
        Material.SetTexture("BlueNoise", blueNoise);

        Material.SetVector("scatterColor", scatterColor);
        Material.SetFloat("brightness", brightness);
        Material.SetFloat("transmitThreshold", transmitThreshold);
        Material.SetFloat("inScatterMultiplier", inScatterMultiplier);
        Material.SetFloat("outScatterMultiplier", outScatterMultiplier);
        Material.SetFloat("forwardScatter", forwardScattering);
        Material.SetFloat("backwardScatter", backwardScattering);
        Material.SetFloat("scatterMultiplier", scatterMultiplier);

        Material.SetVector("cloudSpeed", cloudSpeed);

        Graphics.Blit(src, dest, Material);
    }
}