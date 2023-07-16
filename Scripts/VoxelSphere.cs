using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyBox;
using UnityEngine;

public class VoxelSphere : MonoBehaviour {
    public class Voxel {
        public Vector3 LocalPosition;
        public int Order;

        public Voxel(Vector3 position, int order) {
            LocalPosition = position;
            Order = order;
        }
    }

    [Serializable]
    public struct DebugVoxel {
        public Vector3 LocalPosition;
        public Vector3 Center;
        public float FurthestVoxel;
        public int SphereID;
        public Vector3 Color;

        public DebugVoxel(Vector3 position, Vector3 center, float furthestVoxel, int sphereID, Color color = default(Color)) {
            LocalPosition = position;
            SphereID = sphereID;
            Color = new Vector3(color.r, color.g, color.b);
            Center = center;
            FurthestVoxel = furthestVoxel;
        }

        public static int SIZE = System.Runtime.InteropServices.Marshal.SizeOf(typeof(DebugVoxel));
    }

    [Serializable]
    public struct VoxelCell {
        public int Occupied;

        public static int SIZE = System.Runtime.InteropServices.Marshal.SizeOf(typeof(VoxelCell));
    }


    public SmokeSettings Settings;

    public VoxelCell[] VoxelsGrid;

    public ComputeShader Material;
    public Bounds GlobalBounds;
    public Vector3Int VoxelResolution;

    private ComputeBuffer m_voxelsGridBuffer;

    public RenderTexture depthTex;


    private Vector3 m_center;
    private float m_maxRadius;
    private float m_voxelScale;
    private Mesh m_voxelMesh;
    private AnimationCurve m_growthCurve;
    private float m_growthTime;


    private Material m_material;
    private ComputeBuffer m_voxelBuffer;
    private ComputeBuffer m_argsBuffer;

    private List<DebugVoxel> m_debugVoxels = new List<DebugVoxel>();
    private List<Voxel> m_voxelsToTransition = new List<Voxel>();
    private Dictionary<Vector3, Voxel> m_voxelsCache = new Dictionary<Vector3, Voxel>();

    private bool m_running;

    public void Initalize(SmokeSettings settings, Vector3 center, float radius, Mesh voxelMesh, float voxelScale = 1, float growthSpeed = 0.9f, AnimationCurve growthCurve = null) {
        Settings = settings;
        m_center = center + new Vector3(0, voxelScale / 2, 0);
        m_maxRadius = radius;
        m_voxelMesh = voxelMesh;
        m_voxelScale = voxelScale;
        m_growthTime = growthSpeed;
        m_growthCurve = growthCurve;

        if (growthCurve == null)
            growthCurve = AnimationCurve.Linear(0, 0, 1, 1);

        Material = (ComputeShader)Instantiate(Resources.Load("VoxelShader"));

        m_material = new Material(Shader.Find("Voxel/VoxelDebugShader"));
        m_material.SetFloat("_VoxelScale", m_voxelScale);

        depthTex = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.RHalf, RenderTextureReadWrite.Linear);
        depthTex.enableRandomWrite = true;
        depthTex.Create();

        Raymarcher.Instance.RegisterVoxelSphere(this);

        CalculateSphere();

        UpdateGridSize();

