using UnityEngine;

namespace Team15.Deck
{
public class shipHandler : MonoBehaviour
{
    public float shipSpeed = 0.05f, gravity = 0.02f, orbitRadius = 0.05f, orbitSpeed = 1f;
    public float boundaryPadding = 0.25f;
    public float orbitDuration = 5f;
    public GameObject spawnEffectPrefab, arrivalEffectPrefab, orbitEffectPrefab, collisionEffectPrefab;
    public AudioSource spawnSound, arrivalSound, orbitSound, collisionSound;

    private Vector3 targetPosition;
    private Rigidbody rb;
    private float collisionCooldown = 0f;
    private float orbitAngle;
    private float orbitTimer;
    private bool isDestroyed;
    
    // Map bounds to stay within
    private float boundXMin = -4.6f, boundXMax = 4.6f, boundZMin = -4f, boundZMax = 4f;
    
    private enum ShipState { Spawning, Traveling, Arrived, Orbiting }
    private ShipState currentState = ShipState.Spawning;

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
        
        if (spawnEffectPrefab != null)
            SpawnEffect(spawnEffectPrefab);
        
        if (spawnSound != null)
        {
            spawnSound.enabled = true;
            spawnSound.Stop();
            spawnSound.PlayOneShot(spawnSound.clip);
            Debug.Log("Sound played: " + spawnSound.clip.name);
        }
        
        Debug.Log("Ship spawned at: " + transform.localPosition + ", Target: " + targetPosition);
    }

    void Update()
    {
        if (collisionCooldown > 0)
            collisionCooldown -= Time.deltaTime;

        switch (currentState)
        {
            case ShipState.Spawning:
                currentState = ShipState.Traveling;
                break;
        }
    }

    void FixedUpdate()
    {
        if (currentState == ShipState.Traveling)
        {
            TravelToTarget();
        }
        else if (currentState == ShipState.Orbiting)
        {
            OrbitTarget();
        }
    }

    void TravelToTarget()
    {
        Vector3 currentPosition = transform.localPosition;
        Vector3 offset = targetPosition - currentPosition;
        offset.y = 0;
        float distanceToTarget = offset.magnitude;

        if (distanceToTarget <= orbitRadius)
        {
            EnterOrbit();
            return;
        }

        Vector3 nextPosition = currentPosition + offset.normalized * Mathf.Min(shipSpeed * Time.fixedDeltaTime, distanceToTarget - orbitRadius);
        nextPosition.y = targetPosition.y;
        MoveToLocalPosition(nextPosition);
    }

    void EnterOrbit()
    {
        Vector3 offset = transform.localPosition - targetPosition;
        offset.y = 0;
        orbitAngle = offset.sqrMagnitude > 0.0001f
            ? Mathf.Atan2(offset.z, offset.x)
            : 0f;
        MoveToLocalPosition(targetPosition + new Vector3(Mathf.Cos(orbitAngle), 0, Mathf.Sin(orbitAngle)) * orbitRadius);
        currentState = ShipState.Arrived;

        if (arrivalEffectPrefab != null)
            SpawnEffect(arrivalEffectPrefab);

        if (arrivalSound != null)
        {
            arrivalSound.enabled = true;
            arrivalSound.Stop();
            arrivalSound.PlayOneShot(arrivalSound.clip);
        }
        
        if (orbitEffectPrefab != null)
            SpawnEffect(orbitEffectPrefab);
        
        if (orbitSound != null)
        {
            orbitSound.enabled = true;
            orbitSound.Stop();
            orbitSound.PlayOneShot(orbitSound.clip);
            Debug.Log("Sound played: " + orbitSound.clip.name);
        }
        
        currentState = ShipState.Orbiting;
        orbitTimer = 0f;
    }

    void OrbitTarget()
    {
        orbitTimer += Time.fixedDeltaTime;
        if (orbitTimer >= orbitDuration)
        {
            DestroyShip();
            return;
        }

        orbitAngle += orbitSpeed * Time.fixedDeltaTime;
        Vector3 orbitPosition = targetPosition + new Vector3(Mathf.Cos(orbitAngle), 0, Mathf.Sin(orbitAngle)) * orbitRadius;
        MoveToLocalPosition(orbitPosition);
    }

    void MoveToLocalPosition(Vector3 localPosition)
    {
        localPosition.x = Mathf.Clamp(localPosition.x, boundXMin, boundXMax);
        localPosition.z = Mathf.Clamp(localPosition.z, boundZMin, boundZMax);
        localPosition.y = targetPosition.y;

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
            
            Debug.Log("Ship collision detected! Destroying both ships");
            
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
        float orbitMargin = orbitRadius + boundaryPadding;
        targetPosition = new Vector3(
            Mathf.Clamp(target.x, boundXMin + orbitMargin, boundXMax - orbitMargin),
            target.y,
            Mathf.Clamp(target.z, boundZMin + orbitMargin, boundZMax - orbitMargin)
        );
        currentState = ShipState.Traveling;
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
