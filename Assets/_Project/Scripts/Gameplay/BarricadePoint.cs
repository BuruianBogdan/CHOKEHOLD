using System.Collections.Generic;
using UnityEngine;

namespace ChokeHold.Gameplay
{
    /// <summary>
    /// Deschidere (usa, trecere) pe care aparatorii pot monta o baricada.
    /// Baricada este un panou Destructible: blocheaza trecerea si vederea pana e sparta.
    /// Trigger-ul de pe acest obiect acopera deschiderea, ca jucatorul sa o poata tinti si cand e goala.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class BarricadePoint : MonoBehaviour
    {
        [SerializeField] private GameObject panel;

        private static readonly List<BarricadePoint> all = new List<BarricadePoint>();

        private Destructible panelDestructible;

        public bool IsBuilt =>
            panel != null &&
            panel.activeSelf &&
            (panelDestructible == null || !panelDestructible.IsBroken);

        /// <summary>Scoate toate baricadele (apelat la inceput de runda).</summary>
        public static void ResetAll()
        {
            foreach (BarricadePoint point in all)
            {
                if (point.panel != null)
                {
                    point.panel.SetActive(false);
                }
            }
        }

        private void Awake()
        {
            GetComponent<BoxCollider>().isTrigger = true;

            if (panel != null)
            {
                panelDestructible = panel.GetComponent<Destructible>();
                panel.SetActive(false);
            }
        }

        private void OnEnable()
        {
            all.Add(this);
        }

        private void OnDisable()
        {
            all.Remove(this);
        }

        public void Build()
        {
            if (panel == null || IsBuilt)
            {
                return;
            }

            panel.SetActive(true);

            if (panelDestructible != null)
            {
                panelDestructible.ResetState();
            }
        }

        private void OnDrawGizmos()
        {
            BoxCollider box = GetComponent<BoxCollider>();

            if (box == null)
            {
                return;
            }

            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.85f, 0.55f, 0.2f, 0.8f);
            Gizmos.DrawWireCube(box.center, box.size);
        }
    }
}
