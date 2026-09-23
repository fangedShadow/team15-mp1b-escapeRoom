using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using TMPro;

public class ActionHandler : MonoBehaviour
{
    public InputActionReference quitAction, lightAction, interactAction, spawnShipAction, teleportToWinAction;

    public Transform controller, globe, map, player, pointer;
    public float rayDistance = 20f, pointerBobAmount = 0.02f, pointerBobSpeed = 2f, pointerZAmount = 0.02f, shipSpeed = 2f, spawnXMin=-4.6f, spawnXMax=4.6f, spawnY = 0.04f, spawnZMin=-4f, spawnZMax=4f;
    public Vector3 correctShipPoint = new Vector3(-2.5f, 0f, -0.8f);
    public Vector3 keySpawnPosition = new Vector3(-4.5f, 4f, 3.5f);
    public float correctShipPointRadius = 0.75f;
    public int puzzleLevel = 1;
    public GameObject key2InBucket;
    public GameObject key2Riddle, key3Riddle;
    public Material angleViewerMaterial;
    public MeshCollider[] finalFakeWalls;
    public GameObject finalKey;
    public CoinPouch coinPouch;
    public GameObject[] winObjects;
    public GameObject winFireworksPrefab;
    public Transform winFireworksPoint;
    public GameObject winCanvas;
    public GameObject winText;
    public Transform riddleFacingTransform;
    public Transform riddle3FacingTransform;
    public Collider teleportRoomBounds;
    public Transform teleportRoomPoint, winRoomPoint;
    public float teleportRoomRadius = 3f;
    public Light light;
    public Vector3 pointerStartPosition, selectedTargetPoint;
    private GameObject currentHitObject;
    private bool hasSelectedPoint = false;
    public GameObject lightEffectPrefab, shipPrefab, keyPrefab, pointSelectEffectPrefab;
    public AudioSource lightChangeSound, pointSelectSound, winRoomEntranceSound;
    private LineRenderer debugLine;
    public int maxShips = 5;
    private List<GameObject> activeShips = new List<GameObject>();
    [SerializeField] private bool synchronizeAllPointLights = true;
    [SerializeField] private float lightInputCooldown = 0.2f;
    private readonly Color[] lightColors =
    {
        Color.white,
        new Color(1f, 0.65f, 0f),
        new Color(1f, 0f, 1f),
        Color.green
    };
    private int lightColorIndex;
    private float lastLightInputTime = -Mathf.Infinity;
    private Light[] controlledLights;
    public bool key1Spawned;
    private bool gameWon;
    [Range(0f, 1f)] public float riddleViewAngle = 0.4f;

