using UnityEngine;
using UnityEditor;


namespace Sinnwrig.FogVolumes.Editor
{
    public static class HandleExtensions
    {
        public static void DrawWireCapsule(Matrix4x4 trsMatrix)
        {
            Handles.matrix = trsMatrix;
            Vector3 posDownC = Vector3.down * 0.5f;

            Handles.DrawWireDisc(posDownC, Vector3.up, 0.5f);

            Handles.DrawWireArc(posDownC, Vector3.right, Vector3.forward, 180f, 0.5f);
            Handles.DrawWireArc(posDownC, Vector3.forward, Vector3.right, -180f, 0.5f);

            Vector3 posUpC = Vector3.up * 0.5f;

            Handles.DrawWireDisc(posUpC, Vector3.up, 0.5f);

            Handles.DrawWireArc(posUpC, Vector3.right, Vector3.forward, -180f, 0.5f);
            Handles.DrawWireArc(posUpC, Vector3.forward, Vector3.right, 180f, 0.5f);

            Handles.DrawLine(posDownC + Vector3.right * 0.5f, posUpC + Vector3.right * 0.5f);
            Handles.DrawLine(posDownC + Vector3.left * 0.5f, posUpC + Vector3.left * 0.5f);
            Handles.DrawLine(posDownC + Vector3.forward * 0.5f, posUpC + Vector3.forward * 0.5f);
            Handles.DrawLine(posDownC + Vector3.back * 0.5f, posUpC + Vector3.back * 0.5f);
        }

        public static void DrawWireCylinder(Matrix4x4 trsMatrix)
        {
            Handles.matrix = trsMatrix;
            Vector3 posDown = Vector3.down;

            Handles.DrawWireDisc(posDown, Vector3.up, 0.5f);

            Vector3 posUp = Vector3.up;

            Handles.DrawWireDisc(posUp, Vector3.up, 0.5f);

            Handles.DrawLine(posDown + Vector3.right * 0.5f, posUp + Vector3.right * 0.5f);
            Handles.DrawLine(posDown + Vector3.left * 0.5f, posUp + Vector3.left * 0.5f);
            Handles.DrawLine(posDown + Vector3.forward * 0.5f, posUp + Vector3.forward * 0.5f);
            Handles.DrawLine(posDown + Vector3.back * 0.5f, posUp + Vector3.back * 0.5f);
        }

        public static void DrawWireSphere(Matrix4x4 trsMatrix)
        {
            Handles.matrix = trsMatrix;

            Handles.DrawWireDisc(Vector3.zero, Vector3.up, 0.5f);
            Handles.DrawWireDisc(Vector3.zero, Vector3.forward, 0.5f);
            Handles.DrawWireDisc(Vector3.zero, Vector3.right, 0.5f);
        }

        public static void DrawWireCube(Matrix4x4 trsMatrix)
        {
            Handles.matrix = trsMatrix;
            Handles.DrawWireCube(Vector3.zero, Vector3.one);
        }
    }
}