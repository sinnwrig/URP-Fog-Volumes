using UnityEngine;


public static class CullingUtility
{
    private static Plane[] frustumPlanes = new Plane[6];


    public static void InitCullingPlanes(Camera camera)
    {
        GeometryUtility.CalculateFrustumPlanes(camera, frustumPlanes);
    }


    public static bool CullSphere(Vector3 position, float radius)
    {
        for (int i = 0; i < frustumPlanes.Length; i++) 
		{
			float distance = frustumPlanes[i].GetDistanceToPoint(position);

			if (distance < 0 && Mathf.Abs(distance) > radius) 
				return false;
		}

        return true;
    }


    public static bool CullBounds(Bounds bounds)
    {
        return GeometryUtility.TestPlanesAABB(frustumPlanes, bounds);
    }
}