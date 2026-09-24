using System.Collections.Generic;
using UnityEngine;

namespace Team15.Storage
{
    public class MatchStrikeZone : MonoBehaviour
    {
        public float minimumStrikeDistance = 0.08f;
        readonly Dictionary<Collider, Vector3> entries = new Dictionary<Collider, Vector3>();

        void OnTriggerEnter(Collider other)
        {
            var match = other.GetComponentInParent<MatchKey>();
            if (match && match.CanStrike)
                entries[other] = transform.InverseTransformPoint(match.transform.position);
        }

        void OnTriggerStay(Collider other)
        {
            var match = other.GetComponentInParent<MatchKey>();
            if (!match || !match.CanStrike)
            {
                entries.Remove(other);
                return;
            }
            if (!entries.TryGetValue(other, out var entry)) return;

            // Measure travel against the striker, not against the room: moving
            // the whole box together with a match must not count as a swipe.
            Vector3 movement = transform.InverseTransformPoint(match.transform.position) - entry;
            if (transform.TransformVector(movement).magnitude < minimumStrikeDistance) return;
            match.LightMatch();
            entries.Remove(other);
        }

        void OnTriggerExit(Collider other) => entries.Remove(other);
        void OnDisable() => entries.Clear();
    }
}
