using UnityEngine;

public class CannonController : MonoBehaviour
{
    [Header("Cannon")]
    [SerializeField] private Transform muzzle;
    [SerializeField] private GameObject cannonballPrefab;
    [SerializeField] private float launchForce = 15f;

    [Header("Interaction")]
    [SerializeField] private float cooldown = 1f;

    [Header("Key")]
    [SerializeField] private KeyReveal keyReveal;

    private float lastFireTime = -Mathf.Infinity;

    public void Fire()
    {
        if (Time.time < lastFireTime + cooldown)
            return;

        if (muzzle == null || cannonballPrefab == null)
        {
            Debug.LogWarning("Cannon is missing a muzzle or cannonball prefab.");
            return;
        }

        lastFireTime = Time.time;

        GameObject cannonball = Instantiate(
            cannonballPrefab,
            muzzle.position,
            muzzle.rotation
        );

        Rigidbody rb = cannonball.GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.AddForce(muzzle.forward * launchForce, ForceMode.Impulse);
        }
        else
        {
            Debug.LogWarning("Cannonball prefab needs a Rigidbody.");
        }
    }
}
