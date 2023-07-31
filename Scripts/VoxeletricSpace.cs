using System.Collections.Generic;
using UnityEngine;

public class VoxeletricSpace : MonoBehaviour {
    public SmokeSettings settings;

    public float MaxRadius = 10;
    public float VoxelScale = 1;
    public Mesh VoxelMesh;
    public AnimationCurve GrowthCurve;
    public float GrowthTime = 0.3f;

    public List<VoxelSphere> m_spheres = new List<VoxelSphere>();

    private void Update() {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Input.GetMouseButtonDown(1) && Physics.Raycast(ray, out hit, Mathf.Infinity)) {
            var sphere = new GameObject("Smoke Grenade", typeof(VoxelSphere)).GetComponent<VoxelSphere>();

            sphere.Initialize(settings, hit.point, MaxRadius, VoxelMesh, VoxelScale, GrowthTime, GrowthCurve);

            m_spheres.Add(sphere);
        }

        if (Input.GetMouseButtonDown(2)) {
            m_spheres.ForEach(s => Destroy(s.gameObject));
            m_spheres.Clear();
        }
    }
}