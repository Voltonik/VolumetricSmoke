using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(Raymarcher))]
public class RaymarcherEditor : Editor {
    private int slice;

    int to1D(Vector3Int pos, Vector3Int VoxelResolution) {
        return pos.x + pos.y * VoxelResolution.x + pos.z * VoxelResolution.x * VoxelResolution.y;
    }

    public override void OnInspectorGUI() {
        base.OnInspectorGUI();

        var script = (Raymarcher)target;

        // if (script.m_spheres.Count == 0)
        //     return;

        // slice = EditorGUILayout.IntSlider(slice, 0, script.m_spheres[0].VoxelResolution.z - 1);

        // Color oldColor = GUI.color;

        // for (int y = script.m_spheres[0].VoxelResolution.y - 1; y >= 0; y--) {
        //     EditorGUILayout.BeginHorizontal();
        //     for (int x = 0; x < script.m_spheres[0].VoxelResolution.x; x++) {
        //         int i = to1D(new Vector3Int(x, y, slice), script.m_spheres[0].VoxelResolution);
        //         float vp = script.m_spheres[0].EditorVoxelsGrid[i].VoxelDistance;

        //         GUI.color = Color.Lerp(Color.black, Color.green, (float)vp / (float)script.m_spheres[0].maxRadius) + new Color(0, 0.4f, 0);

        //         if (vp == 0)
        //             GUI.color = Color.black;

        //         GUILayout.Box(vp.ToString("D4"));
        //     }
        //     EditorGUILayout.EndHorizontal();
        // }

        // GUI.color = oldColor;
    }
}