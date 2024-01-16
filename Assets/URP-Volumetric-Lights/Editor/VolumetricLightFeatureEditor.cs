using UnityEngine;
using UnityEditor;


[CustomEditor(typeof(VolumetricFogFeature))]
public class VolumetricLightFeatureEditor : Editor
{
    SerializedProperty resolution;

    void OnEnable()
    {
        resolution = serializedObject.FindProperty("resolution");
    }


    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(resolution);

        serializedObject.ApplyModifiedProperties();
    }
}
