using UnityEngine;

namespace ChokeHold.Gameplay
{
    public enum WeaponKind
    {
        Rifle = 0,
        Pistol = 1
    }

    /// <summary>
    /// Definitia unei arme: modelele (persoana intai / a treia), statisticile si clipurile.
    /// HitscanWeapon citeste de aici; CharacterVisuals pune modelul TP in mana personajului.
    /// </summary>
    [CreateAssetMenu(menuName = "CHOKEHOLD/Weapon Data", fileName = "Weapon")]
    public class WeaponData : ScriptableObject
    {
        [Header("Identitate")]
        public string displayName = "Weapon";
        public WeaponKind kind = WeaponKind.Rifle;
        public Team team = Team.Defenders;

        [Header("Modele")]
        public GameObject firstPersonPrefab;
        public GameObject thirdPersonPrefab;
        public Vector3 firstPersonOffset = new Vector3(0.12f, -0.14f, 0.32f);   // pozitia manerului fata de camera, in hip-fire
        public float adsEyeRelief = 0.10f;                                         // distanta ochi - socket Sight la ADS (m)

        [Header("Statistici")]
        public float damage = 30f;
        public float fireRate = 10f;            // focuri pe secunda
        public bool automatic = true;
        public int magazineSize = 30;
        public float reloadTime = 2.2f;
        public float reloadEmptyTime = 2.8f;
        public float range = 150f;

        [Header("Recul si dispersie")]
        public float recoilVertical = 0.8f;
        public float recoilHorizontal = 0.15f;
        public float recoilKick = 0.08f;        // cat se trage arma inapoi (m)
        public float hipSpread = 0.02f;
        public float aimSpread = 0.004f;
        public float aimFieldOfView = 48f;

        [Header("Sunete")]
        public AudioClip shootSound;
        public AudioClip reloadSound;
    }
}
