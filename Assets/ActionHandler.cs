using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using TMPro;

public class ActionHandler : MonoBehaviour
{
    public InputActionReference quitAction, lightAction, interactAction, spawnShipAction, teleportToWinAction;
    public Transform controller, globe, map, player, pointer;
    public Transform roomSpace;
    public Camera viewCamera;
    public float rayDistance = 20f, pointerBobAmount = 0.02f, pointerBobSpeed = 2f, pointerZAmount = 0.02f;
    public float shipSpeed = 2f, spawnXMin = -4.6f, spawnXMax = 4.6f, spawnY = 0.04f, spawnZMin = -4f, spawnZMax = 4f;
    public Vector3 correctShipPoint = new Vector3(-2.5f, 0f, -0.8f);
    public Vector3 keySpawnPosition = new Vector3(-4.5f, 4f, 3.5f);
    public float correctShipPointRadius = 0.75f;
    public int puzzleLevel = 1;
    public GameObject key2InBucket, key2Riddle, key3Riddle;
    public Material angleViewerMaterial;
    public MeshCollider[] finalFakeWalls;
    public GameObject finalKey;
    public CoinPouch coinPouch;
    public GameObject[] winObjects;
    public GameObject winFireworksPrefab;
    public Transform winFireworksPoint;
    public GameObject winCanvas, winText;
    public Transform riddleFacingTransform, riddle3FacingTransform;
    public Collider teleportRoomBounds;
    public Transform teleportRoomPoint, winRoomPoint;
    public float teleportRoomRadius = 3f;
    public Light light;
    public Vector3 pointerStartPosition, selectedTargetPoint;
    public GameObject lightEffectPrefab, shipPrefab, keyPrefab, pointSelectEffectPrefab;
    public AudioSource lightChangeSound, pointSelectSound, winRoomEntranceSound;
    public int maxShips = 5;
    public bool key1Spawned;
    [SerializeField] private bool synchronizeAllPointLights = true;
    [SerializeField] private float lightInputCooldown = 0.2f;
    [Range(0f, 1f)] public float riddleViewAngle = 0.4f;

    public System.Func<bool> VictoryAllowed;
    public event System.Action RoomCompleted;
    public event System.Action Won;
    public bool IsRoomComplete => unlockedLocks[0] && unlockedLocks[1] && unlockedLocks[2] && coinsComplete;
    public bool CanCollectCoins => unlockedLocks[0] && unlockedLocks[1];
    public bool HasWon => gameWon;
    public int InsertedCoins { get; private set; }

    private readonly bool[] unlockedLocks = new bool[3];
    private readonly List<GameObject> activeShips = new List<GameObject>();
    private readonly List<Material> ownedMaterials = new List<Material>();
    private readonly List<KeyValuePair<InputAction, System.Action<InputAction.CallbackContext>>> subscriptions =
        new List<KeyValuePair<InputAction, System.Action<InputAction.CallbackContext>>>();
    private readonly Color[] lightColors = { Color.white, new Color(1f, 0.65f, 0f), new Color(1f, 0f, 1f), Color.green };
    private Light[] controlledLights;
    private int lightColorIndex;
    private float lastLightInputTime = -Mathf.Infinity;
    private bool coinsComplete, completionSent, gameWon, started, hasSelectedPoint;
    private GameObject currentHitObject;
    private Vector3 currentAimPoint;
    private LineRenderer debugLine;

