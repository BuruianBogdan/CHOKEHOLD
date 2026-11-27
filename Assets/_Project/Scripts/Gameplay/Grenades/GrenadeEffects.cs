using System.Collections.Generic;
using UnityEngine;

namespace ChokeHold.Gameplay.Grenades
{
    /// <summary>Ce face fiecare grenada cand explodeaza (daune, orbire, fum, foc).</summary>
    public static class GrenadeEffects
    {
        public const float FragRadius = 7f;
        public const float FragDamage = 98f;
        public const float FlashRange = 30f;
        public const float NeedleRange = 9f;
        public const int NeedleCount = 24;
        public const float NeedleDamage = 4f;
        public const int NeedleDamageCap = 5;

        /// <summary>Tot in afara de hitbox-uri, mainile FP si grenadele (Ignore Raycast).</summary>
        public static int WorldMask => ~LayerMask.GetMask("Hitbox", "FirstPerson", "Ignore Raycast", "UI");

        public static void Detonate(GrenadeType type, Vector3 position, Vector3 normal, TeamMember owner)
        {
            switch (type)
            {
                case GrenadeType.Frag: Frag(position); break;
                case GrenadeType.Smoke: SmokeCloud.Create(position); break;
                case GrenadeType.Flash: Flash(position); break;
                case GrenadeType.Molotov: FireArea.Create(position, normal, owner); break;
                case GrenadeType.Needle: Needle(position); break;
            }
        }

        // ------------------------------------------------------------------

        private static void Frag(Vector3 position)
        {
            GrenadeFx.Explosion(position, 1f);
            foreach (TeamMember member in Snapshot())
            {
                Vector3 body = member.transform.position + Vector3.up * 1f;
                float distance = Vector3.Distance(position, body);
                if (distance < FragRadius && Visible(position, body, member))
                {
                    float falloff = 1f - distance / FragRadius;
                    member.Health.TakeDamage(FragDamage * falloff);
                }
            }
        }

        private static void Flash(Vector3 position)
        {
            GameObject root = new GameObject("FX_Flash");
            root.transform.position = position;
            Light light = GrenadeFx.FlashLight(root.transform, position, Color.white, 25f, 18f);
            ParticleSystem burst = GrenadeFx.NewSystem(root.transform, "Burst", GrenadeFx.Additive, position);
            ParticleSystem.MainModule main = burst.main;
            main.startLifetime = 0.12f;
            main.startSize = new ParticleSystem.MinMaxCurve(1.5f, 2.5f);
            main.startSpeed = 0f;
            main.startColor = new Color(1f, 1f, 0.95f, 1f);
            burst.Emit(3);
            root.AddComponent<FxLifetime>().Configure(1.5f, light, 0.15f);

            foreach (TeamMember member in Snapshot())
            {
                GrenadeScreenEffects screen = member.GetComponent<GrenadeScreenEffects>();
                Camera eye = EyeOf(member);
                if (screen == null || eye == null)
                {
                    continue;
                }

                Vector3 to = position - eye.transform.position;
                float distance = to.magnitude;
                if (distance > FlashRange || !Visible(position, eye.transform.position, member))
                {
                    continue;
                }

                // cat de direct te uiti spre ea: in fata = orbire completa, din lateral mai putin, din spate putin
                float facing = Vector3.Dot(eye.transform.forward, to / Mathf.Max(0.01f, distance));
                float angle = facing > 0.5f ? 1f : facing > -0.3f ? Mathf.Lerp(0.35f, 1f, (facing + 0.3f) / 0.8f) : 0.15f;
                float near = Mathf.Lerp(1f, 0.35f, Mathf.Clamp01((distance - 4f) / (FlashRange - 4f)));
                float strength = angle * near;
                screen.Flash(strength, 0.25f + 1.9f * strength, 0.6f + 2.4f * strength);
            }
        }

