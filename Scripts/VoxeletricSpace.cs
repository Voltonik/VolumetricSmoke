using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MyBox;
using UnityEngine;

public class VoxeletricSpace : MonoBehaviour {
    public struct Voxel {
        public Vector3 Position;
        public Vector3 Color;
        public int Radius;

        public Voxel(Vector3 position, Color color, int radius) {
            Position = position;
            Color = new Vector3(color.r, color.g, color.b);
            Radius = radius;
        }
    }

    public Vector3 Center;
    public float MaxRadius = 10;
    public float VoxelScale = 1;
    public Mesh VoxelMesh;
    public AnimationCurve GrowthCurve;
    [Min(0.01f)] public float GrowthSpeed = 3;

    public bool RestartAnimation;

    private bool m_running;
    private Material m_material;
    private ComputeBuffer m_voxelBuffer;
    private ComputeBuffer m_argsBuffer;
    private uint[] m_args = new uint[5] { 0, 0, 0, 0, 0 };

    public float m_normalizedTime, m_transitionTimer;

    private int m_voxelStride;
    private List<Voxel> m_voxels = new List<Voxel>();
    private List<Voxel> m_voxelsCache = new List<Voxel>();
    private List<Voxel> m_voxelsToTransition = new List<Voxel>();

    private void Start() {
        m_material = new Material(Shader.Find("Voxel/VoxelShader"));
        m_material.SetFloat("_VoxelScale", VoxelScale);

        int r = Mathf.RoundToInt(MaxRadius / VoxelScale);

        for (int tx = -r; tx <= r; tx++) {
            for (int ty = -r; ty <= r; ty++) {
                for (int tz = -r; tz <= r; tz++) {
                    int sqrMag = tx * tx + ty * ty + tz * tz;
                    int sqrR = r * r;

                    if (sqrMag < sqrR)
                        m_voxelsCache.Add(new Voxel(new Vector3(tx, ty, tz), Random.ColorHSV(0, 1, 0.7f, 1, 0.7f, 1), sqrMag));
                }
            }
        }

        m_voxelsToTransition = m_voxelsCache;
        m_voxelsToTransition.Shuffle();
        m_voxelsToTransition = m_voxelsToTransition.OrderBy(v => v.Radius).ToList();

        m_voxelStride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Voxel));

        m_argsBuffer = new ComputeBuffer(1, m_args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        m_voxelBuffer = new ComputeBuffer(m_voxelsCache.Count, m_voxelStride);
    }

    private void Update() {
        if (RestartAnimation) {
            if (m_voxelBuffer != null)
                m_voxelBuffer.Release();

            m_voxelBuffer = new ComputeBuffer(m_voxelsCache.Count, m_voxelStride);

            m_args[0] = (uint)VoxelMesh.GetIndexCount(0);
            m_args[1] = (uint)m_voxelsCache.Count;
            m_args[2] = (uint)VoxelMesh.GetIndexStart(0);
            m_args[3] = (uint)VoxelMesh.GetBaseVertex(0);

            m_argsBuffer.SetData(m_args);

            m_normalizedTime = 0;

            m_voxels.Clear();

            m_voxelsToTransition = m_voxelsCache;
            m_voxelsToTransition.Shuffle();
            m_voxelsToTransition = m_voxelsToTransition.OrderBy(v => v.Radius).ToList();

            m_running = true;
            RestartAnimation = false;

            StartCoroutine(TransitionVoxels());
        }

        Graphics.DrawMeshInstancedIndirect(VoxelMesh, 0, m_material, new Bounds(Center, new Vector3(MaxRadius, MaxRadius, MaxRadius)), m_argsBuffer);
    }

    private IEnumerator TransitionVoxels() {
        while (m_voxelsToTransition.Count > 0) {
            m_transitionTimer += Time.deltaTime;
            m_normalizedTime += Time.deltaTime;

            float m_minTimeBetweenTicks = 0.001f * GrowthCurve.Evaluate(m_normalizedTime) / GrowthSpeed;

            while (m_transitionTimer >= m_minTimeBetweenTicks) {
                m_transitionTimer -= m_minTimeBetweenTicks;

                if (m_voxelsToTransition.Count == 0)
                    break;

                int i = Random.Range(0, m_voxelsToTransition.FindIndex(v => v.Radius != m_voxelsToTransition[0].Radius));

                m_voxels.Add(m_voxelsToTransition[i]);
                m_voxelsToTransition.RemoveAt(i);
            }

            m_voxelBuffer.SetData(m_voxels);
            m_material.SetBuffer("voxelBuffer", m_voxelBuffer);

            yield return null;
        }
    }

    private void OnDisable() {
        m_voxelBuffer?.Release();
        m_argsBuffer?.Release();

        m_voxelBuffer = null;
        m_argsBuffer = null;
    }
}
