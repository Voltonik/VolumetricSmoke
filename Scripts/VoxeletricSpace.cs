using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyBox;
using UnityEngine;

public class VoxeletricSpace : MonoBehaviour {
    public class VoxelSphere {
        public struct Voxel {
            public Vector3 Position;
            public Vector3 Color;
            public int Radius;

            public Voxel(Vector3 position, Color color, int radius) {
                Position = position;
                Color = new Vector3(color.r, color.g, color.b);
                Radius = radius;
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
        private uint[] m_args = new uint[5] { 0, 0, 0, 0, 0 };

        private float m_normalizedTime, m_transitionTimer;

        private int m_voxelStride;
        private List<Voxel> m_voxels = new List<Voxel>();
        private List<Voxel> m_voxelsCache = new List<Voxel>();
        private List<Voxel> m_voxelsToTransition = new List<Voxel>();

        public VoxelSphere(Vector3 center, float radius, Mesh voxelMesh, float voxelScale = 1, float growthSpeed = 0.9f, AnimationCurve growthCurve = null) {
            m_center = center;
            m_maxRadius = radius;
            m_voxelMesh = voxelMesh;
            m_voxelScale = voxelScale;
            m_growthSpeed = growthSpeed;
            m_growthCurve = growthCurve;

            if (growthCurve == null)
                growthCurve = AnimationCurve.Linear(0, 0, 1, 1);

            m_material = new Material(Shader.Find("Voxel/VoxelShader"));
            m_material.SetFloat("_VoxelScale", m_voxelScale);

            int r = Mathf.RoundToInt(m_maxRadius / m_voxelScale);

            for (int tx = -r; tx <= r; tx++) {
                for (int ty = -r; ty <= r; ty++) {
                    for (int tz = -r; tz <= r; tz++) {
                        int sqrMag = tx * tx + ty * ty + tz * tz;
                        int sqrR = r * r;

                        if (sqrMag < sqrR) {
                            Voxel voxel = new Voxel(new Vector3(tx, ty, tz), Random.ColorHSV(0, 1, 0.7f, 1, 0.7f, 1), sqrMag);

                            if (!Physics.Raycast(m_center, voxel.Position, m_maxRadius)) {
                                m_voxelsCache.Add(voxel);
                            }
                        }
                    }
                }
            }
        }

        public async void Explode() {
            m_voxelBuffer = new ComputeBuffer(m_voxelsCache.Count, Voxel.SIZE);
            m_argsBuffer = new ComputeBuffer(1, m_args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);

            m_args[0] = (uint)m_voxelMesh.GetIndexCount(0);
            m_args[1] = (uint)m_voxelsCache.Count;
            m_args[2] = (uint)m_voxelMesh.GetIndexStart(0);
            m_args[3] = (uint)m_voxelMesh.GetBaseVertex(0);

            m_argsBuffer.SetData(m_args);

            m_voxels.Clear();

            m_voxelsToTransition = m_voxelsCache;
            m_voxelsToTransition.Shuffle();
            m_voxelsToTransition = m_voxelsToTransition.OrderBy(v => v.Radius).ToList();

            m_normalizedTime = 0;

            while (Application.isPlaying) {
                m_transitionTimer += Time.deltaTime;
                m_normalizedTime += Time.deltaTime;

                float m_minTimeBetweenTicks = 0.001f * m_growthCurve.Evaluate(m_normalizedTime) / m_growthSpeed;

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

                Graphics.DrawMeshInstancedIndirect(m_voxelMesh, 0, m_material, new Bounds(m_center, new Vector3(m_maxRadius, m_maxRadius, m_maxRadius)), m_argsBuffer);

                await Task.Yield();
            }

            m_voxelBuffer.Release();
            m_argsBuffer.Release();
        }
    }

    public float MaxRadius = 10;
    public float VoxelScale = 1;
    public Mesh VoxelMesh;
    public AnimationCurve GrowthCurve;
    public float GrowthSpeed = 0.9f;

    private void Update() {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Input.GetMouseButtonDown(0) && Physics.Raycast(ray, out hit, Mathf.Infinity)) {
            var sphere = new VoxelSphere(hit.point, MaxRadius, VoxelMesh, VoxelScale, GrowthSpeed, GrowthCurve);

            sphere.Explode();
        }
    }
}