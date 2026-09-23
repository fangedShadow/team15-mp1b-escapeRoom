using UnityEngine;

namespace BlackTide.MP1B
{
    public sealed class CabinSpawn : MonoBehaviour
    {
        public string spawnId = "FromCaptain";
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(.25f, .9f, .8f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up, .25f);
            Gizmos.DrawLine(transform.position + Vector3.up, transform.position + Vector3.up + transform.forward);
        }
    }
}
