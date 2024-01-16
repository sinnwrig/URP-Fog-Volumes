using UnityEngine;
using UnityEditor;


[CustomEditor(typeof(VolumetricFogFeature))]
public class VolumetricFogFeatureEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
    }
}
