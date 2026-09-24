using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;


namespace Team15.Deck
{
public class ActionHandler : MonoBehaviour
{
    public InputActionReference quitAction, switchViewAction, lightAction, interactAction, spawnShipAction;

    public Transform controller, globe, map, player, roomview, externalview, pointer;
    public float rayDistance = 20f, pointerBobAmount = 0.02f, pointerBobSpeed = 2f, pointerZAmount = 0.02f, shipSpeed = 2f, gravity = 0.2f, spawnXMin=-4.6f, spawnXMax=4.6f, spawnY = 0.04f, spawnZMin=-4f, spawnZMax=4f;
    public Light light;
    public Vector3 pointerStartPosition, selectedTargetPoint;
    private GameObject currentHitObject;
    private RaycastHit currentHit;
    private bool isExternalView = false, hasSelectedPoint = false;
    public GameObject lightEffectPrefab, shipPrefab, pointSelectEffectPrefab, viewChangeEffectPrefab;
    public AudioSource lightChangeSound, pointSelectSound;
    private LineRenderer debugLine;
    public int maxShips = 5;
    private List<GameObject> activeShips = new List<GameObject>();
    void Start()
    {
        // Enable actions
        if (quitAction != null)
        {
            quitAction.action.Enable();

            quitAction.action.performed += (ctx) =>
            {
                #if UNITY_EDITOR
                            UnityEditor.EditorApplication.isPlaying = false;
                #else
                            Application.Quit();
                #endif
            };
        }

        if (switchViewAction != null)
        {
            switchViewAction.action.Enable();

            switchViewAction.action.performed += (ctx) =>
            {
                isExternalView = !isExternalView;

                Transform destination =
                    isExternalView ? externalview : roomview;

                if (player != null && destination != null)
                {
                    player.SetPositionAndRotation(
                        destination.position,
                        destination.rotation
                    );

                    if (viewChangeEffectPrefab != null)
                    {
                        Instantiate(
                            viewChangeEffectPrefab,
                            destination.position,
                            destination.rotation
                        );
                    }
                }

                
            };
        }


        if (lightAction != null)
        {
            lightAction.action.Enable();

            lightAction.action.performed += (ctx) =>
            {
                ChangeLightColor();
            };
        }


        if (pointer != null)
        {
            pointerStartPosition = pointer.localPosition;
        }

        GameObject lineObj = new GameObject("DebugRayLine");
        lineObj.transform.parent = controller;
        lineObj.transform.localPosition = Vector3.zero;
        
        debugLine = lineObj.AddComponent<LineRenderer>();
        debugLine.material = new Material(Shader.Find("Sprites/Default"));
        debugLine.startWidth = 0.02f;
        debugLine.endWidth = 0.02f;
        debugLine.startColor = Color.red;
        debugLine.endColor = Color.red;
        debugLine.positionCount = 2;

        if (interactAction != null)
        {
            interactAction.action.Enable();

            interactAction.action.performed += (ctx) =>
            {
                if (currentHitObject != null)
                {
                    TriggerEffectOnObject(currentHitObject);
                }
            };
        }

        if (spawnShipAction != null)
        {
            spawnShipAction.action.Enable();
            spawnShipAction.action.performed += (ctx) =>
            {
                if (hasSelectedPoint)
                    SpawnShip(selectedTargetPoint);
            };
        }
    }
    void Update()
    {
        activeShips.RemoveAll(ship => ship == null);

        if (globe != null)
        {
            globe.Rotate(
                Vector3.up,
                20f * Time.deltaTime
            );
        }


        if (pointer != null)
        {
           float movement = Mathf.Sin(Time.time * pointerBobSpeed);

            pointer.localPosition = pointerStartPosition + Vector3.up * movement * pointerBobAmount + Vector3.forward * movement * pointerZAmount;
        }

        DoRaycast();
    }
    void ChangeLightColor()
        {
            if (light == null)
                return;

            light.color = Random.ColorHSV(
                0f,
                1f,
                0.7f,
                1f,
                0.8f,
                1f
            );
            
             if (lightChangeSound != null)
                {
                    // Enable the component
                    lightChangeSound.enabled = true;
                    
                    // Wait a frame if needed
                    lightChangeSound.Stop();
                    lightChangeSound.PlayOneShot(lightChangeSound.clip);
                    
                    Debug.Log("Sound played: " + lightChangeSound.clip.name);
                }
                else
                {
                    Debug.LogWarning("No AudioSource assigned!");
                }
            
            if (lightEffectPrefab != null)
            {
                Instantiate(lightEffectPrefab, light.transform.position, Quaternion.identity);
                Debug.Log("Light change effect spawned");
            }
        }