        private static void Needle(Vector3 position)
        {
            // acele: linii scurte care ies in toate partile si se opresc in pereti
            GameObject root = new GameObject("FX_Needles");
            root.transform.position = position;
            for (int i = 0; i < NeedleCount; i++)
            {
                Vector3 dir = Random.onUnitSphere;
                dir.y = Mathf.Abs(dir.y) * 0.5f;
                dir.Normalize();
                float length = Physics.Raycast(position, dir, out RaycastHit hit, NeedleRange, WorldMask, QueryTriggerInteraction.Ignore) ? hit.distance : NeedleRange;
                NeedleStreak.Create(root.transform, position, dir, length);
            }

            ParticleSystem puff = GrenadeFx.NewSystem(root.transform, "Puff", GrenadeFx.Alpha, position);
            ParticleSystem.MainModule main = puff.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
            main.startColor = new Color(0.55f, 0.75f, 0.8f, 0.55f);
            puff.Emit(8);
            Light light = GrenadeFx.FlashLight(root.transform, position, new Color(0.4f, 0.95f, 1f), 4f, 5f);
            root.AddComponent<FxLifetime>().Configure(1.2f, light, 0.2f);

            foreach (TeamMember member in Snapshot())
            {
                Vector3 head = member.transform.position + Vector3.up * 1.5f;
                Camera eye = EyeOf(member);
                if (eye != null)
                {
                    head = eye.transform.position;
                }

                float distance = Vector3.Distance(position, head);
                if (distance > NeedleRange || !Visible(position, head, member))
                {
                    continue;
                }

                // mai aproape = mai multe ace (8 la cativa centimetri, 1-2 la marginea razei)
                int needles = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(8f, 1f, distance / NeedleRange)), 1, 8);
                member.Health.TakeDamage(NeedleDamage * Mathf.Min(needles, NeedleDamageCap));

                GrenadeScreenEffects screen = member.GetComponent<GrenadeScreenEffects>();
                if (screen == null || eye == null)
                {
                    continue;
                }

