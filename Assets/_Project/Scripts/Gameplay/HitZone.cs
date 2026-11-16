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

        public HitZoneKind Kind => kind;
        public float Multiplier => multiplier;

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
        }
    }
}
