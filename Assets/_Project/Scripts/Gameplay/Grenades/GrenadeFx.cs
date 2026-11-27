using UnityEngine;

namespace ChokeHold.Gameplay.Grenades
{
    /// <summary>
    /// Modelele grenadelor (prefab din Resources/Grenades daca exista, altfel unul provizoriu din primitive) si
    /// efectele vizuale facute din cod: particule, lumini, liniile acelor.
    /// </summary>
    public static class GrenadeFx
    {
        private static Material alphaMaterial;
        private static Material additiveMaterial;
        private static Texture2D softTexture;
        private static Shader litShader;

        // ------------------------------------------------------------------
        // Modelul
        // ------------------------------------------------------------------

        public static GameObject CreateModel(GrenadeType type)
        {
            GrenadeSpec spec = GrenadeSpec.Of(type);
            GameObject prefab = Resources.Load<GameObject>($"Grenades/{spec.Prefab}");
            if (prefab != null)
            {
                GameObject instance = Object.Instantiate(prefab);
                foreach (Collider c in instance.GetComponentsInChildren<Collider>(true))
                {
                    Object.Destroy(c);
                }
                instance.name = spec.Prefab;
                return instance;
            }

            GameObject root = new GameObject(spec.Prefab);
            Color dark = new Color(0.08f, 0.08f, 0.09f);
            switch (type)
            {
                case GrenadeType.Frag:
                    Part(root, PrimitiveType.Sphere, Vector3.zero, new Vector3(0.062f, 0.07f, 0.062f), spec.Color, 0.35f);
                    Part(root, PrimitiveType.Cylinder, new Vector3(0f, 0.042f, 0f), new Vector3(0.022f, 0.012f, 0.022f), dark, 0.6f);
                    Part(root, PrimitiveType.Cube, new Vector3(0.016f, 0.02f, 0f), new Vector3(0.008f, 0.06f, 0.016f), dark, 0.6f);
                    break;
                case GrenadeType.Smoke:
                    Part(root, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.06f, 0.06f, 0.06f), spec.Color, 0.4f);
                    Part(root, PrimitiveType.Cylinder, new Vector3(0f, 0.015f, 0f), new Vector3(0.062f, 0.008f, 0.062f), new Color(0.85f, 0.85f, 0.82f), 0.3f);
                    Part(root, PrimitiveType.Cylinder, new Vector3(0f, 0.066f, 0f), new Vector3(0.02f, 0.008f, 0.02f), dark, 0.6f);
                    break;
                case GrenadeType.Flash:
                    Part(root, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.05f, 0.055f, 0.05f), spec.Color, 0.5f);
                    Part(root, PrimitiveType.Cylinder, new Vector3(0f, -0.045f, 0f), new Vector3(0.052f, 0.006f, 0.052f), new Color(0.15f, 0.3f, 0.75f), 0.4f);
                    Part(root, PrimitiveType.Cylinder, new Vector3(0f, 0.06f, 0f), new Vector3(0.02f, 0.008f, 0.02f), new Color(0.45f, 0.45f, 0.45f), 0.7f);
                    break;
                case GrenadeType.Molotov:
                    Part(root, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.065f, 0.07f, 0.065f), spec.Color, 0.85f);
                    Part(root, PrimitiveType.Sphere, new Vector3(0f, 0.07f, 0f), new Vector3(0.065f, 0.04f, 0.065f), spec.Color, 0.85f);
                    Part(root, PrimitiveType.Cylinder, new Vector3(0f, 0.105f, 0f), new Vector3(0.024f, 0.03f, 0.024f), spec.Color, 0.85f);
                    Part(root, PrimitiveType.Cube, new Vector3(0.012f, 0.125f, 0f), new Vector3(0.022f, 0.06f, 0.018f), new Color(0.62f, 0.55f, 0.42f), 0.05f);
                    break;
                case GrenadeType.Needle:
                    Part(root, PrimitiveType.Sphere, Vector3.zero, new Vector3(0.068f, 0.06f, 0.068f), spec.Color, 0.6f);
                    Part(root, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.071f, 0.004f, 0.071f), new Color(0.2f, 0.95f, 1f), 0.5f, 2.5f);
                    Part(root, PrimitiveType.Cylinder, new Vector3(0f, 0.036f, 0f), new Vector3(0.02f, 0.008f, 0.02f), dark, 0.6f);
                    break;
            }
            return root;
        }

        private static void Part(GameObject root, PrimitiveType shape, Vector3 position, Vector3 scale, Color color, float smoothness, float emission = 0f)
        {
            GameObject part = GameObject.CreatePrimitive(shape);
            Object.Destroy(part.GetComponent<Collider>());
            part.transform.SetParent(root.transform, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;

            litShader ??= Shader.Find("Universal Render Pipeline/Lit");
            Material material = new Material(litShader);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", 0.3f);
            if (emission > 0f)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * emission);
            }
            part.GetComponent<Renderer>().sharedMaterial = material;
        }

        public static void SetLayer(GameObject go, int layer, bool shadows)
        {
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = layer;
            }
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        // ------------------------------------------------------------------
        // Particule
        // ------------------------------------------------------------------

        public static Material Alpha => alphaMaterial != null ? alphaMaterial : alphaMaterial = NewParticleMaterial(10f);
        public static Material Additive => additiveMaterial != null ? additiveMaterial : additiveMaterial = NewParticleMaterial(1f);

        private static Material NewParticleMaterial(float dstBlend)
        {
            Material m = new Material(Resources.Load<Shader>("Shaders/ChokeHoldParticle"));
            m.mainTexture = SoftTexture;
            m.SetFloat("_SrcBlend", 5f);
            m.SetFloat("_DstBlend", dstBlend);
            m.renderQueue = 3000;
            return m;
        }

        /// <summary>Disc moale (64 px), alb, cu alfa care scade spre margine.</summary>
        public static Texture2D SoftTexture
        {
            get
            {
                if (softTexture != null)
                {
                    return softTexture;
                }

                const int size = 64;
                softTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = (x + 0.5f) / size * 2f - 1f;
                        float dy = (y + 0.5f) / size * 2f - 1f;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Clamp01(1f - d);
                        // nori: doua octave de zgomot fac marginea zdrentuita si interiorul neuniform
                        float n = Mathf.PerlinNoise(x * 0.09f + 3.1f, y * 0.09f + 7.7f) * 0.65f + Mathf.PerlinNoise(x * 0.23f + 11f, y * 0.23f + 5f) * 0.35f;
                        float shape = Mathf.Clamp01(a * (0.55f + 0.9f * n));
                        softTexture.SetPixel(x, y, new Color(1f, 1f, 1f, shape * shape * (3f - 2f * shape)));
                    }
                }
                softTexture.Apply();
                return softTexture;
            }
        }

        /// <summary>Un ParticleSystem oprit, gata de configurat (fara emisie continua implicita).</summary>
        public static ParticleSystem NewSystem(Transform parent, string name, Material material, Vector3 position)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f;
            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }

        public static Light FlashLight(Transform parent, Vector3 position, Color color, float intensity, float range)
        {
            GameObject go = new GameObject("Light");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            Light light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            return light;
        }

        // ------------------------------------------------------------------
        // Efecte gata facute
        // ------------------------------------------------------------------

        /// <summary>Explozie: flacara scurta, fum gri, lumina. Se sterge singura.</summary>
        public static void Explosion(Vector3 position, float scale)
        {
            GameObject root = new GameObject("FX_Explosion");
            root.transform.position = position;

            ParticleSystem fire = NewSystem(root.transform, "Fire", Additive, position + Vector3.up * 0.3f);
            ParticleSystem.MainModule main = fire.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.38f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.6f * scale, 1.6f * scale);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f * scale, 9f * scale);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.75f, 0.35f, 1f), new Color(1f, 0.45f, 0.12f, 0.9f));
            ParticleSystem.ShapeModule shape = fire.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.4f * scale;
            ParticleSystem.ColorOverLifetimeModule fade = fire.colorOverLifetime;
            fade.enabled = true;
            fade.color = Fade(new Color(1f, 0.9f, 0.6f), new Color(0.8f, 0.2f, 0.05f));
            // flacara se desenaza peste fum (altfel fumul inchis o face verzuie)
            fire.GetComponent<ParticleSystemRenderer>().sortingFudge = -40f;
            fire.Emit(36);

            ParticleSystem smoke = NewSystem(root.transform, "Smoke", Alpha, position + Vector3.up * 0.5f);
            main = smoke.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(1.5f * scale, 3.2f * scale);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 3f * scale);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.24f, 0.23f, 0.22f, 0.55f), new Color(0.34f, 0.33f, 0.31f, 0.6f));
            main.startDelay = new ParticleSystem.MinMaxCurve(0.03f, 0.12f);
            main.gravityModifier = -0.08f;
            shape = smoke.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.6f * scale;
            fade = smoke.colorOverLifetime;
            fade.enabled = true;
            fade.color = Fade(Color.white, Color.white);
            smoke.Emit(16);

            ParticleSystem sparks = NewSystem(root.transform, "Sparks", Additive, position + Vector3.up * 0.2f);
            main = sparks.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(8f, 16f);
            main.startColor = new Color(1f, 0.8f, 0.4f, 1f);
            main.gravityModifier = 1f;
            sparks.Emit(40);

            Light light = FlashLight(root.transform, position + Vector3.up * 0.6f, new Color(1f, 0.65f, 0.3f), 9f * scale, 12f * scale);
            root.AddComponent<FxLifetime>().Configure(3f, light, 0.25f);
        }

        /// <summary>Gradient de la culoarea de inceput la cea de sfarsit, cu alfa care intra repede si iese lin.</summary>
        public static ParticleSystem.MinMaxGradient Fade(Color from, Color to)
        {
            Gradient g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(from, 0f), new GradientColorKey(to, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.08f), new GradientAlphaKey(0.7f, 0.6f), new GradientAlphaKey(0f, 1f) });
            return new ParticleSystem.MinMaxGradient(g);
        }
    }

    /// <summary>Sterge efectul dupa un timp; lumina se stinge in fadeLight secunde.</summary>
    public class FxLifetime : MonoBehaviour
    {
        private float until;
        private Light light;
        private float lightFade;
        private float lightStart;
        private float born;

        public void Configure(float seconds, Light fadingLight, float fadeLight)
        {
            born = Time.time;
            until = Time.time + seconds;
            light = fadingLight;
            lightFade = Mathf.Max(0.01f, fadeLight);
            lightStart = light != null ? light.intensity : 0f;
        }

        private void Update()
        {
            if (light != null)
            {
                light.intensity = lightStart * Mathf.Clamp01(1f - (Time.time - born) / lightFade);
            }
            if (Time.time >= until)
            {
                Destroy(gameObject);
            }
        }
    }
}
