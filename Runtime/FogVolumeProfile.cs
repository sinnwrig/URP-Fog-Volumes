using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
    using UnityEditor;
    using UnityEngine.SceneManagement;
    using UnityEditor.SceneManagement;
#endif


namespace Sinnwrig.FogVolumes
{
    /// <summary>How light should affect fog volumes.</summary>
    [System.Serializable]
    public enum LightingMode 
    { 
        /// <summary>
        /// Lights do not affect the volume
        /// </summary>
        Unlit,  

        /// <summary>
        /// Only lights affect the volume
        /// </summary>
        Lit,

        /// <summary>
        /// Lights and shadows affect the volume.
        /// </summary>
        Shadowed 
    }

    [CreateAssetMenu(menuName = "Fog Volumes/Volume Profile")]
    [HelpURL("https://github.com/sinnwrig/URP-Fog-Volumes/blob/main/README.md")]
    public class FogVolumeProfile : ScriptableObject
    {
        /// <summary>
        /// The ambient color of the fog when not lit.
        /// </summary>
        [ColorUsage(false, true)]
        public Color ambientColor = Color.white;
        
        /// <summary>
        /// The opacity of the ambient fog. A value of 0 will be only additive. Values higher than 0 will allow fog to influence background color based on density. 
        /// </summary>
        public float ambientOpacity = 1;

        /// <summary>
        /// The base (multiplicative) color of the fog.
        /// </summary>
        [ColorUsage(false, true)]
        public Color albedo = Color.white;
        


        /// <summary>
        /// The thresholds for the step length taken by the raymarcher. Minimum step length is stored in the [x] component. Maximum step length is stored in the [y] component. 
        /// </summary>
        public Vector2 minMaxStepLength = new Vector2(0.5f, 3f);

        /// <summary>
        /// The factor at which the step length taken by the raymarcher is increased.
        /// </summary>
        [Range(1, 2)] 
        public float stepIncrementFactor = 1.1f;

        /// <summary>
        /// The maximum total length of a ray.
        /// </summary>
        [Min(0)] 
        public float maxRayLength = 50.0f;

        /// <summary>
        /// The maximum amount of samples or steps the raymarcher is allowed to take.
        /// </summary>
        [Range(1, 1024)]
        public int maxSampleCount = 36;

        /// <summary>
        /// The intensity of pseudorandom jitter used to offset the starting point of the raymarcher.
        /// </summary>
        public float jitterStrength = 0.05f;



        /// <summary>
        /// The lighting mode of the fog.
        /// </summary>
        public LightingMode lightingMode = LightingMode.Lit;

        /// <summary>
        /// Modulates the strength and intensity of the lights affecting the fog.
        /// </summary>
        [Min(0)] 
        public float lightIntensityModifier = 1;

        /// <summary>
        /// How much light is scattered towards the camera. Higher values will produce brighter fog.
        /// </summary>
        [Range(0, 1)] 
        public float scattering = 0.1f;
        
        /// <summary>
        /// How quickly light power is reduced based on its distance to the camera. Higher values will fade fog further away.
        /// </summary>
        [Range(0, 1)] 
        public float extinction = 0.05f;

        /// <summary>
        /// Determines the distribution of scattered light based on the viewing angle. Higher values will increase how strongly brightness is focused on light position.
        /// </summary>
        [Range(0, 0.999f)] 
        public float mieG = 0.1f;  

        /// <summary>
        /// The value at which the brightness of the fog will be clamped.
        /// </summary>
        [Range(0, 100)] 
        public float brightnessClamp = 10f;



        /// <summary>
        /// The world-space noise to apply to the fog volume.
        /// </summary>
        public Texture3D noiseTexture;

        /// <summary>
        /// The scale of the noise texture.
        /// </summary>
        [Min(0)] 
        public float scale = 0.1f;

        /// <summary>
        /// The direction in which the noise should scroll.
        /// </summary>
        public Vector3 noiseScroll = new Vector3(0, -0.15f, 0);

        /// <summary>
        /// How intensely the noise will affect the fog.
        /// </summary>
        [Range(0, 1)] 
        public float noiseIntensity = 1;

        /// <summary>
        /// The offset applied to the noise texture values.
        /// </summary>
        [Range(0, 1)] 
        public float intensityOffset = 0.5f;



        private Material material;

        private LocalKeyword? noiseKeyword = null;
        private LocalKeyword? lightsKeyword = null;
        private LocalKeyword? shadowsKeyword = null;


        private void OnDisable() => OnDestroy();
        private void OnDestroy() => DestroyImmediate(material);


        public Material GetMaterial(Shader shader, CommandBuffer cmd)
        {
            if (material == null)
                material = new Material(shader);

            noiseKeyword ??= new LocalKeyword(shader, "NOISE_ENABLED");
            lightsKeyword ??= new LocalKeyword(shader, "LIGHTING_ENABLED");
            shadowsKeyword ??= new LocalKeyword(shader, "SHADOWS_ENABLED");

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
            material.SetKeyword(noiseKeyword.Value, noiseTexture != null);

            if (noiseTexture != null)
            {
                // Not sure if its bad to update this multiple times per frame. Think it should be fine though.
                material.SetTexture("_NoiseTexture", noiseTexture);

                material.SetVector("_NoiseVelocity", (-noiseScroll) * scale);
                material.SetVector("_NoiseData", new Vector3(scale, noiseIntensity, intensityOffset));
            }
        }


        private void SetupLighting(CommandBuffer cmd)
        {
            material.SetVector("_Ambient", ambientColor);
            material.SetFloat("_AmbientOpacity", ambientOpacity);

            material.SetVector("_Albedo", albedo);

            material.SetFloat("_IntensityModifier", lightIntensityModifier);
            material.SetVector("_StepParams", new Vector4(minMaxStepLength.x, minMaxStepLength.y, stepIncrementFactor, maxRayLength));
            material.SetInt("_SampleCount", maxSampleCount);
            material.SetFloat("_Jitter", jitterStrength);

            material.SetFloat("_MieG", mieG);
            material.SetFloat("_Scattering", scattering);
            material.SetFloat("_Extinction", extinction);

            material.SetFloat("_BrightnessClamp", brightnessClamp);

            material.SetKeyword(lightsKeyword.Value, lightingMode == LightingMode.Lit);
            material.SetKeyword(shadowsKeyword.Value, lightingMode == LightingMode.Shadowed);
        }
    }
}