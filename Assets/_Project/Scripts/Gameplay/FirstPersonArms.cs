using UnityEngine;

namespace ChokeHold.Gameplay
{
    /// <summary>
    /// Mainile de la persoana intai (FPArms_CT / FPArms_T): umerii stau ficsi fata de camera, mainile se pun prin IK
    /// pe doua oase (UpperArm - LowerArm - Hand) pe socket-urile armei (Grip_R, Grip_L). Asa sway-ul, bob-ul, reculul
    /// si ADS-ul armei (HitscanWeapon) le muta si mainile. Mana stanga poate fi trimisa temporar in alta parte
    /// (incarcator, maner de armare) de FirstPersonAnimator. Degetele se strang procedural pe maner.
    /// </summary>
    public class FirstPersonArms : MonoBehaviour
    {
        [Header("Corp")]
        [SerializeField] private Vector3 shoulderOffset = new Vector3(0.02f, -0.24f, 0.0f);  // mijlocul umerilor, fata de camera
        [SerializeField] private float bodyYaw = 32f;                                           // pozitie de tragere: umarul stang inainte
        [SerializeField] private Vector3 rightElbowHint = new Vector3(0.7f, -1f, -0.3f);      // directia cotului, fata de camera
        [SerializeField] private Vector3 leftElbowHint = new Vector3(-0.5f, -1f, 0.1f);
        [SerializeField] private Vector3 pistolLeftElbowHint = new Vector3(-0.55f, -1f, -0.25f); // pistol: cotul stang in afara, antebratele converg spre maner

        [Header("Degete (grade pe falanga)")]
        [SerializeField] private float rightCurl = 62f;
        [SerializeField] private float triggerCurl = 10f;
        [SerializeField] private float leftCurl = 48f;
        [SerializeField] private float handguardCurl = 28f;
        [SerializeField] private float leftThumbCurl = 6f;
        [Tooltip("Pistol: centrul palmei stangi fata de palma dreapta, in spatiul armei (x dreapta, y sus, z teava).")]
        [SerializeField] private Vector3 pistolSupportOffset = new Vector3(-0.03f, -0.016f, 0.008f);
        [SerializeField] private float pistolSupportCurl = 66f;
        [SerializeField] private float thumbCurl = 22f;
        [Tooltip("Pistol: degetul mare drept, inainte pe partea stanga a cadrului (nu peste spatele manerului).")]
        [SerializeField] private float pistolThumbCurl = 2f;

        private Transform viewCamera;
        private Arm right;
        private Arm left;

        private Transform weaponRoot;          // orientarea armei (socket-urile pot avea rotatia din import)
        private Transform rightTarget;
        private Transform leftTarget;
        private WeaponKind kind;
        private LeftGrip leftGrip = LeftGrip.Handguard;
        private bool reverseGrip;               // cutit tinut invers (karambit)
        private Renderer[] renderers;

        [SerializeField] private float grenadeCurl = 70f;
        private Vector3 rightOverridePosition;
        private Quaternion rightOverrideRotation = Quaternion.identity;
        private float rightOverrideWeight;

        private Vector3 leftOverridePosition;
        private Quaternion leftOverrideRotation;
        private float leftOverrideWeight;

        /// <summary>Arma pe care stau mainile: socket-urile Grip_R / Grip_L si tipul (pozitia mainilor difera).</summary>
        public void Bind(WeaponSockets sockets, WeaponKind weaponKind)
        {
            Bind(sockets, weaponKind, weaponKind == WeaponKind.Pistol ? LeftGrip.PistolSupport : LeftGrip.Handguard);
        }

        /// <summary>Ca mai sus, cu felul in care sta mana stanga (None = mana stanga nu tine arma, ex. cutitul).</summary>
        public void Bind(WeaponSockets sockets, WeaponKind weaponKind, LeftGrip grip, bool reverse = false)
        {
            reverseGrip = reverse;
            weaponRoot = sockets != null ? sockets.transform : null;
            rightTarget = sockets != null ? sockets.GripRight : null;
            leftTarget = sockets != null && grip != LeftGrip.None ? sockets.GripLeft : null;
            kind = weaponKind;
            leftGrip = grip;
        }

        /// <summary>Ascunde mainile (luneta: se vede doar ecranul lunetei).</summary>
        public bool Hidden
        {
            set
            {
                renderers ??= GetComponentsInChildren<Renderer>(true);
                foreach (Renderer r in renderers)
                {
                    if (r != null) r.enabled = !value;
                }
            }
        }

        /// <summary>Trimite mana stanga la o pozitie (in spatiul lumii); weight 0 = inapoi pe arma.</summary>
        public void SetLeftOverride(Vector3 position, Quaternion weaponRotation, float weight)
        {
            leftOverridePosition = position;
            leftOverrideRotation = weaponRotation;
            leftOverrideWeight = Mathf.Clamp01(weight);
        }

