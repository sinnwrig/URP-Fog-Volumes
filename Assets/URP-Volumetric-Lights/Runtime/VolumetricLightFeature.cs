using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;


#if UNITY_EDITOR
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
#endif


public class VolumetricLightFeature : ScriptableRendererFeature
{
    public VolumetricResolution resolution;
    public float lightRange;
    public float falloffRange;


    public bool noise = true;
    public Texture3D noiseTexture;


    private VolumetricLightPass lightPass;
    private Shader bilateralBlur;
    private Shader volumetricLight;

    public bool createTex = false;


    public override void Create()
    {
        ValidateShaders();

        lightPass = new VolumetricLightPass(this, bilateralBlur, volumetricLight) 
        { 
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing,
        };

#if UNITY_EDITOR
        if (noise && noiseTexture == null)
        {
            string[] assets = AssetDatabase.FindAssets("LightNoiseTexture");

            if (assets.Length > 0)
                noiseTexture = AssetDatabase.LoadAssetAtPath<Texture3D>(AssetDatabase.GUIDToAssetPath(assets[0]));
            else    
                Debug.LogWarning("Volumetric Light Feature is missing noise texture while noise is enabled - noise will not be applied in shader.", this);
        }

        EditorSceneManager.activeSceneChangedInEditMode += OnSceneChanged;
#endif
    }

#if UNITY_EDITOR
    // For some reason light pass must be refreshed on editor scene changes or else output color will be completely black
    private void OnSceneChanged(Scene a, Scene b)
    { 
        lightPass = new VolumetricLightPass(this, bilateralBlur, volumetricLight) 
        { 
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing,
        };
    }

    protected override void Dispose(bool disposing)
    {
        EditorSceneManager.activeSceneChangedInEditMode -= OnSceneChanged;
    }
#endif


    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (!renderingData.cameraData.isPreviewCamera)
        {
            lightPass.ConfigureInput(ScriptableRenderPassInput.Depth);
            renderer.EnqueuePass(lightPass);
        }
    }


    void ValidateShaders() 
    {
        if (!AddAlwaysIncludedShader("Hidden/BilateralBlur", ref bilateralBlur)) 
            Debug.LogError($"BilateralBlur shader missing! Make sure 'Hidden/BilateralBlur' is located somewhere in your project and included in 'Always Included Shaders'", this);

        if (!AddAlwaysIncludedShader("Hidden/VolumetricLight", ref volumetricLight))
            Debug.LogError($"VolumetricLight shader missing! Make sure 'Hidden/VolumetricLight' is located somewhere in your project and included in 'Always Included Shaders'", this);
    }


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
