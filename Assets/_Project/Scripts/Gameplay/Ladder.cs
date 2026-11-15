using System.Collections.Generic;
using UnityEngine;

namespace ChokeHold.Gameplay
{
    /// <summary>
    /// Scara de mana: un volum (trigger) lipit de perete. Cat timp jucatorul e in volum, W urca, S coboara.
    /// transform.forward iese din perete (spre locul de unde vii).
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class Ladder : MonoBehaviour
    {
        private static readonly List<Ladder> all = new List<Ladder>();

        [SerializeField] private float climbSpeed = 3.2f;

        private BoxCollider volume;

        public static IReadOnlyList<Ladder> All => all;
        public float ClimbSpeed => climbSpeed;
        public Vector3 Outward => transform.forward;

        /// <summary>Piciorul scarii (la podea) si capatul de sus (la nivelul acoperisului).</summary>
        public Vector3 Bottom => transform.position;
        public Vector3 Top => transform.position + Vector3.up * Height;
        public float Height => Volume.size.y - 1f;

        private BoxCollider Volume => volume != null ? volume : (volume = GetComponent<BoxCollider>());

        private void Awake()
        {
            volume = GetComponent<BoxCollider>();
            volume.isTrigger = true;
        }

        private void OnEnable()
        {
            if (volume == null)
            {
                volume = GetComponent<BoxCollider>();
            }

            all.Add(this);
        }

        private void OnDisable()
        {
            all.Remove(this);
        }

        public bool Contains(Vector3 point)
        {
            Vector3 local = transform.InverseTransformPoint(point) - Volume.center;
            Vector3 half = Volume.size * 0.5f;
            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
        }

        public static Ladder Find(Vector3 point)
        {
            foreach (Ladder ladder in all)
            {
                if (ladder.Contains(point))
                {
                    return ladder;
                }
            }

            return null;
        }
    }
}
