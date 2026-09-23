using System.Collections;
using TMPro;
using UnityEngine;

namespace BlackTide.MP1B
{
    public enum CabinPuzzleKind { Bell, Log, Seal }

    [DisallowMultipleComponent]
    public sealed class CabinPuzzle : MonoBehaviour
    {
        public CabinPuzzleKind kind;
        public CabinRoom room;
        public string puzzleName;
        public CabinButton[] buttons = new CabinButton[0];
        public int[] solution = new[] { 0, 2, 1 };
        public CabinItem reward;
        public Transform rewardSpawn;
        public Transform revealTransform;
        public Vector3 revealOpenOffset;
        public Vector3 revealOpenEuler;
        public float revealDuration = 0.7f;
        public AudioSource sound;
        public AudioClip[] bellTones = new AudioClip[0];
        public float[] bellPitches = new[] { 0.8f, 1f, 1.25f };
        public AudioClip successSound;
        public AudioClip errorSound;
        public string clueTitle;
        public TMP_Text feedbackText;
        [TextArea(3, 10)] public string clueBody;
        public bool IsSolved { get; private set; }
        public bool RewardReleased { get; private set; }
        public bool ClueDiscovered { get; private set; }
        public bool IsReplaying { get; private set; }
        public int InputCount => _inputCount;
        public int SelectedMask => _selectedMask;
        public bool IsArmed => _armed;

        int _inputCount;
        int _selectedMask;
        bool _armed;
        bool _initialized;
        Vector3 _revealClosedPosition;
        Quaternion _revealClosedRotation;
        Vector3 _rewardPosition;
        Quaternion _rewardRotation;
        Coroutine _replay;
        AudioSource[] _bellVoices;
        static readonly Color WrongFeedback = new Color(1f, 0.12f, 0.08f);

        void Awake() => Initialize();

        void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            if (revealTransform)
            {
                _revealClosedPosition = revealTransform.localPosition;
                _revealClosedRotation = revealTransform.localRotation;
            }
            if (reward)
            {
                _rewardPosition = rewardSpawn ? rewardSpawn.position : reward.transform.position;
                _rewardRotation = rewardSpawn ? rewardSpawn.rotation : reward.transform.rotation;
            }
        }

        void OnEnable()
        {
            Initialize();
            if (IsSolved && !RewardReleased) ReleaseReward();
            if (IsSolved) SetReveal(1f);
            ClearBellFeedback();
            UpdateSelection();
        }

        void OnDisable()
        {
            _replay = null;
            IsReplaying = false;
        }

        public void Input(int index)
        {
            if (!isActiveAndEnabled || IsSolved || IsReplaying || index < 0 || index >= buttons.Length) return;
            if (kind == CabinPuzzleKind.Bell) Ring(index);
            if (kind == CabinPuzzleKind.Seal) { SealInput(index); return; }
            var sequence = kind == CabinPuzzleKind.Log ? LogSequence : solution;
            if (sequence == null || sequence.Length == 0) return;
            bool correct = index == sequence[_inputCount];
            if (kind == CabinPuzzleKind.Bell && buttons[index])
                buttons[index].Pulse(correct ? buttons[index].selectedColor : WrongFeedback);
            if (!correct) { Wrong(); return; }
            _inputCount++;
            if (_inputCount == sequence.Length) Solve();
            else Notify("Order accepted: " + _inputCount + " / " + sequence.Length);
        }

        static readonly int[] LogSequence = { 0, 1, 2 };

        void SealInput(int index)
        {
            if (index == 0)
            {
                if (!_armed)
                {
                    _armed = true;
                    _selectedMask = 0;
                    UpdateSelection();
                    Notify("Captain's mark accepted. Choose two symbols, then sign again.");
                }
                else if (_selectedMask == ((1 << 1) | (1 << 2))) Solve();
                else Wrong();
                return;
            }
            if (!_armed) { Notify("First, leave the captain's mark."); return; }
            if (index > 3) return;
            _selectedMask ^= 1 << index;
            UpdateSelection();
            Notify("Selection saved. Press a symbol again to remove it; sign to confirm.");
        }

        public void Replay()
        {
            if (kind != CabinPuzzleKind.Bell || !isActiveAndEnabled) return;
            DiscoverClue();
            _inputCount = 0;
            if (_replay != null) StopCoroutine(_replay);
            ClearBellFeedback();
            _replay = StartCoroutine(ReplaySignal());
        }

        IEnumerator ReplaySignal()
        {
            IsReplaying = true;
            Notify("Listen to the signal. Repeat the order; timing does not matter.");
            foreach (int index in solution)
            {
                Ring(index);
                yield return new WaitForSeconds(0.7f);
            }
            IsReplaying = false;
            _replay = null;
            Notify(IsSolved ? "Signal already decoded." : "Your turn. Repeat the three bells in order.");
        }

