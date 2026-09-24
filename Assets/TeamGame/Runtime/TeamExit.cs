using BlackTide;
using BlackTide.MP1B;
using UnityEngine;

namespace Team15
{
    [RequireComponent(typeof(BoxCollider), typeof(CabinTransition))]
    public sealed class TeamExit : MonoBehaviour
    {
        public TeamRoom room;
        public void TryEnter(PiratePlayer player)
        {
            if (!player || !room || !TeamSession.Instance || CabinTransition.IsTravelling) return;
            room.RefreshProgress();
            GetComponent<CabinTransition>().Travel();
        }
        void OnTriggerEnter(Collider other)
        {
            // Hand spheres and held props cannot accidentally trigger a room change.
            var player = other.GetComponent<PiratePlayer>();
            if (player && other == player.body) TryEnter(player);
        }
        void OnTriggerStay(Collider other)
        {
            if (!room || !room.IsComplete) return;
            var player = other.GetComponent<PiratePlayer>();
            if (player && other == player.body) TryEnter(player);
        }
    }
}
