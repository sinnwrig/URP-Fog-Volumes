using UnityEngine;
using UnityEditor;
using UnityEditor.Rendering;
using System.IO;

// Ripped from the SRP Volume component editor with significant changes. 
namespace Sinnwrig.FogVolumes.Editor
{
    [CustomEditor(typeof(FogVolume)), CanEditMultipleObjects]
    public class FogVolumeEditor : UnityEditor.Editor
    {
        static class Styles 
        {
            public static readonly GUIContent volumeType = EditorGUIUtility.TrTextContent("Volume Type", "Defines the primitive shape of the volume. Shape is affected by the object transform");

            public static readonly GUIContent radiusFade = EditorGUIUtility.TrTextContent("Radius Fade", "Defines how intensely the fog will fade towards the edges of the volume.");
            public static readonly GUIContent edgeFadeX = EditorGUIUtility.TrTextContent("Edge Fade X", radiusFade.tooltip);
            public static readonly GUIContent edgeFadeY = EditorGUIUtility.TrTextContent("Edge Fade Y", radiusFade.tooltip);
            public static readonly GUIContent edgeFadeZ = EditorGUIUtility.TrTextContent("Edge Fade Z", radiusFade.tooltip);
            public static readonly GUIContent heightFade = EditorGUIUtility.TrTextContent("Height Fade", radiusFade.tooltip);

            public static readonly GUIContent fadeOffsetX = EditorGUIUtility.TrTextContent("Fade Offset X",  "Offsets the fade center");
            public static readonly GUIContent fadeOffsetY = EditorGUIUtility.TrTextContent("Fade Offset Y", fadeOffsetX.tooltip);
            public static readonly GUIContent fadeOffsetZ = EditorGUIUtility.TrTextContent("Fade Offset Z", fadeOffsetX.tooltip);
            public static readonly GUIContent heightOffset = EditorGUIUtility.TrTextContent("Height Offset", fadeOffsetX.tooltip);
            
            public static readonly GUIContent lightsFade = EditorGUIUtility.TrTextContent("Lighting Fade", "Whether or not lighting fades with ambient color");

            public static readonly GUIContent maxDistance = EditorGUIUtility.TrTextContent("Max Distance", "Maximum distance the Fog Volume will draw at before being culled");
            public static readonly GUIContent distanceFade = EditorGUIUtility.TrTextContent("Distance Fade", "At what percentage from the maximum distance will the fog begin to fade out");

            public static readonly GUIContent lightLayerMask = EditorGUIUtility.TrTextContent("Light Mask", "This volume will only be affected by lights in the selected scene-layers");
            public static readonly GUIContent disableLightLimit = EditorGUIUtility.TrTextContent("Disable Light Limit", "Disable the per-object light limit for this volume");

            public static readonly GUIContent profile = EditorGUIUtility.TrTextContent("Profile", "A Fog Volume Profile is a Scriptable Object which defines how Fog Volumes draw fog in the scene.");
            public static readonly GUIContent profileInstance = EditorGUIUtility.TrTextContent("Profile (Instance)", profile.tooltip);
            public static readonly GUIContent newLabel = EditorGUIUtility.TrTextContent("New", "Create a new profile.");
            public static readonly GUIContent saveLabel = EditorGUIUtility.TrTextContent("Save", "Save the instantiated profile");
            public static readonly GUIContent cloneLabel = EditorGUIUtility.TrTextContent("Clone", "Create a new profile and copy the content of the currently assigned profile.");
            public static readonly string noVolumeMessage = L10n.Tr("Please select or create a new Fog Volume profile to begin applying fog to the scene.");
        }


        static FogVolume CreateNewVolume(string name, Vector3 scale, MenuCommand command)
        {
            GameObject go = new GameObject(name);
            go.transform.localScale = scale;

            GameObjectUtility.SetParentAndAlign(go, command.context as GameObject);
            Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);

            Selection.activeObject = go;

            return go.AddComponent<FogVolume>();
        }


        [MenuItem("GameObject/Volume/Sphere Fog Volume", false, 11000)]
        static void CreateSphere(MenuCommand command)
        {
            var volume = CreateNewVolume("Sphere Fog Volume", Vector3.one * 15, command);
            volume.volumeType = VolumeType.Sphere;
        }


        [MenuItem("GameObject/Volume/Box Fog Volume", false, 11000)]
        static void CreateBox(MenuCommand command)
        {
            var volume = CreateNewVolume("Box Fog Volume", Vector3.one * 15, command);
            volume.volumeType = VolumeType.Cube;
        }


        [MenuItem("GameObject/Volume/Capsule Fog Volume", false, 11000)]
        static void CreateCapsule(MenuCommand command)
        {
            var volume = CreateNewVolume("Capsule Fog Volume", Vector3.one * 15, command);
            volume.volumeType = VolumeType.Capsule;
        }


        [MenuItem("GameObject/Volume/Cylinder Fog Volume", false, 11000)]
        static void CreateCylinder(MenuCommand command)
        {
            var volume = CreateNewVolume("Cylinder Fog Volume", Vector3.one * 15, command);
            volume.volumeType = VolumeType.Cylinder;
        }

    
        private SerializedProperty profile;
        private SerializedProperty volumeType;
        private SerializedProperty edgeFade;
        private SerializedProperty fadeOffset;
        private SerializedProperty lightsFade;
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
            fadeOffset = fetcher.Find("fadeOffset");
            lightsFade = fetcher.Find("lightsFade");
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

