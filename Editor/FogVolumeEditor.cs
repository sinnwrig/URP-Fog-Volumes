using UnityEngine;
using UnityEditor;
using UnityEditor.Rendering;
using System.IO;

// Ripped from the SRP Volume component editor with significant changes. 
namespace Sinnwrig.FogVolumes.Editor
{
    [CustomEditor(typeof(FogVolume))]
    public class FogVolumeEditor : UnityEditor.Editor
    {
        static class Styles
        {
            public static readonly GUIContent volumeType = EditorGUIUtility.TrTextContent("Volume Type", "Defines the primitive shape of the volume. Shape is affected by the object transform");

            public static readonly GUIContent radiusFade = EditorGUIUtility.TrTextContent("Radius Fade", "Defines how intensely the fog will fade towards the edge of the volume.");
            public static readonly GUIContent edgeFadeX = EditorGUIUtility.TrTextContent("Edge Fade X", radiusFade.tooltip);
            public static readonly GUIContent edgeFadeY = EditorGUIUtility.TrTextContent("Edge Fade Y", radiusFade.tooltip);
            public static readonly GUIContent edgeFadeZ = EditorGUIUtility.TrTextContent("Edge Fade Z", radiusFade.tooltip);
            public static readonly GUIContent heightFade = EditorGUIUtility.TrTextContent("Height Fade", radiusFade.tooltip);

            public static readonly GUIContent maxDistance = EditorGUIUtility.TrTextContent("Max Distance", "Maximum distance the Fog Volume will draw at before being culled");
            public static readonly GUIContent distanceFade = EditorGUIUtility.TrTextContent("Distance Fade", "At what percentage from the maximum distance will the fog begin to fade out");

            public static readonly GUIContent lightLayerMask = EditorGUIUtility.TrTextContent("Light Mask", "This volume will only be affected by lights in the selected scene-layers");
            public static readonly GUIContent disableLightLimit = EditorGUIUtility.TrTextContent("Disable Light Limit", "Disable the per-object light limit for this volume");

            public static readonly GUIContent profile = EditorGUIUtility.TrTextContent("Profile", "A Fog Volume Profile is a Scriptable Object which defines how Fog Volumes draw fog in the scene.");
            public static readonly GUIContent profileInstance = EditorGUIUtility.TrTextContent("Profile (Instance)", profile.tooltip);
            public static readonly GUIContent newLabel = EditorGUIUtility.TrTextContent("New", "Create a new profile.");
            public static readonly GUIContent saveLabel = EditorGUIUtility.TrTextContent("Save", "Save the instantiated profile");
            public static readonly GUIContent cloneLabel = EditorGUIUtility.TrTextContent("Clone", "Create a new profile and copy the content of the currently assigned profile.");
            public static readonly string noVolumeMessage = L10n.Tr("Please select or create a new Fog Volume profile to begin applying effects to the scene.");
        }

    
        private SerializedProperty profile;
        private SerializedProperty volumeType;
        private SerializedProperty edgeFade;
        private SerializedProperty maxDistance;
        private SerializedProperty distanceFade;
        private SerializedProperty lightLayerMask;
        private SerializedProperty disableLightLimit;

        private UnityEditor.Editor profileEditor;


        private void SetProfileEditor()
        {
            profileEditor = null;
            if (profile.objectReferenceValue != null)
                profileEditor = CreateEditor(profile.objectReferenceValue);
        }


        private void OnEnable()
        {
            PropertyFetcher<FogVolume> fetcher = new(serializedObject);

            profile = fetcher.Find("sharedProfile");

            SetProfileEditor();

            volumeType = fetcher.Find("volumeType");
            edgeFade = fetcher.Find("edgeFade");
            maxDistance = fetcher.Find("maxDistance");
            distanceFade = fetcher.Find("distanceFade");
            lightLayerMask = fetcher.Find("lightLayerMask");
            disableLightLimit = fetcher.Find("disableLightLimit");
        }


