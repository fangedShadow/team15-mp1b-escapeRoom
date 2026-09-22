using UnityEngine;

public class CollectibleManager : MonoBehaviour
{
    public GameObject objectToAppear;

    private int collectedCount = 0;
    private int totalCollectibles;

    private void Start()
    {
        totalCollectibles = FindObjectsOfType<Collectible>().Length;

        objectToAppear.SetActive(false);
    }

    public void CollectItem()
    {
        collectedCount++;

        if (collectedCount >= totalCollectibles)
        {
            objectToAppear.SetActive(true);
        }
    }
}

