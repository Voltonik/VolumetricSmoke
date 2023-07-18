using MyBox;
using UnityEngine;

[CreateAssetMenu(menuName = "Voxel/Smoke Settings", fileName = "NewSmokeSettings")]
public class SmokeSettings : ScriptableObject {
    public enum VoxelFilteringMethod {
        None,
        Acerola,
        i1,
        i2,
        Dfm
    }

    public float cloudScale = 1;
    public float densityMultiplier = 2.8f;
    public float densityOffset = 13.23f;
    public float densityFalloff = 1;
    public int marchSteps = 100;
    public int lightmarchSteps = 4;
    public VoxelFilteringMethod voxelFilteringMethod;
    [ConditionalField(nameof(voxelFilteringMethod), false, VoxelFilteringMethod.i1, VoxelFilteringMethod.i2)] public float voxelSmoothFactor1 = 0.625f;
    [ConditionalField(nameof(voxelFilteringMethod), false, VoxelFilteringMethod.i2)] public float voxelSmoothFactor2 = 0.625f;
    [Range(0.01f, 0.1f)] public float stepSize = 0.1f;
    [Range(0.01f, 1.0f)] public float lightStepSize = 1f;
    [ColorUsage(true, true)] public Color scatterColor;
    public float brightness = 0.426f;
    public float transmitThreshold = 0.535f;
    public float inScatterMultiplier = 0.611f;
    public float outScatterMultiplier = 0.447f;
    public float forwardScattering = 0.686f;
    public float backwardScattering = 0.564f;
    public float scatterMultiplier = 1;
    public Vector3 cloudSpeed = new Vector3(0.2f, 0f, 0.1f);
}