    void Start()
    {
        SetKey2Visible(false);
        ApplyAngleViewerMaterial();
        if (finalKey != null)
            finalKey.SetActive(false);
        if (winCanvas != null)
            winCanvas.SetActive(false);
        if (winText != null)
            winText.SetActive(false);
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

        if (light == null)
        {
            Debug.LogError("ActionHandler has no Light assigned. Assign the visible point light in the Inspector.");
        }
        else
        {
            lightColorIndex = 0;
            controlledLights = GetControlledLights();
            ApplyLightPreset();
            UpdatePuzzleHints();
            UpdateFinalPuzzle();
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

        if (teleportToWinAction != null)
        {
            teleportToWinAction.action.Enable();
            teleportToWinAction.action.performed += (ctx) => TryTeleportToWinRoom();
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
        UpdateAngleViewerDirections(); 
        UpdatePuzzleHints();
        UpdateFinalPuzzle();
    }
    void ChangeLightColor()
        {
            if (light == null)
            {
                Debug.LogError("Light input received, but ActionHandler.light is null.");
                return;
            }

            float currentTime = Time.unscaledTime;
            if (currentTime - lastLightInputTime < lightInputCooldown)
            {
                Debug.LogWarning("Ignored duplicate light input within " + lightInputCooldown + " seconds.");
                return;
            }
            lastLightInputTime = currentTime;

            lightColorIndex = (lightColorIndex + 1) % lightColors.Length;
            ApplyLightPreset();
            UpdatePuzzleHints();
            UpdateFinalPuzzle();
             if (lightChangeSound != null)
                {
                    // Enable the component
                    lightChangeSound.enabled = true;
                    
                    // Wait a frame if needed
                    lightChangeSound.Stop();
                    lightChangeSound.PlayOneShot(lightChangeSound.clip);
                    
                }
                else
                {
                    Debug.LogWarning("No AudioSource assigned!");
                }
            
            if (lightEffectPrefab != null)
            {
                Instantiate(lightEffectPrefab, light.transform.position, Quaternion.identity);
            }
        }

    public void SetPuzzleLevel(int level)
        {
            puzzleLevel = Mathf.Max(puzzleLevel, level);
            UpdatePuzzleHints();
            UpdateFinalPuzzle();
        }

    public void RegisterCoin(int count, int requiredCoins)
        {
            if (count >= requiredCoins)
                puzzleLevel = Mathf.Max(puzzleLevel, 4);

            UpdateFinalPuzzle();
        }

    public void WinGame()
        {
            if (gameWon)
                return;

            gameWon = true;
            puzzleLevel = 4;

            if (winObjects != null)
            {
                foreach (GameObject winObject in winObjects)
                {
                    if (winObject != null)
                        winObject.SetActive(true);
                }
            }

            if (winFireworksPrefab != null)
            {
                Vector3 position = winFireworksPoint != null
                    ? winFireworksPoint.position
                    : transform.position;
                Instantiate(winFireworksPrefab, position, Quaternion.identity);
            }

            if (winCanvas != null)
                winCanvas.SetActive(true);
            if (winText != null)
                winText.SetActive(true);
            StartCoroutine(DiscoLights());
        }

        void TryTeleportToWinRoom()
        {
            if (puzzleLevel < 4 || player == null || winRoomPoint == null || !IsInTeleportRoom())
                return;

            player.SetPositionAndRotation(winRoomPoint.position, winRoomPoint.rotation);
            if (winRoomEntranceSound != null)
            {
                winRoomEntranceSound.Stop();
                if (winRoomEntranceSound.clip != null)
                    winRoomEntranceSound.PlayOneShot(winRoomEntranceSound.clip);
            }
            WinGame();
        }

        bool IsInTeleportRoom()
        {
            if (teleportRoomBounds != null)
                {
                    Ray standRay = new Ray(player.position + Vector3.up * 0.5f, Vector3.down);
                    return teleportRoomBounds.Raycast(standRay, out RaycastHit hit, 2f);
                }

            return teleportRoomPoint != null &&
                Vector3.Distance(player.position, teleportRoomPoint.position) <= teleportRoomRadius;
        }

    System.Collections.IEnumerator DiscoLights()
        {
            while (gameWon)
            {
                lightColorIndex = Random.Range(0, lightColors.Length);
                ApplyLightPreset();
                yield return new WaitForSeconds(0.35f);
            }
        }

    void UpdateFinalPuzzle()
        {
            bool gold = lightColorIndex == 1;
            bool canPassWalls = puzzleLevel >= 3 && gold;

            if (finalFakeWalls != null)
            {
                foreach (MeshCollider wall in finalFakeWalls)
                {
                    if (wall != null)
                        wall.enabled = !canPassWalls;
                }
            }

            if (finalKey != null)
                finalKey.SetActive(puzzleLevel >= 4);
        }

    void UpdatePuzzleHints()
        {
            bool purple = lightColorIndex == 2;
            bool gold = lightColorIndex == 1;

            if (key2Riddle != null)
                key2Riddle.SetActive(puzzleLevel >= 2 && purple);

            if (key3Riddle != null)
                key3Riddle.SetActive(puzzleLevel == 3 && gold);

            SetKey2Visible(puzzleLevel >= 2 && purple);
        }

    void ApplyAngleViewerMaterial()
    {
        if (angleViewerMaterial == null)
            return;

        ApplyAngleViewerMaterial(key2Riddle, riddleFacingTransform);
        ApplyAngleViewerMaterial(key3Riddle, riddle3FacingTransform);
    }

    void ApplyAngleViewerMaterial(GameObject riddle, Transform facing)
    {
        if (riddle == null || angleViewerMaterial == null)
            return;

        TMP_Text[] textComponents = riddle.GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text textComponent in textComponents)
        {
            if (textComponent.fontSharedMaterial == null)
                continue;

            // KEY FIX: clone the text's OWN existing material first.
            // This keeps _MainTex correctly pointing at the real font atlas.
            Material newMaterial = new Material(textComponent.fontSharedMaterial);
            newMaterial.shader = angleViewerMaterial.shader; // just swap the shader, texture stays bound
            if (newMaterial.HasProperty("_Color"))
                newMaterial.SetColor("_Color", Color.red);

            textComponent.fontSharedMaterial = newMaterial;
            textComponent.fontMaterial = newMaterial;
            textComponent.material = newMaterial;
            textComponent.SetMaterialDirty();
            textComponent.SetVerticesDirty();
        }
    }

    void UpdateAngleViewerDirections()
    {
        UpdateAngleViewerDirection(key2Riddle, riddleFacingTransform);
        UpdateAngleViewerDirection(key3Riddle, riddle3FacingTransform);
    }

    void UpdateAngleViewerDirection(GameObject riddle, Transform facing)
    {
        if (riddle == null)
            return;

        TMP_Text[] textComponents = riddle.GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text textComponent in textComponents)
        {
            if (textComponent.fontSharedMaterial != null)
                SetAngleViewerProperties(textComponent.fontSharedMaterial, facing);
        }
    }

