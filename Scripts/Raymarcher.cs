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
    public int LatestSphereID;

    private ComputeBuffer m_voxelsBuffer;

    public Texture3D VoxelsGrid;

    public Vector3 offset;

    private void OnEnable() {
        Instance = this;
    }

    private void OnDrawGizmos() {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(GlobalBounds.center, GlobalBounds.size);
    }

    public void UpdateGridSize() {
        VoxelsGrid = new Texture3D(Mathf.CeilToInt(GlobalBounds.size.x),
            Mathf.CeilToInt(GlobalBounds.size.y),
            Mathf.CeilToInt(GlobalBounds.size.z),
            TextureFormat.RGHalf, 0);

        VoxelsGrid.wrapMode = TextureWrapMode.Clamp;

        foreach (var voxel in GlobalVoxels)
            UpdateVoxelGrid(voxel, -1, 1, false);

        VoxelsGrid.Apply();

        Material.SetTexture("voxelGrid", VoxelsGrid);

        Material.SetVector("boundsMin", GlobalBounds.min);
        Material.SetVector("boundsMax", GlobalBounds.max);
        Material.SetVector("boundsExtent", GlobalBounds.size);
    }

    public void UpdateVoxelGrid(VoxelSphere.VoxelData voxel, float furthestVoxel, float normalizedTime, bool apply = true) {
        // TODO: improve
        Vector3 voxelPos = voxel.Center + voxel.LocalPosition;
        Vector3Int intVoxelPos = new Vector3Int((int)voxelPos.x, (int)voxelPos.y, (int)voxelPos.z);

        Vector3Int boxPos = new Vector3Int((int)(GlobalBounds.min.x - offset.x), (int)(GlobalBounds.min.y - offset.y), (int)(GlobalBounds.min.z - offset.z)) - intVoxelPos;

        float r = furthestVoxel == 0 ? 1 : ((furthestVoxel + radius) / (voxel.LocalPosition).sqrMagnitude);
        if (furthestVoxel == -1)
            r = 1;
        VoxelsGrid.SetPixel(Mathf.Abs(boxPos.x), Mathf.Abs(boxPos.y), Mathf.Abs(boxPos.z), new Color(1, furthestVoxel, normalizedTime), 0);

        _SmokeOrigin = voxel.Center;

        if (apply) {
            VoxelsGrid.Apply();

            Material.SetTexture("voxelGrid", VoxelsGrid);
            Material.SetVector("_SmokeOrigin", voxel.Center);
            Material.SetFloat("normalizedTime", normalizedTime);
            Material.SetFloat("maxRadius", Mathf.Sqrt(furthestVoxel));
        }
    }

    public Vector3 _SmokeOrigin;

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
        // UpdateGridSize();

        RenderTexture buffer = RenderTexture.GetTemporary(src.width / 4, src.height / 4, 0);

        DownSample4x(src, buffer);

        Material.SetVector("_SmokeOrigin", _SmokeOrigin);

        Material.SetFloat("_DensityFalloff", densityFalloff);
        Material.SetFloat("_Radius", radius);

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