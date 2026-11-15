using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ChokeHold.EditorTools
{
    // Pregatire pentru texturare.
    //
    // Toate cutiile hartii sunt cuburi scalate. Cubul standard Unity are UV 0..1 pe fiecare fata, deci
    // o textura s-ar intinde pe un perete de 30 m si s-ar turti pe o plinta. Aici fiecare cutie primeste
    // o plasa cu aceeasi forma (cub unitate, deci scala, colliderele si restul codului raman neschimbate),
    // dar cu UV la scara reala: 1 unitate UV = 1 metru pe fiecare fata.
    //   - u merge pe orizontala, v pe verticala (pe pereti); pe podele/tavane u = X, v = Z;
    //   - UV2 generat pentru lightmap (pentru un bake de lumina ulterior).
    //
    // Plasele sunt salvate o singura data, ca sub-asset-uri ale unui singur fisier, si refolosite
    // de la o reconstruire la alta (o plasa pe dimensiune, rotunjita la centimetru).
    public static partial class BlacksiteK17MapBuilder
    {
        private const string BoxMeshAssetPath = "Assets/_Project/Art/Meshes/GreyboxBoxMeshes.asset";

        private static void BakeBoxUVs(GameObject root)
        {
            EnsureFolder("Assets/_Project/Art/Meshes");

            Dictionary<string, Mesh> cache = new Dictionary<string, Mesh>();
            Object mainAsset = AssetDatabase.LoadMainAssetAtPath(BoxMeshAssetPath);

            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(BoxMeshAssetPath))
            {
                if (asset is Mesh mesh)
                {
                    cache[mesh.name] = mesh;
                }
            }

            int replaced = 0, created = 0;

            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh current = filter.sharedMesh;

                // Doar cuburile primitive (nu cilindrii, nu plasele deja inlocuite).
                if (current == null || current.name != "Cube")
                {
                    continue;
                }

                Vector3 size = Abs(filter.transform.lossyScale);
                string key = $"Box_{Mathf.RoundToInt(size.x * 100f)}_{Mathf.RoundToInt(size.y * 100f)}_{Mathf.RoundToInt(size.z * 100f)}";

                if (!cache.TryGetValue(key, out Mesh mesh))
                {
                    mesh = CreateUVBox(key, size);

                    if (mainAsset == null)
                    {
                        AssetDatabase.CreateAsset(mesh, BoxMeshAssetPath);
                        mainAsset = mesh;
                    }
                    else
                    {
                        AssetDatabase.AddObjectToAsset(mesh, mainAsset);
                    }

                    cache[key] = mesh;
                    created++;
                }

                filter.sharedMesh = mesh;
                replaced++;
            }

            if (created > 0)
            {
                AssetDatabase.SaveAssets();
            }

            Debug.Log($"Texturare: {replaced} cutii cu UV la scara reala ({cache.Count} plase, {created} noi).");
        }

        /// <summary>Cub unitate (-0,5..0,5) cu UV in metri pentru dimensiunea data si UV2 pentru lightmap.</summary>
        private static Mesh CreateUVBox(string name, Vector3 size)
        {
            Vector3[] normals =
            {
                Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back
            };

            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> meshNormals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> triangles = new List<int>();

            foreach (Vector3 n in normals)
            {
                // Doua axe in planul fetei: u (orizontal) si v (vertical pe pereti, Z pe podea).
                Vector3 u, v;

                if (n.y != 0f)
                {
                    u = Vector3.right;
                    v = Vector3.forward;
                }
                else
                {
                    u = Vector3.Cross(Vector3.up, n); // orizontal, astfel incat textura sa nu fie in oglinda
                    v = Vector3.up;
                }

                int start = vertices.Count;
                Vector2[] corners = { new Vector2(-0.5f, -0.5f), new Vector2(0.5f, -0.5f), new Vector2(0.5f, 0.5f), new Vector2(-0.5f, 0.5f) };

                foreach (Vector2 c in corners)
                {
                    Vector3 p = n * 0.5f + u * c.x + v * c.y;
                    vertices.Add(p);
                    meshNormals.Add(n);
                    uvs.Add(new Vector2(Vector3.Dot(p, u) * Vector3.Dot(Abs(u), size), Vector3.Dot(p, v) * Vector3.Dot(Abs(v), size)));
                }

                AddTriangle(triangles, vertices, n, start, start + 1, start + 2);
                AddTriangle(triangles, vertices, n, start, start + 2, start + 3);
            }

            Mesh mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetNormals(meshNormals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            Unwrapping.GenerateSecondaryUVSet(mesh);
            return mesh;
        }

        /// <summary>Adauga triunghiul cu orientarea care il face vizibil dinspre normala.</summary>
        private static void AddTriangle(List<int> triangles, List<Vector3> vertices, Vector3 normal, int a, int b, int c)
        {
            Vector3 cross = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);

            if (Vector3.Dot(cross, normal) > 0f)
            {
                triangles.Add(a);
                triangles.Add(b);
                triangles.Add(c);
            }
            else
            {
                triangles.Add(a);
                triangles.Add(c);
                triangles.Add(b);
            }
        }
    }
}
