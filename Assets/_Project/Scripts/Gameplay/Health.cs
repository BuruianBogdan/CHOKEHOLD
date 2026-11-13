using System;
using UnityEngine;

namespace ChokeHold.Gameplay
{
    public class Health : MonoBehaviour
    {
        [SerializeField] private float maximumHealth = 100f;

        [Tooltip("Tintele simple se distrug la moarte. Jucatorii si botii (TeamMember) nu - ei sunt respawnati de RoundManager.")]
        [SerializeField] private bool destroyOnDeath = true;

        private float currentHealth;
        private bool isDead;

        public float CurrentHealth => currentHealth;
        public float MaximumHealth => maximumHealth;
        public bool IsDead => isDead;

        public bool DestroyOnDeath
        {
            get => destroyOnDeath;
            set => destroyOnDeath = value;
        }

        /// <summary>Cat timp e true, obiectul nu primeste damage (ex. perete intarit).</summary>
        public bool Invulnerable { get; set; }

        public event Action<Health, float> Damaged;
        public event Action<Health> Died;

        private void Awake()
        {
            currentHealth = maximumHealth;
        }

        public void TakeDamage(float damage)
        {
            if (isDead || Invulnerable || damage <= 0f)
            {
                return;
            }

            currentHealth = Mathf.Max(0f, currentHealth - damage);
            Debug.Log($"{gameObject.name} mai are {currentHealth} viata.");

            Damaged?.Invoke(this, damage);

            if (currentHealth <= 0f)
            {
                Die();
            }
        }

        public void ResetHealth()
        {
            currentHealth = maximumHealth;
            isDead = false;
        }

        private void Die()
        {
            isDead = true;
            Debug.Log($"{gameObject.name} a fost eliminat");

            Died?.Invoke(this);

            if (destroyOnDeath)
            {
                Destroy(gameObject);
            }
        }
    }
}
