using UnityEngine;
using UnityEditor;
using UnityEditor.Rendering;


namespace Sinnwrig.FogVolumes.Editor
{
    [CustomEditor(typeof(FogVolumeProfile))]
    public class FogVolumeProfileEditor : UnityEditor.Editor
    {
        private SerializedProperty fogAlbedo;
        private SerializedProperty intensity;

        private SerializedProperty minMaxStepLength;
        private SerializedProperty stepIncrementFactor;
        private SerializedProperty maxRayLength;

        private SerializedProperty maxSampleCount;
        private SerializedProperty jitterStrength;

        private SerializedProperty lightingMode;
        private SerializedProperty lightIntensityModifier;
        private SerializedProperty scattering;
        private SerializedProperty extinction;
        private SerializedProperty mieG;  
        private SerializedProperty brightnessClamp;

        private SerializedProperty noiseTexture;
        private SerializedProperty scale;
        private SerializedProperty noiseScroll;
        private SerializedProperty noiseIntensity;
        private SerializedProperty intensityOffset;


        static readonly string[] lightingOptions = new string[]
        {
            "No Lighting",
            "Has Lighting",
            "Has Shadows"
        };


        private void OnEnable()
        {
            PropertyFetcher<FogVolume> fetcher = new(serializedObject);

            fogAlbedo = fetcher.Find("fogAlbedo");
            intensity = fetcher.Find("intensity");

            minMaxStepLength = fetcher.Find("minMaxStepLength");
            stepIncrementFactor = fetcher.Find("stepIncrementFactor");
            maxRayLength = fetcher.Find("maxRayLength");

            maxSampleCount = fetcher.Find("maxSampleCount");
            jitterStrength = fetcher.Find("jitterStrength");

            lightingMode = fetcher.Find("lightingMode");
            lightIntensityModifier = fetcher.Find("lightIntensityModifier");
            scattering = fetcher.Find("scattering");
            extinction = fetcher.Find("extinction");

            mieG = fetcher.Find("mieG");
            brightnessClamp = fetcher.Find("brightnessClamp");

            noiseTexture = fetcher.Find("noiseTexture");
            scale = fetcher.Find("scale");
            noiseScroll = fetcher.Find("noiseScroll");
            noiseIntensity = fetcher.Find("noiseIntensity");
            intensityOffset = fetcher.Find("intensityOffset");
        }


        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Appearance", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(fogAlbedo);
            EditorGUILayout.PropertyField(intensity);

            EditorGUILayout.Space(EditorGUIUtility.singleLineHeight * 0.5f);
            EditorGUILayout.LabelField("Raymarching", EditorStyles.boldLabel);

            MinMaxProperty(0, 10, minMaxStepLength, new GUIContent("Step Size Range"));

            EditorGUILayout.PropertyField(stepIncrementFactor);
            EditorGUILayout.PropertyField(maxRayLength);

            EditorGUILayout.PropertyField(maxSampleCount);
            EditorGUILayout.PropertyField(jitterStrength);

            EditorGUILayout.Space(EditorGUIUtility.singleLineHeight * 0.5f);
            EditorGUILayout.LabelField("Lighting", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(lightingMode);

            EditorGUILayout.PropertyField(lightIntensityModifier);
            EditorGUILayout.PropertyField(scattering);
            EditorGUILayout.PropertyField(extinction);

            EditorGUILayout.PropertyField(mieG);
            EditorGUILayout.PropertyField(brightnessClamp);

            EditorGUILayout.Space(EditorGUIUtility.singleLineHeight * 0.5f);
            EditorGUILayout.LabelField("Noise", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(noiseTexture);

            if (noiseTexture.objectReferenceValue != null)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(scale);
                EditorGUILayout.PropertyField(noiseScroll);
                EditorGUILayout.PropertyField(noiseIntensity);
                EditorGUILayout.PropertyField(intensityOffset);
                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();
        }


        private static void MinMaxProperty(float min, float max, SerializedProperty property, GUIContent label, params GUILayoutOption[] options)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(label);

            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
            var v2Val = property.vector2Value;

            v2Val.x = EditorGUILayout.DelayedFloatField(v2Val.x, GUILayout.Width(50f));
            EditorGUILayout.MinMaxSlider(ref v2Val.x, ref v2Val.y, min, max);
            v2Val.y = EditorGUILayout.DelayedFloatField(v2Val.y, GUILayout.Width(50f));

            property.vector2Value = v2Val;

            EditorGUI.showMixedValue = false;
            EditorGUILayout.EndHorizontal();
        }
    }
}