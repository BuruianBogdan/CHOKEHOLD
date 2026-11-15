using UnityEngine;

namespace ChokeHold.Gameplay
{
    /// <summary>Roteste lumina unui girofar in jurul verticalei (doar vizual).</summary>
    public class BeaconSpin : MonoBehaviour
    {
        [SerializeField] private float degreesPerSecond = 220f;

        private void Update()
        {
            transform.Rotate(Vector3.up, degreesPerSecond * Time.deltaTime, Space.World);
        }
    }
}
