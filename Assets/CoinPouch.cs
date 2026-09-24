using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class CoinPouch : MonoBehaviour
{
    public AudioSource coinInsertSound;
    public ActionHandler puzzleHandler;
    public int requiredCoins = 3;
    private readonly HashSet<CoinItem> insertedCoins = new HashSet<CoinItem>();
    public int InsertedCount => insertedCoins.Count;

    void OnTriggerEnter(Collider other) => CollectCoin(other.gameObject);
    void OnTriggerStay(Collider other) => CollectCoin(other.gameObject);
    void OnCollisionEnter(Collision collision) => CollectCoin(collision.gameObject);

    void CollectCoin(GameObject objectInPouch)
    {
        if (puzzleHandler == null || !puzzleHandler.CanCollectCoins)
            return;
        CoinItem coin = objectInPouch.GetComponentInParent<CoinItem>();
        if (coin == null)
            coin = objectInPouch.GetComponentInChildren<CoinItem>();
        if (coin == null || !insertedCoins.Add(coin))
            return;
        if (coinInsertSound != null && coinInsertSound.clip != null)
        {
            coinInsertSound.enabled = true;
            coinInsertSound.PlayOneShot(coinInsertSound.clip);
        }
        XRGrabInteractable grab = coin.GetComponentInParent<XRGrabInteractable>();
        if (grab == null)
            grab = coin.GetComponentInChildren<XRGrabInteractable>();
        if (grab != null)
        {
            while (grab.isSelected && grab.interactionManager != null)
                grab.interactionManager.SelectExit(grab.interactorsSelecting[0], grab);
        }
        Destroy(grab != null ? grab.gameObject : coin.gameObject);
        puzzleHandler.RegisterCoin(insertedCoins.Count, Mathf.Max(3, requiredCoins));
    }
}
