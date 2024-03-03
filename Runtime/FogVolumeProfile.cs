using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
    using UnityEditor;
    using UnityEngine.SceneManagement;
    using UnityEditor.SceneManagement;
#endif


namespace Sinnwrig.FogVolumes
{

    public enum LightingMode { Unlit, Lit, Shadowed }

    [CreateAssetMenu(menuName = "Fog Volumes/Volume Profile")]
    public class FogVolumeProfile : ScriptableObject
    {
        // Appearance 

        [ColorUsage(false, false)]
        public Color ambientColor = Color.white;
         
        public float ambientIntensity = 1;

        [ColorUsage(false, true)]
        public Color fogAlbedo = Color.white;
        

        // Ray settings

        public Vector2 minMaxStepLength = new Vector2(0.5f, 3f);

        [Range(1, 2)] 
        public float stepIncrementFactor = 1.1f;

        [Min(0)] 
        public float maxRayLength = 50.0f;

        [Range(1, 1024)]
        public int maxSampleCount = 36;

        public float jitterStrength = 0.05f;


        // Lighting

        public LightingMode lightingMode = LightingMode.Lit;

        [Min(0)] 
        public float lightIntensityModifier = 1;

        [Range(0, 1)] 
        public float scattering = 0.1f;

        [Range(0, 1)] 
        public float extinction = 0.05f;

        [Range(0, 0.999f)] 
        public float mieG = 0.1f;  

        [Range(0, 100)] 
        public float brightnessClamp = 10f;

        // Noise

        public Texture3D noiseTexture;

        [Min(0)] 
        public float scale = 0.1f;

        public Vector3 noiseScroll = new Vector3(0, -0.15f, 0);

        [Range(0, 1)] 
        public float noiseIntensity = 1;

        [Range(0, 1)] 
        public float intensityOffset = 0.5f;


        private Material material;


        private GlobalKeyword? noise = null;
        private GlobalKeyword? light = null;
        private GlobalKeyword? shadow = null;


        private void OnDisable() => OnDestroy();
        private void OnDestroy() => DestroyImmediate(material);


        public Material GetMaterial(Shader shader, CommandBuffer cmd)
        {
            if (material == null)
                material = new Material(shader);

            noise ??= GlobalKeyword.Create("NOISE_ENABLED");
            light ??= GlobalKeyword.Create("LIGHTING_ENABLED");
            shadow ??= GlobalKeyword.Create("SHADOWS_ENABLED");

            SetupProperties(cmd);

            return material;
        }


        public void SetupProperties(CommandBuffer cmd)
        {
            SetupLighting(cmd);
            SetupNoise(cmd);
        }


        private void SetupNoise(CommandBuffer cmd)
        {
            cmd.SetKeyword(noise.Value, noiseTexture != null);

            if (noiseTexture != null)
            {
                material.SetTexture("_NoiseTexture", noiseTexture);
                material.SetVector("_NoiseVelocity", (-noiseScroll) * scale);
                material.SetVector("_NoiseData", new Vector3(scale, noiseIntensity, intensityOffset));
            }
        }


        private void SetupLighting(CommandBuffer cmd)
        {
            material.SetVector("_Ambient", ambientColor);
            material.SetFloat("_Intensity", ambientIntensity);

            material.SetVector("_Albedo", fogAlbedo);

            material.SetFloat("_IntensityModifier", lightIntensityModifier);
            material.SetVector("_StepParams", new Vector4(minMaxStepLength.x, minMaxStepLength.y, stepIncrementFactor, maxRayLength));
            material.SetInt("_SampleCount", maxSampleCount);
            material.SetFloat("_Jitter", jitterStrength);

            material.SetFloat("_MieG", mieG);
            material.SetFloat("_Scattering", scattering);
            material.SetFloat("_Extinction", extinction);

            material.SetFloat("_BrightnessClamp", brightnessClamp);

            cmd.SetKeyword(light.Value, lightingMode == LightingMode.Lit);
            cmd.SetKeyword(shadow.Value, lightingMode == LightingMode.Shadowed);
        }
    }
}