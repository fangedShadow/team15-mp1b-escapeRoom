using UnityEngine;

public class KeyRevealBox : MonoBehaviour
{
    public GameObject key;

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            // Reveal the key
            key.SetActive(true);

            // Make the box disappear
            Destroy(gameObject);
        }
    }
}

