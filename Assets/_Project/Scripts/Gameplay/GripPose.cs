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
            Directions(right, kind, kind == WeaponKind.Pistol ? LeftGrip.PistolSupport : LeftGrip.Handguard, out fingers, out palm);
        }

        public static void Directions(bool right, WeaponKind kind, LeftGrip leftGrip, out Vector3 fingers, out Vector3 palm)
        {
            Directions(right, kind, leftGrip, false, out fingers, out palm);
        }

        /// <summary>
        /// reverse = cutit tinut invers (karambit): manerul trece prin pumn, inelul (capatul din spate, -z) sta la
        /// aratator, lama iese pe sub degetul mic. Incheieturile degetelor spre partea concava a lamei (-y), palma spre +x.
        /// </summary>
        public static void Directions(bool right, WeaponKind kind, LeftGrip leftGrip, bool reverse, out Vector3 fingers, out Vector3 palm)
        {
            if (right && kind == WeaponKind.Knife && reverse)
            {
                fingers = new Vector3(0f, -0.97f, -0.25f);
                palm = new Vector3(1f, 0f, 0f);
            }
            else if (right && kind == WeaponKind.Knife)
            {
                // pumnul pe maner (orizontal, lama inainte): palma spre stanga, degetele in jos, in jurul manerului
                fingers = new Vector3(-0.35f, -0.93f, 0.10f);
                palm = new Vector3(-0.95f, 0.25f, 0.0f);
            }
            else if (right)
            {
                // mana dreapta pe manerul pistol: palma spre stanga, degetele inainte-jos in jurul manerului
                fingers = new Vector3(-0.25f, -0.30f, 0.92f);
                palm = new Vector3(-0.95f, 0.05f, -0.25f);
            }
            else if (leftGrip == LeftGrip.VerticalGrip)
            {
                // mana stanga pe manerul vertical, oglinda mainii drepte: palma spre dreapta, degetele inainte-jos
                fingers = new Vector3(0.25f, -0.30f, 0.92f);
                palm = new Vector3(0.95f, 0.05f, -0.25f);
            }
            else if (kind == WeaponKind.Pistol || leftGrip == LeftGrip.PistolSupport)
            {
                // mana stanga peste degetele drepte: palma spre dreapta, degetele in jos in jurul lor, degetul mare inainte
                fingers = new Vector3(0.25f, -0.50f, 0.83f);
                palm = new Vector3(0.92f, 0.30f, -0.10f);
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

        public static Quaternion HandRotation(Quaternion weapon, bool right, WeaponKind kind, LeftGrip leftGrip, Quaternion restFrameInverse)
        {
            return HandRotation(weapon, right, kind, leftGrip, false, restFrameInverse);
        }

        public static Quaternion HandRotation(Quaternion weapon, bool right, WeaponKind kind, LeftGrip leftGrip, bool reverse, Quaternion restFrameInverse)
        {
            Directions(right, kind, leftGrip, reverse, out Vector3 fingers, out Vector3 palm);
            return Quaternion.LookRotation(weapon * fingers, weapon * palm) * restFrameInverse;
        }

        /// <summary>
        /// Rotatia unui cutit tinut invers fata de montura armei TP din mana: lama iese in jos, pe sub palma, iar inelul
        /// sta sus. (Pozitia e aleasa pe capturi: mana TP sta deschisa, in poza de pistol, iar cutitul tinut drept
        /// intra aproape tot in palma.)
        /// </summary>
        public static Quaternion ReverseGripInMount()
        {
            return Quaternion.Euler(90f, 0f, 0f);
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
