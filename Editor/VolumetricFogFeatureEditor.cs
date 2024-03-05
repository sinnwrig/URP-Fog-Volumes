using UnityEngine;
using UnityEditor;
using UnityEditor.Rendering;


namespace Sinnwrig.FogVolumes.Editor
{
    [CustomEditor(typeof(VolumetricFogFeature))]
    public class VolumetricFogFeatureEditor : UnityEditor.Editor
    {
        static class Styles
        {
            public static readonly GUIContent resolution = EditorGUIUtility.TrTextContent("Render Resolution", 
                "Sets the resolution the volumetric fog renders at. Non-native resolutions are incompatible with Temporal Rendering.");
            public static readonly GUIContent reprojection = EditorGUIUtility.TrTextContent("Temporal Rendering", 
                "Enables Temporal Rendering, which spreads out rendering volumes over multiple frames.");
            public static readonly GUIContent disableBlur = EditorGUIUtility.TrTextContent("Disable Blur", 
                "Whether or not to disable the bilateral blur applied to the render result. Only works when rendering at native resolution.");
            public static readonly GUIContent temporalResolution = EditorGUIUtility.TrTextContent("Temporal Resolution", 
                "The fractional resolution that the temporal passes should render at. Higher values will improve performance exponentially, but will also introduce ghosting." + 
                "The number of frames it will take to fully render a temporal pass is equal to (resolution ^ 2)");

            public static readonly GUIContent beforeTransparents = EditorGUIUtility.TrTextContent("Before Transparents", "Render the fog before the transaprent object pass");
        }

        private SerializedProperty resolution;
        private SerializedProperty temporalRendering;
        private SerializedProperty disableBlur;
        private SerializedProperty temporalResolution;
        private SerializedProperty beforeTransparents;
        private SerializedProperty data;


        private void OnEnable()
        {
            PropertyFetcher<FogVolume> fetcher = new(serializedObject);

            resolution = fetcher.Find("resolution");
            temporalRendering = fetcher.Find("temporalRendering");
            disableBlur = fetcher.Find("disableBlur");
            temporalResolution = fetcher.Find("temporalResolution");
            beforeTransparents = fetcher.Find("beforeTransparents");
            data = fetcher.Find("data");
        }


        public override void OnInspectorGUI()
        {
            VolumetricFogFeature actualTarget = (VolumetricFogFeature)target;

            serializedObject.Update();

            using var scope = new EditorGUI.ChangeCheckScope();

            bool reprojection = temporalRendering.boolValue;

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

            EditorGUILayout.PropertyField(temporalRendering, Styles.reprojection);

            if (reprojection)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(temporalResolution, Styles.temporalResolution);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.PropertyField(beforeTransparents, Styles.beforeTransparents);

            // Render Feature detects when properties have been applied regardless of if there was change, and completely refreshes the pass
            // Prevent that by only applying properties when something actually changes
            if (scope.changed)
    	        serializedObject.ApplyModifiedProperties();

            GUI.enabled = false;
            EditorGUILayout.PropertyField(data);
            GUI.enabled = true;
        }
    }
}