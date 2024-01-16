using UnityEngine;
using UnityEditor;


[CustomEditor(typeof(FogVolume)), CanEditMultipleObjects]
public class FogVolumeEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
    }


    void OnSceneGUI()
    {
        FogVolume volume = (FogVolume)target;

        Matrix4x4 trsMatrix = volume.transform.localToWorldMatrix;
        Handles.matrix = trsMatrix;

        Handles.color = Color.gray;

        Bounds bounds = volume.GetBounds();
        Handles.DrawWireCube(bounds.center, bounds.size);

        Handles.color = Color.white;

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
}
