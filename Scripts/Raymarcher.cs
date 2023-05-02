using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[ExecuteInEditMode]
public class Raymarcher : MonoBehaviour {
    public static Raymarcher Instance;

    [Header("Main")]
    public Material EditorMaterial;

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

    private void OnEnable() {
        Instance = this;
    }

    public void UpdateMaterial(Material material) {
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

    private void Update() {
        if (EditorMaterial != null)
            UpdateMaterial(EditorMaterial);
    }
}