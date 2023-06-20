using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode, ImageEffectAllowedInSceneView]
public class Raymarcher : MonoBehaviour {
    public static Raymarcher Instance;

    public Material Material;
    public Bounds GlobalBounds;

    public bool Debug;

    [Header("Main")]
    public float radius = 10;
    public float cloudScale = 1;
    public float densityMultiplier = 2.8f;
    public float densityOffset = 13.23f;
    public float densityFalloff = 1;
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

    public List<VoxelSphere.VoxelData> GlobalVoxels = new List<VoxelSphere.VoxelData>();
    public List<VoxelSphere.VoxelData> RealtimeVoxels = new List<VoxelSphere.VoxelData>();
    public int LatestSphereID;

    private ComputeBuffer m_voxelsBuffer;

    public Texture3D VoxelsGrid;

    private void OnEnable() {
        Instance = this;
    }

    private void OnDrawGizmos() {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(GlobalBounds.center, GlobalBounds.size);
    }
    public Vector3 offset;
    public void UpdateVoxelGrid() {
        // TODO: improve

        VoxelsGrid = new Texture3D(Mathf.CeilToInt(GlobalBounds.size.x),
            Mathf.CeilToInt(GlobalBounds.size.y),
            Mathf.CeilToInt(GlobalBounds.size.z),
            TextureFormat.RGHalf, 0);

        VoxelsGrid.wrapMode = TextureWrapMode.Clamp;

        foreach (var voxel in GlobalVoxels) {
            Vector3 voxelPos = voxel.Center + voxel.LocalPosition;
            Vector3 intVoxelPos = new Vector3((int)voxelPos.x, (int)voxelPos.y, (int)voxelPos.z);

            Vector3 boxPos = new Vector3((int)GlobalBounds.min.x - offset.x, (int)GlobalBounds.min.y - offset.y, (int)GlobalBounds.min.z - offset.z) - intVoxelPos;

            VoxelsGrid.SetPixel((int)Mathf.Abs(boxPos.x), (int)Mathf.Abs(boxPos.y), (int)Mathf.Abs(boxPos.z), Color.red, 0);

            _SmokeOrigin = voxel.Center;
        }

        VoxelsGrid.Apply();
    }

    public Vector3 _SmokeOrigin;

    private void OnRenderImage(RenderTexture src, RenderTexture dest) {
        if (Debug || Material == null || RealtimeVoxels.Count == 0) {
            Graphics.Blit(src, dest);

            return;
        }

        UpdateVoxelGrid();

        Material.SetVector("boundsMin", GlobalBounds.min);
        Material.SetVector("boundsMax", GlobalBounds.max);
        Material.SetVector("boundsExtent", GlobalBounds.size);
        Material.SetVector("boundsCenter", GlobalBounds.center);

        Material.SetVector("_SmokeOrigin", _SmokeOrigin);

        Material.SetFloat("_DensityFalloff", densityFalloff);
        Material.SetFloat("_Radius", radius);

        Material.SetTexture("voxelGrid", VoxelsGrid);

        // m_voxelsBuffer = new ComputeBuffer(RealtimeVoxels.Count, VoxelSphere.VoxelData.SIZE);
        // m_voxelsBuffer.SetData(RealtimeVoxels);

        // Material.SetBuffer("voxelBuffer", m_voxelsBuffer);
        // Material.SetInt("voxelsCount", RealtimeVoxels.Count);

        // m_voxelsBuffer.Release();

        Material.SetFloat("scale", cloudScale);
        Material.SetFloat("densityMultiplier", densityMultiplier);
        Material.SetFloat("densityOffset", densityOffset);

        Material.SetInt("marchSteps", marchSteps);
        Material.SetInt("lightmarchSteps", lightmarchSteps);
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