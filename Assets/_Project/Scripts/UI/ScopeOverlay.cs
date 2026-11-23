using ChokeHold.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ChokeHold.UI
{
    /// <summary>
    /// Ecranul de luneta: negru cu un cerc transparent si reticulul (fire subtiri pentru lunetiste, chevron si
    /// cerc pentru ACOG). Texturile se genereaza din cod, deci nu depinde de asset-uri.
    /// </summary>
    public class ScopeOverlay : MonoBehaviour
    {
        private GameObject root;
        private GameObject sniperReticle;
        private GameObject acogReticle;

        public static ScopeOverlay Create(Transform owner)
        {
            GameObject go = new GameObject("ScopeOverlay");
            go.transform.SetParent(owner, false);
            ScopeOverlay overlay = go.AddComponent<ScopeOverlay>();
            overlay.Build();
            overlay.Show(false, SightType.None);
            return overlay;
        }

        public void Show(bool visible, SightType sight)
        {
            if (root == null)
            {
                return;
            }

            root.SetActive(visible);
            sniperReticle.SetActive(visible && sight == SightType.SniperScope);
            acogReticle.SetActive(visible && sight == SightType.Acog);
        }

        private void Build()
        {
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;      // sub HUD (50), peste lume
            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;     // cercul ramane rotund, raportat la inaltime

            root = new GameObject("Scope", typeof(RectTransform));
            root.transform.SetParent(transform, false);
            Stretch(root.GetComponent<RectTransform>());

            // Cercul: un patrat de inaltimea ecranului cu gaura rotunda, plus benzi negre pe laturi
            float size = 1080f;
            Image lens = Rect(root.transform, "Lens", Vector2.zero, new Vector2(size, size), Color.white);
            lens.sprite = CircleMask(512);
            Image leftBand = Rect(root.transform, "Left", new Vector2(-size, 0f), new Vector2(size, size * 2f), Color.black);
            Image rightBand = Rect(root.transform, "Right", new Vector2(size, 0f), new Vector2(size, size * 2f), Color.black);

            // Reticul de lunetista: fire subtiri pe tot cercul, mai groase spre margine
            sniperReticle = new GameObject("Sniper", typeof(RectTransform));
            sniperReticle.transform.SetParent(root.transform, false);
            Color line = new Color(0f, 0f, 0f, 0.95f);
            Rect(sniperReticle.transform, "H", Vector2.zero, new Vector2(size, 2f), line);
            Rect(sniperReticle.transform, "V", Vector2.zero, new Vector2(2f, size), line);
            Rect(sniperReticle.transform, "HL", new Vector2(-size * 0.36f, 0f), new Vector2(size * 0.28f, 6f), line);
            Rect(sniperReticle.transform, "HR", new Vector2(size * 0.36f, 0f), new Vector2(size * 0.28f, 6f), line);
            Rect(sniperReticle.transform, "VB", new Vector2(0f, -size * 0.36f), new Vector2(6f, size * 0.28f), line);

            // ACOG: chevron rosu deasupra centrului si cerc fin
            acogReticle = new GameObject("Acog", typeof(RectTransform));
            acogReticle.transform.SetParent(root.transform, false);
            Color red = new Color(0.95f, 0.15f, 0.1f, 0.95f);
            Image l = Rect(acogReticle.transform, "ChevronL", new Vector2(-7f, -6f), new Vector2(3f, 18f), red);
            l.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -40f);
            Image r = Rect(acogReticle.transform, "ChevronR", new Vector2(7f, -6f), new Vector2(3f, 18f), red);
            r.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 40f);
            Rect(acogReticle.transform, "Drop", new Vector2(0f, -60f), new Vector2(2f, 70f), new Color(0f, 0f, 0f, 0.8f));
            Image ring = Rect(acogReticle.transform, "Ring", Vector2.zero, new Vector2(size * 0.85f, size * 0.85f), new Color(0f, 0f, 0f, 0.6f));
            ring.sprite = Ring(512, 0.985f);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Image Rect(Transform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>Patrat negru cu cerc transparent (margine usor estompata, ca o lentila).</summary>
        private static Sprite CircleMask(int size)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float r = size * 0.5f;
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - r) * (x + 0.5f - r) + (y + 0.5f - r) * (y + 0.5f - r)) / r;
                    float alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.93f, 1f, d));
                    // umbra spre margine: lentila se intuneca usor
                    float vignette = Mathf.Lerp(0f, 0.35f, Mathf.InverseLerp(0.6f, 0.93f, d));
                    byte a = (byte)(Mathf.Clamp01(Mathf.Max(alpha, vignette)) * 255f);
                    pixels[y * size + x] = new Color32(0, 0, 0, a);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        private static Sprite Ring(int size, float inner)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float r = size * 0.5f;
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - r) * (x + 0.5f - r) + (y + 0.5f - r) * (y + 0.5f - r)) / r;
                    byte a = (byte)(d > inner && d < 1f ? 255 : 0);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }
    }
}
