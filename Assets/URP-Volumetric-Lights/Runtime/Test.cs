using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Test : MonoBehaviour
{
    public Transform poynt;



    static Vector3 ClampExterior(Vector3 point)
    {
        return new Vector3(
            Mathf.Clamp(point.x, -0.5f, 0.5f),
            Mathf.Clamp(point.y, -0.5f, 0.5f),
            Mathf.Clamp(point.z, -0.5f, 0.5f));
    }


    static Vector3 ClampInterior(Vector3 point)
    {
        float abX = Mathf.Abs(point.x);
        float abY = Mathf.Abs(point.y);
        float abZ = Mathf.Abs(point.z);

        point.x = (abX > abY && abX > abZ ? 0.5f : abX) * Mathf.Sign(point.x);
        point.y = (abY > abX && abY > abZ ? 0.5f : abY) * Mathf.Sign(point.y);
        point.z = (abZ > abX && abZ > abY ? 0.5f : abZ) * Mathf.Sign(point.z);

        return point;
    }


    static Vector3 ClampBox(Vector3 point)
    {
        if (Mathf.Abs(point.x) <= 0.5f && Mathf.Abs(point.y) <= 0.5f && Mathf.Abs(point.z) <= 0.5f)
            return ClampInterior(point);
        
        return ClampExterior(point);
    }


    static Vector3 ClosestPoint(Matrix4x4 transform, Matrix4x4 invTransform, Vector3 point)
    {
        Vector3 localPoint = invTransform.MultiplyPoint3x4(point);  
        Vector3 clamped = ClampBox(localPoint);
        Vector3 worldPoint = transform.MultiplyPoint3x4(clamped);
        return worldPoint;
    }


    void OnDrawGizmos()
    {
        if (poynt == null)
            return;

        Gizmos.matrix = transform.localToWorldMatrix;

        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(Vector3.zero, Vector3.one);

        Vector3 point = ClosestPoint(transform.localToWorldMatrix, transform.worldToLocalMatrix, poynt.position);

        Gizmos.matrix = Matrix4x4.identity;

        Gizmos.color = Color.red;
        Gizmos.DrawSphere(point, 0.25f);
    }
}