    void SetAngleViewerProperties(Material material, Transform facing)
    {
        if (material == null)
            return;

        if (material.HasProperty("_ViewThreshold") && angleViewerMaterial.HasProperty("_ViewThreshold"))
            material.SetFloat("_ViewThreshold", angleViewerMaterial.GetFloat("_ViewThreshold"));

        Vector3 dir = facing != null ? facing.forward : Vector3.forward;
        if (dir.sqrMagnitude < 0.0001f)
            dir = Vector3.forward;

        if (material.HasProperty("_FacingDirectionWS"))
            material.SetVector("_FacingDirectionWS", dir);
    }

    void SetKey2Visible(bool visible)
    {
        if (key2InBucket == null)
            return;

        KeyItem key = key2InBucket.GetComponent<KeyItem>();
        if (key == null)
            key = key2InBucket.AddComponent<KeyItem>();

        if (!key.IsClaimed)
            key2InBucket.SetActive(visible);
    }

    Light[] GetControlledLights()
    {

            if (!synchronizeAllPointLights)
                return new[] { light };

            Light[] sceneLights = FindObjectsByType<Light>(FindObjectsInactive.Include);
            List<Light> pointLights = new List<Light>();

            foreach (Light sceneLight in sceneLights)
            {
                if (sceneLight.type == LightType.Point && sceneLight.gameObject.activeInHierarchy)
                    pointLights.Add(sceneLight);
            }

            if (!pointLights.Contains(light))
                pointLights.Add(light);

            return pointLights.ToArray();
        }

    void ApplyLightPreset()
        {
            if (controlledLights == null)
                controlledLights = GetControlledLights();

            Color presetColor = lightColors[lightColorIndex];
            foreach (Light controlledLight in controlledLights)
            {
                if (controlledLight == null)
                    continue;

                controlledLight.enabled = true;
                controlledLight.color = presetColor;
                controlledLight.intensity = Mathf.Max(controlledLight.intensity, 1f);
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

            shipHandler shipController = newShip.GetComponent<shipHandler>();
            if (shipController != null)
            {
                shipController.shipSpeed = shipSpeed;
                shipController.SetMapBounds(spawnXMin, spawnXMax, spawnZMin, spawnZMax);
                if (IsCorrectShipPoint(localTargetPoint) && !key1Spawned)
                {
                    shipController.successKeyPrefab = keyPrefab;
                    key1Spawned = true;
                }
                shipController.successKeyPosition = keySpawnPosition;
                shipController.rewardOnArrival = IsCorrectShipPoint(localTargetPoint);
                shipController.SetTargetPosition(localTargetPoint);
            }

        }

    bool IsCorrectShipPoint(Vector3 targetPoint)
        {
            Vector2 selectedXZ = new Vector2(targetPoint.x, targetPoint.z);
            Vector2 correctXZ = new Vector2(correctShipPoint.x, correctShipPoint.z);
            return Vector2.Distance(selectedXZ, correctXZ) <= correctShipPointRadius;
        }
    void DoRaycast()
        {
            Ray ray = new Ray(controller.position, controller.forward);
            
            if (Physics.Raycast(ray, out RaycastHit hit, rayDistance))
            {
                currentHitObject = hit.collider.gameObject;
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
                interactable.PlayEffect();
            }
        }
}
