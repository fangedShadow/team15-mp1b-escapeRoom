using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace BlackTide.MP1B
{
    [DisallowMultipleComponent]
    public sealed class CabinRoom : MonoBehaviour
    {
        public static CabinRoom Instance { get; private set; }
        public string ownerId = "captain";
        public CabinPuzzle[] puzzles = new CabinPuzzle[0];
        public CabinSocket[] coinSockets = new CabinSocket[0];
        public Light[] coinLights = new Light[0];
        public Renderer[] coinLightRenderers = new Renderer[0];
        public Color unlockedLampColor = new Color(0.2f, 1f, 0.35f);
        public float lampIntensity = 1.4f;
        public Transform chestLid;
        public Vector3 chestOpenEuler = new Vector3(105f, 0f, 0f);
        public float chestOpenDuration = 1.3f;
        public CabinItem goldKey;
        public Transform goldKeySpawn;
        public CabinSocket exitSocket;
        public Transform exitLockVisual;
        public Vector3 exitUnlockEuler = new Vector3(0f, 0f, 90f);
        public TMP_Text progressText;
        public TMP_Text statusText;
        public TMP_Text clueText;
        public GameObject cluePanel;
        public CabinCollectible[] gems = new CabinCollectible[0];
        public CabinItem[] looseProps = new CabinItem[0];
        public AudioSource sound;
        public AudioClip socketSound;
        public AudioClip chestSound;
        public AudioClip exitSound;
        public AudioSource exitUnlockAudio;
        public UnityEvent onCabinCleared = new UnityEvent();
        public UnityEvent onCabinReset = new UnityEvent();
        public event Action<bool> CompletionChanged;
        public bool IsCleared { get; private set; }
        public bool IsChestOpen { get; private set; }
        public string CurrentMessage => Time.unscaledTime <= _messageUntil ? _currentMessage : string.Empty;
        public float MessageExpiresAt => _messageUntil;
        public int DepositedCoins
        {
            get { int count = 0; foreach (var socket in coinSockets) if (socket && socket.IsUnlocked) count++; return count; }
        }
        public int CollectedGems
        {
            get { int count = 0; foreach (var gem in gems) if (gem && gem.IsCollected) count++; return count; }
        }

        Quaternion _chestClosed;
        Quaternion _lockClosed;
        Vector3 _goldPosition;
        Quaternion _goldRotation;
        Vector3[] _propPositions;
        Quaternion[] _propRotations;
        float _confirmUntil = -1f;
        float _messageUntil;
        string _currentMessage = string.Empty;
        float _scoreboardAt;
        bool _started;
        bool _openingChest;
        MaterialPropertyBlock _lampBlock;
        Action<CabinItem>[] _coinHandlers;

        void Awake()
        {
            Instance = this;
            _chestClosed = chestLid ? chestLid.localRotation : Quaternion.identity;
            _lockClosed = exitLockVisual ? exitLockVisual.localRotation : Quaternion.identity;
            if (goldKey)
            {
                _goldPosition = goldKeySpawn ? goldKeySpawn.position : goldKey.transform.position;
                _goldRotation = goldKeySpawn ? goldKeySpawn.rotation : goldKey.transform.rotation;
            }
            _lampBlock = new MaterialPropertyBlock();
            _propPositions = new Vector3[looseProps.Length];
            _propRotations = new Quaternion[looseProps.Length];
            for (int i = 0; i < looseProps.Length; i++)
                if (looseProps[i])
                {
                    _propPositions[i] = looseProps[i].transform.position;
                    _propRotations[i] = looseProps[i].transform.rotation;
                }
        }

        void OnEnable()
        {
            Instance = this;
            _coinHandlers = new Action<CabinItem>[coinSockets.Length];
            for (int i = 0; i < coinSockets.Length; i++)
            {
                int captured = i;
                _coinHandlers[i] = _ => OnCoinAccepted(captured);
                if (coinSockets[i]) coinSockets[i].Accepted += _coinHandlers[i];
            }
            if (exitSocket) exitSocket.Accepted += OnExitAccepted;
            if (_started)
            {
                for (int i = 0; i < coinSockets.Length; i++) SetLamp(i, coinSockets[i] && coinSockets[i].IsUnlocked ? 1f : 0f);
                if (DepositedCoins == 3 && !IsChestOpen) FinishChestOpening();
                if (IsChestOpen && chestLid) chestLid.localRotation = _chestClosed * Quaternion.Euler(chestOpenEuler);
                if (IsCleared && exitLockVisual) exitLockVisual.localRotation = _lockClosed * Quaternion.Euler(exitUnlockEuler);
            }
        }

        void Start()
        {
            _started = true;
            ResetRoom();
            ShowMessage("Captain's Orders: solve three puzzles, earn three coins, then unlock the chest.");
        }

        void OnDisable()
        {
            if (_coinHandlers != null)
                for (int i = 0; i < coinSockets.Length; i++)
                    if (coinSockets[i]) coinSockets[i].Accepted -= _coinHandlers[i];
            if (exitSocket) exitSocket.Accepted -= OnExitAccepted;
            _openingChest = false;
            _confirmUntil = -1f;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.rKey.wasPressedThisFrame) RequestReset();
                if (keyboard.escapeKey.wasPressedThisFrame) { CloseClue(); _confirmUntil = -1f; }
            }
            if (Time.unscaledTime >= _scoreboardAt)
            {
                _scoreboardAt = Time.unscaledTime + 0.2f;
                UpdateProgress();
            }
            if (statusText && Time.unscaledTime > _messageUntil)
                statusText.text = IsCleared ? "CABIN CLEARED\nTake the key and walk through the open doorway." :
                    "Solve in any order.\nBell signal  •  Captain's log  •  Symbol seal";
        }

        void OnCoinAccepted(int index)
        {
            if (sound && socketSound) sound.PlayOneShot(socketSound);
            StartCoroutine(LightLamp(index));
            ShowMessage("Coin accepted: " + DepositedCoins + " / 3.");
            if (DepositedCoins == 3 && !IsChestOpen && !_openingChest) StartCoroutine(OpenChest());
        }

        IEnumerator LightLamp(int index)
        {
            for (float t = 0f; t < 0.6f; t += Time.deltaTime)
            {
                SetLamp(index, Mathf.SmoothStep(0f, 1f, t / 0.6f));
                yield return null;
            }
            SetLamp(index, 1f);
        }

        void SetLamp(int index, float amount)
        {
            if (index < coinLights.Length && coinLights[index])
            {
                coinLights[index].color = unlockedLampColor;
                coinLights[index].intensity = lampIntensity * amount;
            }
            if (index >= coinLightRenderers.Length || !coinLightRenderers[index]) return;
            if (_lampBlock == null) _lampBlock = new MaterialPropertyBlock();
            var mesh = coinLightRenderers[index];
            mesh.GetPropertyBlock(_lampBlock);
            Color color = Color.Lerp(new Color(0.07f, 0.09f, 0.06f), unlockedLampColor, amount);
            _lampBlock.SetColor("_BaseColor", color);
            _lampBlock.SetColor("_Color", color);
            _lampBlock.SetColor("_EmissionColor", unlockedLampColor * (1.6f * amount));
            mesh.SetPropertyBlock(_lampBlock);
        }

        IEnumerator OpenChest()
        {
            _openingChest = true;
            if (sound && chestSound) sound.PlayOneShot(chestSound);
            for (float t = 0f; t < chestOpenDuration; t += Time.deltaTime)
            {
                if (chestLid) chestLid.localRotation = Quaternion.Slerp(_chestClosed,
                    _chestClosed * Quaternion.Euler(chestOpenEuler), Mathf.SmoothStep(0f, 1f, t / Mathf.Max(0.01f, chestOpenDuration)));
                yield return null;
            }
            FinishChestOpening();
            ShowMessage("The chest is open. Grab the golden key and unlock the exit.");
        }

        void FinishChestOpening()
        {
            _openingChest = false;
            IsChestOpen = true;
            if (chestLid) chestLid.localRotation = _chestClosed * Quaternion.Euler(chestOpenEuler);
            if (goldKey) goldKey.SetAvailable(true);
        }

        void OnExitAccepted(CabinItem item)
        {
            if (IsCleared) return;
            if (!IsChestOpen || DepositedCoins != 3)
            {
                if (exitSocket) exitSocket.ResetSocket();
                ShowMessage("The captain's three coin locks must be opened first.");
                return;
            }
            IsCleared = true;
            if (exitUnlockAudio && exitSound)
            {
                exitUnlockAudio.clip = exitSound;
                exitUnlockAudio.Play();
            }
            else if (sound && exitSound) sound.PlayOneShot(exitSound);
            StartCoroutine(TurnExitLock());
            ShowMessage("CABIN CLEARED. You may take back the key. Walk through the open doorway to continue.");
            CompletionChanged?.Invoke(true);
            onCabinCleared.Invoke();
        }

        IEnumerator TurnExitLock()
        {
            for (float t = 0f; t < 0.7f; t += Time.deltaTime)
            {
                if (exitLockVisual) exitLockVisual.localRotation = Quaternion.Slerp(_lockClosed,
                    _lockClosed * Quaternion.Euler(exitUnlockEuler), Mathf.SmoothStep(0f, 1f, t / 0.7f));
                yield return null;
            }
            if (exitLockVisual) exitLockVisual.localRotation = _lockClosed * Quaternion.Euler(exitUnlockEuler);
        }

        public void ReadClue(CabinPuzzle puzzle)
        {
            if (!puzzle) return;
            puzzle.DiscoverClue();
            if (clueText) clueText.text = puzzle.clueTitle + "\n\n" + puzzle.clueBody;
            if (cluePanel)
            {
                var player = UnityEngine.Object.FindFirstObjectByType<PiratePlayer>();
                if (player && player.view)
                {
                    var view = player.view.transform;
                    var facing = Quaternion.Euler(0f, view.eulerAngles.y, 0f);
                    // Leave this panel fixed in the world while the reader moves their head.
                    // Its front faces local -Z, with a reachable Close control below the text.
                    cluePanel.transform.SetPositionAndRotation(view.position +
                        facing * Vector3.forward * 1.5f - Vector3.up * 0.05f, facing);
                }
                cluePanel.SetActive(true);
            }
            UpdateProgress();
        }

        public void CloseClue() { if (cluePanel) cluePanel.SetActive(false); }

        public void RecoverItems()
        {
            int recovered = 0;
            foreach (var puzzle in puzzles) if (puzzle && puzzle.RecoverReward()) recovered++;
            if (IsChestOpen && goldKey)
            {
                var item = CabinItem.Find(goldKey.itemId);
                if (item && !item.IsHeld && !item.IsDocked)
                {
                    item.Restore(goldKeySpawn ? goldKeySpawn.position : _goldPosition,
                        goldKeySpawn ? goldKeySpawn.rotation : _goldRotation);
                    item.SetAvailable(true);
                    recovered++;
                }
            }
            ShowMessage(recovered > 0 ? "Returned " + recovered + " loose key item(s) to their trays." :
                "No loose earned keys to recover. Held and docked items stay in place.");
        }

        public void RequestReset()
        {
            if (Time.unscaledTime <= _confirmUntil)
            {
                ResetRoom();
                ShowMessage("Captain's cabin reset. A new voyage begins.");
            }
            else
            {
                _confirmUntil = Time.unscaledTime + 6f;
                ShowMessage("RESET THIS CABIN? Press RESET VOYAGE (or R) again within 6 seconds. Esc cancels.");
            }
        }

        public void ResetRoom()
        {
            StopAllCoroutines();
            _openingChest = false;
            IsChestOpen = false;
            IsCleared = false;
            _confirmUntil = -1f;
            _currentMessage = string.Empty;
            _messageUntil = 0f;
            if (sound) sound.Stop();
            if (exitUnlockAudio) exitUnlockAudio.Stop();
            foreach (var socket in coinSockets) if (socket) socket.ResetSocket();
            if (exitSocket) exitSocket.ResetSocket();
            foreach (var puzzle in puzzles) if (puzzle) puzzle.ResetPuzzle();
            foreach (var gem in gems) if (gem) gem.ResetCollectible();
            for (int i = 0; i < looseProps.Length; i++)
                if (looseProps[i] && looseProps[i].ownerId == ownerId)
                {
                    var prop = CabinItem.Find(looseProps[i].itemId);
                    if (!prop) prop = looseProps[i];
                    prop.Restore(_propPositions[i], _propRotations[i]);
                    prop.SetAvailable(true);
                }
            if (chestLid) chestLid.localRotation = _chestClosed;
            if (exitLockVisual) exitLockVisual.localRotation = _lockClosed;
            for (int i = 0; i < coinSockets.Length; i++) SetLamp(i, 0f);
            var key = goldKey ? CabinItem.Find(goldKey.itemId) : null;
            if (!key) key = goldKey;
            if (key)
            {
                key.ReleaseHeld();
                key.Restore(goldKeySpawn ? goldKeySpawn.position : _goldPosition,
                    goldKeySpawn ? goldKeySpawn.rotation : _goldRotation);
                key.SetAvailable(false);
            }
            CloseClue();
            UpdateProgress();
            CompletionChanged?.Invoke(false);
            onCabinReset.Invoke();
        }

        public void ShowMessage(string message)
        {
            _currentMessage = message ?? string.Empty;
            if (statusText) statusText.text = message;
            _messageUntil = Time.unscaledTime + 7f;
        }

        public void UpdateProgress()
        {
            if (!progressText) return;
            int earned = IsChestOpen ? 1 : 0;
            int clues = 0;
            foreach (var puzzle in puzzles)
            {
                if (!puzzle) continue;
                if (puzzle.RewardReleased) earned++;
                if (puzzle.ClueDiscovered) clues++;
            }
            int keyTotal = puzzles.Length + 1;
            progressText.text = "CAPTAIN'S PROGRESS\n" +
                "Keys revealed: " + earned + " / " + keyTotal + "   |   Remaining: " + (keyTotal - earned) + "\n" +
                "Coin locks open: " + DepositedCoins + " / 3   |   Remaining: " + (3 - DepositedCoins) + "\n" +
                "Golden key: " + (IsChestOpen ? "REVEALED" : "IN CHEST") + "\n" +
                "Exit lock: " + (IsCleared ? "OPEN" : "LOCKED") + "\n" +
                "Clues read: " + clues + " / " + puzzles.Length + "   |   Remaining: " + (puzzles.Length - clues) + "\n" +
                "Optional rubies: " + CollectedGems + " / " + gems.Length;
        }
    }
}
