using UnityEngine;
using UnityEditor;
using UnityEditor.Rendering;
using System.IO;



[CustomEditor(typeof(FogVolume))]
public class FogVolumeEditor : Editor
{
    static class Styles
    {
        public static readonly GUIContent profileInstance = EditorGUIUtility.TrTextContent("Profile (Instance)", "A Fog Volume Profile is a Scriptable Object which defines how Fog Volumes draw fog in the scene.");
        public static readonly GUIContent profile = EditorGUIUtility.TrTextContent("Profile", "A Fog Volume Profile is a Scriptable Object which defines how Fog Volumes draw fog in the scene.");
        public static readonly GUIContent newLabel = EditorGUIUtility.TrTextContent("New", "Create a new profile.");
        public static readonly GUIContent saveLabel = EditorGUIUtility.TrTextContent("Save", "Save the instantiated profile");
        public static readonly GUIContent cloneLabel = EditorGUIUtility.TrTextContent("Clone", "Create a new profile and copy the content of the currently assigned profile.");
        public static readonly string noVolumeMessage = L10n.Tr("Please select or create a new Volume profile to begin applying effects to the scene.");
    }

 
    private SerializedProperty profile;
    private SerializedProperty volumeType;
    private SerializedProperty edgeFade;
    private SerializedProperty maxDistance;
    private SerializedProperty distanceFade;

    private Editor profileEditor;


    void OnEnable()
    {
        profile = serializedObject.FindProperty("sharedProfile");

        profileEditor = null;
        if (profile.objectReferenceValue != null)
            profileEditor = CreateEditor(profile.objectReferenceValue);

        volumeType = serializedObject.FindProperty("volumeType");
        edgeFade = serializedObject.FindProperty("edgeFade");
        maxDistance = serializedObject.FindProperty("maxDistance");
        distanceFade = serializedObject.FindProperty("distanceFade");
    }


    public override void OnInspectorGUI()
    {
        FogVolume actualTarget = (FogVolume)target;

        serializedObject.Update();

        EditorGUILayout.PropertyField(volumeType);

        DrawFadeField();

        EditorGUILayout.PropertyField(maxDistance);
        EditorGUILayout.PropertyField(distanceFade);

        bool assetHasChanged = DrawProfileField(actualTarget);

        profile.isExpanded = DrawHeaderToggleFoldout(new GUIContent("Volume Profile"), 5f, profile.isExpanded);

        if (profile.isExpanded)
        {
            DrawProfileEditor(actualTarget, assetHasChanged);   
        } 

	    serializedObject.ApplyModifiedProperties();

        if (profile.objectReferenceValue == null)
            EditorGUILayout.HelpBox(Styles.noVolumeMessage, MessageType.Info);
    }


    void OnSceneGUI()
    {
        FogVolume volume = (FogVolume)target;

        Matrix4x4 trsMatrix = volume.transform.localToWorldMatrix;
        Handles.matrix = trsMatrix;

        Handles.color = Color.gray;

        Bounds bounds = volume.GetBounds();
        Handles.DrawWireCube(bounds.center, bounds.size);

        Handles.color = volume.profileReference == null ? Color.red : Color.white;

        switch (volume.volumeType)
        {
            case VolumeType.Sphere:
                HandleExtensions.DrawWireSphere(trsMatrix);
            break;

            case VolumeType.Cube:
                HandleExtensions.DrawWireCube(trsMatrix);
            break;

            case VolumeType.Capsule:
                HandleExtensions.DrawWireCapsule(trsMatrix);
            break;

            case VolumeType.Cylinder:
                HandleExtensions.DrawWireCylinder(trsMatrix);
            break;
        }
    }


    private void DrawFadeField()
    {
        using var scope = new EditorGUI.ChangeCheckScope();

        Vector3 fade = edgeFade.vector3Value;

        if ((VolumeType)volumeType.enumValueFlag == VolumeType.Cube)
        {
            fade.x = EditorGUILayout.Slider("X Side Fade", fade.x, 0, 1);
            fade.y = EditorGUILayout.Slider("Y Side Fade", fade.y, 0, 1);
            fade.z = EditorGUILayout.Slider("Z Side Fade", fade.z, 0, 1);
        }
        else if ((VolumeType)volumeType.enumValueFlag == VolumeType.Cylinder)
        {
            fade.x = EditorGUILayout.Slider("Radius Fade", fade.x, 0, 1);
            fade.y = EditorGUILayout.Slider("Height Fade", fade.y, 0, 1);
        }
        else
        {
            fade.x = EditorGUILayout.Slider("Radius Fade", fade.x, 0, 1);
        }

        if (scope.changed)
            edgeFade.vector3Value = fade;
    }


