using System.Collections.Generic;
using UnityEngine;

public class VoxeletricSpace : MonoBehaviour {
    public float MaxRadius = 10;
    public float VoxelScale = 1;
    public Mesh VoxelMesh;
    public AnimationCurve GrowthCurve;
    public float GrowthTime = 0.3f;

    private List<VoxelSphere> m_spheres = new List<VoxelSphere>();

    public Vector3 SnapToGrid(Vector3 position, float gridSize) {
        Vector3 snappedPosition = new Vector3(
            Mathf.Round(position.x / gridSize) * gridSize + gridSize / 2,
            Mathf.Round(position.y / gridSize) * gridSize + gridSize / 2,
            Mathf.Round(position.z / gridSize) * gridSize + gridSize / 2
        );
        return snappedPosition;
    }


    private void Update() {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Input.GetMouseButtonDown(0) && Physics.Raycast(ray, out hit, Mathf.Infinity)) {
            var sphere = new VoxelSphere(SnapToGrid(hit.point, 1), MaxRadius, VoxelMesh, VoxelScale, GrowthTime, GrowthCurve);

            m_spheres.Add(sphere);
        }

        if (Input.GetMouseButtonDown(1)) {
            m_spheres.ForEach(s => s.Destroy());
            m_spheres.Clear();
        }
    }
}