    void SpawnShip(Vector3 targetPoint)
        {
            // CRITICAL: Only spawn if hitting the MAP directly
            if (currentHitObject == null || (currentHitObject != map && !currentHitObject.transform.IsChildOf(map)))
            {
                Debug.LogWarning("Must aim at the MAP to spawn ships!");
                return;
            }

            if (activeShips.Count >= maxShips)
            {
                Debug.LogWarning("Max ships reached!");
                return;
            }

            if (shipPrefab == null)
            {
                Debug.LogError("Ship prefab not assigned!");
                return;
            }

            Vector3 localTargetPoint = map.InverseTransformPoint(targetPoint);

            Vector3 localSpawnPos = new Vector3(
                Random.Range(spawnXMin, spawnXMax),
                spawnY,
                Random.Range(spawnZMin, spawnZMax)
            );

            Vector3 worldSpawnPos = map.TransformPoint(localSpawnPos);

            GameObject newShip = Instantiate(shipPrefab, worldSpawnPos, Quaternion.identity, map);
            activeShips.Add(newShip);

            if (pointSelectSound != null)
            {
                pointSelectSound.enabled = true;
                pointSelectSound.Stop();
                pointSelectSound.PlayOneShot(pointSelectSound.clip);
                Debug.Log("Sound played at spawn point");
            }
            
            if (pointSelectEffectPrefab != null)
            {
                Instantiate(pointSelectEffectPrefab, newShip.transform.position, newShip.transform.rotation, newShip.transform);
            }

            shipHandler shipController = newShip.GetComponent<shipHandler>();
            if (shipController != null)
            {
                shipController.shipSpeed = shipSpeed;
                shipController.gravity = gravity;
                shipController.orbitRadius = 0.5f; 
                shipController.SetMapBounds(spawnXMin, spawnXMax, spawnZMin, spawnZMax);
                shipController.SetTargetPosition(localTargetPoint); 
            }

            Debug.Log("Ship spawned! Total ships: " + activeShips.Count);
        }
    void DoRaycast()
        {
            Ray ray = new Ray(controller.position, controller.forward);
            
            if (Physics.Raycast(ray, out RaycastHit hit, rayDistance))
            {
                currentHitObject = hit.collider.gameObject;
                currentHit = hit;
            
                debugLine.SetPosition(0, controller.position);
                debugLine.SetPosition(1, hit.point);

                selectedTargetPoint = hit.point; 
                hasSelectedPoint = true;
            }
            else
            {
                currentHitObject = null;
                
                debugLine.SetPosition(0, controller.position);
                debugLine.SetPosition(1, controller.position + controller.forward * rayDistance);
            }
        }

     void TriggerEffectOnObject(GameObject targetObject)
        {
            InteractHandler interactable = targetObject.GetComponentInParent<InteractHandler>();
            
            if (interactable != null)
            {
                Debug.Log("Found InteractHandler on: " + interactable.gameObject.name);
                interactable.PlayEffect();
            }
            else
            {
                Debug.Log("Object " + targetObject.name + " has no InteractHandler component (checked parents too)");
            }
        }
}
}
