using System.Collections.Generic;
using UnityEngine;

public class VoxeletricSpace : MonoBehaviour {
    public float MaxRadius = 10;
    public float VoxelScale = 1;
    public Mesh VoxelMesh;
    public AnimationCurve GrowthCurve;
    public float GrowthTime = 0.3f;

    private List<VoxelSphere> m_spheres = new List<VoxelSphere>();

    private void Update() {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Input.GetMouseButtonDown(0) && Physics.Raycast(ray, out hit, Mathf.Infinity)) {
            var sphere = new VoxelSphere(hit.point, MaxRadius, VoxelMesh, VoxelScale, GrowthTime, GrowthCurve);

            // sphere.Explode();

            m_spheres.Add(sphere);
        }

        if (Input.GetMouseButtonDown(1)) {
            m_spheres.ForEach(s => s.Destroy());
            m_spheres.Clear();
        }
    }
}