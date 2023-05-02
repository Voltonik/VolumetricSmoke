using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyBox;
using UnityEngine;

public class VoxelSphere {
    public class Voxel {
        public Vector3 LocalPosition;
        public int Order;

        public Voxel(Vector3 position, int order) {
            LocalPosition = position;
            Order = order;
        }
    }

    public struct VoxelData {
        public Vector3 Position;

        public VoxelData(Vector3 position) {
            Position = position;
        }

        public static int SIZE = System.Runtime.InteropServices.Marshal.SizeOf(typeof(VoxelData));
    }

    private Vector3 m_center;
    private float m_maxRadius;
    private float m_voxelScale;
    private Mesh m_voxelMesh;
    private AnimationCurve m_growthCurve;
    private float m_growthTime;

    private Material m_material;
    private ComputeBuffer m_voxelBuffer;
    private ComputeBuffer m_argsBuffer;

    private List<VoxelData> m_voxels = new List<VoxelData>();
    private List<Voxel> m_voxelsToTransition = new List<Voxel>();
    private Dictionary<Vector3, Voxel> m_voxelsCache = new Dictionary<Vector3, Voxel>();

    private bool m_running;

    public VoxelSphere(Vector3 center, float radius, Mesh voxelMesh, float voxelScale = 1, float growthSpeed = 0.9f, AnimationCurve growthCurve = null) {
        m_center = center + new Vector3(0, voxelScale / 2, 0);
        m_maxRadius = radius;
        m_voxelMesh = voxelMesh;
        m_voxelScale = voxelScale;
        m_growthTime = growthSpeed;
        m_growthCurve = growthCurve;

        if (growthCurve == null)
            growthCurve = AnimationCurve.Linear(0, 0, 1, 1);

        m_material = new Material(Shader.Find("Voxel/VoxelShader"));
        m_material.SetFloat("_VoxelScale", m_voxelScale);

        CalculateSphere();
    }

    private void CalculateSphere() {
        int r = Mathf.RoundToInt(m_maxRadius / m_voxelScale);
        int deleted = 0;
        int maxRadius = 0;

        List<Voxel> outLayerVoxels = new List<Voxel>();

        GenerateSphere(r, v => {
            if (!Physics.Raycast(m_center + Vector3.up * 0.1f, v.LocalPosition, m_maxRadius)) {
                m_voxelsCache.Add(v.LocalPosition, v);

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
                    && !Physics.CheckBox(m_center + connectedVoxels[j].LocalPosition + Vector3.up * 0.1f, Vector3.one * (m_voxelScale / 2))) {
                        connectedVoxels[j].Order = nextIndex;
                        m_voxelsCache.Add(connectedVoxels[j].LocalPosition, connectedVoxels[j]);
                        newLayerVoxels.Add(connectedVoxels[j]);
                        deleted--;
                        nextIndex++;

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
            outLayerVoxels.Shuffle();
        }
    }

    public static Voxel[] GetConnectedVoxels(Voxel v) {
        return new[] {
                new Voxel(v.LocalPosition + Vector3.up, Mathf.RoundToInt((v.LocalPosition + Vector3.up).sqrMagnitude)),
                new Voxel(v.LocalPosition + Vector3.down, Mathf.RoundToInt((v.LocalPosition + Vector3.down).sqrMagnitude)),
                new Voxel(v.LocalPosition + Vector3.right, Mathf.RoundToInt((v.LocalPosition + Vector3.right).sqrMagnitude)),
                new Voxel(v.LocalPosition + Vector3.left, Mathf.RoundToInt((v.LocalPosition + Vector3.left).sqrMagnitude)),
                new Voxel(v.LocalPosition + Vector3.forward, Mathf.RoundToInt((v.LocalPosition + Vector3.forward).sqrMagnitude)),
                new Voxel(v.LocalPosition + Vector3.back, Mathf.RoundToInt((v.LocalPosition + Vector3.back).sqrMagnitude)),
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

        m_voxels.Clear();

        m_voxelsToTransition = m_voxelsCache.Values.OrderBy(v => v.Order).ToList();

        m_running = true;
    }

    public async void Explode() {
        Reset();

        float t = 0;
        int prev_i = 0;
        bool animating = true;

        Vector3 minPosition = Vector3.zero;
        Vector3 maxPosition = Vector3.zero;
        Bounds bounds = new Bounds(m_center, new Vector3(m_maxRadius, m_maxRadius, m_maxRadius));

        while (Application.isPlaying && m_running) {
            if (animating && t <= m_growthTime) {
                float normalizedTime = t / m_growthTime;

                int starting_i = prev_i + 1;
                int end_i = Mathf.RoundToInt(m_voxelsToTransition.Count * normalizedTime);

                for (int i = starting_i; i < end_i; i++) {
                    prev_i = i;

                    VoxelData voxel = new VoxelData(m_voxelsToTransition[i].LocalPosition);
                    m_voxels.Add(voxel);

                    voxel.Position += m_center;

                    if (voxel.Position.x + voxel.Position.y + voxel.Position.z < minPosition.x + minPosition.y + minPosition.z)
                        minPosition = voxel.Position;
                    else if (voxel.Position.x + voxel.Position.y + voxel.Position.z > maxPosition.x + maxPosition.y + maxPosition.z)
                        maxPosition = voxel.Position;

                    m_voxelBuffer?.Release();
                    m_voxelBuffer = new ComputeBuffer(m_voxels.Count, VoxelData.SIZE);

                    m_voxelBuffer.SetData(m_voxels);
                    m_material.SetBuffer("voxelBuffer", m_voxelBuffer);

                    uint[] args = new uint[5] {
                        (uint)m_voxelMesh.GetIndexCount(0),
                        (uint)m_voxels.Count,
                        (uint)m_voxelMesh.GetIndexStart(0),
                        (uint)m_voxelMesh.GetBaseVertex(0),
                        0
                    };

                    m_argsBuffer.SetData(args);
                }

                float next_t = Mathf.Clamp(t + Time.deltaTime * (1 - m_growthCurve.Evaluate(t)), 0.001f, m_growthTime);

                if (next_t == m_growthTime)
                    animating = false;
                else
                    t = next_t;
            }

            Raymarcher.Instance.UpdateMaterial(m_material);
            Graphics.DrawMeshInstancedIndirect(m_voxelMesh, 0, m_material, bounds, m_argsBuffer);

            await Task.Yield();
        }

        Clean();
    }

    public void Destroy() {
        Reset();

        m_running = false;

        Clean();
    }

    private void Clean() {
        m_voxelBuffer?.Release();
        m_argsBuffer?.Release();

        m_voxelBuffer = null;
        m_argsBuffer = null;
    }

    ~VoxelSphere() {
        Clean();
    }
}