using UnityEngine;

namespace ChokeHold.UI
{
    public class CrosshairUI : MonoBehaviour
    {
        [Header("Lines")]
        [SerializeField] private RectTransform top;
        [SerializeField] private RectTransform bottom;
        [SerializeField] private RectTransform left;
        [SerializeField] private RectTransform right;

        [Header("Settings")]
        [SerializeField] private float minimumGap = 8f;
        [SerializeField] private float maximumGap = 35f;
        [SerializeField] private float smoothSpeed = 12f;
        [SerializeField] private float spreadToPixels = 500f;

        private float targetGap;
        private float currentGap;

        private void Awake()
        {
            currentGap = minimumGap;
            targetGap = minimumGap;
        }

        private void Update()
        {
            currentGap = Mathf.Lerp(
                currentGap,
                targetGap,
                smoothSpeed * Time.deltaTime
            );

            UpdateLinePositions();
        }

        public void SetSpread(float spread)
        {
            targetGap = Mathf.Clamp(
                minimumGap + spread * spreadToPixels,
                minimumGap,
                maximumGap
            );
        }

        private void UpdateLinePositions()
        {
            if (top != null)
            {
                top.anchoredPosition = new Vector2(0f, currentGap);
            }

            if (bottom != null)
            {
                bottom.anchoredPosition = new Vector2(0f, -currentGap);
            }

            if (left != null)
            {
                left.anchoredPosition = new Vector2(-currentGap, 0f);
            }

            if (right != null)
            {
                right.anchoredPosition = new Vector2(currentGap, 0f);
            }
        }
    }
}