using UnityEngine;
using UnityEngine.UI;

namespace ChokeHold.Gameplay.Grenades
{
    /// <summary>
    /// Efectele pe ecranul jucatorului local: albul de la flash si orbirea pe o parte de la grenada cu ace
    /// (granita curbata, estompata, crucea ramane vizibila). Isi construieste singur Canvas-ul.
    /// </summary>
    public class GrenadeScreenEffects : MonoBehaviour
    {
        private Image flashImage;
        private RawImage needleImage;
        private Material needleMaterial;

        private float flashStrength;
        private float flashHoldUntil;
        private float flashFadeEnd;
        private float flashFadeLength = 1f;

        private int needleSide = 1;
        private float needleTarget;
        private float needleCoverage;
        private float needleStart;
        private float needleEnd;
        private float needlePulse;

        /// <summary>Flash: alb la intensitatea strength (0-1) timp de hold secunde, apoi se stinge in fade secunde.</summary>
        public void Flash(float strength, float hold, float fade)
        {
            Build();
            // un flash mai slab nu scurteaza unul mai puternic deja in curs
            if (strength < CurrentFlashAlpha() && Time.time < flashFadeEnd)
            {
                return;
            }

            flashStrength = Mathf.Clamp01(strength);
            flashHoldUntil = Time.time + hold;
            flashFadeLength = Mathf.Max(0.05f, fade);
            flashFadeEnd = flashHoldUntil + flashFadeLength;
        }

        /// <summary>Ace: side 1 = dreapta, -1 = stanga; mai multe ace = mai mult timp (2,5 -> 4,5 s).</summary>
        public void NeedleBlind(int side, int needles, float strength)
        {
            Build();
            float duration = Mathf.Lerp(2.5f, 4.5f, Mathf.Clamp01((needles - 1) / 5f));
            needleSide = side >= 0 ? 1 : -1;
            needleTarget = Mathf.Clamp01(strength);
            needleStart = Time.time;
            needleEnd = Time.time + duration;
            needlePulse = 1f;
        }

        public void Clear()
        {
            flashFadeEnd = 0f;
            flashHoldUntil = 0f;
            needleEnd = 0f;
            needleCoverage = 0f;
            needlePulse = 0f;
        }

        /// <summary>Pentru capturi: cat acopera acum fiecare efect.</summary>
        public float FlashAlpha => CurrentFlashAlpha();
        public float NeedleCoverage => needleCoverage;

        private float CurrentFlashAlpha()
        {
            if (Time.time <= flashHoldUntil)
            {
                return flashStrength;
            }
            if (Time.time >= flashFadeEnd)
            {
                return 0f;
            }
            float t = (Time.time - flashHoldUntil) / flashFadeLength;
            return flashStrength * (1f - t) * (1f - t);
        }

        private void Update()
        {
            if (flashImage == null)
            {
                return;
            }

            float alpha = CurrentFlashAlpha();
            flashImage.color = new Color(1f, 1f, 1f, alpha);
            flashImage.enabled = alpha > 0.001f;

            // ace: intra in 0,1 s, sta, iar in ultimele 1,2 s se retrage spre margine
            float target = 0f;
            if (Time.time < needleEnd)
            {
                float left = needleEnd - Time.time;
                target = needleTarget * Mathf.Clamp01(left / 1.2f);
            }
            float speed = target > needleCoverage ? 10f : 4f;
            needleCoverage = Mathf.MoveTowards(needleCoverage, target, Time.deltaTime * speed);
            needlePulse = Mathf.MoveTowards(needlePulse, 0f, Time.deltaTime * 2.2f);

            bool show = needleCoverage > 0.001f || needlePulse > 0.001f;
            needleImage.enabled = show;
            if (show)
            {
                needleMaterial.SetFloat("_Side", needleSide);
                needleMaterial.SetFloat("_Coverage", Mathf.SmoothStep(0f, 1f, needleCoverage));
                needleMaterial.SetFloat("_Pulse", needlePulse);
            }
        }

        private void Build()
        {
            if (flashImage != null)
            {
                return;
            }

            GameObject canvasObject = new GameObject("GrenadeScreen Canvas");
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 55;   // peste HUD si cruce, sub meniul de cumparare
            RectTransform root = canvasObject.GetComponent<RectTransform>();

            needleMaterial = new Material(Resources.Load<Shader>("Shaders/ChokeHoldNeedleBlind"));
            needleImage = Full<RawImage>(root, "NeedleBlind");
            needleImage.material = needleMaterial;
            needleImage.color = Color.white;
            needleImage.enabled = false;

            flashImage = Full<Image>(root, "Flash");
            flashImage.color = new Color(1f, 1f, 1f, 0f);
            flashImage.enabled = false;
        }

        private static T Full<T>(RectTransform parent, string name) where T : Graphic
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            T graphic = go.AddComponent<T>();
            graphic.raycastTarget = false;
            return graphic;
        }
    }
}
