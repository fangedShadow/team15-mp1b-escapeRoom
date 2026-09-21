using UnityEngine;

public class Sword : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Rope"))
        {
            Destroy(other.gameObject);
        }
    }
}