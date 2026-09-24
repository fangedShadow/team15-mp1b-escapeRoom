using System.Collections.Generic;
using UnityEngine;
namespace Team15.Storage
{
public class CoinPouch : MonoBehaviour
{
    public AudioSource coinInsertSound;
    public ActionHandler puzzleHandler;
    public int requiredCoins = 3;

    private readonly HashSet<CoinItem> insertedCoins = new HashSet<CoinItem>();

    void OnTriggerEnter(Collider other)
    {
        CollectCoin(other.gameObject);
    }

    void OnCollisionEnter(Collision collision)
    {
        CollectCoin(collision.gameObject);
    }

    void CollectCoin(GameObject objectInPouch)
    {
        CoinItem coin = objectInPouch.GetComponentInParent<CoinItem>();
        if (coin == null)
            coin = objectInPouch.GetComponentInChildren<CoinItem>();

        if (coin == null)
            return;

        if (!insertedCoins.Add(coin))
            return;

        if (coinInsertSound != null && coinInsertSound.clip != null)
        {
            coinInsertSound.enabled = true;
            coinInsertSound.PlayOneShot(coinInsertSound.clip);
        }

        Destroy(coin.gameObject);
        puzzleHandler?.RegisterCoin(insertedCoins.Count, requiredCoins);
    }
}

}
