using UnityEngine;

public class MovingTarget : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float distance = 2f;
    [SerializeField] private float speed = 2f;

    private Vector3 startPosition;
    private int direction = 1;

    private void Start()
    {
        startPosition = transform.position;
    }

    private void Update()
    {
        // Move along the Z axis
        float movement = direction * speed * Time.deltaTime;
        transform.Translate(Vector3.forward * movement, Space.World);

        // Check if the target has reached either end
        float distanceFromStart = transform.position.z - startPosition.z;

        if (direction == 1 && distanceFromStart >= distance)
        {
            ChangeDirection(-1);
        }
        else if (direction == -1 && distanceFromStart <= -distance)
        {
            ChangeDirection(1);
        }
    }

    private void ChangeDirection(int newDirection)
    {
        direction = newDirection;

        // Rotate 180 degrees around the Y axis
        transform.Rotate(0f, 180f, 0f);
    }
}
