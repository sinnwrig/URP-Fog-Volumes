using UnityEngine;


public static class BoundsUtility
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


    public static readonly Bounds capsuleBounds = new Bounds
    {
        min = capsuleCorners[7],
        max = capsuleCorners[0]
    };


    public static Vector4 GetViewportRect(Matrix4x4 transform, Camera camera, Vector3[] points)
    {
        Vector4 viewportRect = new Vector4(1, 1, 0, 0);

        for (int i = 0; i < points.Length; i++)
        {
            Vector3 worldPos = transform.MultiplyPoint3x4(points[i]);
            Vector4 posLocal = camera.worldToCameraMatrix.MultiplyPoint3x4(worldPos);
            Vector4 viewport = camera.projectionMatrix * posLocal;

            viewport.x /= viewport.w;
            viewport.y /= viewport.w;

            viewport.x = viewport.x * 0.5f + 0.5f;
            viewport.y = viewport.y * 0.5f + 0.5f;

            // When corner is behind, clamp to a screen edge to prevent the rect from clipping the bounding box
            // Hacky solution, but more reliable than anything else I tried.
            if (posLocal.z > 0)
            {
                viewport.x = posLocal.x < 0 ? 0 : 1;
                viewport.y = posLocal.y < 0 ? 0 : 1;
            }
            
            viewportRect.x = Mathf.Min(viewport.x, viewportRect.x);
            viewportRect.y = Mathf.Min(viewport.y, viewportRect.y);

            viewportRect.z = Mathf.Max(viewport.x, viewportRect.z);
            viewportRect.w = Mathf.Max(viewport.y, viewportRect.w);
        }

        viewportRect.z -= viewportRect.x;
        viewportRect.w -= viewportRect.y;

        return viewportRect;
    }


    public static bool InsideBounds(Matrix4x4 invTransform, Vector3 point, Bounds bounds)
    {
        Vector3 localPoint = invTransform.MultiplyPoint3x4(point);
        return bounds.Contains(localPoint);
    }


    public static bool WithinDistance(Matrix4x4 transform, Matrix4x4 invTransform, Vector3 point, float distance, Bounds bounds)
    {
        Vector3 localPoint = invTransform.MultiplyPoint3x4(point);
        Vector3 closestPoint = bounds.ClosestPoint(localPoint);
        Vector3 worldPoint = transform.MultiplyPoint3x4(closestPoint);
        return (worldPoint - point).sqrMagnitude < (distance * distance);
    }
}