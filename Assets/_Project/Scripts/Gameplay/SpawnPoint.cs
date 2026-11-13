using UnityEngine;

namespace ChokeHold.Gameplay
{
    /// <summary>
    /// Punct de spawn pentru o echipa. GameMode-ul (de facut) va cauta toate
    /// SpawnPoint-urile din scena si va alege unul liber pentru fiecare jucator.
    /// </summary>
    public class SpawnPoint : MonoBehaviour
    {
        [SerializeField] private Team team = Team.Attackers;

        public Team Team => team;

        private void OnDrawGizmos()
        {
            Gizmos.color = team == Team.Attackers
                ? new Color(0.2f, 0.45f, 1f)
                : new Color(1f, 0.25f, 0.2f);

            Vector3 position = transform.position;

            Gizmos.DrawWireSphere(position + Vector3.up * 1f, 0.5f);
            Gizmos.DrawLine(position, position + Vector3.up * 2f);
            Gizmos.DrawLine(
                position + Vector3.up * 1f,
                position + Vector3.up * 1f + transform.forward * 1.5f
            );
        }
    }
}
