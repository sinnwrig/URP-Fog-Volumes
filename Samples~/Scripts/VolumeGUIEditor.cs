using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;


public class VolumeGUIEditor : MonoBehaviour
{
    private static string[] sceneNames;
    private static int selectedSceneIndex;

    static UniversalRendererData rendererData;
    static VolumetricFogFeature feature;

    static List<FogVolume> sceneVolumes;


    static void RefreshProps()
    {
        var rp = (UniversalRenderPipelineAsset)QualitySettings.renderPipeline;
        var prop = typeof(UniversalRenderPipelineAsset).GetProperty("scriptableRendererData", BindingFlags.NonPublic | BindingFlags.Instance);
        rendererData = (UniversalRendererData)prop.GetValue(rp);
        feature = (VolumetricFogFeature)rendererData.rendererFeatures.Find(x => x.GetType() == typeof(VolumetricFogFeature));

        sceneVolumes = VolumetricFogPass.ActiveVolumes.ToList();
    }


    [RuntimeInitializeOnLoadMethod]
    static void InitVolumeGUI()
    {
        // Get the names of all scenes in the build settings
        sceneNames = new string[SceneManager.sceneCountInBuildSettings];
        for (int i = 0; i < sceneNames.Length; i++)
            sceneNames[i] = System.IO.Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(i));

