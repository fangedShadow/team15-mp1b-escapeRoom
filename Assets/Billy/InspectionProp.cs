using UnityEngine;

// Fit the collider to the imported mesh before XRGrabInteractable registers it.
[DefaultExecutionOrder(-200)]
[RequireComponent(typeof(MeshFilter), typeof(BoxCollider), typeof(Rigidbody))]
public class InspectionProp : MonoBehaviour
{
    private void Awake()
    {
        Mesh mesh = GetComponent<MeshFilter>().sharedMesh;
        if (mesh == null)
            return;
        BoxCollider box = GetComponent<BoxCollider>();
        box.center = mesh.bounds.center;
        box.size = mesh.bounds.size;
    }
}
