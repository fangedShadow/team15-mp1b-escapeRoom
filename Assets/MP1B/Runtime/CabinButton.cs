using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace BlackTide.MP1B
{
    public enum CabinButtonOperation { PuzzleInput, Replay, ReadClue, CloseClue, Recover, Reset, Travel }

    [DisallowMultipleComponent]
    public sealed class CabinButton : MonoBehaviour
    {
        public string prompt = "Press";
        public CabinButtonOperation operation;
        public CabinRoom room;
        public CabinPuzzle puzzle;
        public int index;
        public CabinTransition travel;
        public Transform visual;
        public Renderer indicator;
        public Vector3 pressedOffset = new Vector3(0f, 0f, 0.015f);
        public Color selectedColor = new Color(0.2f, 0.85f, 0.45f);
        public AudioSource sound;
        public UnityEvent onPressed = new UnityEvent();

        XRSimpleInteractable _xr;
        Vector3 _rest;
        bool _initialized;
        bool _selected;
        float _lastPress = -10f;
        Coroutine _motion;
        MaterialPropertyBlock _block;
        Color _baseColor = Color.white;

        void Awake() => Initialize();

        void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            if (!visual) visual = transform;
            _rest = visual.localPosition;
            _block = new MaterialPropertyBlock();
            if (indicator && indicator.sharedMaterial)
            {
                var material = indicator.sharedMaterial;
                if (material.HasProperty("_BaseColor")) _baseColor = material.GetColor("_BaseColor");
                else if (material.HasProperty("_Color")) _baseColor = material.color;
            }
            _xr = GetComponent<XRSimpleInteractable>();
        }

        void OnEnable()
        {
            Initialize();
            if (_xr) _xr.selectEntered.AddListener(OnSelected);
            ApplyVisual(_selected ? 1f : 0f);
        }

        void OnDisable()
        {
            if (_xr) _xr.selectEntered.RemoveListener(OnSelected);
            _motion = null;
        }

        void OnSelected(SelectEnterEventArgs _) => Press();

        public void Press()
        {
            if (Team15.TeamSession.GameplayBlocked) return;
            if (!isActiveAndEnabled || Time.unscaledTime - _lastPress < 0.18f) return;
            bool bellInput = operation == CabinButtonOperation.PuzzleInput && puzzle && puzzle.kind == CabinPuzzleKind.Bell;
            if (bellInput && puzzle.IsReplaying) return;
            Initialize();
            _lastPress = Time.unscaledTime;
            if (sound) sound.Play();
            if (!bellInput || puzzle.IsSolved) Pulse();
            var targetRoom = room ? room : CabinRoom.Instance;
            switch (operation)
            {
                case CabinButtonOperation.PuzzleInput: if (puzzle) puzzle.Input(index); break;
                case CabinButtonOperation.Replay: if (puzzle) puzzle.Replay(); break;
                case CabinButtonOperation.ReadClue: if (puzzle && targetRoom) targetRoom.ReadClue(puzzle); break;
                case CabinButtonOperation.CloseClue: if (targetRoom) targetRoom.CloseClue(); break;
                case CabinButtonOperation.Recover: if (targetRoom) targetRoom.RecoverItems(); break;
                case CabinButtonOperation.Reset: if (targetRoom) targetRoom.RequestReset(); break;
                case CabinButtonOperation.Travel: if (travel) travel.Travel(); break;
            }
            onPressed.Invoke();
        }

        public void SetSelected(bool selected)
        {
            Initialize();
            _selected = selected;
            if (_motion != null) StopCoroutine(_motion);
            _motion = null;
            ApplyVisual(selected ? 1f : 0f);
        }

        public void Pulse() => Pulse(selectedColor);

        public void Pulse(Color feedbackColor)
        {
            Initialize();
            if (!isActiveAndEnabled) return;
            if (_motion != null) StopCoroutine(_motion);
            _motion = StartCoroutine(PressMotion(feedbackColor));
        }

        IEnumerator PressMotion(Color feedbackColor)
        {
            for (float t = 0f; t < 0.09f; t += Time.deltaTime)
            {
                ApplyVisual(Mathf.SmoothStep(0f, 1f, t / 0.09f), feedbackColor);
                yield return null;
            }
            for (float t = 0f; t < 0.16f; t += Time.deltaTime)
            {
                ApplyVisual(_selected ? 1f : 1f - Mathf.SmoothStep(0f, 1f, t / 0.16f), _selected ? selectedColor : feedbackColor);
                yield return null;
            }
            ApplyVisual(_selected ? 1f : 0f);
            _motion = null;
        }

        void ApplyVisual(float amount) => ApplyVisual(amount, selectedColor);

        void ApplyVisual(float amount, Color feedbackColor)
        {
            if (visual) visual.localPosition = _rest + pressedOffset * amount;
            if (!indicator) return;
            indicator.GetPropertyBlock(_block);
            Color color = Color.Lerp(_baseColor, feedbackColor, amount);
            _block.SetColor("_BaseColor", color);
            _block.SetColor("_Color", color);
            _block.SetColor("_EmissionColor", feedbackColor * amount * 0.6f);
            indicator.SetPropertyBlock(_block);
        }
    }
}