        void Ring(int index)
        {
            if (!sound || index < 0 || index >= bellTones.Length || !bellTones[index]) return;
            if (_bellVoices == null || _bellVoices.Length != bellTones.Length)
                _bellVoices = new AudioSource[bellTones.Length];
            if (!_bellVoices[index])
            {
                var voice = gameObject.AddComponent<AudioSource>();
                voice.playOnAwake = false;
                voice.outputAudioMixerGroup = sound.outputAudioMixerGroup;
                voice.spatialBlend = sound.spatialBlend;
                voice.rolloffMode = sound.rolloffMode;
                voice.minDistance = sound.minDistance;
                voice.maxDistance = sound.maxDistance;
                voice.volume = sound.volume;
                _bellVoices[index] = voice;
            }
            bool reusedClip = false;
            for (int i = 0; i < bellTones.Length; i++)
                if (i != index && bellTones[i] == bellTones[index]) reusedClip = true;
            _bellVoices[index].pitch = reusedClip && index < bellPitches.Length ? bellPitches[index] : 1f;
            _bellVoices[index].Stop();
            _bellVoices[index].PlayOneShot(bellTones[index]);
        }

        void Wrong()
        {
            _inputCount = 0;
            _selectedMask = 0;
            _armed = false;
            UpdateSelection();
            if (kind == CabinPuzzleKind.Log || kind == CabinPuzzleKind.Seal)
            {
                foreach (var button in buttons) if (button) button.Pulse(WrongFeedback);
                PlayFeedback(errorSound);
            }
            Notify("Try again. Your input has been cleared.");
        }

        void Solve()
        {
            if (IsSolved) return;
            IsSolved = true;
            UpdateSelection();
            PlayFeedback(successSound);
            Notify((string.IsNullOrEmpty(puzzleName) ? kind.ToString() : puzzleName) + " solved. Take your coin from the tray.");
            StartCoroutine(RevealReward());
        }

        void PlayFeedback(AudioClip clip)
        {
            if (!sound || !clip) return;
            sound.Stop();
            sound.clip = clip;
            sound.pitch = 1f;
            sound.Play();
        }

        IEnumerator RevealReward()
        {
            for (float t = 0f; t < revealDuration; t += Time.deltaTime)
            {
                SetReveal(Mathf.SmoothStep(0f, 1f, t / Mathf.Max(0.01f, revealDuration)));
                yield return null;
            }
            SetReveal(1f);
            ReleaseReward();
        }

        void ReleaseReward()
        {
            if (RewardReleased || !IsSolved) return;
            RewardReleased = true;
            var item = reward ? CabinItem.Find(reward.itemId) : null;
            if (!item) item = reward;
            if (item)
            {
                item.Restore(rewardSpawn ? rewardSpawn.position : _rewardPosition,
                    rewardSpawn ? rewardSpawn.rotation : _rewardRotation);
                item.SetAvailable(true);
            }
        }

        void SetReveal(float amount)
        {
            if (!revealTransform) return;
            revealTransform.localPosition = _revealClosedPosition + revealOpenOffset * amount;
            revealTransform.localRotation = Quaternion.Slerp(_revealClosedRotation,
                _revealClosedRotation * Quaternion.Euler(revealOpenEuler), amount);
        }

        void UpdateSelection()
        {
            // Bells use transient green/red feedback for the current note's correctness.
            // Clearing a wrong attempt must not cancel the button's feedback coroutine.
            if (kind == CabinPuzzleKind.Bell) return;
            for (int i = 0; i < buttons.Length; i++)
                if (buttons[i]) buttons[i].SetSelected(IsSolved || (kind == CabinPuzzleKind.Seal &&
                    (i == 0 ? _armed : (_selectedMask & (1 << i)) != 0)));
        }

        void ClearBellFeedback()
        {
            if (kind != CabinPuzzleKind.Bell) return;
            foreach (var button in buttons) if (button) button.SetSelected(false);
        }

        public void DiscoverClue() => ClueDiscovered = true;

        public void ResetPuzzle()
        {
            Initialize();
            StopAllCoroutines();
            _replay = null;
            IsReplaying = false;
            IsSolved = false;
            RewardReleased = false;
            ClueDiscovered = false;
            _inputCount = 0;
            _selectedMask = 0;
            _armed = false;
            if (sound) sound.Stop();
            if (_bellVoices != null)
                foreach (var voice in _bellVoices) if (voice) voice.Stop();
            if (feedbackText) feedbackText.text = kind == CabinPuzzleKind.Bell ?
                "Replay the signal, then ring the bells in order." : "Read the clue, then enter your answer.";
            SetReveal(0f);
            ClearBellFeedback();
            UpdateSelection();
            var item = reward ? CabinItem.Find(reward.itemId) : null;
            if (!item) item = reward;
            if (item)
            {
                item.ReleaseHeld();
                item.Restore(rewardSpawn ? rewardSpawn.position : _rewardPosition,
                    rewardSpawn ? rewardSpawn.rotation : _rewardRotation);
                item.SetAvailable(false);
            }
        }

        public bool RecoverReward()
        {
            if (!RewardReleased || !reward) return false;
            var item = CabinItem.Find(reward.itemId);
            if (!item || item.IsHeld || item.IsDocked) return false;
            item.Restore(rewardSpawn ? rewardSpawn.position : _rewardPosition,
                rewardSpawn ? rewardSpawn.rotation : _rewardRotation);
            item.SetAvailable(true);
            return true;
        }

        void Notify(string message)
        {
            if (feedbackText) feedbackText.text = message;
            var targetRoom = room ? room : CabinRoom.Instance;
            if (targetRoom) targetRoom.ShowMessage(message);
        }
    }
}
