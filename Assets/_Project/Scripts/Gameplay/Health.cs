using UnityEngine;

namespace ChokeHold.Gameplay
{
    public class Health : MonoBehaviour
    {
        [SerializeField] private float maximumHealth = 100f;
        
        private float currentHealth;
        
        private void Awake()
        {
            currentHealth = maximumHealth;
        }
        
        public void TakeDamage(float damage)
        {
            currentHealth -= damage;
            Debug.Log($"{gameObject.name} mai are {currentHealth} viata.");
            if (currentHealth <= 0f)
            {
                Die();
            }
        }

        private void Die()
        {
            Debug.Log($"{gameObject.name} a fost eliminat");
            Destroy(gameObject);
        }
    }
}