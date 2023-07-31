// using UnityEngine;
// using UnityEditor;
// using MyBox;

// [CustomEditor(typeof(Raymarcher))]
// public class RaymarcherEditor : Editor {
//     private Vector3 pos;
//     private int slice;

//     private Vector3Int VoxelResolution;
//     private Vector3 boundsMin, boundsMax, boundsExtent;
//     private float[] voxelGrid;

//     private float offset = 0.5f;

//     private bool debugging;

//     int flatten(Vector3Int pos) {
//         return pos.x + pos.y * VoxelResolution.x + pos.z * VoxelResolution.x * VoxelResolution.y;
//     }

//     float mix(float x, float y, float a) {
//         a = Mathf.Clamp01(a);
//         return x * (1 - a) + y * a;
//     }

//     Vector3 fract(Vector3 x) {
//         return x - new Vector3(Mathf.Floor(x.x), Mathf.Floor(x.y), Mathf.Floor(x.z));
//     }

//     float step(float edge, float x) {
//         return x < edge ? 0 : 1;
//     }

//     Vector3 step(Vector3 edge, Vector3 x) {
//         return new Vector3(step(edge.x, x.x), step(edge.y, x.y), step(edge.z, x.z));
//     }

//     float insideBox(Vector3 v, Vector3 bottomLeft, Vector3 topRight) {
//         Vector3 s = step(bottomLeft, v) - step(topRight, v);
//         return s.x * s.y * s.z;
//     }

//     float getTrilinearVoxel(Vector3 pos) {
//         if (insideBox(pos, boundsMin, boundsMax) == 0)
//             return 0;

//         Vector3 seedPos = pos - boundsMin;

//         seedPos.x -= offset;
//         seedPos.y -= offset;
//         seedPos.z -= offset;

//         Vector3Int vi = new Vector3Int(Mathf.FloorToInt(seedPos.x), Mathf.FloorToInt(seedPos.y), Mathf.FloorToInt(seedPos.z));

//         float weight1 = 0.0f;
//         float weight2 = 0.0f;
//         float weight3 = 0.0f;
//         float value = 0.0f;

//         for (int i = 0; i < 2; i++) {
//             weight1 = 1 - Mathf.Min(Mathf.Abs(seedPos.x - (vi.x + i)), VoxelResolution.x);
//             for (int j = 0; j < 2; j++) {
//                 weight2 = 1 - Mathf.Min(Mathf.Abs(seedPos.y - (vi.y + j)), VoxelResolution.y);
//                 for (int k = 0; k < 2; k++) {
//                     weight3 = 1 - Mathf.Min(Mathf.Abs(seedPos.z - (vi.z + k)), VoxelResolution.z);
//                     value += weight1 * weight2 * weight3 * voxelGrid[flatten(vi + new Vector3Int(i, j, k))];
//                 }
//             }
//         }

//         return value;
//     }

//     float SDFSphere(float dist, float radius) {
//         return dist - radius;
//     }

//     float densityAtPosition(Vector3 rayPos, float normalizedTime, float maxRadius, Vector3 _SmokeOrigin) {
//         float v = getTrilinearVoxel(rayPos);

//         float radius = normalizedTime * maxRadius;

//         float dist = (rayPos - _SmokeOrigin).magnitude;

//         dist = Mathf.Max(dist, v);

//         float sphere = SDFSphere(dist, radius);

//         float falloff = Mathf.Clamp01(sphere);

//         return Mathf.Clamp01(v) * (1 - falloff);
//     }

//     public override void OnInspectorGUI() {
//         base.OnInspectorGUI();

//         debugging = EditorGUILayout.Toggle(debugging);
//         if (!debugging)
//             return;

//         var script = (Raymarcher)target;

//         if (script.m_spheres.Count == 0)
//             return;

//         pos = Camera.main.transform.position;

//         Color oldColor = GUI.color;

