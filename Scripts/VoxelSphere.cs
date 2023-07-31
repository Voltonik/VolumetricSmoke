using System;
using System.Collections.Generic;
using MyBox;
using UnityEngine;

public class VoxelSphere : MonoBehaviour {
    public class Voxel {
        public Vector3 LocalPosition;
        public float VoxelDistance;
        public float Order;

        public Voxel(Vector3 position, float voxelDistance) {
            LocalPosition = position;
            VoxelDistance = voxelDistance;
            Order = voxelDistance;
        }
    }

    [Serializable]
    public struct DebugVoxel {
        public Vector3 LocalPosition;
        public Vector3 Color;

        public DebugVoxel(Vector3 position, Color color = default(Color)) {
            LocalPosition = position;
            Color = new Vector3(color.r, color.g, color.b);
        }

        public static int SIZE = System.Runtime.InteropServices.Marshal.SizeOf(typeof(DebugVoxel));
    }

    public SmokeSettings Settings;

    public float[] VoxelsGrid;

    public ComputeShader SmokeCS;
    public Bounds GlobalBounds;
    public Vector3Int VoxelResolution;

    private ComputeBuffer m_voxelsGridBuffer;

    public RenderTexture depthTex;

    private float normalizedTime;
    private float maxRadius = 0;
    private Vector3 m_center;

    private float m_maxRadius;
    private float m_voxelScale;
    private Mesh m_voxelMesh;
    private AnimationCurve m_growthCurve;
    private float m_growthTime;


    private Material m_debugMaterial;
    private ComputeBuffer m_voxelBuffer;
    private ComputeBuffer m_argsBuffer;

    private List<DebugVoxel> m_debugVoxels = new List<DebugVoxel>();
    private List<Voxel> m_voxelsToTransition = new List<Voxel>();
    private Dictionary<Vector3, Voxel> m_voxelsCache = new Dictionary<Vector3, Voxel>();
    private SortedDictionary<float, List<Voxel>> m_orderedVoxels = new SortedDictionary<float, List<Voxel>>();

    private int prev_i = 0;
    private Bounds renderBounds;

    public void Initialize(SmokeSettings settings, Vector3 center, float radius, Mesh voxelMesh, float voxelScale = 1, float growthSpeed = 0.9f, AnimationCurve growthCurve = null) {
        Settings = settings;
        m_center = center + new Vector3(0, voxelScale / 2, 0);
        m_maxRadius = radius;
        m_voxelMesh = voxelMesh;
        m_voxelScale = voxelScale;
        m_growthTime = growthSpeed;
        m_growthCurve = growthCurve;

        if (growthCurve == null)
            growthCurve = AnimationCurve.Linear(0, 0, 1, 1);
        m_growthCurve.postWrapMode = WrapMode.Clamp;

        transform.position = m_center;

        SmokeCS = (ComputeShader)Instantiate(Resources.Load("VoxelShader"));
        SmokeCS.SetVector("_SmokeOrigin", m_center);

        m_debugMaterial = new Material(Shader.Find("Voxel/VoxelDebugShader"));
        m_debugMaterial.SetFloat("_VoxelScale", m_voxelScale);

        depthTex = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.RHalf, RenderTextureReadWrite.Linear);
        depthTex.enableRandomWrite = true;
        depthTex.Create();