    void Start()
    {
        SetKeyIdentity(key2InBucket, "bucket");
        SetKeyIdentity(finalKey, "final");
        SetKey2Visible(false);
        if (finalKey != null) finalKey.SetActive(false);
        if (winCanvas != null) winCanvas.SetActive(false);
        if (winText != null) winText.SetActive(false);
        ApplyAngleViewerMaterial(key2Riddle);
        ApplyAngleViewerMaterial(key3Riddle);
        if (pointer != null) pointerStartPosition = pointer.localPosition;
        controlledLights = GetControlledLights();
        ApplyLightPreset();
        UpdatePuzzleHints();
        UpdateFinalPuzzle();
        GameObject lineObject = new GameObject("Navigation Aim");
        lineObject.transform.SetParent(transform, false);
        debugLine = lineObject.AddComponent<LineRenderer>();
        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null)
        {
            Material material = new Material(shader);
            ownedMaterials.Add(material);
            debugLine.sharedMaterial = material;
        }
        debugLine.startWidth = debugLine.endWidth = 0.005f;
        debugLine.startColor = debugLine.endColor = Color.red;
        debugLine.positionCount = 2;
        started = true;
        BindInputActions();
    }

    void OnEnable()
    {
        if (started) BindInputActions();
    }

    void OnDisable() => UnbindInputActions();

    public void BindInputActions()
    {
        UnbindInputActions();
        Subscribe(quitAction, OnQuit);
        Subscribe(lightAction, OnLight);
        Subscribe(interactAction, OnInteract);
        Subscribe(spawnShipAction, OnShip);
        Subscribe(teleportToWinAction, OnTeleport);
    }

    void Subscribe(InputActionReference reference, System.Action<InputAction.CallbackContext> callback)
    {
        if (reference == null || reference.action == null) return;
        reference.action.Enable();
        reference.action.performed += callback;
        subscriptions.Add(new KeyValuePair<InputAction, System.Action<InputAction.CallbackContext>>(reference.action, callback));
    }

    void UnbindInputActions()
    {
        foreach (var subscription in subscriptions)
            subscription.Key.performed -= subscription.Value;
        subscriptions.Clear();
    }

    void OnQuit(InputAction.CallbackContext context)
    {
        if (!isActiveAndEnabled) return;
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #else
        Application.Quit();
        #endif
    }
    void OnLight(InputAction.CallbackContext context) { if (isActiveAndEnabled) ChangeLightColor(); }
    void OnInteract(InputAction.CallbackContext context) { if (isActiveAndEnabled) InteractWithTarget(); }
    void OnShip(InputAction.CallbackContext context) { if (isActiveAndEnabled) SpawnSelectedShip(); }
    void OnTeleport(InputAction.CallbackContext context) { if (isActiveAndEnabled) TryTeleportToWinRoom(); }

    void Update()
    {
        activeShips.RemoveAll(ship => ship == null);
        if (globe != null) globe.Rotate(Vector3.up, 20f * Time.deltaTime);
        if (pointer != null)
        {
            float motion = Mathf.Sin(Time.time * pointerBobSpeed);
            pointer.localPosition = pointerStartPosition + Vector3.up * motion * pointerBobAmount + Vector3.forward * motion * pointerZAmount;
        }
        DoRaycast();
        UpdateAngleViewerDirection(key2Riddle, riddleFacingTransform);
        UpdateAngleViewerDirection(key3Riddle, riddle3FacingTransform);
        UpdatePuzzleHints();
        UpdateFinalPuzzle();
    }

    public void ChangeLightColor()
    {
        if (!isActiveAndEnabled || light == null || gameWon || Time.unscaledTime - lastLightInputTime < lightInputCooldown)
            return;
        lastLightInputTime = Time.unscaledTime;
        lightColorIndex = (lightColorIndex + 1) % lightColors.Length;
        ApplyLightPreset();
        UpdatePuzzleHints();
        UpdateFinalPuzzle();
        PlaySound(lightChangeSound);
        if (lightEffectPrefab != null)
            Instantiate(lightEffectPrefab, light.transform.position, Quaternion.identity, transform);
    }

    public static string KeyForLock(int index)
    {
        return index == 0 ? "map" : index == 1 ? "bucket" : index == 2 ? "final" : string.Empty;
    }

    public bool IsLockUnlocked(int index) => index >= 0 && index < 3 && unlockedLocks[index];

    public bool CanUnlockLock(int index, KeyItem key)
    {
        if (key == null || index < 0 || index > 2 || unlockedLocks[index] || key.keyId != KeyForLock(index))
            return false;
        if (index == 0) return key1Spawned;
        if (!unlockedLocks[index - 1]) return false;
        return index != 2 || coinsComplete;
    }

    public void RegisterLockOpened(int index)
    {
        if (index < 0 || index > 2 || unlockedLocks[index]) return;
        if (index > 0 && !unlockedLocks[index - 1]) return;
        if (index == 2 && !coinsComplete) return;
        unlockedLocks[index] = true;
        SetPuzzleLevel(index + 2);
        NotifyCompletion();
    }

    public void SetPuzzleLevel(int level)
    {
        puzzleLevel = Mathf.Max(puzzleLevel, level);
        UpdatePuzzleHints();
        UpdateFinalPuzzle();
    }

    public void RegisterCoin(int count, int requiredCoins)
    {
        if (!CanCollectCoins) return;
        InsertedCoins = Mathf.Max(InsertedCoins, count);
        coinsComplete = InsertedCoins >= Mathf.Max(3, requiredCoins);
        if (coinsComplete) puzzleLevel = Mathf.Max(puzzleLevel, 4);
        UpdateFinalPuzzle();
        NotifyCompletion();
    }

    void NotifyCompletion()
    {
        if (completionSent || !IsRoomComplete) return;
        completionSent = true;
        RoomCompleted?.Invoke();
    }

    bool CanWin() => IsRoomComplete && (VictoryAllowed == null || VictoryAllowed());

    public void WinGame()
    {
        if (gameWon || !isActiveAndEnabled || !CanWin()) return;
        gameWon = true;
        if (winObjects != null)
            foreach (GameObject item in winObjects)
                if (item != null) item.SetActive(true);
        if (winFireworksPrefab != null)
            Instantiate(winFireworksPrefab, winFireworksPoint != null ? winFireworksPoint.position : transform.position, Quaternion.identity, transform);
        if (winCanvas != null) winCanvas.SetActive(true);
        if (winText != null) winText.SetActive(true);
        StartCoroutine(DiscoLights());
        Won?.Invoke();
    }

    public void TryTeleportToWinRoom()
    {
        if (gameWon || !isActiveAndEnabled || !CanWin() || player == null || winRoomPoint == null || !IsInTeleportRoom())
            return;
        CharacterController body = player.GetComponent<CharacterController>();
        bool wasEnabled = body != null && body.enabled;
        if (body != null) body.enabled = false;
        Camera camera = viewCamera != null ? viewCamera : player.GetComponentInChildren<Camera>();
        float yaw = winRoomPoint.eulerAngles.y - (camera != null ? camera.transform.eulerAngles.y : player.eulerAngles.y);
        player.Rotate(Vector3.up, yaw, Space.World);
        Vector3 offset = camera != null ? Vector3.ProjectOnPlane(camera.transform.position - player.position, Vector3.up) : Vector3.zero;
        player.position = winRoomPoint.position - offset;
        Physics.SyncTransforms();
        if (body != null) body.enabled = wasEnabled;
        PlaySound(winRoomEntranceSound);
        WinGame();
    }

    public bool IsInTeleportRoom()
    {
        if (player == null) return false;
        if (teleportRoomBounds != null)
            return teleportRoomBounds.Raycast(new Ray(player.position + Vector3.up * 0.5f, Vector3.down), out _, 2f);
        return teleportRoomPoint != null && Vector3.Distance(player.position, teleportRoomPoint.position) <= RoomDistance(teleportRoomRadius);
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
        bool canPass = CanCollectCoins && lightColorIndex == 1;
        if (finalFakeWalls != null)
            foreach (MeshCollider wall in finalFakeWalls)
                if (wall != null) wall.enabled = !canPass;
        if (finalKey != null)
        {
            KeyItem key = finalKey.GetComponentInChildren<KeyItem>(true);
            if (key != null && !key.IsClaimed) finalKey.SetActive(coinsComplete);
        }
    }

    void UpdatePuzzleHints()
    {
        bool purple = lightColorIndex == 2;
        if (key2Riddle != null) key2Riddle.SetActive(unlockedLocks[0] && purple);
        if (key3Riddle != null) key3Riddle.SetActive(CanCollectCoins && !coinsComplete && lightColorIndex == 1);
        SetKey2Visible(unlockedLocks[0] && purple);
    }

    void SetKeyIdentity(GameObject item, string id)
    {
        if (item == null) return;
        KeyItem[] keys = item.GetComponentsInChildren<KeyItem>(true);
        if (keys.Length == 0) keys = new[] { item.AddComponent<KeyItem>() };
        foreach (KeyItem key in keys) key.keyId = id;
    }

    void SetKey2Visible(bool visible)
    {
        if (key2InBucket == null) return;
        KeyItem key = key2InBucket.GetComponentInChildren<KeyItem>(true);
        if (key != null && !key.IsClaimed && !key.HasBeenPickedUp)
            key2InBucket.SetActive(visible);
    }

    void ApplyAngleViewerMaterial(GameObject riddle)
    {
        if (riddle == null || angleViewerMaterial == null) return;
        foreach (TMP_Text text in riddle.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.fontSharedMaterial == null) continue;
            Material material = new Material(text.fontSharedMaterial);
            material.shader = angleViewerMaterial.shader;
            if (material.HasProperty("_Color")) material.SetColor("_Color", Color.red);
            ownedMaterials.Add(material);
            text.fontSharedMaterial = material;
            text.fontMaterial = material;
            text.material = material;
            text.SetMaterialDirty();
            text.SetVerticesDirty();
        }
    }

    void UpdateAngleViewerDirection(GameObject riddle, Transform facing)
    {
        if (riddle == null || angleViewerMaterial == null) return;
        foreach (TMP_Text text in riddle.GetComponentsInChildren<TMP_Text>(true))
        {
            Material material = text.fontSharedMaterial;
            if (material == null) continue;
            if (material.HasProperty("_ViewThreshold") && angleViewerMaterial.HasProperty("_ViewThreshold"))
                material.SetFloat("_ViewThreshold", angleViewerMaterial.GetFloat("_ViewThreshold"));
            if (material.HasProperty("_FacingDirectionWS"))
                material.SetVector("_FacingDirectionWS", facing != null ? facing.forward : Vector3.forward);
        }
    }

    Light[] GetControlledLights()
    {
        List<Light> result = new List<Light>();
        if (synchronizeAllPointLights)
            foreach (Light candidate in FindObjectsByType<Light>(FindObjectsInactive.Include))
                if (candidate.gameObject.scene == gameObject.scene && candidate.type == LightType.Point && candidate.gameObject.activeInHierarchy)
                    result.Add(candidate);
        if (light != null && !result.Contains(light)) result.Add(light);
        return result.ToArray();
    }

    void ApplyLightPreset()
    {
        if (controlledLights == null) controlledLights = GetControlledLights();
        foreach (Light target in controlledLights)
        {
            if (target == null) continue;
            target.enabled = true;
            target.color = lightColors[lightColorIndex];
            target.intensity = Mathf.Max(target.intensity, 1f);
        }
    }

    public void SelectMapPoint(Vector3 worldPoint)
    {
        if (map == null) return;
        selectedTargetPoint = worldPoint;
        hasSelectedPoint = true;
        PlaySound(pointSelectSound);
        if (pointSelectEffectPrefab != null)
        {
            GameObject effect = Instantiate(pointSelectEffectPrefab, worldPoint, map.rotation, roomSpace);
            Destroy(effect, 4f);
        }
    }

    public void SpawnSelectedShip()
    {
        if (!isActiveAndEnabled || !hasSelectedPoint || map == null) return;
        activeShips.RemoveAll(ship => ship == null);
        if (activeShips.Count >= maxShips || shipPrefab == null) return;
        Vector3 localTarget = map.InverseTransformPoint(selectedTargetPoint);
        Vector3 start = new Vector3(Random.Range(spawnXMin, spawnXMax), spawnY, Random.Range(spawnZMin, spawnZMax));
        GameObject ship = Instantiate(shipPrefab, map);
        ship.transform.localPosition = start;
        ship.transform.localRotation = Quaternion.identity;
        activeShips.Add(ship);
        shipHandler handler = ship.GetComponent<shipHandler>();
        if (handler == null) return;
        handler.shipSpeed = shipSpeed;
        handler.SetMapBounds(spawnXMin, spawnXMax, spawnZMin, spawnZMax);
        handler.successKeyPrefab = null;
        handler.puzzleHandler = this;
        handler.rewardOnArrival = Vector2.Distance(new Vector2(localTarget.x, localTarget.z), new Vector2(correctShipPoint.x, correctShipPoint.z)) <= correctShipPointRadius;
        handler.SetTargetPosition(localTarget);
    }

    public bool TryCreateMapReward()
    {
        if (key1Spawned || keyPrefab == null) return false;
        GameObject reward = Instantiate(keyPrefab, roomSpace);
        reward.transform.position = RoomPoint(keySpawnPosition);
        reward.transform.rotation = roomSpace != null ? roomSpace.rotation : Quaternion.identity;
        SetKeyIdentity(reward, "map");
        key1Spawned = true;
        return true;
    }

    public Vector3 RoomPoint(Vector3 point) => roomSpace != null ? roomSpace.TransformPoint(point) : point;
    public float RoomDistance(float distance) => roomSpace != null ? distance * Mathf.Abs(roomSpace.lossyScale.x) : distance;

    void DoRaycast()
    {
        if (controller == null) { if (debugLine != null) debugLine.enabled = false; return; }
        float distance = Mathf.Max(3f, RoomDistance(rayDistance));
        Ray ray = new Ray(controller.position, controller.forward);
        Vector3 end = ray.GetPoint(distance);
        bool hasHit = Physics.Raycast(ray, out RaycastHit hit, distance, ~(1 << 2), QueryTriggerInteraction.Ignore);
        currentHitObject = hasHit ? hit.collider.gameObject : null;
        if (hasHit) { currentAimPoint = hit.point; end = hit.point; }
        if (debugLine != null)
        {
            debugLine.enabled = true;
            debugLine.SetPosition(0, ray.origin);
            debugLine.SetPosition(1, end);
        }
    }

    public void InteractWithTarget()
    {
        if (!isActiveAndEnabled) return;
        DoRaycast();
        if (currentHitObject == null) return;
        if (map != null && (currentHitObject.transform == map || currentHitObject.transform.IsChildOf(map)))
            SelectMapPoint(currentAimPoint);
        InteractHandler effect = currentHitObject.GetComponentInParent<InteractHandler>();
        if (effect != null) effect.PlayEffect();
    }

    static void PlaySound(AudioSource source)
    {
        if (source == null || source.clip == null) return;
        source.enabled = true;
        source.PlayOneShot(source.clip);
    }

    void OnDestroy()
    {
        UnbindInputActions();
        foreach (Material material in ownedMaterials)
            if (material != null) Destroy(material);
    }
}
