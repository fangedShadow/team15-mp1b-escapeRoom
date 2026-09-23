using UnityEngine;

namespace BlackTide
{
    public sealed class SignalProjectile : MonoBehaviour
    {
        public Vector3 velocity;
        public float remainingLife = 4f;
        public ParticleSystem impact;
        public AudioSource impactSound;
        void Update()
        {
            Vector3 step = velocity * Time.deltaTime;
            if (Physics.SphereCast(transform.position, 0.07f, step.normalized,
                    out RaycastHit hit, step.magnitude, ~(1 << 2), QueryTriggerInteraction.Ignore))
            {
                if (impact) { impact.transform.position = hit.point; impact.Play(); }
                if (impactSound) { impactSound.transform.position = hit.point; impactSound.Play(); }
                Destroy(gameObject);
                return;
            }
            transform.position += step;
            remainingLife -= Time.deltaTime;
            if (remainingLife <= 0f) Destroy(gameObject);
        }
    }
}
