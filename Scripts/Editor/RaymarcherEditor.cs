using UnityEngine;
using UnityEditor;
using MyBox;

[CustomEditor(typeof(Raymarcher))]
public class RaymarcherEditor : Editor {
    private int slice;
    private Vector3 samplePos;

    private void OnSceneGUI() {
        Handles.PositionHandle(samplePos, Quaternion.identity);
    }

    public static Vector3 SnapToGrid(Vector3 position, float gridSize) {
        Vector3 snappedPosition = new Vector3(
            Mathf.Round(position.x / gridSize) * gridSize + gridSize / 2,
            Mathf.Round(position.y / gridSize) * gridSize + gridSize / 2,
            Mathf.Round(position.z / gridSize) * gridSize + gridSize / 2
        );
        return snappedPosition;
    }

    int to1D(Vector3Int pos, Vector3Int VoxelResolution) {
        return pos.x + pos.y * VoxelResolution.x + pos.z * VoxelResolution.x * VoxelResolution.y;
    }

    bool PointInOABB(Vector3 point, BoxCollider box) {
        point = box.transform.InverseTransformPoint(point) - box.center;

        float halfX = (box.size.x * 0.5f);
        float halfY = (box.size.y * 0.5f);
        float halfZ = (box.size.z * 0.5f);

        if (point.x < halfX && point.x > -halfX &&
           point.y < halfY && point.y > -halfY &&
           point.z < halfZ && point.z > -halfZ)
            return true;
        else
            return false;
    }

    Vector3Int SeedPos(Vector3 pos, Vector3 boundsExtent, Vector3 boundsMin, ref Raymarcher.VoxelCell[] voxelGrid) {
        return (pos - boundsMin).ToVector3Int();
    }

    public override void OnInspectorGUI() {
        base.OnInspectorGUI();

        // var script = (Raymarcher)target;

        // var oldColor = GUI.color;

        // samplePos = EditorGUILayout.Vector3Field("seedPos", samplePos);

        // if (GUILayout.Button("min"))
        //     samplePos = script.GlobalBounds.min;

        // if (GUILayout.Button("max"))
        //     samplePos = script.GlobalBounds.max;

        // Vector3 pos = SeedPos(samplePos, script.GlobalBounds.extents, script.GlobalBounds.min, ref script.VoxelsGrid);

        // int i = to1D(pos.ToVector3Int(), script.VoxelResolution);

        // if (i >= 0 && i < script.VoxelsGrid.Length)
        //     GUI.color = script.VoxelsGrid[i].Occupied == 1 ? Color.black : Color.white;

        // GUILayout.Box($"{pos}");
        // GUILayout.Box($"{i}");

        // GUI.color = oldColor;
    }
}