    private bool DrawProfileField(FogVolume actualTarget)
    {
        bool showCopy = profile.objectReferenceValue != null;

        // The layout system breaks alignment when mixing inspector fields with custom layout'd fields, do the layout manually instead
        int buttonWidth = showCopy ? 45 : 60;
        float indentOffset = EditorGUI.indentLevel * 15f;
        
        var lineRect = EditorGUILayout.GetControlRect();
        var labelRect = new Rect(lineRect.x, lineRect.y, EditorGUIUtility.labelWidth - indentOffset - 3, lineRect.height);
        var fieldRect = new Rect(labelRect.xMax + 5, lineRect.y, lineRect.width - labelRect.width - buttonWidth * (showCopy ? 2 : 1) - 5, lineRect.height);
        var buttonNewRect = new Rect(fieldRect.xMax, lineRect.y, buttonWidth, lineRect.height);
        var buttonCopyRect = new Rect(buttonNewRect.xMax, lineRect.y, buttonWidth, lineRect.height);

        using (var scope = new EditorGUI.ChangeCheckScope())
        {
            var isProfileInstance = actualTarget.HasInstantiatedProfile();
            FogVolumeProfile editedProfile;
            
            if (isProfileInstance)
            {
                var prevMixedValueState = EditorGUI.showMixedValue;
                EditorGUI.showMixedValue = profile.hasMultipleDifferentValues;
                EditorGUI.PrefixLabel(labelRect, Styles.profileInstance);
                editedProfile = (FogVolumeProfile)EditorGUI.ObjectField(fieldRect, actualTarget.profile, typeof(FogVolumeProfile), false);
                EditorGUI.showMixedValue = prevMixedValueState;
            }
            else
            {
                fieldRect = new Rect(labelRect.x, labelRect.y, labelRect.width + fieldRect.width, fieldRect.height);
                EditorGUI.ObjectField(fieldRect, profile, Styles.profile);
                editedProfile = (FogVolumeProfile)profile.objectReferenceValue;
            }

            if (scope.changed)
            {
                if (isProfileInstance)
                    actualTarget.profile = null; // Clear the instantiated profile, from now on we're using shared again
                else
                    profile.objectReferenceValue = editedProfile;

                return true;
            }
        }

        if (GUI.Button(buttonNewRect, Styles.newLabel, showCopy ? EditorStyles.miniButtonLeft : EditorStyles.miniButton))
        {
            profile.objectReferenceValue = InstantiateProfile(actualTarget);
            actualTarget.profile = null; // Make sure we're not using an instantiated profile anymore
            return true;
        }

        if (showCopy && GUI.Button(buttonCopyRect, actualTarget.HasInstantiatedProfile() ? Styles.saveLabel : Styles.cloneLabel, EditorStyles.miniButtonRight))
        {
            profile.objectReferenceValue = CopyProfile(actualTarget, profile.objectReferenceValue);
            actualTarget.profile = null; // Make sure we're not using an instantiated profile anymore
            return true;
        }


        return false;
    }


