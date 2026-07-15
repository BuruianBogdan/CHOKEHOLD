using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ChokeHold.Gameplay
{
    public class PlayerInteraction : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera playerCamera;
        [SerializeField] private TMP_Text interactionText;

        [Header("Interaction")]
        [SerializeField] private float interactionRange = 3f;
        [SerializeField] private LayerMask interactionMask = ~0;

        private IInteractable currentInteractable;

        private void Awake()
        {
            HideInteractionText();
        }

        private void OnEnable()
        {
            HideInteractionText();
        }

        private void Update()
        {
            CheckForInteractable();

            if (
                currentInteractable != null &&
                Keyboard.current != null &&
                Keyboard.current.fKey.wasPressedThisFrame
            )
            {
                currentInteractable.Interact();
            }
        }

        private void CheckForInteractable()
        {
            currentInteractable = null;

            if (playerCamera == null)
            {
                HideInteractionText();
                return;
            }

            Ray ray = new Ray(
                playerCamera.transform.position,
                playerCamera.transform.forward
            );

            bool hasHit = Physics.Raycast(
                ray,
                out RaycastHit hit,
                interactionRange,
                interactionMask,
                QueryTriggerInteraction.Ignore
            );

            if (!hasHit)
            {
                HideInteractionText();
                return;
            }

            currentInteractable =
                hit.collider.GetComponentInParent<IInteractable>();

            if (currentInteractable == null)
            {
                HideInteractionText();
                return;
            }

            ShowInteractionText(
                currentInteractable.InteractionText
            );
        }

        private void ShowInteractionText(string message)
        {
            if (interactionText == null)
            {
                return;
            }

            interactionText.text = $"[F] {message}";
            interactionText.gameObject.SetActive(true);
        }

        private void HideInteractionText()
        {
            if (interactionText == null)
            {
                return;
            }

            interactionText.gameObject.SetActive(false);
        }
    }
}