using UnityEngine;

public class KeyReveal : MonoBehaviour
{
    [SerializeField] private GameObject keyObject;

    private void Awake()
    {
        if (keyObject == null)
            keyObject = gameObject;

        keyObject.SetActive(false);
    }

    public void RevealKey()
    {
        keyObject.SetActive(true);
    }
}