        /// <summary>
        /// Mana dreapta tine altceva decat arma (grenada): palma la position, cadrul "armei" imaginare = rotation
        /// (z inainte, y sus). weight 0 = inapoi pe arma.
        /// </summary>
        public void SetRightOverride(Vector3 position, Quaternion rotation, float weight)
        {
            rightOverridePosition = position;
            rightOverrideRotation = rotation;
            rightOverrideWeight = Mathf.Clamp01(weight);
        }

        private void Awake()
        {
            viewCamera = transform.parent;
            right = new Arm(transform, "Right");
            left = new Arm(transform, "Left");

            // Radacina se aseaza o data: umerii in fata-jos fata de ochi, trunchiul rotit spre dreapta.
            Quaternion yaw = Quaternion.Euler(0f, bodyYaw, 0f);
            Vector3 shoulders = (transform.InverseTransformPoint(right.Upper.position) + transform.InverseTransformPoint(left.Upper.position)) * 0.5f;
            transform.localRotation = yaw;
            transform.localPosition = shoulderOffset - yaw * shoulders;
        }

        private void LateUpdate()
        {
            Apply();
        }

        /// <summary>Pune mainile pe arma (IK + degete). Ruleaza in LateUpdate, dupa ce arma s-a miscat.</summary>
        public void Apply()
        {
            if (viewCamera == null || !right.Valid || !left.Valid)
            {
                return;
            }

            right.ResetPose();
            left.ResetPose();

            Quaternion cameraRotation = viewCamera.rotation;

            if (rightOverrideWeight > 0.5f)
            {
                // grenada in mana dreapta: pumn strans in jurul ei (toate degetele, si aratatorul)
                Vector3 position = rightTarget != null ? Vector3.Lerp(rightTarget.position, rightOverridePosition, rightOverrideWeight) : rightOverridePosition;
                Quaternion hand = GripPose.HandRotation(rightOverrideRotation, true, WeaponKind.Rifle, LeftGrip.Handguard, false, right.RestFrameInverse);
                right.Solve(position, hand, right.Upper.position + cameraRotation * rightElbowHint);
                right.Curl(grenadeCurl, grenadeCurl, thumbCurl);
            }
            else if (rightTarget != null)
            {
                Quaternion hand = GripPose.HandRotation(weaponRoot.rotation, true, kind, leftGrip, reverseGrip, right.RestFrameInverse);
                right.Solve(rightTarget.position, hand, right.Upper.position + cameraRotation * rightElbowHint);
                // la arme aratatorul sta intins pe tragaci; la cutit toate degetele strang manerul
                right.Curl(rightCurl, kind == WeaponKind.Knife ? rightCurl : triggerCurl, kind == WeaponKind.Pistol ? pistolThumbCurl : thumbCurl);
            }

            if (leftTarget != null)
            {
                Quaternion weapon = weaponRoot.rotation;
                Vector3 position = leftTarget.position;

                // pistol: mana stanga se lipeste de mana dreapta (palma pe degetele ei, din stanga), nu de un punct de pe maner
                if (kind == WeaponKind.Pistol && rightTarget != null)
                {
                    position = rightTarget.position + weapon * pistolSupportOffset;
                }

                if (leftOverrideWeight > 0f)
                {
                    position = Vector3.Lerp(position, leftOverridePosition, leftOverrideWeight);
                    weapon = Quaternion.Slerp(weapon, leftOverrideRotation, leftOverrideWeight);
                }

                Quaternion hand = GripPose.HandRotation(weapon, false, kind, leftGrip, left.RestFrameInverse);
                left.Solve(position, hand, left.Upper.position + cameraRotation * (kind == WeaponKind.Pistol ? pistolLeftElbowHint : leftElbowHint));
                // sub garda degetele doar se ridica pe laterale (palma deschisa); pe manerul vertical si peste mana dreapta strang
                bool cup = leftGrip == LeftGrip.Handguard && kind != WeaponKind.Pistol;
                float curl = cup ? handguardCurl : kind == WeaponKind.Pistol ? pistolSupportCurl : leftCurl;
                left.Curl(curl, curl, cup ? 4f : leftThumbCurl);
            }
        }

        // ------------------------------------------------------------------

        private sealed class Arm
        {
            public readonly Transform Upper;
            public readonly Transform Lower;
            public readonly Transform Hand;
            public readonly bool Valid;
            public readonly Quaternion RestFrameInverse;     // inversa cadrului (degete, palma) in spatiul mainii

            private readonly Vector3 palmOffset;               // centrul palmei fata de incheietura, in spatiul mainii
            private readonly Vector3 fingerAxis;               // in spatiul mainii
            private readonly Vector3 palmAxis;
            private readonly Transform[][] fingers;            // [deget][falanga]; 0 = degetul mare, 1 = aratatorul
            private readonly Quaternion[][] fingerRest;
            private readonly Quaternion upperRest;
            private readonly Quaternion lowerRest;
            private readonly Quaternion handRest;

