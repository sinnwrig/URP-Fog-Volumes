using UnityEngine;
using UnityEditor;


[CustomEditor(typeof(FogVolumeProfile))]
public class FogVolumeProfileEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
    }
}
