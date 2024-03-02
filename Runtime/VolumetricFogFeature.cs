using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;


#if UNITY_EDITOR
    using UnityEditor;
    using UnityEngine.SceneManagement;
    using UnityEditor.SceneManagement;
#endif

namespace Sinnwrig.FogVolumes
{
    [DisallowMultipleRendererFeature("Volumetric Fog")]
    [Tooltip("Renders active fog volumes as an additive volumetric effect.")]
    public class VolumetricFogFeature : ScriptableRendererFeature
    {
        public VolumetricResolution resolution;

        public bool temporalRendering = false;
        public bool disableBlur = true;

        [Range(2, 16)] public int temporalResolution = 3;

        private VolumetricFogPass lightPass;

        public FogVolumeData data;



        private void CreateLightPass()
        {
            lightPass?.Dispose();

            lightPass = new VolumetricFogPass(this, data.bilateralBlur, data.volumetricFog, data.blitAdd, data.reprojection) 
            { 
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing,
            };
        }


        public override void Create()
        { 
            ValidateShaders();
            CreateLightPass();
    
            #if UNITY_EDITOR
                EditorSceneManager.activeSceneChangedInEditMode += OnSceneChanged;
            #endif
        }

        #if UNITY_EDITOR
            private void OnSceneChanged(Scene a, Scene b) => CreateLightPass();
        #endif

        protected override void Dispose(bool disposing)
        {
            lightPass?.Dispose();

            #if UNITY_EDITOR
                EditorSceneManager.activeSceneChangedInEditMode -= OnSceneChanged;
            #endif
        }


        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            // Don't draw in material previews
            if (!renderingData.cameraData.isPreviewCamera)
            {
                lightPass.ConfigureInput(ScriptableRenderPassInput.Depth);
                
                renderer.EnqueuePass(lightPass);
            }
        }


        // Ehsure all neccesary shaders are included in the project's GraphicsSettings- to prevent having to do manual reference tracking
        private void ValidateShaders() 
        {
            if (data == null)
                data = AssetDatabase.LoadAssetAtPath<FogVolumeData>("Packages/com.sinnwrig.fogvolumes/Runtime/Data/FogVolumeData.asset");

            if (data == null)
                Debug.LogError("Could not find fog volume data. Fog will not render");
        }
    }
}