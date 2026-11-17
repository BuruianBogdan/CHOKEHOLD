using UnityEngine;

namespace ChokeHold.Gameplay
{
    /// <summary>
    /// Cum stau mainile pe arma: directia degetelor si normala palmei in spatiul armei (x dreapta, y sus, z teava),
    /// plus cadrul de repaus al unei maini din pozitiile oaselor. Folosit de mainile FP (FirstPersonArms) si de
    /// IK-ul mainii stangi de la persoana a treia (ThirdPersonWeaponIK), ca sa tina arma la fel.
    /// </summary>
    public static class GripPose
    {
        public static void Directions(bool right, WeaponKind kind, out Vector3 fingers, out Vector3 palm)
        {
            if (right)
            {
                // mana dreapta pe manerul pistol: palma spre stanga, degetele inainte-jos in jurul manerului
                fingers = new Vector3(-0.25f, -0.30f, 0.92f);
                palm = new Vector3(-0.95f, 0.05f, -0.25f);
            }
            else if (kind == WeaponKind.Pistol)
            {
                // mana stanga peste degetele drepte: palma spre dreapta
                fingers = new Vector3(0.15f, -0.45f, 0.88f);
                palm = new Vector3(0.95f, 0.10f, -0.10f);
            }
            else
            {
                // mana stanga sub garda: palma in sus, degetele spre dreapta in jurul ei
                fingers = new Vector3(0.70f, 0.30f, 0.65f);
                palm = new Vector3(0.20f, 0.97f, -0.10f);
            }
        }

        /// <summary>Rotatia (in lume) a mainii pe arma, din cadrul ei de repaus.</summary>
        public static Quaternion HandRotation(Quaternion weapon, bool right, WeaponKind kind, Quaternion restFrameInverse)
        {
            Directions(right, kind, out Vector3 fingers, out Vector3 palm);
            return Quaternion.LookRotation(weapon * fingers, weapon * palm) * restFrameInverse;
        }

        /// <summary>
        /// Cadrul mainii in spatiul ei: degetele spre MiddleProximal, palma din ordinea aratator -> deget mic;
        /// palmOffset = centrul palmei fata de incheietura (tot in spatiul mainii).
        /// </summary>
        public struct HandFrame
        {
            public Vector3 FingerAxis;
            public Vector3 PalmAxis;
            public Vector3 PalmOffset;
            public Quaternion RestFrameInverse;
        }

        public static HandFrame Measure(Transform hand, Transform index, Transform middle, Transform little, bool right)
        {
            Vector3 finger = (middle.position - hand.position).normalized;
            Vector3 across = (little.position - index.position).normalized;
            Vector3 palm = right ? Vector3.Cross(across, finger) : Vector3.Cross(finger, across);
            palm = Vector3.ProjectOnPlane(palm, finger).normalized;

            Quaternion inverse = Quaternion.Inverse(hand.rotation);
            HandFrame frame;
            frame.FingerAxis = inverse * finger;
            frame.PalmAxis = inverse * palm;
            frame.RestFrameInverse = Quaternion.Inverse(Quaternion.LookRotation(frame.FingerAxis, frame.PalmAxis));
            frame.PalmOffset = inverse * ((middle.position - hand.position) * 0.55f + palm * 0.018f);
            return frame;
        }

        public static Transform Find(Transform root, string name)
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
