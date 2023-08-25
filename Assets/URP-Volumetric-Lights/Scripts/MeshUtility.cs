using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEngine.Rendering;

public static class MeshUtility
{   
    public static Mesh CreateConeMesh(int segments)
    {
        Mesh mesh = new() { name = "Cone Mesh" };

        Vector3[] vertices = new Vector3[2 + segments];
        int[] triangles = new int[segments * 3 * 2];

        vertices[0] = new Vector3(0, 0, 0);
        vertices[1] = new Vector3(0, 0, 1);

        float angle = 0;
        float step = Mathf.PI * 2.0f / segments;

        for (int i = 0; i < segments; i++, angle += step) 
        {
            vertices[i + 2] = new Vector3(-Mathf.Cos(angle), Mathf.Sin(angle), 1);
        }

        int index = 0;

        // Do cone side triangles
        for (int i = 0; i < segments; i++)
        {   
            triangles[index++] = 0;
            triangles[index++] = i + 2;

            // If loop is at end, use third vertex instead of going out of bounds
            triangles[index++] = i == segments - 1 ? 2 : i + 3;
        }

        // Do cone base triangles
        for (int i = 0; i < segments; i++)
        {
            triangles[index++] = 1;

            // If loop is at end, use third vertex instead of going out of bounds
            triangles[index++] = i == segments - 1 ? 2 : i + 3;
            triangles[index++] = i + 2;
        }


        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();

        return mesh;
    }

    
    // Code from:  https://github.com/kaiware007/IcoSphereCreator/blob/master/Assets/IcoSphereCreator/IcoSphereCreator.cs
    public static Mesh CreateIcosphere(int resolution)
    {
        int nn = resolution * 4;
        int vertexNum = nn * nn / 16 * 24;

        Vector3[] vertices = new Vector3[vertexNum];
        int[] triangles = new int[vertexNum];

        Quaternion[] init_vectors = new Quaternion[24];

        // 0
        init_vectors[0] = new Quaternion(0, 1, 0, 0);   //the triangle vertical to (1,1,1)
        init_vectors[1] = new Quaternion(0, 0, 1, 0);
        init_vectors[2] = new Quaternion(1, 0, 0, 0);

        // 1
        init_vectors[3] = new Quaternion(0, -1, 0, 0);  //to (1,-1,1)
        init_vectors[4] = new Quaternion(1, 0, 0, 0);
        init_vectors[5] = new Quaternion(0, 0, 1, 0);

        // 2
        init_vectors[6] = new Quaternion(0, 1, 0, 0);   //to (-1,1,1)
        init_vectors[7] = new Quaternion(-1, 0, 0, 0);
        init_vectors[8] = new Quaternion(0, 0, 1, 0);

        // 3
        init_vectors[9] = new Quaternion(0, -1, 0, 0);  //to (-1,-1,1)
        init_vectors[10] = new Quaternion(0, 0, 1, 0);
        init_vectors[11] = new Quaternion(-1, 0, 0, 0);

        // 4
        init_vectors[12] = new Quaternion(0, 1, 0, 0);  //to (1,1,-1)
        init_vectors[13] = new Quaternion(1, 0, 0, 0);
        init_vectors[14] = new Quaternion(0, 0, -1, 0);

        // 5
        init_vectors[15] = new Quaternion(0, 1, 0, 0);  //to (-1,1,-1)
        init_vectors[16] = new Quaternion(0, 0, -1, 0);
        init_vectors[17] = new Quaternion(-1, 0, 0, 0);

        // 6
        init_vectors[18] = new Quaternion(0, -1, 0, 0); //to (-1,-1,-1)
        init_vectors[19] = new Quaternion(-1, 0, 0, 0);
        init_vectors[20] = new Quaternion(0, 0, -1, 0);

        // 7
        init_vectors[21] = new Quaternion(0, -1, 0, 0); //to (1,-1,-1)
        init_vectors[22] = new Quaternion(0, 0, -1, 0);
        init_vectors[23] = new Quaternion(1, 0, 0, 0);
        
        int j = 0; 

        for (int i = 0; i < 24; i += 3)
        {
            /*
			 *                   c _________d
			 *    ^ /\           /\        /
			 *   / /  \         /  \      /
			 *  p /    \       /    \    /
			 *   /      \     /      \  /
			 *  /________\   /________\/
			 *     q->       a         b
			 */
            for (int p = 0; p < resolution; p++)
            {   
                //edge index 1
                Quaternion edge_p1 = Quaternion.Lerp(init_vectors[i], init_vectors[i + 2], (float)p / resolution);
                Quaternion edge_p2 = Quaternion.Lerp(init_vectors[i + 1], init_vectors[i + 2], (float)p / resolution);
                Quaternion edge_p3 = Quaternion.Lerp(init_vectors[i], init_vectors[i + 2], (float)(p + 1) / resolution);
                Quaternion edge_p4 = Quaternion.Lerp(init_vectors[i + 1], init_vectors[i + 2], (float)(p + 1) / resolution);

                for (int q = 0; q < (resolution - p); q++)
                {   
                    //edge index 2
                    Quaternion a = Quaternion.Lerp(edge_p1, edge_p2, (float)q / (resolution - p));
                    Quaternion b = Quaternion.Lerp(edge_p1, edge_p2, (float)(q + 1) / (resolution - p));
                    Quaternion c, d;

                    if(edge_p3 == edge_p4)
                    {
                        c = edge_p3;
                        d = edge_p3;
                    }
                    else
                    {
                        c = Quaternion.Lerp(edge_p3, edge_p4, (float)q / (resolution - p - 1));
                        d = Quaternion.Lerp(edge_p3, edge_p4, (float)(q + 1) / (resolution - p - 1));
                    }

                    triangles[j] = j;
                    vertices[j++] = new Vector3(a.x, a.y, a.z).normalized * 0.5f;
                    triangles[j] = j;
                    vertices[j++] = new Vector3(b.x, b.y, b.z).normalized * 0.5f;
                    triangles[j] = j;
                    vertices[j++] = new Vector3(c.x, c.y, c.z).normalized * 0.5f;

                    if (q < resolution - p - 1)
                    {
                        triangles[j] = j;
                        vertices[j++] = new Vector3(c.x, c.y, c.z).normalized * 0.5f;
                        triangles[j] = j;
                        vertices[j++] = new Vector3(b.x, b.y, b.z).normalized * 0.5f;
                        triangles[j] = j;
                        vertices[j++] = new Vector3(d.x, d.y, d.z).normalized * 0.5f;
                    }
                }
            }
        }
        
        
        RemoveDuplicateVertices(ref vertices, ref triangles);


        Mesh mesh = new()
        {
            name = "IcoSphere",
            vertices = vertices,
            triangles = triangles,
        };

        mesh.RecalculateBounds();
        mesh.RecalculateNormals();

        return mesh;
    }


    public static void RemoveDuplicateVertices(ref Vector3[] vertices, ref int[] triangles, float errorMargin = 0.00001f)
    {
        // Get non-duplicate vertices
        HashSet<Vector3> uniqueVertices = new();

        for (int i = 0; i < vertices.Length; i++) 
        {
            uniqueVertices.Add(vertices[i]);
        }


        Vector3[] newVertices = uniqueVertices.ToArray();

        for (int i = 0; i < triangles.Length; i++) 
        {
            Vector3 vertex = vertices[triangles[i]];

            int index = Array.FindIndex(newVertices, (x) => {
                return 
                    (Mathf.Abs(x.x - vertex.x) < errorMargin) &&
                    (Mathf.Abs(x.y - vertex.y) < errorMargin) &&
                    (Mathf.Abs(x.z - vertex.z) < errorMargin);
            });
            
            triangles[i] = index;
        }


        vertices = newVertices;
    }
}
