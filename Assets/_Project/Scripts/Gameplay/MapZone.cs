using UnityEngine;

namespace ChokeHold.Gameplay
{
    /// <summary>
    /// Marker de level design: deseneaza conturul si numele unei zone
    /// (A Site, Mid Lab etc.) in Scene view. Nu are efect in joc.
    /// </summary>
    public class MapZone : MonoBehaviour
    {
        [SerializeField] private string displayName = "Zone";
        [SerializeField] private Vector3 size = new Vector3(10f, 4f, 10f);
        [SerializeField] private Color color = new Color(0.4f, 0.9f, 0.4f, 1f);

        public string DisplayName => displayName;
        public Vector3 Size => size;

        private void OnDrawGizmos()
        {
            Gizmos.color = color;
            Gizmos.DrawWireCube(
                transform.position + Vector3.up * size.y * 0.5f,
                size
            );

#if UNITY_EDITOR
            UnityEditor.Handles.color = color;
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * (size.y + 0.5f),
                displayName
            );
#endif
        }
    }
}
