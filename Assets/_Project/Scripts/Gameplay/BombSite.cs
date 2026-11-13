using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChokeHold.Gameplay
{
    /// <summary>
    /// Zona de plantare a bombei. Tine evidenta jucatorilor aflati in zona,
    /// ca logica de plant/defuse (de facut) sa poata intreba IsPlayerInside.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class BombSite : MonoBehaviour
    {
        [SerializeField] private string siteId = "A";

        private readonly HashSet<FpsController> playersInside =
            new HashSet<FpsController>();

        public string SiteId => siteId;

        public event Action<BombSite, FpsController> PlayerEntered;
        public event Action<BombSite, FpsController> PlayerExited;

        public bool IsPlayerInside(FpsController player)
        {
            if (player == null)
            {
                return false;
            }

            // Verificare directa pe volum: evenimentele de trigger pot rata iesirea
            // cand jucatorul e teleportat la respawn.
            Vector3 chest = player.transform.position + Vector3.up;
            return GetComponent<BoxCollider>().bounds.Contains(chest);
        }

        private void Reset()
        {
            GetComponent<BoxCollider>().isTrigger = true;
        }

        private void Awake()
        {
            GetComponent<BoxCollider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            FpsController player =
                other.GetComponentInParent<FpsController>();

            if (player != null && playersInside.Add(player))
            {
                PlayerEntered?.Invoke(this, player);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            FpsController player =
                other.GetComponentInParent<FpsController>();

            if (player != null && playersInside.Remove(player))
            {
                PlayerExited?.Invoke(this, player);
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
            Gizmos.color = new Color(1f, 0.8f, 0.1f, 0.15f);
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = new Color(1f, 0.8f, 0.1f, 0.9f);
            Gizmos.DrawWireCube(box.center, box.size);
        }
    }
}
