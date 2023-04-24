using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

using Random = UnityEngine.Random;

public class VoxelSphere {
    public struct Voxel {
        public Vector3 Position;
        public Vector3 Color;
        public int SqrRadius;

        public Voxel(Vector3 position, Color color, int radius) {
            Position = position;
            Color = new Vector3(color.r, color.g, color.b);
            SqrRadius = radius;
        }

        public static int SIZE = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Voxel));
    }

    private Vector3 m_center;
    private float m_maxRadius;
    private float m_voxelScale;
    private Mesh m_voxelMesh;
    private AnimationCurve m_growthCurve;
    private float m_growthSpeed;

    private Material m_material;
    private ComputeBuffer m_voxelBuffer;
    private ComputeBuffer m_argsBuffer;

    private float m_normalizedTime, m_transitionTimer;
    private float m_massToAirRatio;

    private List<Voxel> m_voxels = new List<Voxel>();
    private List<Voxel> m_deletedVoxels = new List<Voxel>();
    private List<Voxel> m_voxelsToTransition = new List<Voxel>();
    private Dictionary<Vector3, Voxel> m_voxelsCache = new Dictionary<Vector3, Voxel>();
    private Dictionary<Vector3, Voxel> m_sceneVoxels = new Dictionary<Vector3, Voxel>();

    private bool m_running;

    public VoxelSphere(Vector3 center, float radius, Mesh voxelMesh, float voxelScale = 1, float massToAirRatio = 1, float growthSpeed = 0.9f, AnimationCurve growthCurve = null) {
        m_center = center;
        m_maxRadius = radius;
        m_voxelMesh = voxelMesh;
        m_voxelScale = voxelScale;
        m_growthSpeed = growthSpeed;
        m_growthCurve = growthCurve;
        m_massToAirRatio = massToAirRatio;

        if (growthCurve == null)
            growthCurve = AnimationCurve.Linear(0, 0, 1, 1);

        m_material = new Material(Shader.Find("Voxel/VoxelShader"));
        m_material.SetFloat("_VoxelScale", m_voxelScale);

        CalculateSphere();
    }

    private void CalculateSphere() {
        int r = Mathf.RoundToInt(m_maxRadius / m_voxelScale);

        GenerateSphere(r, v => {
            if (!Physics.Raycast(m_center + Vector3.up * 0.1f, v.Position, m_maxRadius)) {
                m_voxelsCache.Add(v.Position, v);
            } else {
                m_deletedVoxels.Add(v);
            }
        });

        r = 0;

        float t1 = Time.realtimeSinceStartup;

        while (m_deletedVoxels.Count > 0) {
            r++;

            m_maxRadius += 0.3f;

            GenerateSphere(r, v => {
                if (!m_voxelsCache.ContainsKey(v.Position)
                && IsVoxelConnected(m_voxelsCache, v)
                && !Physics.CheckBox(m_center + v.Position, Vector3.one * (m_voxelScale / 2))) {

                    m_voxelsCache.Add(v.Position, v);

                    if (m_deletedVoxels.Count > 0)
                        m_deletedVoxels.RemoveAt(0);
                }
            }, () => m_deletedVoxels.Count == 0);
        }

        float t2 = Time.realtimeSinceStartup;

        Debug.Log($"took {t2 - t1}s");
    }

    private static bool IsVoxelConnected(Dictionary<Vector3, Voxel> voxels, Voxel voxel) {
        return voxels.ContainsKey(voxel.Position + Vector3.up)
                    || voxels.ContainsKey(voxel.Position + Vector3.down)
                    || voxels.ContainsKey(voxel.Position + Vector3.right)
                    || voxels.ContainsKey(voxel.Position + Vector3.left)
                    || voxels.ContainsKey(voxel.Position + Vector3.forward)
                    || voxels.ContainsKey(voxel.Position + Vector3.back);
    }

    private static void GenerateSphere(int r, Action<Voxel> sphereVoxel, Func<bool> breakCondition = null) {
        for (int tx = 0; tx < r; tx++) {
            for (int ty = 0; ty < r; ty++) {
                for (int tz = 0; tz < r; tz++) {
                    int sqrMag = tx * tx + ty * ty + tz * tz;

                    if (sqrMag < r * r) {
                        sphereVoxel.Invoke(new Voxel(new Vector3(tx, ty, tz), Random.ColorHSV(0, 1, 0.7f, 1, 0.7f, 1), sqrMag));
                        if (breakCondition != null && breakCondition.Invoke())
                            return;

                        if (tx != 0) {
                            sphereVoxel.Invoke(new Voxel(new Vector3(-tx, ty, tz), Random.ColorHSV(0, 1, 0.7f, 1, 0.7f, 1), sqrMag));
                            if (breakCondition != null && breakCondition.Invoke())
                                return;

                            if (tz != 0) {
                                sphereVoxel.Invoke(new Voxel(new Vector3(-tx, ty, -tz), Random.ColorHSV(0, 1, 0.7f, 1, 0.7f, 1), sqrMag));
                                if (breakCondition != null && breakCondition.Invoke())
                                    return;
                            }
                        }

                        if (tz != 0) {
                            sphereVoxel.Invoke(new Voxel(new Vector3(tx, ty, -tz), Random.ColorHSV(0, 1, 0.7f, 1, 0.7f, 1), sqrMag));
                            if (breakCondition != null && breakCondition.Invoke())
                                return;
                        }

                        if (ty != 0) {
                            sphereVoxel.Invoke(new Voxel(new Vector3(tx, -ty, tz), Random.ColorHSV(0, 1, 0.7f, 1, 0.7f, 1), sqrMag));
                            if (breakCondition != null && breakCondition.Invoke())
                                return;

                            if (tx != 0) {
                                sphereVoxel.Invoke(new Voxel(new Vector3(-tx, -ty, tz), Random.ColorHSV(0, 1, 0.7f, 1, 0.7f, 1), sqrMag));
                                if (breakCondition != null && breakCondition.Invoke())
                                    return;

                                if (tz != 0) {
                                    sphereVoxel.Invoke(new Voxel(new Vector3(-tx, -ty, -tz), Random.ColorHSV(0, 1, 0.7f, 1, 0.7f, 1), sqrMag));
                                    if (breakCondition != null && breakCondition.Invoke())
                                        return;
                                }
                            }

                            if (tz != 0) {
                                sphereVoxel.Invoke(new Voxel(new Vector3(tx, -ty, -tz), Random.ColorHSV(0, 1, 0.7f, 1, 0.7f, 1), sqrMag));
                                if (breakCondition != null && breakCondition.Invoke())
                                    return;
                            }
                        }
                    }
                }
            }
        }
    }

    public void Reset() {
        uint[] args = new uint[5] { 0, 0, 0, 0, 0 };

        m_voxelBuffer = new ComputeBuffer(m_voxelsCache.Count, Voxel.SIZE);
        m_argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);

        args[0] = (uint)m_voxelMesh.GetIndexCount(0);
        args[1] = (uint)m_voxelsCache.Count;
        args[2] = (uint)m_voxelMesh.GetIndexStart(0);
        args[3] = (uint)m_voxelMesh.GetBaseVertex(0);

        m_argsBuffer.SetData(args);

        m_voxels.Clear();

        m_voxelsToTransition = m_voxelsCache.Values.OrderBy(v => v.SqrRadius).ToList();

        m_normalizedTime = 0;

        m_running = true;
    }

    public async void Explode() {
        Reset();

        bool transitioning = true;

        while (Application.isPlaying && m_running) {
            m_transitionTimer += Time.deltaTime;
            m_normalizedTime += Time.deltaTime;

            float m_minTimeBetweenTicks = 0.001f * m_growthCurve.Evaluate(m_normalizedTime) / m_growthSpeed;

            while (transitioning && m_transitionTimer >= m_minTimeBetweenTicks) {
                m_transitionTimer -= m_minTimeBetweenTicks;

                var connectedVoxels = m_voxelsToTransition.Where(v => v.Position == Vector3.zero || IsVoxelConnected(m_sceneVoxels, v)).ToList();

                if (connectedVoxels.Count == 0) {
                    transitioning = false;

                    Debug.Log($"DONE: {m_sceneVoxels.Count}/{m_voxelsCache.Count}");

                    break;
                }

                int i = Random.Range(0, connectedVoxels.FindIndex(v => v.SqrRadius != m_voxelsToTransition[0].SqrRadius) + 1);

                m_sceneVoxels.Add(connectedVoxels[i].Position, connectedVoxels[i]);
                m_voxels.Add(connectedVoxels[i]);
                m_voxelsToTransition.RemoveAll(v => v.Position == connectedVoxels[i].Position);

                m_voxelBuffer.SetData(m_voxels);
                m_material.SetBuffer("voxelBuffer", m_voxelBuffer);
            }

            Graphics.DrawMeshInstancedIndirect(m_voxelMesh, 0, m_material, new Bounds(m_center, new Vector3(m_maxRadius, m_maxRadius, m_maxRadius)), m_argsBuffer);

            await Task.Yield();
        }

        m_voxelBuffer.Release();
        m_argsBuffer.Release();

        m_voxelBuffer = null;
        m_argsBuffer = null;
    }

    public void Destroy() {
        Reset();

        m_running = false;
    }

    ~VoxelSphere() {
        m_voxelBuffer?.Release();
        m_argsBuffer?.Release();

        m_voxelBuffer = null;
        m_argsBuffer = null;
    }
}