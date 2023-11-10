using System;
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


    public static Mesh CreateConeMesh(int segments)
    {
        Mesh mesh = new Mesh() { name = "Cone" };

        Vector3[] vertices = new Vector3[segments + 2];
        Vector2[] uvs = new Vector2[segments + 2];

        vertices[0] = Vector3.zero;
        vertices[1] = Vector3.forward;

        uvs[0] = Vector2.zero;
        uvs[1] = new Vector2(0.5f, 0.5f);

        int[] triangles = new int[segments * 3 * 2];

        float angle = 0;
        float step = Mathf.PI * 2.0f / segments;
        for (int i = 0; i < segments; i++)
        {
            int vertex = i + 2;
            int nextVertex = i + 3 >= vertices.Length ? 2 : i + 3;

            vertices[vertex] = new Vector3(-Mathf.Cos(angle), Mathf.Sin(angle), 1);
            uvs[vertex] = new Vector2((float)i / segments, 0.0f);

            int sideIndex = i * 3;

            // Cone sides
            triangles[sideIndex++] = 0; // Cone apex vertex
            triangles[sideIndex++] = vertex; // This vertex
            triangles[sideIndex++] = nextVertex; // Next vertex

            int baseIndex = i * 3 + segments * 3;

            // Cone base
            triangles[baseIndex++] = 1; // Base center vertex
            triangles[baseIndex++] = nextVertex; // Next vertex
            triangles[baseIndex++] = vertex; // This vertex

            angle += step;
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs; // To be honest, I have no idea how to unwrap cone UV's, but at least there's something.

        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();

        mesh.Optimize();

        return mesh;
    }
}