        public override void OnInspectorGUI()
        {
            FogVolume actualTarget = (FogVolume)target;

            serializedObject.Update();

            EditorGUILayout.PropertyField(volumeType, Styles.volumeType);

            DrawFadeField();

            DrawDistanceField();

            EditorGUILayout.PropertyField(lightLayerMask, Styles.lightLayerMask);
            EditorGUILayout.PropertyField(disableLightLimit, Styles.disableLightLimit);

            bool assetHasChanged = DrawProfileField(actualTarget);

            EditorGUILayout.Space(5f);

            if (profile.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox(Styles.noVolumeMessage, MessageType.Warning);
            }
            else
            {
                profile.isExpanded = DrawHeaderToggleFoldout(new GUIContent("Volume Profile"), profile.isExpanded);

                if (profile.isExpanded)
                    DrawProfileEditor(actualTarget, assetHasChanged);   
            }

    	    serializedObject.ApplyModifiedProperties();
        }


        private void OnSceneGUI()
        {
            FogVolume volume = (FogVolume)target;

            Matrix4x4 trsMatrix = volume.transform.localToWorldMatrix;
            Handles.matrix = trsMatrix;

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


        private void DrawDistanceField()
        {
            EditorGUILayout.PropertyField(maxDistance, Styles.maxDistance);

            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(distanceFade, Styles.distanceFade);
            EditorGUI.indentLevel--;
        }


        private void DrawFadeField()
        {
            using var scope = new EditorGUI.ChangeCheckScope();

            Vector3 fade = edgeFade.vector3Value;

            EditorGUI.indentLevel++;

            if ((VolumeType)volumeType.enumValueFlag == VolumeType.Cube)
            {
                fade.x = EditorGUILayout.Slider(Styles.edgeFadeX, fade.x, -1, 1);
                fade.y = EditorGUILayout.Slider(Styles.edgeFadeY, fade.y, -1, 1);
                fade.z = EditorGUILayout.Slider(Styles.edgeFadeZ, fade.z, -1, 1);
                EditorGUI.indentLevel--;
            }
            else if ((VolumeType)volumeType.enumValueFlag == VolumeType.Cylinder)
            {
                fade.x = EditorGUILayout.Slider(Styles.radiusFade, fade.x, -1, 1);
                fade.y = EditorGUILayout.Slider(Styles.heightFade, fade.y, -1, 1);
                EditorGUI.indentLevel--;
            }
            else
            {
                EditorGUI.indentLevel--;
                fade.x = EditorGUILayout.Slider(Styles.radiusFade, fade.x, -1, 1);
            }

            if (!scope.changed)
                return;

            edgeFade.vector3Value = fade;
        }


        private bool DrawProfileField(FogVolume actualTarget)
        {
            using var scope = new EditorGUI.ChangeCheckScope();

            bool showCopy = profile.objectReferenceValue != null;

            // The layout system breaks alignment when mixing inspector fields with custom layout'd fields, do the layout manually instead
            int buttonWidth = showCopy ? 45 : 60;
            float indentOffset = EditorGUI.indentLevel * 15f;

            var lineRect = EditorGUILayout.GetControlRect();

            var label = new Rect(lineRect.x, lineRect.y, EditorGUIUtility.labelWidth - indentOffset - 3, lineRect.height);
            var field = new Rect(label.xMax + 5, lineRect.y, lineRect.width - label.width - buttonWidth * (showCopy ? 2 : 1) - 5, lineRect.height);
            var buttonNew = new Rect(field.xMax, lineRect.y, buttonWidth, lineRect.height);
            var buttonCopy = new Rect(buttonNew.xMax, lineRect.y, buttonWidth, lineRect.height);

            var isProfileInstance = actualTarget.HasInstantiatedProfile();
            FogVolumeProfile editedProfile;

            if (isProfileInstance)
            {
                EditorGUI.PrefixLabel(label, Styles.profileInstance);
                editedProfile = (FogVolumeProfile)EditorGUI.ObjectField(field, actualTarget.profile, typeof(FogVolumeProfile), false);
            }
            else
            {
                field = new Rect(label.x, label.y, label.width + field.width, field.height);
                EditorGUI.ObjectField(field, profile, Styles.profile);
                editedProfile = (FogVolumeProfile)profile.objectReferenceValue;
            }

            if (scope.changed)
            {
                if (isProfileInstance)
                    actualTarget.profile = null;
                else
                    profile.objectReferenceValue = editedProfile;

                return true;
            }

            if (GUI.Button(buttonNew, Styles.newLabel, showCopy ? EditorStyles.miniButtonLeft : EditorStyles.miniButton))
            {
                var profileInstance = InstantiateProfile(actualTarget);

                if (profileInstance != null)
                {
                    profile.objectReferenceValue = profileInstance;
                    actualTarget.profile = null;
                    return true;
                }
            }

            if (!showCopy)
                return false;

            var copyLabel = actualTarget.HasInstantiatedProfile() ? Styles.saveLabel : Styles.cloneLabel;
            if (GUI.Button(buttonCopy, copyLabel, EditorStyles.miniButtonRight))
            {
                profile.objectReferenceValue = CopyProfile(actualTarget, profile.objectReferenceValue);
                actualTarget.profile = null;
                return true;
            }

            return false;
        }


        private static void CreateUniqueAsset(Object obj, string path)
        {
            AssetDatabase.CreateAsset(obj, AssetDatabase.GenerateUniqueAssetPath(path));
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
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
                string targetDirectory = Path.GetDirectoryName(scene.path);
                string profileFolder = Path.Combine(targetDirectory, scene.name);

                if (!AssetDatabase.IsValidFolder(targetDirectory))
                {
                    Debug.LogError($"Parent directory for {scene.name} could not be found. Cannot create profile.");
                    return null;
                }

                if (!AssetDatabase.IsValidFolder(profileFolder))
                    AssetDatabase.CreateFolder(targetDirectory, scene.name);

                path = profileFolder;
            }

            path += targetName.ReplaceInvalidFileNameCharacters() + ".asset";
            var profile = CreateInstance<FogVolumeProfile>();
            CreateUniqueAsset(profile, path);
            return profile;
        }