    private FogVolumeProfile InstantiateProfile(FogVolume target)
    {
        var targetName = target.gameObject.name + " Profile";
        var scene = target.gameObject.scene;

        string path;

        if (string.IsNullOrEmpty(scene.path))
        {
            path = "Assets/";
        }
        else
        {
            var scenePath = Path.GetDirectoryName(scene.path);
            var extPath = scene.name;
            var profilePath = scenePath + Path.DirectorySeparatorChar + extPath;

            if (!AssetDatabase.IsValidFolder(profilePath))
            {
                var directories = profilePath.Split(Path.DirectorySeparatorChar);
                string rootPath = "";
                foreach (var directory in directories)
                {
                    var newPath = rootPath + directory;
                    if (!AssetDatabase.IsValidFolder(newPath))
                        AssetDatabase.CreateFolder(rootPath.TrimEnd(Path.DirectorySeparatorChar), directory);
                    rootPath = newPath + Path.DirectorySeparatorChar;
                }
            }

            path = profilePath + Path.DirectorySeparatorChar;
        }

        path += targetName.ReplaceInvalidFileNameCharacters() + ".asset";
        path = AssetDatabase.GenerateUniqueAssetPath(path);

        var profile = CreateInstance<FogVolumeProfile>();
        AssetDatabase.CreateAsset(profile, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return profile;
    }


    private FogVolumeProfile CopyProfile(FogVolume origin, Object pathObj)
    {
        var path = AssetDatabase.GetAssetPath(pathObj);

        path = AssetDatabase.GenerateUniqueAssetPath(path);

        var asset = Instantiate(origin.profileReference);
        AssetDatabase.CreateAsset(asset, path);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        return asset;
    }


    private void DrawProfileEditor(FogVolume actualTarget, bool assetHasChanged)
    {
        if (profile.objectReferenceValue == null && !actualTarget.HasInstantiatedProfile())
        {
            if (assetHasChanged)
                profileEditor = null;
        }
        else
        {
            if (assetHasChanged || actualTarget.profileReference != profileEditor?.target)
            {
                serializedObject.ApplyModifiedProperties();
                serializedObject.Update();

                profileEditor = null;
                
                if (actualTarget.profileReference != null)
                    profileEditor = CreateEditor(actualTarget.profileReference);
            }

            profileEditor?.OnInspectorGUI();
            EditorGUILayout.Space();
        }

        if (actualTarget.sharedProfile == null && profile.objectReferenceValue != null)
        {
            if (Event.current.type != EventType.Layout)
            {
                actualTarget.sharedProfile = (FogVolumeProfile)profile.objectReferenceValue;
                if (actualTarget.HasInstantiatedProfile())
                    actualTarget.profile = null;
                
                if (actualTarget.sharedProfile != null)
                    profileEditor = CreateEditor(actualTarget.sharedProfile);
            }
        }
    }


    private static void GetHeaderToggleRects(float space, out Rect labelRect, out Rect foldoutRect, out Rect backgroundRect)
    {
        backgroundRect = EditorGUI.IndentedRect(GUILayoutUtility.GetRect(1f, space + 17f));

        backgroundRect.y += space;
        backgroundRect.height -= space;

        labelRect = backgroundRect;
        labelRect.xMin += 32f;
        labelRect.xMax -= 20f + 16 + 5;
        foldoutRect = backgroundRect;
        foldoutRect.y += 1f;
        foldoutRect.width = 13f;
        foldoutRect.height = 13f;

        // Background rect should be full-width
        backgroundRect.xMin = 0f;
        backgroundRect.width += 4f;
    }


    private static void DrawBackground(Rect backgroundRect, bool bottomLine)
    {
        Rect edgeRect = new Rect(backgroundRect.x, backgroundRect.y - 1f, backgroundRect.width, 1f);
        float backgroundTint = 0f;
        EditorGUI.DrawRect(edgeRect, new Color(backgroundTint, backgroundTint, backgroundTint, 1f));

        backgroundTint = EditorGUIUtility.isProSkin ? 0.1f : 1f;
        EditorGUI.DrawRect(backgroundRect, new Color(backgroundTint, backgroundTint, backgroundTint, 0.2f));

        if (!bottomLine)
            return;
        
        edgeRect.y = backgroundRect.y + backgroundRect.height;
        backgroundTint = 0f;
        EditorGUI.DrawRect(edgeRect, new Color(backgroundTint, backgroundTint, backgroundTint, 1f));
    }

    public static bool DrawHeaderToggleFoldout(GUIContent title, float space, bool foldoutExpanded)
    {
        GetHeaderToggleRects(space, out Rect labelRect, out Rect foldoutRect, out Rect backgroundRect);

        DrawBackground(backgroundRect, !foldoutExpanded);

        EditorGUI.LabelField(labelRect, title, EditorStyles.boldLabel);

        bool expanded = GUI.Toggle(foldoutRect, foldoutExpanded, GUIContent.none, EditorStyles.foldout);

        if (Event.current.type == EventType.MouseDown)
        {
            if (backgroundRect.Contains(Event.current.mousePosition))
            {
                // Left click: Expand/Collapse
                if (Event.current.button == 0)
                    expanded = !expanded;
                    
                Event.current.Use();
            }
        }

        return expanded;
    }
}
