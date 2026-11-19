using UnityEngine;

namespace ChokeHold.Gameplay
{
    public enum HitZoneKind
    {
        Head,
        Neck,
        Chest,
        Abdomen,
        Arm,
        Leg
    }

    /// <summary>
    /// Zona de lovire a unui personaj: colliderul e legat de un os, iar damage-ul se inmulteste
    /// (cap x4, corp x1, membre x0,75). Health-ul e pe radacina personajului.
    /// </summary>
    public class HitZone : MonoBehaviour
    {
        [SerializeField] private HitZoneKind kind = HitZoneKind.Chest;
        [SerializeField] private float multiplier = 1f;

        /// <summary>
        /// Layer-ul hitbox-urilor: primesc raze (gloante), dar nu se ciocnesc cu nimic, ca sa nu impinga
        /// CharacterController-ul propriului personaj sau alte corpuri.
        /// </summary>
        public const string LayerName = "Hitbox";

        public HitZoneKind Kind => kind;
        public float Multiplier => multiplier;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void IgnoreHitboxCollisions()
        {
            int layer = LayerMask.NameToLayer(LayerName);
            if (layer < 0)
            {
                return;
            }

            for (int other = 0; other < 32; other++)
            {
                Physics.IgnoreLayerCollision(layer, other, true);
            }
        }

        public static float DefaultMultiplier(HitZoneKind kind)
        {
            switch (kind)
            {
                case HitZoneKind.Head: return 4f;
                case HitZoneKind.Arm:
                case HitZoneKind.Leg: return 0.75f;
                default: return 1f;
            }
        }

        public void Configure(HitZoneKind zoneKind)
        {
            kind = zoneKind;
            multiplier = DefaultMultiplier(zoneKind);

            int layer = LayerMask.NameToLayer(LayerName);
            if (layer >= 0)
            {
                gameObject.layer = layer;
            }
        }
    }
}
