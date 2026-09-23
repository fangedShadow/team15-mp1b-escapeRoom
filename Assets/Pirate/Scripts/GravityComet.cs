using UnityEngine;

namespace BlackTide
{
    public sealed class GravityComet : MonoBehaviour
    {
        public Transform attractor;
        public float gravitationalParameter = 3.5f;
        public float initialRadius = 2.8f;
        public Vector3 velocity;
        public bool autoReset = true;

        void Start() => ResetOrbit();

        public void ResetOrbit()
        {
            if (!attractor) return;
            transform.position = attractor.position + Vector3.right * initialRadius;
            // Circular initial speed v = sqrt(mu / r), perpendicular to the radius.
            velocity = Vector3.forward * Mathf.Sqrt(gravitationalParameter / initialRadius);
            var trail = GetComponent<TrailRenderer>();
            if (trail) trail.Clear();
        }

        void Update()
        {
            if (!attractor) return;
            Vector3 r = attractor.position - transform.position;
            float d2 = Mathf.Max(r.sqrMagnitude, 0.04f);
            Vector3 acceleration = gravitationalParameter * r / (d2 * Mathf.Sqrt(d2));
            // Semi-implicit Euler: integrate acceleration into velocity, then position.
            velocity += acceleration * Time.deltaTime;
            transform.position += velocity * Time.deltaTime;
            if (autoReset && (d2 < 1.1f || d2 > 225f)) ResetOrbit();
        }
    }
}
