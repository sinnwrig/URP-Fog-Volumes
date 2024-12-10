using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;


#if UNITY_EDITOR
    using UnityEditor;
    using UnityEngine.SceneManagement;
    using UnityEditor.SceneManagement;
#endif


public class VolumetricFogFeature : ScriptableRendererFeature
{
    public VolumetricFogPass.VolumetricResolution resolution;

    public bool enableReprojection = false;
    public bool reprojectionBlur = true;
    [Range(1, 24)] public int temporalPassCount;

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
        if (!renderingData.cameraData.isPreviewCamera)
        {
            lightPass.ConfigureInput(ScriptableRenderPassInput.Depth);

            renderer.EnqueuePass(lightPass);
        }
    }

    
    // Ehsure both neccesary shaders are included in the GraphicsSettings- to prevent users from having to keep track of references
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