            bool assetHasChanged = DrawProfileField(actualTarget);

            EditorGUILayout.PropertyField(lightLayerMask, Styles.lightLayerMask);
            EditorGUILayout.PropertyField(disableLightLimit, Styles.disableLightLimit);

            EditorGUILayout.Space(5f);

            edgeFade.isExpanded = DrawHeaderToggleFoldout(new GUIContent("Fade Settings"), edgeFade.isExpanded, false);
            
            if (edgeFade.isExpanded)
            {
                EditorGUI.indentLevel++;
                DrawFadeField();
                EditorGUI.indentLevel--;

                QuickSpace(0.1f);

                EditorGUILayout.PropertyField(lightsFade, Styles.lightsFade);

                QuickSpace(0.1f);

                DrawDistanceField();

                EditorGUILayout.Space(5f);
            }

            
            profile.isExpanded = DrawHeaderToggleFoldout(new GUIContent("Volume Profile"), profile.isExpanded);

            if (profile.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox(Styles.noVolumeMessage, MessageType.Warning);
            }
            else if (profile.isExpanded)
            {
                DrawProfileEditor(actualTarget, assetHasChanged);   
            }

    	    serializedObject.ApplyModifiedProperties();
        }


        private void OnSceneGUI()
        {
            using var scope = new EditorGUI.ChangeCheckScope();

            FogVolume volume = (FogVolume)target;

            Matrix4x4 trsMatrix = volume.transform.localToWorldMatrix;
            Handles.matrix = trsMatrix;

            Handles.color = volume.ProfileReference == null ? Color.red : Color.white;

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


        private static void QuickSpace(float factor)
        {
            EditorGUILayout.Space(EditorGUIUtility.singleLineHeight * factor);
        }


        private void DrawFadeField()
        {
            using var scope = new EditorGUI.ChangeCheckScope();

            Vector3 fade = edgeFade.vector3Value;
            Vector3 offset = fadeOffset.vector3Value;

            if ((VolumeType)volumeType.enumValueFlag == VolumeType.Cube)
            {
                fade.x = EditorGUILayout.Slider(Styles.edgeFadeX, fade.x, 0, 1);
                fade.y = EditorGUILayout.Slider(Styles.edgeFadeY, fade.y, 0, 1);
                fade.z = EditorGUILayout.Slider(Styles.edgeFadeZ, fade.z, 0, 1);

                QuickSpace(0.1f);

                offset.x = EditorGUILayout.Slider(Styles.fadeOffsetX, offset.x, -0.5f, 0.5f);
                offset.y = EditorGUILayout.Slider(Styles.fadeOffsetY, offset.y, -0.5f, 0.5f);
                offset.z = EditorGUILayout.Slider(Styles.fadeOffsetZ, offset.z, -0.5f, 0.5f);
            }
            else if ((VolumeType)volumeType.enumValueFlag == VolumeType.Cylinder)
            {
                fade.x = EditorGUILayout.Slider(Styles.radiusFade, fade.x, 0, 1);
                fade.y = EditorGUILayout.Slider(Styles.heightFade, fade.y, 0, 1);

                QuickSpace(0.1f);

                offset.y = EditorGUILayout.Slider(Styles.heightOffset, offset.y, -0.5f, 0.5f);
            }
            else
            {
                fade.x = EditorGUILayout.Slider(Styles.radiusFade, fade.x, 0, 1);
            }

            if (!scope.changed)
                return;

            Undo.RecordObject(target, "Changed Fade and Offset");

            edgeFade.vector3Value = fade;
            fadeOffset.vector3Value = offset;
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
                Debug.LogWarning("Scene directory could not be determined. Creating profile under Assets/");
                path = "Assets/";
            }
            else
            {
                string targetDirectory = Path.GetDirectoryName(scene.path);

                if (!AssetDatabase.IsValidFolder(targetDirectory))
                {
                    Debug.LogWarning("Scene directory could not be determined. Creating profile under Assets/");
                    path = "Assets/";
                }

                string profileFolder = Path.Combine(targetDirectory, scene.name);

                if (!AssetDatabase.IsValidFolder(profileFolder))
                    AssetDatabase.CreateFolder(targetDirectory, scene.name);

                path = profileFolder;
            }

            path = Path.Combine(path, targetName.ReplaceInvalidFileNameCharacters() + ".asset");

            var profile = CreateInstance<FogVolumeProfile>();
            CreateUniqueAsset(profile, path);
            return profile;
        }


        private FogVolumeProfile CopyProfile(FogVolume origin, Object pathObj)
        {
            var asset = Instantiate(origin.ProfileReference);
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
                if (assetHasChanged || actualTarget.ProfileReference != profileEditor?.target)
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
        public static bool DrawHeaderToggleFoldout(GUIContent title, bool foldoutExpanded, bool hasBottomBorder = true)
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

            if (foldoutExpanded || hasBottomBorder)
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