            public Arm(Transform root, string side)
            {
                Upper = Find(root, side + "UpperArm");
                Lower = Find(root, side + "LowerArm");
                Hand = Find(root, side + "Hand");
                Transform index = Find(root, side + "IndexProximal");
                Transform middle = Find(root, side + "MiddleProximal");
                Transform little = Find(root, side + "LittleProximal");
                Valid = Upper != null && Lower != null && Hand != null && index != null && middle != null && little != null;

                if (!Valid)
                {
                    return;
                }

                upperRest = Upper.localRotation;
                lowerRest = Lower.localRotation;
                handRest = Hand.localRotation;

                GripPose.HandFrame frame = GripPose.Measure(Hand, index, middle, little, side == "Right");
                fingerAxis = frame.FingerAxis;
                palmAxis = frame.PalmAxis;
                RestFrameInverse = frame.RestFrameInverse;
                palmOffset = frame.PalmOffset;

                string[] names = { "Thumb", "Index", "Middle", "Ring", "Little" };
                string[] phalanges = { "Proximal", "Intermediate", "Distal" };
                fingers = new Transform[names.Length][];
                fingerRest = new Quaternion[names.Length][];
                for (int f = 0; f < names.Length; f++)
                {
                    fingers[f] = new Transform[phalanges.Length];
                    fingerRest[f] = new Quaternion[phalanges.Length];
                    for (int p = 0; p < phalanges.Length; p++)
                    {
                        Transform bone = Find(root, side + names[f] + phalanges[p]);
                        fingers[f][p] = bone;
                        fingerRest[f][p] = bone != null ? bone.localRotation : Quaternion.identity;
                    }
                }
            }

            public void ResetPose()
            {
                Upper.localRotation = upperRest;
                Lower.localRotation = lowerRest;
                Hand.localRotation = handRest;
                for (int f = 0; f < fingers.Length; f++)
                {
                    for (int p = 0; p < fingers[f].Length; p++)
                    {
                        if (fingers[f][p] != null)
                        {
                            fingers[f][p].localRotation = fingerRest[f][p];
                        }
                    }
                }
            }

            /// <summary>IK analitic pe doua oase: centrul palmei ajunge in target, cotul spre hint.</summary>
            public void Solve(Vector3 palmTarget, Quaternion handRotation, Vector3 hint)
            {
                Vector3 wrist = palmTarget - handRotation * palmOffset;

                Vector3 a = Upper.position;
                float upperLength = Vector3.Distance(a, Lower.position);
                float lowerLength = Vector3.Distance(Lower.position, Hand.position);

                Vector3 toTarget = wrist - a;
                float distance = Mathf.Clamp(toTarget.magnitude, 0.05f, (upperLength + lowerLength) * 0.999f);
                Vector3 direction = toTarget.normalized;

                Vector3 bend = Vector3.ProjectOnPlane(hint - a, direction);
                if (bend.sqrMagnitude < 1e-6f)
                {
                    bend = Vector3.ProjectOnPlane(Vector3.down, direction);
                }
                bend.Normalize();

                float cos = (upperLength * upperLength + distance * distance - lowerLength * lowerLength) / (2f * upperLength * distance);
                float angle = Mathf.Acos(Mathf.Clamp(cos, -1f, 1f));
                Vector3 elbow = a + (direction * Mathf.Cos(angle) + bend * Mathf.Sin(angle)) * upperLength;

                Upper.rotation = Quaternion.FromToRotation(Lower.position - a, elbow - a) * Upper.rotation;
                Vector3 wristNow = Hand.position;
                Lower.rotation = Quaternion.FromToRotation(wristNow - Lower.position, (a + direction * distance) - Lower.position) * Lower.rotation;
                Hand.rotation = handRotation;
            }

            /// <summary>Strange degetele spre palma (cate un unghi pe falanga); aratatorul separat (tragaci).</summary>
            public void Curl(float fingersAngle, float indexAngle, float thumbAngle)
            {
                Vector3 finger = Hand.rotation * fingerAxis;
                Vector3 palm = Hand.rotation * palmAxis;

                for (int f = 0; f < fingers.Length; f++)
                {
                    float degrees = f == 0 ? thumbAngle : f == 1 ? indexAngle : fingersAngle;
                    float radians = degrees * Mathf.Deg2Rad;
                    Quaternion step = Quaternion.FromToRotation(finger, finger * Mathf.Cos(radians) + palm * Mathf.Sin(radians));

                    for (int p = 0; p < fingers[f].Length; p++)
                    {
                        if (fingers[f][p] != null)
                        {
                            fingers[f][p].rotation = step * fingers[f][p].rotation;
                        }
                    }
                }
            }

            private static Transform Find(Transform root, string name)
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
}
