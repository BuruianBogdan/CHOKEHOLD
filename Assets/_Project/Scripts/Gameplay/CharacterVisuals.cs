using UnityEngine;

namespace ChokeHold.Gameplay
{
    /// <summary>
    /// Pune modelul complet al personajului (dupa echipa) sub obiectul jucatorului / tintei si arma de la
    /// persoana a treia in mana lui. Jucatorul local nu isi vede corpul (doar umbra): mainile si arma de la
    /// persoana intai sunt separate.
    /// </summary>
    public class CharacterVisuals : MonoBehaviour
    {
        [SerializeField] private GameObject defenderPrefab;
        [SerializeField] private GameObject[] attackerPrefabs;
        [SerializeField] private WeaponData weapon;
        [SerializeField] private int attackerVariant = -1;      // -1 = dupa slot (aleator stabil)

        private TeamMember member;
        private GameObject instance;
        private GameObject thirdPersonWeapon;

        public GameObject Instance => instance;
        public CharacterAnimatorDriver Driver { get; private set; }

        private void Awake()
        {
            member = GetComponent<TeamMember>();
            AddBodyBlocker();
            Spawn();
        }

        /// <summary>
        /// Tintele n-au CharacterController, iar hitbox-urile nu se ciocnesc cu nimic: o capsula pe "Ignore Raycast"
        /// opreste jucatorii sa treaca prin ele, fara sa opreasca gloantele (HitscanWeapon ignora layer-ul 2).
        /// </summary>
        private void AddBodyBlocker()
        {
            if (GetComponent<CharacterController>() != null || GetComponent<Collider>() != null)
            {
                return;
            }

            CapsuleCollider blocker = gameObject.AddComponent<CapsuleCollider>();
            blocker.center = new Vector3(0f, 0.9f, 0f);
            blocker.height = 1.8f;
            blocker.radius = 0.28f;
            gameObject.layer = 2;
        }

        public void Spawn()
        {
            if (instance != null)
            {
                Destroy(instance);
            }

            GameObject prefab = ChoosePrefab();
            if (prefab == null)
            {
                return;
            }

            instance = Instantiate(prefab, transform);
            instance.name = prefab.name;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            Driver = instance.GetComponent<CharacterAnimatorDriver>();

            if (member != null && member.IsLocalPlayer)
            {
                foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                }
            }

            AttachWeapon(weapon);
        }

        public void AttachWeapon(WeaponData data)
        {
            weapon = data;

            if (thirdPersonWeapon != null)
            {
                Destroy(thirdPersonWeapon);
            }

            if (instance == null || data == null)
            {
                return;
            }

            if (Driver != null)
            {
                Driver.WeaponKind = (int)data.kind;
            }

            Transform mount = FindDeep(instance.transform, "WeaponMount");
            if (mount == null || data.thirdPersonPrefab == null)
            {
                return;
            }

            thirdPersonWeapon = Instantiate(data.thirdPersonPrefab, mount);
            thirdPersonWeapon.transform.localPosition = Vector3.zero;
            thirdPersonWeapon.transform.localRotation = Quaternion.identity;

            // mana stanga pe garda / peste mana dreapta (IK)
            ThirdPersonWeaponIK ik = instance.GetComponent<ThirdPersonWeaponIK>();
            if (ik != null)
            {
                ik.Bind(thirdPersonWeapon.GetComponent<WeaponSockets>(), data.kind);
            }

            if (member != null && member.IsLocalPlayer)
            {
                foreach (Renderer renderer in thirdPersonWeapon.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                }
            }
        }

        private GameObject ChoosePrefab()
        {
            Team team = member != null ? member.Team : Team.Defenders;

            if (team == Team.Defenders || attackerPrefabs == null || attackerPrefabs.Length == 0)
            {
                return defenderPrefab;
            }

            // Varianta stabila per obiect (numele e unic pentru tinte si jucatori)
            int hash = 0;
            foreach (char c in gameObject.name)
            {
                hash = hash * 31 + c;
            }
            int index = attackerVariant >= 0 ? attackerVariant : Mathf.Abs(hash) % attackerPrefabs.Length;
            return attackerPrefabs[Mathf.Clamp(index, 0, attackerPrefabs.Length - 1)];
        }

        private static Transform FindDeep(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name)
                {
                    return t;
                }
            }

            return null;
        }
    }
}
