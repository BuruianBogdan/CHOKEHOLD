using System.Collections;
using UnityEngine;

namespace ChokeHold.UI
{
    public class HitMarkerUI : MonoBehaviour
    {
        [SerializeField] private float visibleTime = 0.1f;

        private Coroutine hideCoroutine;

        private void Awake()
        {
            gameObject.SetActive(false);
        }

        public void Show()
        {
            Debug.Log("HIT MARKER SHOW");
            gameObject.SetActive(true);

            if (hideCoroutine != null)
            {
                StopCoroutine(hideCoroutine);
            }

            hideCoroutine = StartCoroutine(HideAfterDelay());
        }

        private IEnumerator HideAfterDelay()
        {
            yield return new WaitForSeconds(visibleTime);

            gameObject.SetActive(false);
            hideCoroutine = null;
        }
    }
}