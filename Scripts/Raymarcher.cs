using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode, ImageEffectAllowedInSceneView]
public class Raymarcher : MonoBehaviour {
    public static Raymarcher Instance;

    public bool DebugView;
    [Min(1)] public float ResolutionDivider = 4;

    private Dictionary<VoxelSphere, RenderTexture> m_smokeInstances = new Dictionary<VoxelSphere, RenderTexture>();
    private List<VoxelSphere> m_spheres = new List<VoxelSphere>();

    public RenderTexture smokesArray;
    public RenderTexture depthArray;

    private Material m_compositeMaterial;


    private void Awake() {
        Instance = this;

        if (m_compositeMaterial == null)
            m_compositeMaterial = new Material(Shader.Find("Voxel/Compositing"));
    }

    public void RegisterVoxelSphere(VoxelSphere sphere) {
        Vector2Int res = new Vector2Int(Mathf.CeilToInt(Screen.width / ResolutionDivider), Mathf.CeilToInt(Screen.height / ResolutionDivider));

        var rt = new RenderTexture(res.x, res.y, 0, RenderTextureFormat.ARGB64, RenderTextureReadWrite.Linear);
        rt.enableRandomWrite = true;
        rt.Create();

        m_smokeInstances.Add(sphere, rt);
        m_spheres.Add(sphere);

        depthArray = new RenderTexture(res.x, res.y, 0, RenderTextureFormat.RHalf, RenderTextureReadWrite.Linear);
        depthArray.dimension = UnityEngine.Rendering.TextureDimension.Tex2DArray;
        depthArray.volumeDepth = m_smokeInstances.Count;
        depthArray.enableRandomWrite = true;
        depthArray.Create();

        smokesArray = new RenderTexture(res.x, res.y, 0, RenderTextureFormat.ARGB64, RenderTextureReadWrite.Linear);
        smokesArray.dimension = UnityEngine.Rendering.TextureDimension.Tex2DArray;
        smokesArray.volumeDepth = m_smokeInstances.Count;
        smokesArray.enableRandomWrite = true;
        smokesArray.Create();

        m_compositeMaterial.SetTexture("_SmokeTexArray", smokesArray);
        m_compositeMaterial.SetInt("_SmokesCount", m_smokeInstances.Count);
    }

    public void UnregisterVoxelSphere(VoxelSphere sphere) {
        m_smokeInstances[sphere].Release();

        m_smokeInstances.Remove(sphere);
        m_spheres.Remove(sphere);
    }

    private void OnRenderImage(RenderTexture src, RenderTexture dest) {
        if (m_smokeInstances.Count == 0 || DebugView || m_compositeMaterial == null) {
            Graphics.Blit(src, dest);
            return;
        }

        for (int i = 0; i < m_spheres.Count; i++) {
            m_spheres[i].Render(smokesArray, depthArray, src, i);
        }

        m_compositeMaterial.SetTexture("_DepthTexArray", depthArray);
        Graphics.Blit(src, dest, m_compositeMaterial);
    }
}