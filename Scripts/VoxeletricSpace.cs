using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VoxeletricSpace : MonoBehaviour {
    public int Radius = 10;
    public float VoxelScale = 1;
    public Mesh VoxelMesh;
    public AnimationCurve PropagationCurve;
    
    public bool RestartAnimation;

    private Material m_material;
    private ComputeBuffer m_positionBuffer, m_colorBuffer;
    private ComputeBuffer m_argsBuffer;
    private uint[] m_args = new uint[5] { 0, 0, 0, 0, 0 };

    private float m_animationTime, m_normalizedTime;

    private void Start() {
      m_material = new Material(Shader.Find("Voxel/VoxelShader"));
      m_material.SetFloat("_VoxelScale", VoxelScale);
      
      m_argsBuffer = new ComputeBuffer(1, m_args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
    }

    private void Update() {
      m_normalizedTime = Mathf.Clamp01(m_normalizedTime + Time.deltaTime);
      m_animationTime = PropagationCurve.Evaluate(m_normalizedTime);

      if (RestartAnimation) {
        m_normalizedTime = 0;

        RestartAnimation = false;
      }

      if (m_normalizedTime < 1) {
        UpdateBuffers();
      }
      
      Graphics.DrawMeshInstancedIndirect(VoxelMesh, 0, m_material, new Bounds(Vector3.zero, new Vector3(Radius, Radius, Radius)), m_argsBuffer);
    }

    private void UpdateBuffers() {
      int r = Mathf.RoundToInt(Mathf.Lerp(1, Radius, m_animationTime));
      int instanceCount = 0;
      
        List<Vector3> positions = new List<Vector3>();
        List<Vector3> colors = new List<Vector3>();
        
        for (int tx = -r; tx <= r; tx++){
          for (int ty = -r; ty <= r; ty++){
              for (int tz = -r; tz <= r; tz++){
                  if (tx*tx + ty*ty + tz*tz < r*r) {
                      var color = Random.ColorHSV(0, 1, 0.7f, 1, 0.7f, 1);
              
                      positions.Add(new Vector3(tx, ty, tz));
                      colors.Add(new Vector3(color.r, color.g, color.b));

                      instanceCount++;
                  }
              }
          }
      }
      
        if (instanceCount == 0)
          return;
          
        if (m_positionBuffer != null)
            m_positionBuffer.Release();

        if (m_colorBuffer != null)
            m_colorBuffer.Release();
          
        m_positionBuffer = new ComputeBuffer(instanceCount, sizeof(float)*3);
        m_positionBuffer.SetData(positions);

        m_colorBuffer = new ComputeBuffer(instanceCount, sizeof(float)*3);
        m_colorBuffer.SetData(colors);

        m_material.SetBuffer("positionBuffer", m_positionBuffer);
        m_material.SetBuffer("colorBuffer", m_colorBuffer);

        m_args[0] = (uint)VoxelMesh.GetIndexCount(0);
        m_args[1] = (uint)instanceCount;
        m_args[2] = (uint)VoxelMesh.GetIndexStart(0);
        m_args[3] = (uint)VoxelMesh.GetBaseVertex(0);
        
        m_argsBuffer.SetData(m_args);
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