//         boundsMin = script.m_spheres[0].GlobalBounds.min;
//         boundsMax = script.m_spheres[0].GlobalBounds.max;
//         VoxelResolution = script.m_spheres[0].VoxelResolution;
//         voxelGrid = script.m_spheres[0].VoxelsGrid;
//         offset = EditorGUILayout.FloatField(offset);

//         float vpp = getTrilinearVoxel(pos);
//         float v1 = vpp;

//         GUI.color = Color.Lerp(Color.black, Color.green, (float)vpp / (float)script.m_spheres[0].maxRadius) + new Color(0, 0.4f, 0);

//         if (vpp == 0)
//             GUI.color = Color.black;

//         GUILayout.Box(vpp.ToString());

//         vpp = (pos - script.m_spheres[0].m_center).magnitude;

//         GUI.color = Color.Lerp(Color.black, Color.red, (float)vpp / (float)script.m_spheres[0].maxRadius) + new Color(0.4f, 0, 0);

//         if (vpp == 0)
//             GUI.color = Color.black;

//         GUILayout.Box(vpp.ToString());

//         GUI.color = oldColor;

//         GUILayout.Label($"{Mathf.Max(v1, vpp)}");

//         float v = Mathf.Max(vpp, v1);

//         GUILayout.Label($"Density: {densityAtPosition(pos, script.m_spheres[0].normalizedTime, script.m_spheres[0].maxRadius, script.m_spheres[0].m_center)}");

//         EditorGUILayout.Space();
//         EditorGUILayout.Space();

//         // slice = EditorGUILayout.IntSlider(slice, 0, script.m_spheres[0].VoxelResolution.z - 1);
//         slice = (Camera.main.transform.position - boundsMin).ToVector3Int().z;

//         if (slice < 0 || slice >= script.m_spheres[0].VoxelResolution.z)
//             return;

//         oldColor = GUI.color;

//         for (int y = script.m_spheres[0].VoxelResolution.y - 1; y >= 0; y--) {
//             EditorGUILayout.BeginHorizontal();
//             for (int x = 0; x < script.m_spheres[0].VoxelResolution.x; x++) {
//                 float vp = getTrilinearVoxel(new Vector3Int(x, y, slice) + boundsMin);

//                 GUI.color = Color.Lerp(Color.black, Color.green, (float)vp / (float)script.m_spheres[0].maxRadius) + new Color(0, 0.4f, 0);

//                 if (vp == 0)
//                     GUI.color = Color.black;

//                 if (new Vector3Int(x, y, slice) == (pos - boundsMin).ToVector3Int())
//                     GUI.color = Color.blue;

//                 GUILayout.Box(((int)vp).ToString().PadLeft(3, '0'));
//             }
//             EditorGUILayout.EndHorizontal();
//         }

//         GUI.color = oldColor;

//         EditorGUILayout.Space();

//         oldColor = GUI.color;

//         for (int y = script.m_spheres[0].VoxelResolution.y - 1; y >= 0; y--) {
//             EditorGUILayout.BeginHorizontal();
//             for (int x = 0; x < script.m_spheres[0].VoxelResolution.x; x++) {
//                 int i = flatten(new Vector3Int(x, y, slice));
//                 float vp = script.m_spheres[0].EditorVoxels[i].LocalPosition.magnitude;

//                 GUI.color = Color.Lerp(Color.black, Color.red, (float)vp / (float)script.m_spheres[0].maxRadius) + new Color(0.4f, 0, 0);

//                 if (vp == 0)
//                     GUI.color = Color.black;

//                 if (new Vector3Int(x, y, slice) == (pos - boundsMin).ToVector3Int())
//                     GUI.color = Color.blue;

//                 GUILayout.Box(((int)vp).ToString().PadLeft(3, '0'));
//             }
//             EditorGUILayout.EndHorizontal();
//         }

//         GUI.color = oldColor;
//     }
// }