using ChokeHold.Gameplay;
using UnityEditor;
using UnityEngine;

namespace ChokeHold.EditorTools
{
    // Performanta: occlusion culling (nu se mai desenează ce e in spatele peretilor).
    //
    // Peretii si planseele sunt "Occluder Static". Ce se poate sparge sau misca (pereti moi, baricade, usi,
    // trape) nu are voie sa ascunda nimic: dupa ce e distrus, jucatorul din spate trebuie sa se vada.
    public static partial class BlacksiteK17MapBuilder
    {
        [MenuItem("CHOKEHOLD/Map/Bake Occlusion Culling")]
        public static void BakeOcclusionCulling()
        {
            GameObject root = GameObject.Find(RootName);

            if (root == null)
            {
                Debug.LogWarning("Genereaza intai harta (CHOKEHOLD > Map > Build Blacksite K-17).");
                return;
            }

            int cleared = 0;

            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                GameObject go = renderer.gameObject;
                StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(go);

                bool breakable = go.GetComponentInParent<Destructible>() != null ||
                                 go.GetComponentInParent<BarricadePoint>() != null ||
                                 go.GetComponentInParent<SimpleDoor>() != null ||
                                 go.name.Contains("Hatch") || go.transform.parent != null && go.transform.parent.name.Contains("Hatch");

                // Sticla si piesele subtiri din kit nu ascund nimic; doar peretii, planseele si blocurile pline.
                bool kitPiece = renderer.GetComponent<MeshFilter>() != null && renderer.GetComponent<MeshFilter>().sharedMesh != null &&
                                renderer.GetComponent<MeshFilter>().sharedMesh.name.StartsWith("K17_") &&
                                !renderer.GetComponent<MeshFilter>().sharedMesh.name.StartsWith("K17_Container");

                if ((breakable || kitPiece) && (flags & StaticEditorFlags.OccluderStatic) != 0)
                {
                    GameObjectUtility.SetStaticEditorFlags(go, flags & ~StaticEditorFlags.OccluderStatic);
                    cleared++;
                }
            }

            // Celule mai fine: cu 2 m, pe scarile care coboara sub nivelul curtii camera ajungea in aceeasi celula cu
            // placa podelei si cladirile de dincolo de balustrada disparea gresit.
            StaticOcclusionCulling.smallestOccluder = 1f;
            StaticOcclusionCulling.smallestHole = 0.1f;
            StaticOcclusionCulling.backfaceThreshold = 100f;

            bool ok = StaticOcclusionCulling.Compute();
            Debug.Log($"Occlusion culling: {(ok ? "calculat" : "ESUAT")} ({cleared} obiecte destructibile / mobile nu ascund nimic).");
        }
    }
}
