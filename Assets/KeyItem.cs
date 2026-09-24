using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class KeyItem : MonoBehaviour
{
    public string keyId;
    public bool IsClaimed { get; private set; }
    public bool HasBeenPickedUp { get; private set; }
    private XRGrabInteractable grab;

    public GameObject PickupObject => grab != null ? grab.gameObject : gameObject;

    void Awake()
    {
        grab = GetComponentInParent<XRGrabInteractable>();
        if (grab == null)
            grab = GetComponentInChildren<XRGrabInteractable>(true);
        if (grab != null)
            grab.selectEntered.AddListener(OnPickedUp);
    }

    void OnPickedUp(SelectEnterEventArgs args) => HasBeenPickedUp = true;

    public bool TryClaim()
    {
        if (IsClaimed)
            return false;
        IsClaimed = true;
        return true;
    }

    void OnDestroy()
    {
        if (grab != null)
            grab.selectEntered.RemoveListener(OnPickedUp);
    }
}
