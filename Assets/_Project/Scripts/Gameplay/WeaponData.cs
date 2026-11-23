using UnityEngine;

namespace ChokeHold.Gameplay
{
    /// <summary>Cum tine personajul arma (animatii TP, pozitia mainilor FP). Cutitul foloseste animatiile de pistol.</summary>
    public enum WeaponKind
    {
        Rifle = 0,
        Pistol = 1,
        Knife = 2
    }

    /// <summary>Slotul din inventar si categoria din meniul de cumparare.</summary>
    public enum WeaponCategory
    {
        Knife = 0,
        Pistol = 1,
        Rifle = 2,
        Sniper = 3
    }

    /// <summary>Cum se ocheste: prin catare / red dot / holo (arma vine in fata ochiului) sau prin luneta (ecran de luneta).</summary>
    public enum SightType
    {
        None = 0,
        Iron = 1,
        RedDot = 2,
        Holo = 3,
        Acog = 4,
        SniperScope = 5
    }

    /// <summary>Unde sta mana stanga: sub garda, pe manerul vertical, peste mana dreapta (pistol) sau deloc (cutit).</summary>
    public enum LeftGrip
    {
        Handguard = 0,
        VerticalGrip = 1,
        PistolSupport = 2,
        None = 3
    }

    /// <summary>
    /// Definitia unei arme: modelele (persoana intai / a treia), statisticile, pretul si clipurile.
    /// HitscanWeapon citeste de aici; CharacterVisuals pune modelul TP in mana personajului; BuyMenu afiseaza pretul.
    /// </summary>
    [CreateAssetMenu(menuName = "CHOKEHOLD/Weapon Data", fileName = "Weapon")]
    public class WeaponData : ScriptableObject
    {
        [Header("Identitate")]
        public string displayName = "Weapon";
        public WeaponKind kind = WeaponKind.Rifle;
        public WeaponCategory category = WeaponCategory.Rifle;
        public Team team = Team.Defenders;
        [Tooltip("Ambele echipe o pot cumpara (lunetistele).")]
        public bool anyTeam;
        [Tooltip("Apare in meniul de cumparare.")]
        public bool buyable = true;

        [Header("Economie")]
        public int price = 1000;
        public int killReward = 300;

        [Header("Modele")]
        public GameObject firstPersonPrefab;
        public GameObject thirdPersonPrefab;
        public Vector3 firstPersonOffset = new Vector3(0.12f, -0.14f, 0.32f);   // pozitia manerului fata de camera, in hip-fire
        public Vector3 firstPersonRotation = Vector3.zero;                      // inclinarea armei in hip-fire (grade)
        public float adsEyeRelief = 0.10f;                                         // distanta ochi - socket Sight la ADS (m)
        public LeftGrip leftGrip = LeftGrip.Handguard;
        [Tooltip("Cutit tinut invers (karambit): aratatorul prin inel, lama iese pe sub pumn.")]
        public bool reverseGrip;

        [Header("Ochire")]
        public SightType sight = SightType.Iron;
        [Tooltip("Campul vizual pentru fiecare nivel de zoom al lunetei (click dreapta trece la urmatorul).")]
        public float[] zoomFieldOfView = new float[0];

        [Header("Statistici")]
        public float damage = 30f;
        public float fireRate = 10f;            // focuri pe secunda
        public bool automatic = true;
        public int magazineSize = 30;
        public int reserveAmmo = 90;
        public float reloadTime = 2.2f;
        public float reloadEmptyTime = 2.8f;
        public float range = 150f;
        public float drawTime = 0.6f;
        [Tooltip("Inmultitor pentru viteza de deplasare cu arma in mana.")]
        public float moveSpeed = 1f;
        public bool suppressed;

        [Header("Recul (grade)")]
        [Tooltip("Primul glont: cat urca privirea (x) si cat se duce lateral (y).")]
        public Vector2 kickBase = new Vector2(1f, 0.35f);
        [Tooltip("Cat creste reculul cu fiecare glont din rafala (x in sus, y lateral).")]
        public Vector2 kickPerShot = new Vector2(0.15f, 0.035f);
        [Tooltip("Cat de sus si cat de lateral poate ajunge privirea in rafala.")]
        public Vector2 kickMax = new Vector2(5.5f, 1.75f);
        [Tooltip("Sansa ca rafala sa-si schimbe partea laterala dupa un glont: 1 din (valoare + 1).")]
        public int kickFlip = 8;
        public float recoilKick = 0.08f;        // cat se trage arma inapoi in mana (m)

        [Header("Dispersie")]
        public float hipSpread = 0.02f;
        public float aimSpread = 0.004f;
        public float aimFieldOfView = 48f;

        [Header("Cutit")]
        public float slashDamage = 34f;
        public float stabDamage = 65f;
        public float slashRate = 2.5f;
        public float stabRate = 1f;
        public float meleeRange = 1.7f;
        [Tooltip("Inmultitor la lovitura in spate.")]
        public float backstabMultiplier = 3f;

        [Header("Sunete")]
        public AudioClip shootSound;
        public AudioClip reloadSound;

        public bool IsKnife => category == WeaponCategory.Knife;
        public bool HasScope => sight == SightType.Acog || sight == SightType.SniperScope;

        /// <summary>Echipa poate folosi / cumpara arma.</summary>
        public bool AvailableTo(Team buyer) => anyTeam || team == buyer;
    }
}
