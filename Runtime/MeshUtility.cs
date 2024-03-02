using UnityEngine;

namespace Sinnwrig.FogVolumes
{
    public static class MeshUtility
    {
        private static Mesh _fullscreenQuad;

        public static Mesh FullscreenQuad
        {
            get
            {
                if (_fullscreenQuad != null)
                    return _fullscreenQuad;

                _fullscreenQuad = new Mesh
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

                _fullscreenQuad.UploadMeshData(true);
                return _fullscreenQuad;
            }
        }
    }
}