using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VoxeletricSpace : MonoBehaviour {
    public Vector3 Center;
    public float MaxRadius = 10;
    public float VoxelScale = 1;
    public Mesh VoxelMesh;
    public AnimationCurve GrowthCurve;
    [Min(0.01f)] public float GrowthSpeed = 3;

    public bool RestartAnimation;

    private Material m_material;
    private ComputeBuffer m_positionBuffer, m_colorBuffer;
    private ComputeBuffer m_argsBuffer;
    private uint[] m_args = new uint[5] { 0, 0, 0, 0, 0 };

    private int m_instanceCount;
    private float m_animationTime, m_normalizedTime;
    private int m_currentRadius;
    private int m_previousRadius = -1;

    private void Start() {
        m_material = new Material(Shader.Find("Voxel/VoxelShader"));
        m_material.SetFloat("_VoxelScale", VoxelScale);

        m_argsBuffer = new ComputeBuffer(1, m_args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
    }

    private void Update() {
        m_normalizedTime = Mathf.Clamp01(m_normalizedTime + Time.deltaTime * GrowthSpeed);
        m_animationTime = GrowthCurve.Evaluate(m_normalizedTime);

        if (RestartAnimation) {
            m_normalizedTime = 0;
            m_currentRadius = 0;
            m_instanceCount = 0;
            m_previousRadius = -1;

            RestartAnimation = false;
        }

        if (m_normalizedTime < 1) {
            UpdateBuffers();
        }

        Graphics.DrawMeshInstancedIndirect(VoxelMesh, 0, m_material, new Bounds(Center, new Vector3(MaxRadius, MaxRadius, MaxRadius)), m_argsBuffer);
    }

    private void UpdateBuffers() {
        m_currentRadius = Mathf.RoundToInt(Mathf.Lerp(1, MaxRadius / VoxelScale, m_animationTime));

        if (m_currentRadius != m_previousRadius) {
            List<Vector3> positions = new List<Vector3>();
            List<Vector3> colors = new List<Vector3>();

            for (int tx = -m_currentRadius; tx <= m_currentRadius; tx++) {
                for (int ty = -m_currentRadius; ty <= m_currentRadius; ty++) {
                    for (int tz = -m_currentRadius; tz <= m_currentRadius; tz++) {
                        int sqrMag = tx * tx + ty * ty + tz * tz;
                        int sqrR = m_currentRadius * m_currentRadius;

                        if (sqrMag < sqrR && sqrMag > sqrR - (2 * m_currentRadius)) {
                            var color = Random.ColorHSV(0, 1, 0.7f, 1, 0.7f, 1);

                            positions.Add(new Vector3(tx, ty, tz));
                            colors.Add(new Vector3(color.r, color.g, color.b));

                            m_instanceCount++;
                        }
                    }
                }
            }

            if (m_instanceCount == 0)
                return;

            if (m_positionBuffer != null)
                m_positionBuffer.Release();

            if (m_colorBuffer != null)
                m_colorBuffer.Release();


            // TODO: add to buffers instead of destroying them each call
            m_positionBuffer = new ComputeBuffer(m_instanceCount, sizeof(float) * 3);
            m_positionBuffer.SetData(positions);

            m_colorBuffer = new ComputeBuffer(m_instanceCount, sizeof(float) * 3);
            m_colorBuffer.SetData(colors);

            m_material.SetBuffer("positionBuffer", m_positionBuffer);
            m_material.SetBuffer("colorBuffer", m_colorBuffer);

            m_args[0] = (uint)VoxelMesh.GetIndexCount(0);
            m_args[1] = (uint)m_instanceCount;
            m_args[2] = (uint)VoxelMesh.GetIndexStart(0);
            m_args[3] = (uint)VoxelMesh.GetBaseVertex(0);

            m_argsBuffer.SetData(m_args);

            m_previousRadius = m_currentRadius;
        }
    }


    private void OnDisable() {
        if (m_positionBuffer != null)
            m_positionBuffer.Release();

        m_positionBuffer = null;

        if (m_colorBuffer != null)
            m_colorBuffer.Release();

        m_colorBuffer = null;

        if (m_argsBuffer != null)
            m_argsBuffer.Release();

        m_argsBuffer = null;
    }
}
