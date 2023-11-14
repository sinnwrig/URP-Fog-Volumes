using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System;
using System.Collections;
using System.Collections.Generic;


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


    public Texture3D noiseTex;
    public bool noise = true;


    private VolumetricLightPass lightPass;
    private Shader bilateralBlur;
    private Shader volumetricLight;


    public static IEnumerable<ReadOnlyMemory<char>> SplitInParts(string s, int partLength)
    {
        if (s == null)
            throw new ArgumentNullException(nameof(s));
        if (partLength <= 0)
            throw new ArgumentException("Part length has to be positive.", nameof(partLength));

        for (var i = 0; i < s.Length; i += partLength)
            yield return s.AsMemory().Slice(i, Math.Min(partLength, s.Length - i));
    }


    public override void Create()
    {
        ValidateShaders();

        lightPass = new VolumetricLightPass(this, bilateralBlur, volumetricLight) 
        { 
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing,
        };


        if (noiseTex == null)
        {
            int len = texString.Length / 6;

            Color[] colors = new Color[len];

            int iter = 0;
            foreach (ReadOnlyMemory<char> val in SplitInParts(texString, 6))
            {   
                ColorUtility.TryParseHtmlString(val.ToString(), out colors[iter]);
                iter++;
            }
        }

#if UNITY_EDITOR
        EditorSceneManager.activeSceneChangedInEditMode += OnSceneChanged;
#endif
    }

#if UNITY_EDITOR
    // For some reason, light pass must be refreshed on editor scene changes or else output will be complete black
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
        bilateralBlur = AddAlwaysIncludedShader("Hidden/BilateralBlur");
        volumetricLight = AddAlwaysIncludedShader("Hidden/VolumetricLight");

        if (bilateralBlur == null) 
            Debug.LogError($"BilateralBlur shader missing! Make sure 'Hidden/BilateralBlur' is located somewhere in your project and included in 'Always Included Shaders'", this);

        if (volumetricLight == null)
            Debug.LogError($"VolumetricLight shader missing! Make sure 'Hidden/VolumetricLight' is located somewhere in your project and included in 'Always Included Shaders'", this);
    }


    static Shader AddAlwaysIncludedShader(string shaderName)
    {
        var shader = Shader.Find(shaderName);
        if (shader == null) 
        {
            return null;
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

        return shader;
    }

    string texString = "";
}
