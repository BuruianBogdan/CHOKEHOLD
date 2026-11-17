using System.Collections.Generic;
using UnityEngine;

namespace ChokeHold.Gameplay
{
    /// <summary>
    /// Ragdoll-ul personajului: corpurile rigide si colliderele de pe oase (construite de CharacterPrefabBuilder)
    /// stau cinematice si dezactivate cat timp personajul traieste; la moarte Animator-ul se opreste, corpurile
    /// devin dinamice, iar hitbox-urile de lovire se dezactiveaza. La respawn totul revine.
    /// </summary>
    public class Ragdoll : MonoBehaviour
    {
        [SerializeField] private float deathImpulse = 2.5f;

        private Animator animator;
        private Rigidbody[] bodies;
        private Collider[] ragdollColliders;
        private Collider[] hitboxColliders;
        private Vector3 lastHitDirection = Vector3.forward;
        private bool active;

        public bool IsActive => active;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            bodies = GetComponentsInChildren<Rigidbody>(true);

            List<Collider> ragdoll = new List<Collider>();
            List<Collider> hitboxes = new List<Collider>();

            foreach (Collider collider in GetComponentsInChildren<Collider>(true))
            {
                if (collider.GetComponent<HitZone>() != null)
                {
                    hitboxes.Add(collider);
                }
                else if (collider.GetComponent<Rigidbody>() != null)
                {
                    ragdoll.Add(collider);
                }
            }

            ragdollColliders = ragdoll.ToArray();
            hitboxColliders = hitboxes.ToArray();
            SetAlive(true);
        }

        /// <summary>Directia ultimei lovituri (de la HitscanWeapon), pentru impulsul de la moarte.</summary>
        public void NoteHit(Vector3 direction)
        {
            if (direction.sqrMagnitude > 0.001f)
            {
                lastHitDirection = direction.normalized;
            }
        }

        public void SetAlive(bool alive)
        {
            active = !alive;

            if (animator != null)
            {
                animator.enabled = alive;
            }

            foreach (Collider collider in hitboxColliders)
            {
                collider.enabled = alive;
            }

            foreach (Collider collider in ragdollColliders)
            {
                collider.enabled = !alive;
            }

            foreach (Rigidbody body in bodies)
            {
                body.isKinematic = alive;
                body.detectCollisions = !alive;

                if (!alive)
                {
                    body.linearVelocity = lastHitDirection * deathImpulse;
                }
            }
        }
    }
}
