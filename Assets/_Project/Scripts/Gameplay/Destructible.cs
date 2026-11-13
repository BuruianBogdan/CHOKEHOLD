using System.Collections.Generic;
using UnityEngine;

namespace ChokeHold.Gameplay
{
    /// <summary>
    /// Obiect care se sparge cu gloante (perete moale, trapa, baricada) si se reface la runda urmatoare.
    /// Aparatorii il pot intari: un obiect intarit nu mai primeste damage pana la sfarsitul rundei.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class Destructible : MonoBehaviour
    {
        [SerializeField] private bool canBeReinforced = true;
        [SerializeField] private Material reinforcedMaterial;

        private static readonly List<Destructible> all = new List<Destructible>();

        private Health health;
        private Renderer[] renderers;
        private Collider[] colliders;
        private Material[] originalMaterials;
        private bool initialized;

        public bool IsReinforced { get; private set; }
        public bool IsBroken => health != null && health.IsDead;
        public bool CanBeReinforced => canBeReinforced && !IsReinforced && !IsBroken;

        /// <summary>Reface toate obiectele destructibile active (apelat la inceput de runda).</summary>
        public static void ResetAll()
        {
            foreach (Destructible destructible in all)
            {
                destructible.ResetState();
            }
        }

        private void Awake()
        {
            Initialize();
        }

        private void Initialize()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;

            health = GetComponent<Health>();
            health.DestroyOnDeath = false;

            renderers = GetComponentsInChildren<Renderer>(true);
            colliders = GetComponentsInChildren<Collider>(true);

            originalMaterials = new Material[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                originalMaterials[i] = renderers[i].sharedMaterial;
            }
        }

        private void OnEnable()
        {
            Initialize();
            all.Add(this);
            health.Died += OnDied;
        }

        private void OnDisable()
        {
            all.Remove(this);
            health.Died -= OnDied;
        }

        public void Reinforce()
        {
            if (!CanBeReinforced)
            {
                return;
            }

            IsReinforced = true;
            health.Invulnerable = true;

            if (reinforcedMaterial != null)
            {
                foreach (Renderer partRenderer in renderers)
                {
                    partRenderer.sharedMaterial = reinforcedMaterial;
                }
            }
        }

        public void ResetState()
        {
            Initialize();

            IsReinforced = false;
            health.Invulnerable = false;
            health.ResetHealth();

            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].sharedMaterial = originalMaterials[i];
            }

            SetPresent(true);
        }

        private void OnDied(Health _)
        {
            SetPresent(false);
        }

        private void SetPresent(bool present)
        {
            foreach (Renderer partRenderer in renderers)
            {
                partRenderer.enabled = present;
            }

            foreach (Collider partCollider in colliders)
            {
                partCollider.enabled = present;
            }
        }
    }
}
