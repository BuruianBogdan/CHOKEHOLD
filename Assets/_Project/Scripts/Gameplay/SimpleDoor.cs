using System.Collections;
using UnityEngine;

namespace ChokeHold.Gameplay
{
    public class SimpleDoor : MonoBehaviour, IInteractable
    {
        [Header("Door")]
        [SerializeField] private float openAngle = 90f;
        [SerializeField] private float openSpeed = 4f;

        private Quaternion closedRotation;
        private Quaternion openRotation;

        private bool isOpen;
        private bool isMoving;

        public string InteractionText =>
            isOpen ? "Close door" : "Open door";

        private void Awake()
        {
            closedRotation = transform.localRotation;

            openRotation =
                closedRotation *
                Quaternion.Euler(0f, openAngle, 0f);
        }

        public void Interact()
        {
            if (isMoving)
            {
                return;
            }

            isOpen = !isOpen;

            StopAllCoroutines();
            StartCoroutine(RotateDoor());
        }

        private IEnumerator RotateDoor()
        {
            isMoving = true;

            Quaternion targetRotation = isOpen
                ? openRotation
                : closedRotation;

            while (
                Quaternion.Angle(
                    transform.localRotation,
                    targetRotation
                ) > 0.1f
            )
            {
                transform.localRotation =
                    Quaternion.Slerp(
                        transform.localRotation,
                        targetRotation,
                        openSpeed * Time.deltaTime
                    );

                yield return null;
            }

            transform.localRotation = targetRotation;
            isMoving = false;
        }
    }
}