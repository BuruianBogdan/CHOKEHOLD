using UnityEngine;

namespace ChokeHold.Gameplay.Grenades
{
    public enum GrenadeType
    {
        Frag = 0,
        Smoke = 1,
        Flash = 2,
        Molotov = 3,
        Needle = 4
    }

    /// <summary>Datele fiecarei grenade (pret, cate poti cara, fitil) si modelul provizoriu.</summary>
    public readonly struct GrenadeSpec
    {
        public readonly GrenadeType Type;
        public readonly string Name;
        public readonly string Description;
        public readonly int Price;
        public readonly int MaxCarry;
        /// <summary>Secunde de la aruncare pana explodeaza (smoke: minim, apoi cand se opreste; molotov: la impact).</summary>
        public readonly float Fuse;
        public readonly Color Color;
        /// <summary>Numele prefab-ului din Resources/Grenades (modelul Tripo, cand exista).</summary>
        public readonly string Prefab;

        private GrenadeSpec(GrenadeType type, string name, string description, int price, int maxCarry, float fuse, Color color, string prefab)
        {
            Type = type;
            Name = name;
            Description = description;
            Price = price;
            MaxCarry = maxCarry;
            Fuse = fuse;
            Color = color;
            Prefab = prefab;
        }

        /// <summary>Cate grenade poti cara in total (ca in CS).</summary>
        public const int MaxTotal = 4;

        public static readonly GrenadeSpec[] All =
        {
            new GrenadeSpec(GrenadeType.Frag, "Frag", "Explozie · pana la 98 daune · raza 7 m", 300, 1, 1.6f, new Color(0.28f, 0.33f, 0.2f), "Grenade_Frag"),
            new GrenadeSpec(GrenadeType.Smoke, "Smoke", "Nor de fum · 18 s · stinge focul", 300, 1, 1.2f, new Color(0.55f, 0.57f, 0.58f), "Grenade_Smoke"),
            new GrenadeSpec(GrenadeType.Flash, "Flashbang", "Orbire completa daca te uiti spre ea", 200, 2, 1.5f, new Color(0.12f, 0.13f, 0.14f), "Grenade_Flash"),
            new GrenadeSpec(GrenadeType.Molotov, "Molotov", "Foc pe jos · 7 s · 40 daune/s", 400, 1, 2.5f, new Color(0.16f, 0.3f, 0.18f), "Molotov"),
            new GrenadeSpec(GrenadeType.Needle, "Ace (pini)", "Orbire pe partea lovita · pana la 4,5 s", 300, 1, 1.4f, new Color(0.18f, 0.2f, 0.23f), "Grenade_Needle"),
        };

        public static GrenadeSpec Of(GrenadeType type) => All[(int)type];
    }
}
