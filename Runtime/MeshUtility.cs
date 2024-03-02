using UnityEngine;


public static class MeshUtility
{
    private static Mesh _fullscreenMesh;

    public static Mesh FullscreenMesh
    {
        get
        {
            if (_fullscreenMesh != null)
                return _fullscreenMesh;

            _fullscreenMesh = new Mesh
            {
                name = "Fullscreen Quad",
                vertices = new Vector3[]
                {
                    new(-1.0f, -1.0f, 0.0f),
                    new(-1.0f,  1.0f, 0.0f),
                    new(1.0f, -1.0f, 0.0f),
                    new(1.0f,  1.0f, 0.0f)
                },

                uv = new Vector2[]
                {
                    new(0.0f, 0.0f),
                    new(0.0f, 1.0f),
                    new(1.0f, 0.0f),
                    new(1.0f, 1.0f)
                },

                triangles = new int[] { 0, 1, 2, 2, 1, 3 }
            };

            _fullscreenMesh.UploadMeshData(true);
            return _fullscreenMesh;
        }
    }
}