                Vector3 to = (position - eye.transform.position).normalized;
                float side = Vector3.Dot(eye.transform.right, to);
                float facing = Vector3.Dot(eye.transform.forward, to);
                // din fata (aproape de centru) se inchide ochiul mai apropiat; din spate doar putin, pe margine
                int sign = side >= 0f ? 1 : -1;
                float strength = facing < -0.35f ? 0.35f : 1f;
                screen.NeedleBlind(sign, needles, strength);
            }
        }

        // ------------------------------------------------------------------

        public static bool Visible(Vector3 from, Vector3 to, TeamMember target)
        {
            Vector3 d = to - from;
            float length = d.magnitude;
            if (length < 0.01f)
            {
                return true;
            }
            if (!Physics.Raycast(from, d / length, out RaycastHit hit, length, WorldMask, QueryTriggerInteraction.Ignore))
            {
                return true;
            }
            return hit.collider.GetComponentInParent<TeamMember>() == target;
        }

        private static Camera EyeOf(TeamMember member)
        {
            if (!member.IsLocalPlayer)
            {
                return null;
            }
            HitscanWeapon weapon = member.GetComponentInChildren<HitscanWeapon>(true);
            return weapon != null ? weapon.ViewCamera : null;
        }

        /// <summary>Copie a celor vii: TakeDamage poate declansa evenimente care schimba lista.</summary>
        private static List<TeamMember> Snapshot()
        {
            List<TeamMember> list = new List<TeamMember>();
            foreach (TeamMember m in TeamMember.All)
            {
                if (m != null && m.IsAlive && m.Health != null)
                {
                    list.Add(m);
                }
            }
            return list;
        }
    }

    /// <summary>Linia unui ac: apare scurt, se scurteaza spre capat si dispare.</summary>
    public class NeedleStreak : MonoBehaviour
    {
        private LineRenderer line;
        private Vector3 from;
        private Vector3 to;
        private float born;
        private const float Life = 0.22f;

        public static void Create(Transform parent, Vector3 from, Vector3 direction, float length)
        {
            GameObject go = new GameObject("Needle");
            go.transform.SetParent(parent, false);
            NeedleStreak s = go.AddComponent<NeedleStreak>();
            s.from = from;
            s.to = from + direction * length;
            s.born = Time.time;
            s.line = go.AddComponent<LineRenderer>();
            s.line.sharedMaterial = GrenadeFx.Additive;
            s.line.positionCount = 2;
            s.line.widthMultiplier = 0.012f;
            s.line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            s.line.startColor = new Color(0.7f, 0.95f, 1f, 1f);
            s.line.endColor = new Color(0.8f, 1f, 1f, 0.2f);
            s.Update();
        }

        private void Update()
        {
            float t = Mathf.Clamp01((Time.time - born) / Life);
            line.SetPosition(0, Vector3.Lerp(from, to, Mathf.Max(0f, t * 1.4f - 0.4f)));
            line.SetPosition(1, Vector3.Lerp(from, to, Mathf.Min(1f, t * 1.6f + 0.1f)));
            Color c = line.startColor;
            c.a = 1f - t;
            line.startColor = c;
            if (t >= 1f)
            {
                Destroy(gameObject);
            }
        }
    }

    /// <summary>Norul de fum: particule mari care se intind pana la ~4 m si se opresc in pereti; stinge focul.</summary>
    public class SmokeCloud : MonoBehaviour
    {
        public const float Duration = 18f;
        public const float Radius = 4f;

        public static readonly List<SmokeCloud> Active = new List<SmokeCloud>();

        private float born;

        public float Age => Time.time - born;

        public static SmokeCloud Create(Vector3 position)
        {
            GameObject root = new GameObject("FX_Smoke");
            root.transform.position = position;
            SmokeCloud cloud = root.AddComponent<SmokeCloud>();
            cloud.born = Time.time;

            ParticleSystem ps = GrenadeFx.NewSystem(root.transform, "Cloud", GrenadeFx.Alpha, position + Vector3.up * 0.4f);
            ParticleSystem.MainModule main = ps.main;
            main.duration = 2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(Duration - 1.5f, Duration);
            main.startSize = new ParticleSystem.MinMaxCurve(2.2f, 4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4.2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.4f, 0.41f, 0.42f, 0.9f), new Color(0.55f, 0.55f, 0.56f, 0.95f));
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.maxParticles = 140;
            main.gravityModifier = -0.01f;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.3f;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, 40),
                new ParticleSystem.Burst(0.3f, 30),
                new ParticleSystem.Burst(0.7f, 30),
                new ParticleSystem.Burst(1.2f, 25),
            });

            // se opresc repede: norul ramane pe loc
            ParticleSystem.LimitVelocityOverLifetimeModule limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = 0.15f;
            limit.dampen = 0.06f;

            ParticleSystem.CollisionModule collision = ps.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.collidesWith = GrenadeEffects.WorldMask;
            collision.bounce = 0f;
            collision.dampen = 0.6f;
            collision.radiusScale = 0.35f;
            collision.quality = ParticleSystemCollisionQuality.Medium;

            ParticleSystem.ColorOverLifetimeModule fade = ps.colorOverLifetime;
            fade.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.04f), new GradientAlphaKey(1f, 0.85f), new GradientAlphaKey(0f, 1f) });
            fade.color = new ParticleSystem.MinMaxGradient(g);

            ParticleSystem.RotationOverLifetimeModule spin = ps.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);

            ps.Play();
            Active.Add(cloud);
            Destroy(root, Duration + 1f);
            return cloud;
        }

        /// <summary>Punctul e in fum (fumul e plin dupa ~1 s si incepe sa se rareasca in ultimele 2 s).</summary>
        public bool Contains(Vector3 point)
        {
            return Age > 0.6f && Age < Duration - 2f && Vector3.Distance(point, transform.position) < Radius;
        }

        private void OnDestroy()
        {
            Active.Remove(this);
        }
    }

    /// <summary>Focul molotovului: cerc de flacari pe jos, 7 s, daune celor din el; fumul il stinge.</summary>
    public class FireArea : MonoBehaviour
    {
        public const float Duration = 7f;
        public const float Radius = 2.6f;
        public const float DamagePerSecond = 40f;
        private const float Tick = 0.25f;

        private ParticleSystem flames;
        private ParticleSystem smoke;
        private Light glow;
        private float born;
        private float nextTick;
        private bool dying;

        public static FireArea Create(Vector3 position, Vector3 normal, TeamMember owner)
        {
            GameObject root = new GameObject("FX_Fire");
            root.transform.position = position;
            FireArea fire = root.AddComponent<FireArea>();
            fire.born = Time.time;

            // stropi de sticla si o flacara mare la impact
            GrenadeFx.Explosion(position, 0.45f);

            fire.flames = GrenadeFx.NewSystem(root.transform, "Flames", GrenadeFx.Additive, position + Vector3.up * 0.05f);
            ParticleSystem.MainModule main = fire.flames.main;
            main.loop = true;
            main.duration = Duration;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
            main.startSpeed = 0f;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.6f, 0.2f, 0.9f), new Color(1f, 0.35f, 0.08f, 0.8f));
            main.maxParticles = 400;
            ParticleSystem.ShapeModule shape = fire.flames.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = Radius;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            ParticleSystem.EmissionModule emission = fire.flames.emission;
            emission.rateOverTime = 320f;
            ParticleSystem.VelocityOverLifetimeModule rise = fire.flames.velocityOverLifetime;
            rise.enabled = true;
            rise.space = ParticleSystemSimulationSpace.World;
            rise.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            rise.y = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
            rise.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            ParticleSystem.SizeOverLifetimeModule shrink = fire.flames.sizeOverLifetime;
            shrink.enabled = true;
            shrink.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
            ParticleSystem.ColorOverLifetimeModule fade = fire.flames.colorOverLifetime;
            fade.enabled = true;
            fade.color = GrenadeFx.Fade(new Color(1f, 0.85f, 0.4f), new Color(0.9f, 0.15f, 0.02f));
            // limbi de foc: particule intinse pe directia in care urca
            ParticleSystemRenderer flameRenderer = fire.flames.GetComponent<ParticleSystemRenderer>();
            flameRenderer.renderMode = ParticleSystemRenderMode.Stretch;
            flameRenderer.velocityScale = 0.18f;
            flameRenderer.lengthScale = 1.6f;
            flameRenderer.sortingFudge = -40f;
            fire.flames.Play();

            fire.smoke = GrenadeFx.NewSystem(root.transform, "Smoke", GrenadeFx.Alpha, position + Vector3.up * 1.2f);
            main = fire.smoke.main;
            main.loop = true;
            main.duration = Duration;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 2.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(1f, 2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
            main.startColor = new Color(0.1f, 0.09f, 0.08f, 0.45f);
            shape = fire.smoke.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = Radius * 0.7f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            emission = fire.smoke.emission;
            emission.rateOverTime = 14f;
            fade = fire.smoke.colorOverLifetime;
            fade.enabled = true;
            fade.color = GrenadeFx.Fade(Color.white, Color.white);
            fire.smoke.Play();

            fire.glow = GrenadeFx.FlashLight(root.transform, position + Vector3.up * 0.8f, new Color(1f, 0.5f, 0.15f), 3f, 7f);

            // aruncat in fum: se stinge imediat
            if (InSmoke(position))
            {
                fire.Extinguish();
            }
            return fire;
        }

        private static bool InSmoke(Vector3 point)
        {
            foreach (SmokeCloud cloud in SmokeCloud.Active)
            {
                if (cloud != null && cloud.Contains(point))
                {
                    return true;
                }
            }
            return false;
        }

        private void Update()
        {
            if (glow != null)
            {
                glow.intensity = (dying ? 0.5f : 3f) * (0.75f + Mathf.PerlinNoise(Time.time * 9f, 0.3f) * 0.5f);
            }

            if (dying)
            {
                return;
            }

            if (Time.time - born >= Duration || InSmoke(transform.position))
            {
                Extinguish();
                return;
            }

            if (Time.time < nextTick)
            {
                return;
            }

            nextTick = Time.time + Tick;
            List<TeamMember> burning = new List<TeamMember>();
            foreach (TeamMember m in TeamMember.All)
            {
                if (m == null || !m.IsAlive || m.Health == null)
                {
                    continue;
                }

                Vector3 d = m.transform.position - transform.position;
                if (Mathf.Abs(d.y) < 1.6f && new Vector2(d.x, d.z).magnitude < Radius)
                {
                    burning.Add(m);
                }
            }
            foreach (TeamMember m in burning)
            {
                m.Health.TakeDamage(DamagePerSecond * Tick);
            }
        }

        private void Extinguish()
        {
            dying = true;
            flames.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            smoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Destroy(gameObject, 2.6f);
        }
    }
}
