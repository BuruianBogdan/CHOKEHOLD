using UnityEngine;

namespace ChokeHold.Gameplay.Grenades
{
    /// <summary>
    /// Grenada aruncata: corp fizic care ricoseaza, cu fitil. Frag, flash si ace explodeaza dupa fitil; smoke
    /// porneste dupa fitil cand s-a oprit din rostogolit; molotovul se sparge cand atinge podeaua (sau se aprinde
    /// in aer la capatul fitilului, deasupra solului).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class GrenadeProjectile : MonoBehaviour
    {
        private const float Radius = 0.05f;

        private GrenadeType type;
        private TeamMember owner;
        private Rigidbody body;
        private float age;
        private float stillTime;
        private bool detonated;

        public GrenadeType Type => type;

        public static GrenadeProjectile Spawn(GrenadeType type, Vector3 position, Vector3 velocity, TeamMember owner)
        {
            GameObject go = GrenadeFx.CreateModel(type);
            go.name = $"Thrown_{GrenadeSpec.Of(type).Prefab}";
            go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(velocity.sqrMagnitude > 0.01f ? velocity : Vector3.forward));
            GrenadeFx.SetLayer(go, LayerMask.NameToLayer("Ignore Raycast"), true);

            SphereCollider sphere = go.AddComponent<SphereCollider>();
            sphere.radius = type == GrenadeType.Molotov ? 0.06f : Radius;
            sphere.sharedMaterial = new PhysicsMaterial("Grenade")
            {
                bounciness = type == GrenadeType.Molotov ? 0.05f : 0.38f,
                dynamicFriction = 0.55f,
                staticFriction = 0.7f,
                bounceCombine = PhysicsMaterialCombine.Maximum,
                frictionCombine = PhysicsMaterialCombine.Average,
            };

            Rigidbody body = go.AddComponent<Rigidbody>();
            body.mass = 0.4f;
            body.linearDamping = 0.05f;
            body.angularDamping = 0.8f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearVelocity = velocity;
            body.angularVelocity = Random.insideUnitSphere * 8f;

            // nu se loveste de cel care a aruncat-o
            if (owner != null)
            {
                foreach (Collider c in owner.GetComponentsInChildren<Collider>(true))
                {
                    Physics.IgnoreCollision(sphere, c);
                }
            }

            GrenadeProjectile projectile = go.AddComponent<GrenadeProjectile>();
            projectile.type = type;
            projectile.owner = owner;
            projectile.body = body;
            return projectile;
        }

        private void FixedUpdate()
        {
            if (detonated)
            {
                return;
            }

            age += Time.fixedDeltaTime;
            float fuse = GrenadeSpec.Of(type).Fuse;

            switch (type)
            {
                case GrenadeType.Smoke:
                    stillTime = body.linearVelocity.magnitude < 0.35f ? stillTime + Time.fixedDeltaTime : 0f;
                    if ((age >= fuse && stillTime >= 0.2f) || age >= 4f)
                    {
                        Detonate(transform.position, Vector3.up);
                    }
                    break;

                case GrenadeType.Molotov:
                    if (age >= fuse)
                    {
                        // aprins in aer: focul cade pe ce e sub el (pana la 3 m), altfel se stinge
                        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit ground, 3f, GrenadeEffects.WorldMask, QueryTriggerInteraction.Ignore))
                        {
                            Detonate(ground.point, ground.normal);
                        }
                        else
                        {
                            detonated = true;
                            GrenadeFx.Explosion(transform.position, 0.35f);
                            Destroy(gameObject);
                        }
                    }
                    break;

                default:
                    if (age >= fuse)
                    {
                        Detonate(transform.position, Vector3.up);
                    }
                    break;
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (detonated || type != GrenadeType.Molotov || age < 0.05f)
            {
                return;
            }

            foreach (ContactPoint contact in collision.contacts)
            {
                if (contact.normal.y > 0.6f)
                {
                    Detonate(contact.point, contact.normal);
                    return;
                }
            }
        }

        private void Detonate(Vector3 position, Vector3 normal)
        {
            detonated = true;
            GrenadeEffects.Detonate(type, position + Vector3.up * 0.06f, normal, owner);

            if (type == GrenadeType.Smoke)
            {
                // cutia de fum ramane pe jos cat timp iese fumul
                body.linearVelocity = Vector3.zero;
                body.isKinematic = true;
                Destroy(gameObject, SmokeCloud.Duration + 1f);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}
