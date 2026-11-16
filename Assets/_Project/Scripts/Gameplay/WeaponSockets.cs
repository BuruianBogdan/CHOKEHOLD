using UnityEngine;

namespace ChokeHold.Gameplay
{
    /// <summary>
    /// Punctele de pe modelul unei arme (generate de Tools/Blender/k17_weapons.py):
    /// Muzzle (flacara), Eject (tuburile), Sight (ochiul la ADS), Grip_R / Grip_L (mainile, IK).
    /// Piesele mobile (Magazine, Bolt / Slide, ChargingHandle, Trigger, Selector) sunt copii cu numele lor.
    /// </summary>
    public class WeaponSockets : MonoBehaviour
    {
        [SerializeField] private Transform muzzle;
        [SerializeField] private Transform eject;
        [SerializeField] private Transform sight;
        [SerializeField] private Transform gripRight;
        [SerializeField] private Transform gripLeft;
        [SerializeField] private Transform magazine;
        [SerializeField] private Transform bolt;
        [SerializeField] private Transform chargingHandle;
        [SerializeField] private Transform trigger;

        public Transform Muzzle => muzzle;
        public Transform Eject => eject;
        public Transform Sight => sight;
        public Transform GripRight => gripRight;
        public Transform GripLeft => gripLeft;
        public Transform Magazine => magazine;
        public Transform Bolt => bolt;
        public Transform ChargingHandle => chargingHandle;
        public Transform Trigger => trigger;

        private void Awake()
        {
            if (muzzle == null)
            {
                Resolve();
            }
        }

        /// <summary>Gaseste socket-urile si piesele dupa nume (la construirea prefab-ului sau la prima rulare).</summary>
        public void Resolve()
        {
            muzzle = Find("Muzzle", true);
            eject = Find("Eject", true);
            sight = Find("Sight", true);
            gripRight = Find("Grip_R", true);
            gripLeft = Find("Grip_L", true);
            magazine = Find("Magazine", false);
            bolt = Find("Bolt", false);
            if (bolt == null)
            {
                bolt = Find("Slide", false);
            }
            chargingHandle = Find("ChargingHandle", false);
            trigger = Find("Trigger", false);
        }

        private Transform Find(string name, bool socket)
        {
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                if (t == transform || t.name != name)
                {
                    continue;
                }

                // Socket-urile sunt obiecte goale; piesele au randare.
                bool hasRenderer = t.GetComponent<Renderer>() != null;
                if (socket == !hasRenderer)
                {
                    return t;
                }
            }

            return null;
        }
    }
}