        selectedSceneIndex = SceneManager.GetActiveScene().buildIndex;
    }


    // GUI properties
    static string selectedTab = "Volume";
    static string[] tabNames = { "Volume", "Rendering", "Scene", "Camera" };
    static Rect editorRect = new Rect(10, 10, 350, 400);
    bool isOpen;


    // Camera properties
    bool flying = false, camLocked = true;
    Vector3 flyPos, defaultPos;
    Quaternion flyRot, defaultRot;

    static float mainSpeed = 15; 
    static float shiftSpeed = 3;
    static float camSens = 0.5f;


    static int frameCounter;
    static float timeCounter;
    static int lastFps;
    static float avgTime;


    const float sampleTime = 0.25f;


    static void GetFrameCount()
    {   
        if (timeCounter < sampleTime)
        {
            frameCounter++;
            timeCounter += Time.deltaTime;
        }
        else
        {
            lastFps = (int)(frameCounter / sampleTime);
            avgTime = (timeCounter / frameCounter) * 1000;

            timeCounter = 0;
            frameCounter = 0;
        }
    }


    void Start()
    {
        RefreshProps();

        var main = Camera.main.gameObject;

        defaultPos = main.transform.position;
        defaultRot = main.transform.rotation;
        flyPos = defaultPos;
        flyRot = defaultRot;
    }


    void Update()
    {
        GetFrameCount();

        if (!flying)
            return;

        if (!camLocked)
        {
            Vector3 euler = flyRot.eulerAngles;
     
            euler.x -= Input.GetAxis("Mouse Y") * camSens;          
            euler.y += Input.GetAxis("Mouse X") * camSens;   

            flyRot = Quaternion.Euler(euler);
        }

        Vector3 p = GetBaseInput() * mainSpeed;

        if (Input.GetKey(KeyCode.LeftShift))
            p *= shiftSpeed;
       
        p *= Time.deltaTime;
            
        flyPos += flyRot * p;

        var main = Camera.main.transform;
        main.position = flyPos;
        main.rotation = flyRot;

        static Vector3 GetBaseInput() 
        {
            Vector3 velocity = new Vector3();

            if (Input.GetKey (KeyCode.W))
                velocity += new Vector3(0, 0 , 1);
            else if (Input.GetKey (KeyCode.S))
                velocity += new Vector3(0, 0, -1);

            if (Input.GetKey (KeyCode.A))
                velocity += new Vector3(-1, 0, 0);
            else if (Input.GetKey (KeyCode.D))
                velocity += new Vector3(1, 0, 0);

            if (Input.GetKey (KeyCode.Q))
                velocity += new Vector3(0, -1, 0);
            else if (Input.GetKey (KeyCode.E))
                velocity += new Vector3(0, 1, 0);

            return velocity;
        }
    }


    void OnGUI()
    {
        editorRect = GUILayout.Window(0, editorRect, DrawResizableBox, "Volume GUI Editor");

        if (!flying)
            return;

        Event ev = Event.current;

        if (!editorRect.Contains(ev.mousePosition))
        {
            if (ev.type == EventType.MouseDown && ev.button == 0)
            {
                ev.Use();
                Cursor.lockState = CursorLockMode.Locked;
                camLocked = false;
            }
            else if (ev.type == EventType.KeyDown && ev.keyCode == KeyCode.Escape)
            {
                ev.Use();
                Cursor.lockState = CursorLockMode.None;
                camLocked = true;
            }
        }
    }


    void DrawResizableBox(int windowID)
    {
        GUILayout.BeginHorizontal();

        GUIStyle buttonNormal = GUI.skin.FindStyle("Button");
        GUIStyle buttonPressed = new GUIStyle(buttonNormal);
        buttonPressed.normal = buttonPressed.onActive;
        buttonPressed.active = buttonPressed.onActive;
        buttonPressed.hover = buttonPressed.onActive;
        buttonPressed.focused = buttonPressed.onActive;

        for (int i = 0; i < tabNames.Length; i++)
        {
            if (GUILayout.Button(tabNames[i], tabNames[i] == selectedTab ? buttonPressed : buttonNormal))
                selectedTab = tabNames[i];
        }

        GUILayout.EndHorizontal();

        switch (selectedTab)
        {
            case "Volume":
                DrawVolumeEditor();
            break;

            case "Rendering":
                DrawRenderingEditor();
            break;

            case "Scene":
                DrawSceneSelection();
            break;

            case "Camera":
                DrawCameraEditor();
            break;
        }

        GUI.DragWindow();
    }


    void DrawCameraEditor()
    {
        GUILayout.BeginHorizontal();

        var main = Camera.main.gameObject;

        string buttonText = flying ? "Reset" : "Free Fly";

        if (GUILayout.Button(buttonText))
        {
            flying = !flying;

            if (!flying)
            {
                flyPos = defaultPos;
                flyRot = defaultRot;
                main.transform.position = defaultPos;
                main.transform.rotation = defaultRot;
            }   
            else 
            {
                defaultPos = main.transform.position;
                defaultRot = main.transform.rotation;
                flyPos = defaultPos;
                flyRot = defaultRot;
            }
        }

        GUILayout.EndHorizontal();
            
        bool fs = false;

        mainSpeed = Slider(mainSpeed, 0, 50, new GUIContent("Move Speed"), ref fs);
        shiftSpeed = Slider(shiftSpeed, 0, 5, new GUIContent("Boost Speed"), ref fs);

        camSens = Slider(camSens, 0, 10, new GUIContent("Look Sensitivity"), ref fs);
    }


    void DrawVolumeEditor()
    {
        for (int i = 0; i < sceneVolumes.Count; i++)
        {
            GameObject fVolume = sceneVolumes[i].gameObject;

            GUILayout.BeginHorizontal();

            GUILayout.Label(fVolume.name, GUILayout.Width(160f));

            bool active = fVolume.activeSelf;

            active = GUILayout.Toggle(active, GUIContent.none);

            if (fVolume.activeSelf != active)
                fVolume.SetActive(active);
        
            GUILayout.EndHorizontal();
        }
    }


    void DrawRenderingEditor()
    {
        GUILayout.Label(new GUIContent($"Render time: {lastFps}FPS ({avgTime:0.0}ms)"));

        if (feature == null) 
            return;

        bool isDirty = false;

        if (!feature.temporalRendering)
        {
            feature.resolution = EnumDropown("Render Resolution", feature.resolution, ref isOpen, ref isDirty, GUILayout.Width(180f));
        }
        else
        {
            GUI.enabled = false;
            EnumDropown("Render Resolution", VolumetricResolution.Full, ref isOpen, ref isDirty, GUILayout.Width(180f));
            GUI.enabled = true;   
        }

        if (feature.resolution == VolumetricResolution.Full || feature.temporalRendering)
        {
            feature.disableBlur = InvertedToggle(feature.disableBlur, new GUIContent("Disable Blur"), ref isDirty, GUILayout.Width(160f));
        }
        else
        {
            GUI.enabled = false;
            InvertedToggle(false, new GUIContent("Disable Blur"), ref isDirty, GUILayout.Width(160f));
            GUI.enabled = true;
        }


        feature.temporalRendering = InvertedToggle(feature.temporalRendering, new GUIContent("Temporal Reprojection"), ref isDirty, GUILayout.Width(160f));

        if (feature.temporalRendering)
        {
            feature.temporalResolution = (int)Slider(feature.temporalResolution, 2, 10, new GUIContent("Temporal Size"), ref isDirty, GUILayout.Width(160f));
        }

        if (isDirty)
            rendererData.SetDirty();
    }


    void DrawSceneSelection()
    {
        for (int i = 0; i < sceneNames.Length; i++)
        {
            GUILayout.BeginHorizontal();

            GUI.enabled = true;
            GUILayout.Label(sceneNames[i]);

            GUI.enabled = i != selectedSceneIndex;

            if (GUILayout.Button(i == selectedSceneIndex ? "Loaded" : "Load", GUILayout.Width(75)))
            {
                selectedSceneIndex = i;
                SceneManager.LoadScene(i);
            }

            GUILayout.EndHorizontal();
        }

        GUI.enabled = true;
    }


    static T EnumDropown<T>(string nameField, T value, ref bool isOpen, ref bool isDirty, params GUILayoutOption[] layoutOptions) where T : Enum
    {
        GUILayout.BeginHorizontal();

        GUILayout.Label(nameField, layoutOptions);

        if (GUILayout.Button(value.ToString()))
            isOpen = !isOpen;

        Rect buttonRect = GUILayoutUtility.GetLastRect();

        GUILayout.EndHorizontal();

        if (isOpen)
        {
            Array enums = Enum.GetValues(typeof(T));

            Rect boxRect = buttonRect;
            boxRect.y += buttonRect.height;

            boxRect.height = enums.Length * buttonRect.height + 5f;

            GUI.Box(boxRect, GUIContent.none);

            Rect optionRect = buttonRect;
            optionRect.height -= 2.5f;
            optionRect.width -= 5;
            optionRect.x += 2.5f;

            for (int i = 0; i < enums.Length; i++)
            {
                optionRect.y = boxRect.y + (buttonRect.height * i) + 5;

                T option = (T)enums.GetValue(i);

                if (GUI.Button(optionRect, option.ToString()))
                {
                    isDirty = !isDirty && !value.Equals(option);
                    value = option;
                    isOpen = false;
                }
            }

            if (Event.current.type == EventType.MouseDown && !boxRect.Contains(Event.current.mousePosition) && Event.current.button == 0)
            {
                isOpen = false;
                Event.current.Use();
            }
        }

        return value;
    }


    static bool InvertedToggle(bool value, GUIContent content, ref bool isDirty, params GUILayoutOption[] layoutOptions)
    {
        GUILayout.BeginHorizontal();

        GUILayout.Label(content, layoutOptions);

        bool newValue = GUILayout.Toggle(value, GUIContent.none);

        GUILayout.EndHorizontal();

        isDirty = !isDirty && value != newValue;
        return newValue;
    }


    static float Slider(float value, float leftValue, float rightValue, GUIContent content, ref bool isDirty, params GUILayoutOption[] layoutOptions)
    {
        GUILayout.BeginHorizontal();

        GUILayout.Label(content, layoutOptions);

        float newValue = GUILayout.HorizontalSlider(value, leftValue, rightValue);

        GUILayout.EndHorizontal();

        isDirty = !isDirty && newValue != value;
    
        return newValue;
    }
}
