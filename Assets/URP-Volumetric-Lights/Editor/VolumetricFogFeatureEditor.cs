using UnityEngine;
using UnityEditor;
using UnityEditor.Rendering;


[CustomEditor(typeof(VolumetricFogFeature))]
public class VolumetricFogFeatureEditor : Editor
{
    static class Styles
    {
        public static readonly GUIContent resolution = EditorGUIUtility.TrTextContent("Render Resolution", 
            "Sets the resolution the volumetric fog renders at. Non-native resolutions are incompatible with Temporal Reprojection.");
        public static readonly GUIContent reprojection = EditorGUIUtility.TrTextContent("Temporal Reprojection", 
            "Enables Temporal Reprojection, which spreads out rendering over multiple frames. Performance benefit is not guaranteed on older GPUs or renderers that handle branching badly.");
        public static readonly GUIContent disableBlur = EditorGUIUtility.TrTextContent("Disable Blur", 
            "Whether or not to disable the bilateral blur applied to the render result. Only works when rendering at native resolution.");
        public static readonly GUIContent temporalPassCount = EditorGUIUtility.TrTextContent("Temporal Passes", 
            "The number of frames Temporal Reprojection will spread out the rendering the fog volumes.");
    }

    private SerializedProperty resolution;
    private SerializedProperty temporalReprojection;
    private SerializedProperty disableBlur;
    private SerializedProperty temporalPassCount;


    private void OnEnable()
    {
        PropertyFetcher<FogVolume> fetcher = new(serializedObject);

        resolution = fetcher.Find("resolution");
        temporalReprojection = fetcher.Find("temporalReprojection");
        disableBlur = fetcher.Find("disableBlur");
        temporalPassCount = fetcher.Find("temporalPassCount");
    }


    public override void OnInspectorGUI()
    {
        VolumetricFogFeature actualTarget = (VolumetricFogFeature)target;

        serializedObject.Update();

        using var scope = new EditorGUI.ChangeCheckScope();

        bool reprojection = temporalReprojection.boolValue;

        if (!reprojection)
        {
            EditorGUILayout.PropertyField(resolution, Styles.resolution);
        }
        else
        {
            var cached = resolution.enumValueIndex;
            resolution.enumValueIndex = (int)VolumetricResolution.Full;

            GUI.enabled = false;
            EditorGUILayout.PropertyField(resolution, Styles.resolution);
            GUI.enabled = true;

            resolution.enumValueIndex = cached;
        }

        if (!reprojection && resolution.enumValueIndex != (int)VolumetricResolution.Full)
        {
            var cached = disableBlur.boolValue;
            disableBlur.boolValue = false;

            GUI.enabled = false;
            EditorGUILayout.PropertyField(disableBlur, Styles.disableBlur);
            GUI.enabled = true;

            disableBlur.boolValue = cached;
        }   
        else
        {
            EditorGUILayout.PropertyField(disableBlur, Styles.disableBlur);
        }

        EditorGUILayout.PropertyField(temporalReprojection, Styles.reprojection);

        if (reprojection)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(temporalPassCount, Styles.temporalPassCount);
            EditorGUI.indentLevel--;
        }

        // Render Feature detects when properties have been applied regardless of if there was change, and completely refreshes the pass
        // Prevent that by only applying properties when something actually changes
        if (scope.changed)
	        serializedObject.ApplyModifiedProperties();
    }
}
