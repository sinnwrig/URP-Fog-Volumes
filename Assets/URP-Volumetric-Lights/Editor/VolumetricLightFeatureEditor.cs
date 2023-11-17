using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;


[CustomEditor(typeof(VolumetricLightFeature))]
public class VolumetricLightFeatureEditor : Editor
{
    SerializedProperty resolution;
    SerializedProperty lightRange, falloffRange;
    SerializedProperty noise, noiseTexture;

    void OnEnable()
    {
        resolution = serializedObject.FindProperty("resolution");
        lightRange = serializedObject.FindProperty("lightRange");
        falloffRange = serializedObject.FindProperty("falloffRange");
        noise = serializedObject.FindProperty("noise");
        noiseTexture = serializedObject.FindProperty("noiseTexture");
    }


    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(resolution);
        EditorGUILayout.PropertyField(lightRange);
        EditorGUILayout.PropertyField(falloffRange);

        GUILayout.BeginHorizontal();
        EditorGUILayout.PropertyField(noise);
        EditorGUILayout.PropertyField(noiseTexture, GUIContent.none);
        GUILayout.EndHorizontal();

        serializedObject.ApplyModifiedProperties();
    }
}
