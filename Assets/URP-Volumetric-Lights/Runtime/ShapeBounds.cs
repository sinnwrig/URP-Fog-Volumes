using UnityEngine;

#if UNITY_EDITOR
    using UnityEditor;
#endif


public static class ShapeBounds
{
    public static readonly Vector3[] cubeCorners = new Vector3[]
    {
        // Top 4
        new Vector3(0.5f, 0.5f, 0.5f),
        new Vector3(0.5f, 0.5f, -0.5f),
        new Vector3(-0.5f, 0.5f, 0.5f),
        new Vector3(-0.5f, 0.5f, -0.5f),

        // Bottom 4
        new Vector3(0.5f, -0.5f, 0.5f),
        new Vector3(0.5f, -0.5f, -0.5f),
        new Vector3(-0.5f, -0.5f, 0.5f),
        new Vector3(-0.5f, -0.5f, -0.5f),
    };


    public static readonly Bounds cubeBounds = new Bounds
    {
        min = cubeCorners[7],
        max = cubeCorners[0]
    };


    public static Bounds GetCubeAABB(Matrix4x4 matrix)
    {
        return GeometryUtility.CalculateBounds(cubeCorners, matrix);
    }


    public static readonly Vector3[] capsuleCorners = new Vector3[]
    {
        // Top 4
        new Vector3(0.5f, 1f, 0.5f),
        new Vector3(0.5f, 1f, -0.5f),
        new Vector3(-0.5f, 1f, 0.5f),
        new Vector3(-0.5f, 1f, -0.5f),

        // Bottom 4
        new Vector3(0.5f, -1f, 0.5f),
        new Vector3(0.5f, -1f, -0.5f),
        new Vector3(-0.5f, -1f, 0.5f),
        new Vector3(-0.5f, -1f, -0.5f),
    };

    public static Bounds GetCapsuleAABB(Matrix4x4 matrix)
    {
        return GeometryUtility.CalculateBounds(capsuleCorners, matrix);
    }


    public static readonly Bounds capsuleBounds = new Bounds
    {
        min = capsuleCorners[7],
        max = capsuleCorners[0]
    };


    public static bool BoundsWithinDistance(Vector3 point, float distance, Bounds bounds, Matrix4x4 transform, Matrix4x4 invTransform)
    {
        // Transform the point into the local space of the box
        Vector3 localPoint = invTransform.MultiplyPoint3x4(point);
        Vector3 closest = transform.MultiplyPoint3x4(bounds.ClosestPoint(localPoint));
        return (point - closest).sqrMagnitude < distance * distance;
    }
}