        Explode();
    }

    private void UpdateGridSize() {
        VoxelResolution.x = Mathf.CeilToInt(GlobalBounds.size.x);
        VoxelResolution.y = Mathf.CeilToInt(GlobalBounds.size.y);
        VoxelResolution.z = Mathf.CeilToInt(GlobalBounds.size.z);

        VoxelsGrid = new VoxelCell[VoxelResolution.x * VoxelResolution.y * VoxelResolution.z];

        m_voxelsGridBuffer?.Release();
        m_voxelsGridBuffer = new ComputeBuffer(VoxelsGrid.Length, VoxelCell.SIZE);

        m_voxelsGridBuffer.SetData(VoxelsGrid);

        Material.SetBuffer(0, "voxelGrid", m_voxelsGridBuffer);
        Material.SetVector("VoxelResolution", VoxelResolution.ToVector3());

        Material.SetVector("boundsMin", GlobalBounds.min);
        Material.SetVector("boundsMax", GlobalBounds.max);
        Material.SetVector("boundsSize", GlobalBounds.size);
    }

    private void UpdateVoxelGrid(DebugVoxel voxel, float furthestVoxel, float normalizedTime, bool apply = true) {
        // TODO: improve
        Vector3 voxelPos = (voxel.Center + voxel.LocalPosition) - GlobalBounds.min;

        Vector3Int boxPos = new Vector3Int((int)voxelPos.x, (int)voxelPos.y, (int)voxelPos.z);

        int yOffset = boxPos.y * VoxelResolution.x;
        int zOffset = boxPos.z * VoxelResolution.x * VoxelResolution.y;

        VoxelsGrid[boxPos.x + yOffset + zOffset].Occupied = 1;

        if (apply) {
            m_voxelsGridBuffer.SetData(VoxelsGrid);

            Material.SetBuffer(0, "voxelGrid", m_voxelsGridBuffer);
            Material.SetVector("_SmokeOrigin", voxel.Center);
            Material.SetFloat("maxRadius", Mathf.Sqrt(furthestVoxel));
            Material.SetFloat("normalizedTime", normalizedTime);
        }
    }

    private void AddVoxel(Voxel v) {
        m_voxelsCache.Add(v.LocalPosition, v);

        if (GlobalBounds.size == Vector3.zero)
            GlobalBounds = new Bounds(v.LocalPosition + m_center, Vector3.one * m_voxelScale);
        else
            GlobalBounds.Encapsulate(new Bounds(v.LocalPosition + m_center, Vector3.one * m_voxelScale));
    }

    int maxRadius = 0;

    private void CalculateSphere() {
        int r = Mathf.RoundToInt(m_maxRadius / m_voxelScale);
        int deleted = 0;

        List<Voxel> outLayerVoxels = new List<Voxel>();

        GenerateSphere(r, v => {
            if (!Physics.Raycast(m_center + Vector3.up * 0.1f, v.LocalPosition, m_maxRadius)) {
                AddVoxel(v);

                if (v.Order > maxRadius) {
                    maxRadius = v.Order;
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

        int nextIndex = maxRadius + 1;

        while (deleted > 0) {
            var newLayerVoxels = new List<Voxel>();
            bool foundConnected = false;
            bool end = false;

            for (int i = 0; !end && i < outLayerVoxels.Count; i++) {
                var connectedVoxels = GetConnectedVoxels(outLayerVoxels[i]);

                for (int j = 0; j < 6; j++) {
                    if (!m_voxelsCache.ContainsKey(connectedVoxels[j].LocalPosition)
                    && !Physics.CheckBox(m_center + connectedVoxels[j].LocalPosition + Vector3.up * 0.1f, Vector3.one * (m_voxelScale * 0.5f))) {
                        connectedVoxels[j].Order = nextIndex;
                        AddVoxel(connectedVoxels[j]);
                        newLayerVoxels.Add(connectedVoxels[j]);
                        deleted--;
                        nextIndex++;

                        if (connectedVoxels[j].Order > maxRadius)
                            maxRadius = connectedVoxels[j].Order;

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
                new Voxel(v.LocalPosition + Vector3.up, (int)((v.LocalPosition + Vector3.up).sqrMagnitude)),
                new Voxel(v.LocalPosition + Vector3.down, (int)((v.LocalPosition + Vector3.down).sqrMagnitude)),
                new Voxel(v.LocalPosition + Vector3.right, (int)((v.LocalPosition + Vector3.right).sqrMagnitude)),
                new Voxel(v.LocalPosition + Vector3.left, (int)((v.LocalPosition + Vector3.left).sqrMagnitude)),
                new Voxel(v.LocalPosition + Vector3.forward, (int)((v.LocalPosition + Vector3.forward).sqrMagnitude)),
                new Voxel(v.LocalPosition + Vector3.back, (int)((v.LocalPosition + Vector3.back).sqrMagnitude)),
            };
    }

    public static bool IsVoxelConnected(Dictionary<Vector3, Voxel> voxels, Voxel voxel) {
        return voxels.ContainsKey(voxel.LocalPosition + Vector3.up)
                    || voxels.ContainsKey(voxel.LocalPosition + Vector3.down)
                    || voxels.ContainsKey(voxel.LocalPosition + Vector3.right)
                    || voxels.ContainsKey(voxel.LocalPosition + Vector3.left)
                    || voxels.ContainsKey(voxel.LocalPosition + Vector3.forward)
                    || voxels.ContainsKey(voxel.LocalPosition + Vector3.back);
    }

    private static void GenerateSphere(int r, Action<Voxel> sphereVoxel) {
        for (int tx = 0; tx < r; tx++) {
            for (int ty = 0; ty < r; ty++) {
                for (int tz = 0; tz < r; tz++) {
                    int sqrMag = tx * tx + ty * ty + tz * tz;

                    if (sqrMag < r * r) {
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


        m_voxelsToTransition = m_voxelsCache.Values.OrderBy(v => v.Order).ToList();

        m_running = true;
    }

    public async void Explode() {
        Reset();

        float t = 0;
        int prev_i = 0;
        bool animating = true;

        Bounds renderBounds = new Bounds(m_center, new Vector3(m_maxRadius, m_maxRadius, m_maxRadius));
        Bounds bounds = new Bounds(m_center, Vector3.zero);

        var centerVoxel = new DebugVoxel(Vector3.zero, m_center, maxRadius, 0, UnityEngine.Random.ColorHSV());

        UpdateVoxelGrid(centerVoxel, maxRadius, 0);

        while (Application.isPlaying && m_running) {
            if (animating && t <= m_growthTime) {
                float normalizedTime = t / m_growthTime;

                int starting_i = prev_i + 1;
                int end_i = Mathf.RoundToInt(m_voxelsToTransition.Count * normalizedTime);

                for (int i = starting_i; i < end_i; i++) {
                    prev_i = i;

                    var voxelData = new DebugVoxel(m_voxelsToTransition[i].LocalPosition, m_center, maxRadius, 0, UnityEngine.Random.ColorHSV());

                    m_debugVoxels.Add(voxelData);

                    bounds.Encapsulate(new Bounds(voxelData.LocalPosition + m_center, Vector3.one * m_voxelScale));

                    UpdateVoxelGrid(voxelData, maxRadius, normalizedTime);

                    if (Raymarcher.Instance.DebugView) {
                        m_voxelBuffer?.Release();
                        m_voxelBuffer = new ComputeBuffer(m_debugVoxels.Count, DebugVoxel.SIZE);

                        m_voxelBuffer.SetData(m_debugVoxels);
                        m_material.SetBuffer("voxelBuffer", m_voxelBuffer);
                        m_material.SetInt("voxelsCount", m_debugVoxels.Count);

                        uint[] args = new uint[5] {
                            (uint)m_voxelMesh.GetIndexCount(0),
                            (uint)m_debugVoxels.Count,
                            (uint)m_voxelMesh.GetIndexStart(0),
                            (uint)m_voxelMesh.GetBaseVertex(0),
                            0
                        };

                        m_argsBuffer.SetData(args);
                    }
                }

                float next_t = Mathf.Clamp(t + Time.deltaTime * (1 - m_growthCurve.Evaluate(t)), 0.001f, m_growthTime);

                if (next_t == m_growthTime)
                    animating = false;
                else
                    t = next_t;
            }

            if (Raymarcher.Instance.DebugView)
                Graphics.DrawMeshInstancedIndirect(m_voxelMesh, 0, m_material, renderBounds, m_argsBuffer);

            await Task.Yield();
        }
    }

    public void Render(RenderTexture rt, RenderTexture depth, RenderTexture mainTex, int ind) {
        Matrix4x4 projMatrix = GL.GetGPUProjectionMatrix(Camera.main.projectionMatrix, false);
        Matrix4x4 viewProjMatrix = projMatrix * Camera.main.worldToCameraMatrix;

        Material.SetVector("_CameraWorldPos", Camera.main.transform.position);
        Material.SetMatrix("_CameraToWorld", Camera.main.cameraToWorldMatrix);
        Material.SetMatrix("_CameraInvProjection", projMatrix.inverse);
        Material.SetMatrix("_CameraInvViewProjection", viewProjMatrix.inverse);
        Material.SetTexture(0, "_MainTex", mainTex);
        Material.SetTextureFromGlobal(0, "_DepthTex", "_CameraDepthTexture");
        Material.SetVector("_BufferSize", new Vector2(rt.width, rt.height));
        Material.SetVector("_ScreenSize", new Vector2(Screen.width, Screen.height));
        Material.SetVector("_WorldSpaceLightDir", -FindObjectOfType<Light>().transform.forward);
        Material.SetVector("_LightColor0", FindObjectOfType<Light>().color);


        Material.SetTexture(0, "SmokesArrayResult", rt);
        Material.SetInt("SmokeArrayIndex", ind);
        Material.SetTexture(0, "SmokeDepthResult", depth);

        Material.SetFloat("_DensityFalloff", Settings.densityFalloff);

        Material.SetFloat("scale", Settings.cloudScale);
        Material.SetFloat("densityMultiplier", Settings.densityMultiplier);
        Material.SetFloat("densityOffset", Settings.densityOffset);

        Material.SetInt("marchSteps", Settings.marchSteps);
        Material.SetInt("lightmarchSteps", Settings.lightmarchSteps);
        Material.SetFloat("stepSize", Settings.stepSize);
        Material.SetFloat("lightStepSize", Settings.lightStepSize);
        Material.SetFloat("rayOffset", Settings.rayOffset);
        Material.SetTexture(0, "BlueNoise", Settings.blueNoise);

        Material.SetVector("scatterColor", Settings.scatterColor);
        Material.SetFloat("brightness", Settings.brightness);
        Material.SetFloat("transmitThreshold", Settings.transmitThreshold);
        Material.SetFloat("inScatterMultiplier", Settings.inScatterMultiplier);
        Material.SetFloat("outScatterMultiplier", Settings.outScatterMultiplier);
        Material.SetFloat("forwardScatter", Settings.forwardScattering);
        Material.SetFloat("backwardScatter", Settings.backwardScattering);
        Material.SetFloat("scatterMultiplier", Settings.scatterMultiplier);

        Material.SetVector("cloudSpeed", Settings.cloudSpeed);

        Material.Dispatch(0, Mathf.CeilToInt(rt.width / 8.0f), Mathf.CeilToInt(rt.height / 8.0f), 1);
    }

    private void OnDestroy() {
        Raymarcher.Instance.UnregisterVoxelSphere(this);
    }
}