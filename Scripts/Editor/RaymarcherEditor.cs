using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(Raymarcher))]
public class RaymarcherEditor : Editor {
    private int slice;
    private Vector3 samplePos;

    private void OnSceneGUI() {
        Handles.PositionHandle(samplePos, Quaternion.identity);
    }

    public static Vector3 SnapToGrid(Vector3 position, float gridSize) {
        Vector3 snappedPosition = new Vector3(
            Mathf.Floor(position.x / gridSize) * gridSize,
            Mathf.Floor(position.y / gridSize) * gridSize,
            Mathf.Floor(position.z / gridSize) * gridSize
        );
        return snappedPosition;
    }

    public override void OnInspectorGUI() {
        base.OnInspectorGUI();

        var script = (Raymarcher)target;
        Texture3D tex = script.VoxelsGrid;

        if (tex == null)
            return;

        slice = EditorGUILayout.IntSlider(slice, 0, tex.depth - 1);

        var oldColor = GUI.color;
        for (int y = 0; y < tex.height; y++) {
            EditorGUILayout.BeginHorizontal();
            for (int x = 0; x < tex.width; x++) {
                GUI.color = new Color(tex.GetPixel(x, y, slice).r, tex.GetPixel(x, y, slice).g, 0);
                GUILayout.Box($"      ");
            }
            EditorGUILayout.EndHorizontal();
        }
        GUI.color = oldColor;
    }
}