using System;
using UnityEngine;


public static class MeshUtility
{
    const float padding = 1.1f;

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


    private static Mesh _coneMesh;
    public static Mesh ConeMesh
    {
        get 
        {
            if (_coneMesh != null)
                return _coneMesh;

            // copy & pasted from other project, the geometry is too complex, should be simplified
            _coneMesh = new Mesh() { name = "Cone" };

            const int segmentCount = 16;

            Vector3[] vertices = new Vector3[segmentCount + 2];
            vertices[0] = Vector3.zero;
            vertices[1] = Vector3.forward * 0.5f;

            int[] triangles = new int[segmentCount * 3 * 2];

            float angle = 0;
            float step = Mathf.PI * 2.0f / segmentCount;
            for (int i = 0; i < segmentCount; i++)
            {
                vertices[i + 2] = new Vector3(-Mathf.Cos(angle) * 0.5f, Mathf.Sin(angle) * 0.5f, 1) * padding;

                int next = i + 3 >= vertices.Length ? 2 : i + 3;
                int sideIndex = i * 3;

                // Cone sides
                triangles[sideIndex + 0] = 0; // Cone apex vertex
                triangles[sideIndex + 1] = i + 2; // This vertex
                triangles[sideIndex + 2] = next; // Next vertex

                int baseIndex = i * 3 + segmentCount * 3;

                // Cone base
                triangles[baseIndex + 0] = 1; // Base center vertex
                triangles[baseIndex + 1] = next; // Next vertex
                triangles[baseIndex + 2] = i + 2; // This vertex

                angle += step;
            }

            _coneMesh.vertices = vertices;
            _coneMesh.triangles = triangles;

            _coneMesh.RecalculateBounds();
            _coneMesh.UploadMeshData(true);

            return _coneMesh;
        }
    }



    private static Mesh _sphereMesh;
    public static Mesh SphereMesh
    {
        get 
        {
            if (_sphereMesh != null)
                return _sphereMesh;
            
            _sphereMesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");

            Vector3[] sphereVerts = _sphereMesh.vertices;
            int[] sphereTris = _sphereMesh.triangles;

            for (int i = 0; i < sphereVerts.Length; i++)
            {
                sphereVerts[i] = sphereVerts[i] * 2.0f * padding;
            }

            _sphereMesh.vertices = sphereVerts;
            _sphereMesh.triangles = sphereTris;

            _sphereMesh.RecalculateBounds();
            _sphereMesh.UploadMeshData(true);

            return _sphereMesh;
        }
    }

}