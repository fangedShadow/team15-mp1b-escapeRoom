using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class Collectible : MonoBehaviour
{
    public CollectibleManager manager;

    private bool collected = false;
    private XRGrabInteractable grabInteractable;

    private void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
    }

    private void OnEnable()
    {
        grabInteractable.selectEntered.AddListener(OnGrabbed);
    }

    private void OnDisable()
    {
        grabInteractable.selectEntered.RemoveListener(OnGrabbed);
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        if (collected)
            return;

        collected = true;

        // Tell the manager this collectible was grabbed
        manager.CollectItem();

        // Make the collectible disappear
        gameObject.SetActive(false);
    }
}