        private FogVolumeProfile CopyProfile(FogVolume origin, Object pathObj)
        {
            var asset = Instantiate(origin.profileReference);
            CreateUniqueAsset(asset, AssetDatabase.GetAssetPath(pathObj));
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

                    SetProfileEditor();
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

        // Draw a header similar to those seen on the Volume Components
        public static bool DrawHeaderToggleFoldout(GUIContent title, bool foldoutExpanded)
        {
            Rect backgroundRect = EditorGUILayout.GetControlRect(GUILayout.Height(17f));
            Rect labelRect = new Rect(backgroundRect.x + 16f, backgroundRect.y, backgroundRect.width, backgroundRect.height);
            Rect foldoutRect = new Rect(backgroundRect.x, backgroundRect.y + 1f, 13f, 13f);

            // Background rect should be full-width
            backgroundRect.xMin = 0f;
            backgroundRect.width += 4f;

            Rect edgeRect = new Rect(backgroundRect.x, backgroundRect.y - 1f, backgroundRect.width, 1f);
            EditorGUI.DrawRect(edgeRect, Color.black);

            Color backgroundTint = EditorGUIUtility.isProSkin ? Color.white * 0.1f : Color.white;
            backgroundTint.a = 0.2f;
            EditorGUI.DrawRect(backgroundRect, backgroundTint);

            if (!foldoutExpanded)
            {
                edgeRect.y = backgroundRect.y + backgroundRect.height;
                EditorGUI.DrawRect(edgeRect, Color.black);
            }

            EditorGUI.LabelField(labelRect, title, EditorStyles.boldLabel);

            bool expanded = GUI.Toggle(foldoutRect, foldoutExpanded, GUIContent.none, EditorStyles.foldout);

            if (Event.current.type == EventType.MouseDown && backgroundRect.Contains(Event.current.mousePosition) && Event.current.button == 0)
            {
                expanded = !expanded;
                Event.current.Use();
            }

            return expanded;
        }
    }
}