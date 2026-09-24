using UnityEngine;


namespace Team15.Deck
{
public class MovingTarget : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveDistance = 2f;
    [SerializeField] private float moveSpeed = 2f;

    private Vector3 startPosition;

    private void Start()
    {
        startPosition = transform.position;
    }

    private void Update()
    {
        float movement = Mathf.Sin(Time.time * moveSpeed) * moveDistance;

        // Move back and forth along the Z axis
        transform.position = startPosition + transform.forward * movement;
    }
}
}
