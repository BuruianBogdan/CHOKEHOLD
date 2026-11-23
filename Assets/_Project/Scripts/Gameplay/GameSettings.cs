using UnityEngine;

namespace ChokeHold.Gameplay
{
    /// <summary>
    /// Setarile jucatorului, pastrate intre sesiuni (PlayerPrefs). Se schimba din meniul de setari (Esc).
    /// </summary>
    public static class GameSettings
    {
        private const string SensitivityKey = "K17.Sensitivity";
        private const string ZoomRatioKey = "K17.ZoomSensitivityRatio";
        private const string ScrollJumpKey = "K17.ScrollWheelJump";

        public const float MinSensitivity = 0.1f;
        public const float MaxSensitivity = 10f;

        private static float? sensitivity;
        private static float? zoomRatio;
        private static bool? scrollJump;

        /// <summary>Sensibilitatea mouse-ului: grade pe count = valoare x 0,022 (aceeasi scara ca in shooterele clasice).</summary>
        public static float Sensitivity
        {
            get => sensitivity ??= PlayerPrefs.GetFloat(SensitivityKey, 2f);
            set
            {
                sensitivity = Mathf.Clamp(value, MinSensitivity, MaxSensitivity);
                PlayerPrefs.SetFloat(SensitivityKey, sensitivity.Value);
            }
        }

        /// <summary>Inmultitorul sensibilitatii cu luneta / ACOG (1 = rotirea pe ecran ramane proportionala cu zoom-ul).</summary>
        public static float ZoomSensitivityRatio
        {
            get => zoomRatio ??= PlayerPrefs.GetFloat(ZoomRatioKey, 1f);
            set
            {
                zoomRatio = Mathf.Clamp(value, 0.2f, 3f);
                PlayerPrefs.SetFloat(ZoomRatioKey, zoomRatio.Value);
            }
        }

        /// <summary>Rotita mouse-ului sare (bunny hop); atunci armele se schimba doar din taste (1 / 2 / 3 / Q).</summary>
        public static bool ScrollWheelJump
        {
            get => scrollJump ??= PlayerPrefs.GetInt(ScrollJumpKey, 1) == 1;
            set
            {
                scrollJump = value;
                PlayerPrefs.SetInt(ScrollJumpKey, value ? 1 : 0);
            }
        }

        public static void Save()
        {
            PlayerPrefs.Save();
        }
    }
}
