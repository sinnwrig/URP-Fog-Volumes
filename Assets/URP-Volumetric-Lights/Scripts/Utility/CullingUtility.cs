using UnityEngine;


public static class CullingUtility
{
    private static readonly Plane[] frustumPlanes = new Plane[6];


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


    private static readonly Vector3[] corners = new Vector3[]
    {
        // Front
        new Vector3(0.5f, 0.5f, 0.5f),
        new Vector3(-0.5f, 0.5f, 0.5f),
        new Vector3(0.5f, -0.5f, 0.5f),
        new Vector3(-0.5f, -0.5f, 0.5f),

        // Back
        new Vector3(0.5f, 0.5f, -0.5f),
        new Vector3(-0.5f, 0.5f, -0.5f),
        new Vector3(0.5f, -0.5f, -0.5f),
        new Vector3(-0.5f, -0.5f, -0.5f),
    };


    public static bool CullBox(Matrix4x4 boxTransform)
    {   
        for (int i = 0; i < corners.Length; i++)
        {   
            Vector3 corner = boxTransform.MultiplyPoint3x4(corners[i]);

            for (int j = 0; j < frustumPlanes.Length; j++)
            {
                float distance = frustumPlanes[j].GetDistanceToPoint(corner);

                // If at least one corner is inside, box is good
                if (distance > 0)
                    return true;
            }
        }

        return false;
    }
}