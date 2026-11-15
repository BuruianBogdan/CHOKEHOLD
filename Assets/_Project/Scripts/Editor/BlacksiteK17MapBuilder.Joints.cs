using System.Collections.Generic;
using ChokeHold.Gameplay;
using UnityEngine;

namespace ChokeHold.EditorTools
{
    // Imbinari: elementele construite trebuie sa fie legate intre ele, fara fante.
    //
    //   - capetele peretilor despartitori se prelungesc pana ating zidul de langa ele (in Partition);
    //   - recuzita (cover, containere, utilaje, stanci, rezervoare) aflata la mai putin de 0,8 m de un
    //     perete sau de alt obiect e lipita de acesta: ori sta lipita, ori lasa loc de trecere.
    //
    // Validatorul (sectiunea 7) verifica rezultatul.
    public static partial class BlacksiteK17MapBuilder
    {
        private const float JointSlitMin = 0.03f;
        private const float JointSlitMax = 0.8f;
        private const float PartitionSnap = 1f;

        /// <summary>
        /// Distanta (0..1 m) de la capatul unui perete, pe directia data, pana la primul element de
        /// constructie. 0 daca nu e nimic in raza de 1 m sau daca capatul e deja lipit.
        /// </summary>
        private static float GapToStructure(Vector3 end, Vector3 direction, float topY)
        {
            Physics.SyncTransforms();

            float best = float.MaxValue;

            // Raze pe toata grosimea peretelui: una pe axa poate aluneca exact pe muchia zidului.
            Vector3 side = Vector3.Cross(Vector3.up, direction).normalized;

            foreach (float h in new[] { 0.5f, 1.5f, Mathf.Max(0.5f, topY - 0.5f) })
            {
                foreach (float lateral in new[] { -0.2f, 0f, 0.2f })
                {
                    Vector3 origin = end + Vector3.up * h + side * lateral - direction * 0.01f;

                    if (Physics.CheckSphere(origin, 0.004f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        return 0f; // capatul e deja in contact
                    }

                    if (Physics.Raycast(origin, direction, out RaycastHit hit, PartitionSnap, ~0, QueryTriggerInteraction.Ignore) &&
                        IsConstruction(hit.collider))
                    {
                        best = Mathf.Min(best, hit.distance - 0.01f);
                    }
                }
            }

            return best < JointSlitMin || best > PartitionSnap ? 0f : best;
        }

        /// <summary>Pereti, plansee, scari: lucruri de care se pot lipi alte elemente.</summary>
        private static bool IsConstruction(Collider c)
        {
            return c != null && !c.isTrigger &&
                   c.GetComponentInParent<SimpleDoor>() == null &&
                   c.GetComponentInParent<BarricadePoint>() == null &&
                   c.GetComponentInParent<TeamMember>() == null &&
                   !c.name.Contains("Rail") &&
                   c.name != "WalkSurface" &&
                   !(c is MeshCollider && !c.name.Contains("Tank"));
        }

        private static readonly string[] SnapPropKeywords =
        {
            "Crate", "Cover", "Container", "Tank", "Rock", "Machine", "Shelf", "Generator", "Lockers", "Shed", "Tower"
        };

        private static bool IsSnappableProp(Collider c)
        {
            if (c is MeshCollider && !c.name.Contains("Tank"))
            {
                return false; // tevile trec intentionat prin pereti
            }

            foreach (string keyword in SnapPropKeywords)
            {
                if (c.name.Contains(keyword))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Lipeste recuzita de cel mai apropiat element aflat la mai putin de 0,8 m: o fanta prin care
        /// nu poti trece arata neterminat si e un loc bun de pixel-peek.
        /// </summary>
        private static void SnapPropsToNeighbours()
        {
            List<Collider> props = new List<Collider>();

            foreach (Transform group in new[] { gCover, gStructures })
            {
                foreach (Collider c in group.GetComponentsInChildren<Collider>())
                {
                    if (!c.isTrigger && IsSnappableProp(c))
                    {
                        props.Add(c);
                    }
                }
            }

            int moved = 0;

            for (int pass = 0; pass < 3; pass++)
            {
                Physics.SyncTransforms();
                int movedThisPass = 0;

                foreach (Collider prop in props)
                {
                    if (TrySnap(prop))
                    {
                        movedThisPass++;
                        Physics.SyncTransforms();
                    }
                }

                moved += movedThisPass;

                if (movedThisPass == 0)
                {
                    break;
                }
            }

            Debug.Log($"Imbinari: {moved} mutari de recuzita pentru a inchide fantele.");
        }

        private static bool TrySnap(Collider prop)
        {
            Transform t = prop.transform;
            bool box = prop is BoxCollider;

            Vector3 half = box ? t.lossyScale * 0.5f : prop.bounds.extents;
            Vector3 right = box ? t.right : Vector3.right;
            Vector3 forward = box ? t.forward : Vector3.forward;
            Vector3 center = box ? t.position : prop.bounds.center;
            float baseY = center.y - half.y;

            Vector3[] axes = { right, -right, forward, -forward };
            float[] extents = { half.x, half.x, half.z, half.z };
            Vector3[] alongs = { forward, forward, right, right };
            float[] lengths = { half.z, half.z, half.x, half.x };

            float bestDistance = float.MaxValue;
            Vector3 bestAxis = Vector3.zero;

            for (int f = 0; f < 4; f++)
            {
                float faceMin = float.MaxValue;
                float faceMax = 0f;
                HashSet<float> hitPositions = new HashSet<float>();
                bool touching = false;

                foreach (float s in new[] { -0.8f, 0f, 0.8f })
                {
                    foreach (float h in new[] { 0.4f, Mathf.Min(1.5f, half.y * 2f - 0.2f) })
                    {
                        Vector3 origin = center + axes[f] * (extents[f] + 0.005f) + alongs[f] * (s * lengths[f]);
                        origin.y = baseY + h;

                        // Fata e deja lipita de ceva: nu o mai mutam (o raza pornita din interior ar sari peste).
                        if (Physics.CheckSphere(origin, 0.004f, ~0, QueryTriggerInteraction.Ignore))
                        {
                            touching = true;
                            continue;
                        }

                        if (Physics.Raycast(origin, axes[f], out RaycastHit hit, JointSlitMax, ~0, QueryTriggerInteraction.Ignore) &&
                            hit.collider != prop && IsConstruction(hit.collider) && hit.distance > JointSlitMin)
                        {
                            faceMin = Mathf.Min(faceMin, hit.distance);
                            faceMax = Mathf.Max(faceMax, hit.distance);
                            hitPositions.Add(s);
                        }
                    }
                }

                // Mutam doar spre o suprafata paralela, vazuta din cel putin doua puncte ale fetei
                // la aceeasi distanta; altfel am impinge obiectul intr-un colt sau intr-un perete oblic.
                if (!touching && hitPositions.Count >= 2 && faceMax - faceMin < 0.05f && faceMin < bestDistance)
                {
                    bestDistance = faceMin;
                    bestAxis = axes[f];
                }
            }

            if (bestAxis == Vector3.zero)
            {
                return false;
            }

            Vector3 offset = bestAxis * (bestDistance - 0.005f);

            // Containerele stivuite se muta impreuna cu cel de dedesubt.
            if (prop.name.StartsWith("Container"))
            {
                foreach (Collider other in gCover.GetComponentsInChildren<Collider>())
                {
                    if (other != prop && other.name.StartsWith("Container") &&
                        other.bounds.min.y > prop.bounds.max.y - 0.05f &&
                        Mathf.Abs(other.bounds.center.x - prop.bounds.center.x) < 0.1f &&
                        Mathf.Abs(other.bounds.center.z - prop.bounds.center.z) < 0.1f)
                    {
                        other.transform.position += offset;
                    }
                }
            }

            t.position += offset;
            return true;
        }
    }
}
