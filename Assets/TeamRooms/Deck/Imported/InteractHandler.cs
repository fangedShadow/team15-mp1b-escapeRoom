using UnityEngine;


namespace Team15.Deck
{
public class InteractHandler : MonoBehaviour
{
    public GameObject effectPrefab;
    public AudioSource audioSource;

    public void PlayEffect()
    {
        
       {
    Debug.Log("PlayEffect called on: " + gameObject.name);
    
    if (audioSource != null)
    {
        audioSource.enabled = true;
        
        audioSource.Stop();
        audioSource.PlayOneShot(audioSource.clip);
        
        Debug.Log("Sound played: " + audioSource.clip.name);
    }
    else
    {
        Debug.LogWarning("No AudioSource assigned!");
    }
    
    if (effectPrefab != null)
    {
        Instantiate(effectPrefab, transform.position, Quaternion.identity);
        Debug.Log("Effect instantiated: " + effectPrefab.name);
    }
}
    }
}
}
