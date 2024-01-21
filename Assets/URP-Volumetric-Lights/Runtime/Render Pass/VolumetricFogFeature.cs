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

    public bool enableReprojection;
    [Range(1, 24)] public int temporalPassCount;

    private VolumetricFogPass lightPass;

    private Shader bilateralBlur;
    private Shader volumetricFog;


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

        lightPass = new VolumetricFogPass(this, bilateralBlur, volumetricFog) 
        { 
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing,
        };
 
#if UNITY_EDITOR
        EditorSceneManager.activeSceneChangedInEditMode += OnSceneChanged;
#endif
    }

#if UNITY_EDITOR
    // For some reason our custom rendering pass must be refreshed on editor scene change or the output textures will be completely black
    private void OnSceneChanged(Scene a, Scene b)
    { 
        lightPass = new VolumetricFogPass(this, bilateralBlur, volumetricFog) 
        { 
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing,
        };
    }
#endif

    protected override void Dispose(bool disposing)
    {
#if UNITY_EDITOR
        EditorSceneManager.activeSceneChangedInEditMode -= OnSceneChanged;
#endif

        lightPass.Dispose();
    }


    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (!renderingData.cameraData.isPreviewCamera)
        {
            if (enableReprojection)
                lightPass.ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Motion);
            else
                lightPass.ConfigureInput(ScriptableRenderPassInput.Depth);

            renderer.EnqueuePass(lightPass);
        }
    }

    
    // Ehsure both neccesary shaders are included in the GraphicsSettings- to prevent users from having to keep track of references
    void ValidateShaders() 
    {
        if (!AddAlwaysIncludedShader("Hidden/BilateralBlur", ref bilateralBlur))
        {
            throw new MissingReferenceException(
                $"BilateralBlur shader missing! Make sure 'Hidden/BilateralBlur' is located somewhere in your project and included in 'Always Included Shaders'"
            );
        } 

        if (!AddAlwaysIncludedShader("Hidden/VolumetricFog", ref volumetricFog))
        {
            throw new MissingReferenceException(
                $"VolumetricFog shader missing! Make sure 'Hidden/VolumetricFog' is located somewhere in your project and included in 'Always Included Shaders'"
            );
        }
    }


    // From https://forum.unity.com/threads/modify-always-included-shaders-with-pre-processor.509479/
    static bool AddAlwaysIncludedShader(string shaderName, ref Shader shader)
    {
        if (shader != null)
            return true;

        shader = Shader.Find(shaderName);
        if (shader == null) 
            return false;
     
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

        return true;
    }
}
