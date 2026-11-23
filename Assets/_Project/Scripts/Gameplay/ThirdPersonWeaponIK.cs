using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace ChokeHold.Gameplay
{
    /// <summary>
    /// IK la persoana a treia (com.unity.animation.rigging): mana stanga pe socket-ul Grip_L al armei TP
    /// (TwoBoneIKConstraint, tinta copil al WeaponMount, deci urmeaza mana dreapta animata) si capul spre
    /// punctul in care se uita personajul (MultiAimConstraint, tinta AimTarget). Rig-ul il face CharacterPrefabBuilder.
    /// </summary>
    public class ThirdPersonWeaponIK : MonoBehaviour
    {
        [SerializeField] private TwoBoneIKConstraint leftHand;
        [SerializeField] private Transform leftHandTarget;
        [SerializeField] private MultiAimConstraint headAim;
        [SerializeField] private Transform aimTarget;
        [SerializeField] private float aimDistance = 10f;

        private GripPose.HandFrame leftFrame;
        private bool hasFrame;
        private Transform head;

        public Transform AimTarget => aimTarget;

        private void Awake()
        {
            // Cadrul mainii stangi se masoara in poza din prefab (inainte ca Animator-ul sa o miste)
            Transform hand = GripPose.Find(transform, "LeftHand");
            Transform index = GripPose.Find(transform, "LeftIndexProximal");
            Transform middle = GripPose.Find(transform, "LeftMiddleProximal");
            Transform little = GripPose.Find(transform, "LeftLittleProximal");
            head = GripPose.Find(transform, "Head");

            if (hand != null && index != null && middle != null && little != null)
            {
                leftFrame = GripPose.Measure(hand, index, middle, little, false);
                hasFrame = true;
            }
        }

        /// <summary>Pune tinta mainii stangi pe arma TP (sau opreste IK-ul daca arma nu are Grip_L).</summary>
        public void Bind(WeaponSockets sockets, WeaponKind kind)
        {
            Bind(sockets, kind, kind == WeaponKind.Pistol ? LeftGrip.PistolSupport : LeftGrip.Handguard);
        }

        public void Bind(WeaponSockets sockets, WeaponKind kind, LeftGrip grip)
        {
            if (leftHand == null || leftHandTarget == null)
            {
                return;
            }

            if (sockets == null || sockets.GripLeft == null || grip == LeftGrip.None || !hasFrame)
            {
                leftHand.weight = 0f;
                return;
            }

            Quaternion hand = GripPose.HandRotation(sockets.transform.rotation, false, kind, grip, leftFrame.RestFrameInverse);
            leftHandTarget.SetPositionAndRotation(sockets.GripLeft.position - hand * leftFrame.PalmOffset, hand);
            leftHand.weight = 1f;
        }

        /// <summary>Directia privirii (lume); capul se intoarce spre ea, in limitele constrangerii.</summary>
        public void LookAlong(Vector3 direction)
        {
            if (aimTarget == null || head == null)
            {
                return;
            }

            aimTarget.position = head.position + direction.normalized * aimDistance;
        }
    }
}
