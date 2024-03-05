using System;

using UnityEngine;
using UnityEditor;
using UnityEditorInternal;


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


    // Copied from IMGUI.Controls' PrimitiveBoundsHandle
    public class BoundsHandle
    {
        [Flags]
        public enum Axes
        {
            None = 0,
            X = 1,
            Y = 2,
            Z = 4,
            All = 7
        }


        private static readonly float defaultMidpointHandleSize = 0.03f;

        private static readonly int[] nextAxis = new int[3] { 1, 2, 0 };

        private static GUIContent _editModeButton;

        private int[] m_ControlIDs = new int[6];

        private Bounds bounds;
        private Bounds boundsOnClick;

        public static GUIContent EditModeButton
        {
            get
            {
                if (_editModeButton == null)
                    _editModeButton = new GUIContent(
                        EditorGUIUtility.IconContent("EditCollider").image, 
                        EditorGUIUtility.TrTextContent("Edit bounding volume.\n\n - Hold Alt after clicking control handle to pin center in place.\n - Hold Shift after clicking control handle to scale uniformly.").text);

                return _editModeButton;
            }
        }


        public Vector3 center
        {
            get => bounds.center;
            set => bounds.center = value;
        }

        public Vector3 size
        {
            get => bounds.size;
            set => bounds.size = value;
        }


        public Axes axes;

        public Color handleColor;

        public Handles.CapFunction midpointHandleDrawFunction;

        public Handles.SizeFunction midpointHandleSizeFunction;


        private static float DefaultMidpointHandleSizeFunction(Vector3 position)
        {
            return HandleUtility.GetHandleSize(position) * defaultMidpointHandleSize;
        }


        public BoundsHandle()
        {
            handleColor = Color.white;
            axes = Axes.All;
            midpointHandleDrawFunction = Handles.DotHandleCap;
            midpointHandleSizeFunction = DefaultMidpointHandleSizeFunction;
        }


        public void DrawHandle()
        {
            int i = 0;
            for (int num = m_ControlIDs.Length; i < num; i++)
            {
                m_ControlIDs[i] = GUIUtility.GetControlID(GetHashCode(), FocusType.Passive);
            }

            if (Event.current.alt)
            {
                bool flag = true;
                int[] controlIDs = m_ControlIDs;
                foreach (int num2 in controlIDs)
                {
                    if (num2 == GUIUtility.hotControl)
                    {
                        flag = false;
                        break;
                    }
                }

                if (flag)
                {
                    return;
                }
            }

            Vector3 minPos = bounds.min;
            Vector3 maxPos = bounds.max;
            int hotControl = GUIUtility.hotControl;

            EditorGUI.BeginChangeCheck();

            using (new Handles.DrawingScope(Handles.color * handleColor))
            {
                if (Handles.color.a > 0f)
                {
                    MidpointHandles(ref minPos, ref maxPos);
                }
            }

            bool flag2 = EditorGUI.EndChangeCheck();
            if (hotControl != GUIUtility.hotControl && GUIUtility.hotControl != 0)
            {
                boundsOnClick = bounds;
            }

            if (!flag2)
            {
                return;
            }

            bounds.center = (maxPos + minPos) * 0.5f;
            bounds.size = maxPos - minPos;

            if (Event.current.shift)
            {
                int hotControl2 = GUIUtility.hotControl;
                Vector3 size = bounds.size;
                int num4 = 0;
                if (hotControl2 == m_ControlIDs[2] || hotControl2 == m_ControlIDs[3])
                {
                    num4 = 1;
                }

                if (hotControl2 == m_ControlIDs[4] || hotControl2 == m_ControlIDs[5])
                {
                    num4 = 2;
                }

                if (Mathf.Approximately(boundsOnClick.size[num4], 0f))
                {
                    if (boundsOnClick.size == Vector3.zero)
                    {
                        size = Vector3.one * size[num4];
                    }
                }
                else
                {
                    float num5 = size[num4] / boundsOnClick.size[num4];
                    int num6 = nextAxis[num4];
                    size[num6] = num5 * boundsOnClick.size[num6];
                    num6 = nextAxis[num6];
                    size[num6] = num5 * boundsOnClick.size[num6];
                }

                bounds.size = size;
            }

            if (Event.current.alt)
            {
                bounds.center = boundsOnClick.center;
            }
        }


        private bool IsAxisEnabled(Axes axis) => (axes & axis) == axis;


        private void MidpointHandles(ref Vector3 minPos, ref Vector3 maxPos)
        {
            Vector3 right = Vector3.right;
            Vector3 up = Vector3.up;
            Vector3 forward = Vector3.forward;
            Vector3 vector = (maxPos + minPos) * 0.5f;

            if (IsAxisEnabled(Axes.X))
            {
                maxPos.x = Mathf.Max(MidpointHandle(m_ControlIDs[0], new Vector3(maxPos.x, vector.y, vector.z), up, forward).x, minPos.x);
                minPos.x = Mathf.Min(MidpointHandle(m_ControlIDs[1], new Vector3(minPos.x, vector.y, vector.z), up, -forward).x, maxPos.x);
            }

            if (IsAxisEnabled(Axes.Y))
            {
                maxPos.y = Mathf.Max(MidpointHandle(m_ControlIDs[2], new Vector3(vector.x, maxPos.y, vector.z), right, -forward).y, minPos.y);
                minPos.y = Mathf.Min(MidpointHandle(m_ControlIDs[3], new Vector3(vector.x, minPos.y, vector.z), right, forward).y, maxPos.y);
            }

            if (IsAxisEnabled(Axes.Z))
            {
                maxPos.z = Mathf.Max(MidpointHandle(m_ControlIDs[4], new Vector3(vector.x, vector.y, maxPos.z), up, -right).z, minPos.z);
                minPos.z = Mathf.Min(MidpointHandle(m_ControlIDs[5], new Vector3(vector.x, vector.y, minPos.z), up, right).z, maxPos.z);
            }
        }


        private Vector3 MidpointHandle(int id, Vector3 localPos, Vector3 localTangent, Vector3 localBinormal)
        {
            Color color = Handles.color;

            if (Handles.color.a > 0f && midpointHandleDrawFunction != null)
            {
                Vector3 normalized = Vector3.Cross(localTangent, localBinormal).normalized;
                float size = ((midpointHandleSizeFunction == null) ? 0f : midpointHandleSizeFunction(localPos));
                localPos = Handles.Slider(localPos, normalized, size, midpointHandleDrawFunction, EditorSnapSettings.scale);
            }

            Handles.color = color;
            return localPos;
        }
    }   
}
