using UnityEngine;

namespace Team15.Storage
{
public class shipHandler : MonoBehaviour
{
    public float shipSpeed = 2f;
    public float acceleration = 4f, deceleration = 6f, arrivalDistance = 0.08f;
    public float boundaryPadding = 0.25f;
    public float wrongLocationLifetime = 5f;
    public GameObject orbitEffectPrefab, collisionEffectPrefab;
    public AudioSource orbitSound, collisionSound;
    public GameObject successKeyPrefab;
    public Vector3 successKeyPosition = new Vector3(-4.5f, 4f, 3.5f);
    public bool rewardOnArrival;

    private Vector3 targetPosition;
    private Rigidbody rb;
    private float collisionCooldown = 0f;
    private float wrongLocationTimer;
    private Vector3 localVelocity;
    private bool isDestroyed;
    
    // Map bounds to stay within
    private float boundXMin = -4.6f, boundXMax = 4.6f, boundZMin = -4f, boundZMax = 4f;
    
    private bool hasArrived;

    void Awake()
    {
        if (GetComponentInParent<shipHandler>() != this)
        {
            enabled = false;
            return;
        }

        rb = GetComponent<Rigidbody>();
    }

    void Start()
    {
        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.useGravity = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.linearVelocity = Vector3.zero;
        }
        
    }

    void Update()
    {
        if (collisionCooldown > 0)
            collisionCooldown -= Time.deltaTime;

        if (hasArrived && !rewardOnArrival)
        {
            wrongLocationTimer += Time.deltaTime;
            if (wrongLocationTimer >= wrongLocationLifetime)
                DestroyShip();
        }
    }

    void FixedUpdate()
    {
        if (!hasArrived)
        {
            TravelToTarget();
        }
    }

    void TravelToTarget()
    {
        Vector3 currentPosition = transform.parent != null
            ? transform.parent.InverseTransformPoint(transform.position)
            : transform.position;
        Vector3 offset = targetPosition - currentPosition;
        float distanceToTarget = offset.magnitude;

        if (distanceToTarget <= arrivalDistance)
        {
            MoveToLocalPosition(targetPosition);
            ArriveAtTarget();
            return;
        }

        float stoppingDistance = (localVelocity.sqrMagnitude / (2f * Mathf.Max(deceleration, 0.01f)));
        float accelerationRate = distanceToTarget <= stoppingDistance ? deceleration : acceleration;
        Vector3 desiredVelocity = offset.normalized * shipSpeed;
        localVelocity = Vector3.MoveTowards(localVelocity, desiredVelocity, accelerationRate * Time.fixedDeltaTime);
        Vector3 nextPosition = currentPosition + localVelocity * Time.fixedDeltaTime;
        MoveToLocalPosition(nextPosition);
    }

    void ArriveAtTarget()
    {
        localVelocity = Vector3.zero;
        hasArrived = true;

        Debug.Log("Ship reached " + (rewardOnArrival ? "the correct" : "an incorrect") + " location.");

        if (rewardOnArrival && orbitEffectPrefab != null)
        {
            SpawnEffect(orbitEffectPrefab);
        }

        if (rewardOnArrival && orbitSound != null && orbitSound.clip != null)
        {
            orbitSound.enabled = true;
            orbitSound.Stop();
            orbitSound.PlayOneShot(orbitSound.clip);
        }

        if (rewardOnArrival && successKeyPrefab != null && transform.parent != null)
        {
            GameObject key = Instantiate(
                successKeyPrefab,
                transform.parent.TransformPoint(successKeyPosition),
                transform.parent.rotation,
                transform.parent
            );

            if (key.GetComponent<KeyItem>() == null)
                key.AddComponent<KeyItem>();
        }
    }

    void MoveToLocalPosition(Vector3 localPosition)
    {
        localPosition.x = Mathf.Clamp(localPosition.x, boundXMin, boundXMax);
        localPosition.z = Mathf.Clamp(localPosition.z, boundZMin, boundZMax);
        if (rb != null)
            rb.MovePosition(transform.parent != null ? transform.parent.TransformPoint(localPosition) : localPosition);
        else
            transform.localPosition = localPosition;
    }

    void SpawnEffect(GameObject effectPrefab)
    {
        Quaternion effectRotation = transform.rotation * effectPrefab.transform.localRotation;
        Instantiate(effectPrefab, transform.position, effectRotation, transform);
    }

    void OnCollisionEnter(Collision collision)
    {
        shipHandler otherShip = collision.collider.GetComponentInParent<shipHandler>();
        
        if (otherShip != null && otherShip != this && collisionCooldown <= 0 && !isDestroyed && !otherShip.isDestroyed)
        {
            if (collisionEffectPrefab != null)
            {
                Vector3 effectPosition = collision.contactCount > 0
                    ? collision.GetContact(0).point
                    : transform.position;
                Instantiate(collisionEffectPrefab, effectPosition, transform.rotation);
            }
            
            if (collisionSound != null)
            {
                AudioSource.PlayClipAtPoint(collisionSound.clip, transform.position, collisionSound.volume);
            }
            
            collisionCooldown = 1f;
            otherShip.collisionCooldown = 1f;
            DestroyShip();
            otherShip.DestroyShip();
        }
    }

    void DestroyShip()
    {
        if (isDestroyed)
            return;

        isDestroyed = true;
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.detectCollisions = false;
        }
        Destroy(gameObject);
    }

    public void SetTargetPosition(Vector3 target)
    {
        targetPosition = new Vector3(
            Mathf.Clamp(target.x, boundXMin, boundXMax),
            target.y,
            Mathf.Clamp(target.z, boundZMin, boundZMax)
        );
    }
    public void SetMapBounds(float xMin, float xMax, float zMin, float zMax)
    {
        boundXMin = xMin + boundaryPadding;
        boundXMax = xMax - boundaryPadding;
        boundZMin = zMin + boundaryPadding;
        boundZMax = zMax - boundaryPadding;
    }
}
}
