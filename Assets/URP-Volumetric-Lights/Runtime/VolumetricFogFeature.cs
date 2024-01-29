using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;


#if UNITY_EDITOR
    using UnityEditor;
    using UnityEngine.SceneManagement;
    using UnityEditor.SceneManagement;
#endif


[DisallowMultipleRendererFeature("Volumetric Fog")]
[Tooltip("Renders active fog volumes as an additive volumetric effect.")]
public class VolumetricFogFeature : ScriptableRendererFeature
{
    public VolumetricResolution resolution;

    public bool temporalRendering = false;
    public bool disableBlur = true;

    [Range(2, 16)] public int temporalResolution = 3;

    private VolumetricFogPass lightPass;

    private Shader bilateralBlur;
    private Shader volumetricFog;
    private Shader reprojection;
    private Shader blitAdd;



    private void CreateLightPass()
    {
        lightPass?.Dispose();

        lightPass = new VolumetricFogPass(this, bilateralBlur, volumetricFog, blitAdd, reprojection) 
        { 
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing,
        };
    }


    public override void Create()
    { 
        try
        {
            ValidateShaders();
        }
        catch (MissingReferenceException)
        {
            // Try again, just in case
            ValidateShaders();
        }

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
        lightPass.Dispose();

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
        AddAlwaysIncludedShader("Hidden/BilateralBlur", ref bilateralBlur);
        AddAlwaysIncludedShader("Hidden/VolumetricFog", ref volumetricFog);
        AddAlwaysIncludedShader("Hidden/BlitAdd", ref blitAdd);
        AddAlwaysIncludedShader("Hidden/TemporalReprojection", ref reprojection);
    }


    // From https://forum.unity.com/threads/modify-always-included-shaders-with-pre-processor.509479/
    static void AddAlwaysIncludedShader(string shaderName, ref Shader shader)
    {
        if (shader != null)
            return;
    
        #if UNITY_EDITOR
            AssetDatabase.Refresh();
        #endif

        shader = Shader.Find(shaderName);
        if (shader == null) 
        {
            string namePart = shaderName.Split('/')[1];

            throw new MissingReferenceException($"{namePart} shader missing! Make sure '{shaderName}' is located somewhere in your project and included in 'Always Included Shaders'");
        }
     
        #if UNITY_EDITOR
            var graphicsSettingsObj = AssetDatabase.LoadAssetAtPath<GraphicsSettings>("ProjectSettings/GraphicsSettings.asset");
            var serializedObject = new SerializedObject(graphicsSettingsObj);
            var arrayProp = serializedObject.FindProperty("m_AlwaysIncludedShaders");
            bool hasShader = false;

            for (int i = 0; i < arrayProp.arraySize; ++i)
            {
                var arrayElem = arrayProp.GetArrayElementAtIndex(i);
                if (shader == arrayElem.objectReferenceValue)
                {
                    hasShader = true;
                    break;
                }
            }

            if (!hasShader)
            {
                int arrayIndex = arrayProp.arraySize;
                arrayProp.InsertArrayElementAtIndex(arrayIndex);
                var arrayElem = arrayProp.GetArrayElementAtIndex(arrayIndex);
                arrayElem.objectReferenceValue = shader;

                serializedObject.ApplyModifiedProperties();

                AssetDatabase.SaveAssets();
            }
        #endif
    }
}
