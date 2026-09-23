using UnityEngine;

namespace BlackTide
{
    public sealed class HoverBob : MonoBehaviour
    {
        public float amplitude = 0.075f;
        public float speed = 1.7f;
        Vector3 home;
        void Start() => home = transform.localPosition;
        void Update()
        {
            transform.localPosition = home + Vector3.up * Mathf.Sin(Time.time * speed) * amplitude;
            transform.Rotate(0f, 35f * Time.deltaTime, 0f);
        }
    }
}
