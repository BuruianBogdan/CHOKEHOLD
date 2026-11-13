using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChokeHold.Gameplay
{
    /// <summary>
    /// Orice participant la runda: jucatorul, un bot sau un manechin de test.
    /// Leaga Health de echipa si stie sa "moara" si sa fie respawnat fara a fi distrus.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class TeamMember : MonoBehaviour
    {
        [SerializeField] private Team team = Team.Attackers;

        [Tooltip("Jucatorii si botii pleaca din SpawnPoint-urile echipei. Manechinele de test raman unde au fost puse.")]
        [SerializeField] private bool respawnAtSpawnPoint = true;

        private static readonly List<TeamMember> all = new List<TeamMember>();

        private Health health;
        private FpsController fpsController;
        private HitscanWeapon weapon;
        private CharacterController characterController;
        private Renderer[] renderers;
        private Collider[] colliders;

        private Vector3 startPosition;
        private Quaternion startRotation;

        public static IReadOnlyList<TeamMember> All => all;

        /// <summary>Anunta moartea oricarui membru (RoundManager verifica eliminarea).</summary>
        public static event Action<TeamMember> AnyDied;

        public Team Team => team;
        public Health Health => health;
        public FpsController FpsController => fpsController;
        public bool IsAlive => health != null && !health.IsDead;
        public bool IsLocalPlayer => fpsController != null;
        public bool RespawnsAtSpawnPoint => respawnAtSpawnPoint;

        private void Awake()
        {
            health = GetComponent<Health>();
            health.DestroyOnDeath = false;

            fpsController = GetComponent<FpsController>();
            weapon = GetComponentInChildren<HitscanWeapon>(true);
            characterController = GetComponent<CharacterController>();

            if (!IsLocalPlayer)
            {
                renderers = GetComponentsInChildren<Renderer>(true);
                colliders = GetComponentsInChildren<Collider>(true);
            }

            startPosition = transform.position;
            startRotation = transform.rotation;
        }

        private void OnEnable()
        {
            all.Add(this);
            health.Died += OnHealthDied;
        }

        private void OnDisable()
        {
            all.Remove(this);
            health.Died -= OnHealthDied;
        }

        public void SetTeam(Team newTeam)
        {
            team = newTeam;
        }

        public void Respawn(Vector3 position, Quaternion rotation)
        {
            // CharacterController suprascrie pozitia daca e activ in timpul teleportarii.
            if (characterController != null)
            {
                characterController.enabled = false;
            }

            transform.SetPositionAndRotation(position, rotation);

            if (characterController != null)
            {
                characterController.enabled = true;
            }

            health.ResetHealth();
            SetAliveState(true);

            if (weapon != null)
            {
                weapon.RefillAmmo();
            }
        }

        public void RespawnAtStart()
        {
            Respawn(startPosition, startRotation);
        }

        private void OnHealthDied(Health _)
        {
            SetAliveState(false);
            AnyDied?.Invoke(this);
        }

        private void SetAliveState(bool alive)
        {
            if (IsLocalPlayer)
            {
                // Jucatorul mort ramane pe loc si poate doar sa se uite in jur.
                fpsController.SetInputLock(this, !alive);

                if (weapon != null)
                {
                    weapon.enabled = alive;
                }

                return;
            }

            foreach (Renderer memberRenderer in renderers)
            {
                if (memberRenderer != null)
                {
                    memberRenderer.enabled = alive;
                }
            }

            foreach (Collider memberCollider in colliders)
            {
                if (memberCollider != null)
                {
                    memberCollider.enabled = alive;
                }
            }
        }
    }
}