        Raymarcher.Instance.RegisterVoxelSphere(this);
        CalculateSphere();
        UpdateGridSize();
        Reset();
    }

    private void UpdateGridSize() {
        VoxelResolution.x = Mathf.CeilToInt(GlobalBounds.size.x / m_voxelScale) + 1;
        VoxelResolution.y = Mathf.CeilToInt(GlobalBounds.size.y / m_voxelScale) + 1;
        VoxelResolution.z = Mathf.CeilToInt(GlobalBounds.size.z / m_voxelScale) + 1;

        VoxelsGrid = new float[VoxelResolution.x * VoxelResolution.y * VoxelResolution.z];

        m_voxelsGridBuffer?.Release();
        m_voxelsGridBuffer = new ComputeBuffer(VoxelsGrid.Length, sizeof(float));

        m_voxelsGridBuffer.SetData(VoxelsGrid);

        SmokeCS.SetBuffer(0, "voxelGrid", m_voxelsGridBuffer);
        SmokeCS.SetVector("VoxelResolution", VoxelResolution.ToVector3());
        SmokeCS.SetFloat("maxRadius", maxRadius);

        SmokeCS.SetVector("boundsMin", GlobalBounds.min);
        SmokeCS.SetVector("boundsMax", GlobalBounds.max);
    }

    private void UpdateVoxelCell(Voxel voxel) {
        Vector3 voxelPos = (m_center + voxel.LocalPosition) - GlobalBounds.min;

        Vector3Int boxPos = new Vector3Int((int)voxelPos.x, (int)voxelPos.y, (int)voxelPos.z);

        int yOffset = boxPos.y * VoxelResolution.x;
        int zOffset = boxPos.z * VoxelResolution.x * VoxelResolution.y;

        VoxelsGrid[boxPos.x + yOffset + zOffset] = voxel.VoxelDistance;

        m_voxelsGridBuffer.SetData(VoxelsGrid);

        SmokeCS.SetBuffer(0, "voxelGrid", m_voxelsGridBuffer);
    }

    private void AddVoxel(Voxel v) {
        m_voxelsCache.Add(v.LocalPosition, v);

        if (m_orderedVoxels.ContainsKey(v.Order))
            m_orderedVoxels[v.Order].Add(v);
        else
            m_orderedVoxels.Add(v.Order, new List<Voxel>() { v });

        if (GlobalBounds.size == Vector3.zero)
            GlobalBounds = new Bounds(v.LocalPosition + m_center, Vector3.one * m_voxelScale);
        else
            GlobalBounds.Encapsulate(new Bounds(v.LocalPosition + m_center, Vector3.one * m_voxelScale));
    }

    private void CalculateSphere() {
        int r = Mathf.RoundToInt(m_maxRadius / m_voxelScale);
        int deleted = 0;

        List<Voxel> outLayerVoxels = new List<Voxel>();

        GenerateSphere(r, v => {
            if (!Physics.Raycast(m_center + Vector3.up * 0.1f, v.LocalPosition, m_maxRadius)) {
                AddVoxel(v);

                if (v.VoxelDistance > maxRadius) {
                    maxRadius = v.VoxelDistance;
                    outLayerVoxels.Clear();
                    outLayerVoxels.Add(v);
                } else {
                    outLayerVoxels.Add(v);
                }
            } else {
                deleted++;
            }
        });

        outLayerVoxels.Shuffle();

        float order = maxRadius + 1;

        while (deleted > 0) {
            var newLayerVoxels = new List<Voxel>();
            bool foundConnected = false;
            bool end = false;

            for (int i = 0; !end && i < outLayerVoxels.Count; i++) {
                var connectedVoxels = GetConnectedVoxels(outLayerVoxels[i]);

                for (int j = 0; j < 6; j++) {
                    if (!m_voxelsCache.ContainsKey(connectedVoxels[j].LocalPosition)
                    && !Physics.CheckBox(m_center + connectedVoxels[j].LocalPosition + Vector3.up * 0.1f, Vector3.one * (m_voxelScale * 0.5f))) {
                        connectedVoxels[j].VoxelDistance = outLayerVoxels[i].VoxelDistance + 1;
                        connectedVoxels[j].Order = order;
                        AddVoxel(connectedVoxels[j]);
                        newLayerVoxels.Add(connectedVoxels[j]);
                        deleted--;
                        order++;

                        if (connectedVoxels[j].VoxelDistance > maxRadius)
                            maxRadius = connectedVoxels[j].VoxelDistance;

                        foundConnected = true;

                        if (deleted == 0) {
                            end = true;
                            break;
                        }
                    }
                }
            }

            if (!foundConnected || end)
                break;

            outLayerVoxels = newLayerVoxels;
        }
    }

    public static Voxel[] GetConnectedVoxels(Voxel v) {
        return new[] {
                new Voxel(v.LocalPosition + Vector3.up, ((v.LocalPosition + Vector3.up).sqrMagnitude)),
                new Voxel(v.LocalPosition + Vector3.down, ((v.LocalPosition + Vector3.down).sqrMagnitude)),
                new Voxel(v.LocalPosition + Vector3.right, ((v.LocalPosition + Vector3.right).sqrMagnitude)),
                new Voxel(v.LocalPosition + Vector3.left, ((v.LocalPosition + Vector3.left).sqrMagnitude)),
                new Voxel(v.LocalPosition + Vector3.forward, ((v.LocalPosition + Vector3.forward).sqrMagnitude)),
                new Voxel(v.LocalPosition + Vector3.back, ((v.LocalPosition + Vector3.back).sqrMagnitude)),
            };
    }

    private static void GenerateSphere(int r, Action<Voxel> sphereVoxel) {
        for (int tx = 0; tx < r; tx++) {
            for (int ty = 0; ty < r; ty++) {
                for (int tz = 0; tz < r; tz++) {
                    float sqrMag = Mathf.Sqrt(tx * tx + ty * ty + tz * tz);

                    if (sqrMag < r) {
                        sphereVoxel.Invoke(new Voxel(new Vector3(tx, ty, tz), sqrMag));

                        if (tx != 0) {
                            sphereVoxel.Invoke(new Voxel(new Vector3(-tx, ty, tz), sqrMag));

                            if (tz != 0) {
                                sphereVoxel.Invoke(new Voxel(new Vector3(-tx, ty, -tz), sqrMag));
                            }
                        }

                        if (tz != 0) {
                            sphereVoxel.Invoke(new Voxel(new Vector3(tx, ty, -tz), sqrMag));
                        }

                        if (ty != 0) {
                            sphereVoxel.Invoke(new Voxel(new Vector3(tx, -ty, tz), sqrMag));

                            if (tx != 0) {
                                sphereVoxel.Invoke(new Voxel(new Vector3(-tx, -ty, tz), sqrMag));

                                if (tz != 0) {
                                    sphereVoxel.Invoke(new Voxel(new Vector3(-tx, -ty, -tz), sqrMag));
                                }
                            }

                            if (tz != 0) {
                                sphereVoxel.Invoke(new Voxel(new Vector3(tx, -ty, -tz), sqrMag));
                            }
                        }
                    }
                }
            }
        }
    }

    public void Reset() {
        m_argsBuffer?.Release();
        m_argsBuffer = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);

        foreach (var v in m_orderedVoxels.Values) {
            for (int i = 0; i < v.Count; i++) {
                m_voxelsToTransition.Add(v[i]);
                UpdateVoxelCell(v[i]);
            }
        }

        renderBounds = new Bounds(m_center, new Vector3(m_maxRadius, m_maxRadius, m_maxRadius));
    }

    private void Update() {
        normalizedTime += Time.deltaTime / m_growthTime;
        SmokeCS.SetFloat("normalizedTime", m_growthCurve.Evaluate(normalizedTime));

        if (Raymarcher.Instance.DebugView) {
            int starting_i = prev_i + 1;
            int end_i = Mathf.Clamp(Mathf.RoundToInt(m_voxelsToTransition.Count * normalizedTime), 0, m_voxelsToTransition.Count);

            for (int i = starting_i; i < end_i; i++) {
                prev_i = i;

                var voxelData = new DebugVoxel(m_voxelsToTransition[i].LocalPosition, UnityEngine.Random.ColorHSV());

                m_debugVoxels.Add(voxelData);

                m_voxelBuffer?.Release();
                m_voxelBuffer = new ComputeBuffer(m_debugVoxels.Count, DebugVoxel.SIZE);

                m_voxelBuffer.SetData(m_debugVoxels);
                m_debugMaterial.SetBuffer("voxelBuffer", m_voxelBuffer);
                m_debugMaterial.SetInt("voxelsCount", m_debugVoxels.Count);

                uint[] args = new uint[5] {
                    (uint)m_voxelMesh.GetIndexCount(0),
                    (uint)m_debugVoxels.Count,
                    (uint)m_voxelMesh.GetIndexStart(0),
                    (uint)m_voxelMesh.GetBaseVertex(0),
                    0
                };

                m_argsBuffer.SetData(args);
            }

            Graphics.DrawMeshInstancedIndirect(m_voxelMesh, 0, m_debugMaterial, renderBounds, m_argsBuffer);
        }
    }

    public void Render(RenderTexture rt, RenderTexture depth, int ind) {
        SmokeCS.SetVector("_CameraWorldPos", Camera.main.transform.position);
        SmokeCS.SetMatrix("_CameraToWorld", Camera.main.cameraToWorldMatrix);
        SmokeCS.SetVector("_BufferSize", new Vector2(rt.width, rt.height));

        SmokeCS.SetVector("_WorldSpaceLightDir", -FindObjectOfType<Light>().transform.forward);

        SmokeCS.SetInt("SmokeArrayIndex", ind);
        SmokeCS.SetTexture(0, "SmokesArrayResult", rt);
        SmokeCS.SetTexture(0, "SmokeDepthResult", depth);
        SmokeCS.SetTextureFromGlobal(0, "_DepthTex", "_CameraDepthTexture");

        SmokeCS.SetFloat("scale", Settings.cloudScale);
        SmokeCS.SetFloat("noiseAmplitude", Settings.noiseAmplitude);
        SmokeCS.SetFloat("densityMultiplier", Settings.densityMultiplier);
        SmokeCS.SetFloat("densityOffset", Settings.densityOffset);
        SmokeCS.SetFloat("_DensityFalloff", Settings.densityFalloff);

        SmokeCS.SetInt("marchSteps", Settings.marchSteps);
        SmokeCS.SetInt("lightmarchSteps", Settings.lightmarchSteps);
        SmokeCS.SetFloat("stepSize", Settings.stepSize);
        SmokeCS.SetFloat("lightStepSize", Settings.lightStepSize);

        SmokeCS.SetVector("scatterColor", Settings.scatterColor);
        SmokeCS.SetFloat("brightness", Settings.brightness);
        SmokeCS.SetFloat("transmitThreshold", Settings.transmitThreshold);
        SmokeCS.SetFloat("inScatterMultiplier", Settings.inScatterMultiplier);
        SmokeCS.SetFloat("outScatterMultiplier", Settings.outScatterMultiplier);
        SmokeCS.SetFloat("forwardScatter", Settings.forwardScattering);
        SmokeCS.SetFloat("backwardScatter", Settings.backwardScattering);
        SmokeCS.SetFloat("scatterMultiplier", Settings.scatterMultiplier);

        SmokeCS.SetVector("cloudSpeed", Settings.cloudSpeed);

        SmokeCS.Dispatch(0, Mathf.CeilToInt(rt.width / 8.0f), Mathf.CeilToInt(rt.height / 8.0f), 1);
    }

    private void OnDestroy() {
        Raymarcher.Instance.UnregisterVoxelSphere(this);
    }
}