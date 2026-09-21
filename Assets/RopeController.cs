using UnityEngine;

public class RopeController : MonoBehaviour
{
    public GameObject box;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Sword"))
        {
            // Make the rope disappear
            Destroy(gameObject);

            // Get the box's Rigidbody
            Rigidbody boxRb = box.GetComponent<Rigidbody>();

            // Turn off Hanging Breeze
            HangingBreeze breeze = box.GetComponent<HangingBreeze>();
            if (breeze != null)
            {
                breeze.enabled = false;
            }

            // Remove any movement from before the release
            boxRb.linearVelocity = Vector3.zero;
            boxRb.angularVelocity = Vector3.zero;

            // Let gravity take over
            boxRb.isKinematic = false;
            boxRb.useGravity = true;
        }
    }
}
