using UnityEngine;

namespace BlackTide
{
    public sealed class OrbitalMotion : MonoBehaviour
    {
        [Tooltip("Degrees per real-time second; the moon is a child of this planet.")]
        public float degreesPerSecond = 18f;
        void Update() => transform.Rotate(Vector3.up, degreesPerSecond * Time.deltaTime, Space.Self);
    }
}
