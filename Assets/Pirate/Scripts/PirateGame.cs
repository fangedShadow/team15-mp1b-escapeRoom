using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

namespace BlackTide
{
    public sealed class PirateGame : MonoBehaviour
    {
        public Light ceilingLight;
        public Renderer lightCrystal;
        public Material[] lightMaterials;
        public PiratePlayer player;
        public TMP_Text status;
        public Transform chestLid;
        public GameObject chestTreasure;
        public GameObject[] seals;
        public SignalProjectile signalPrefab;
        public ParticleSystem spawnParticles;
        public AudioSource spawnSound;
        public ParticleSystem treasureParticles;
        public AudioSource treasureSound;
        public GravityComet comet;
        public InputActionAsset inputs;
        public Color[] lightColors = { new Color(1f, .76f, .43f), new Color(.35f, .72f, 1f), new Color(1f, .22f, .36f) };
        readonly HashSet<int> collected = new HashSet<int>();
        readonly Queue<SignalProjectile> liveSignals = new Queue<SignalProjectile>();
        bool opened;
        int colorIndex;
        float quitHeld;
        float nextSpawn;
        Coroutine lidRoutine;

        void Start()
        {
            inputs.Enable();
            ShowMessage("CAPTAIN'S ORDERS\nFind 3 navigation seals, then open the treasure chest.\nPoint at brass markers and press a trigger.");
        }
        bool Pressed(string name) => inputs.FindAction("Gameplay/" + name, true).WasPressedThisFrame();
        void Update()
        {
            if (Pressed("Light")) CycleLight();
            if (Pressed("BreakOut")) BreakOut();
            if (Pressed("Spawn")) SpawnSignal();
            if (Pressed("Reset")) ResetVoyage();
            bool held = inputs.FindAction("Gameplay/Quit", true).IsPressed();
            quitHeld = held ? quitHeld + Time.unscaledDeltaTime : 0f;
            if (quitHeld > 1.2f) Quit();
        }

        public void Collect(int index, GameObject seal)
        {
            if (index < 0 || index >= 3 || !collected.Add(index)) return;
            // Feedback emitters are separate objects, so disabling the seal does not cut them off.
            seal.SetActive(false);
            ShowMessage(collected.Count == 3 ? "ALL THREE SEALS FOUND\nReturn to the captain's chest and press a trigger to unlock it."
                : "NAVIGATION SEAL " + collected.Count + " / 3\nSearch the chart table, barrels and ship's wheel.");
        }
        public void OpenChest()
        {
            if (opened) { ShowMessage("TREASURE RECOVERED\nPress R on keyboard or use the RESET VOYAGE marker to play again."); return; }
            if (collected.Count < 3) { ShowMessage("CHEST LOCKED\nFind " + (3 - collected.Count) + " more navigation seal(s)."); return; }
            opened = true;
            chestTreasure.SetActive(true);
            treasureParticles.Play();
            treasureSound.Play();
            if (lidRoutine != null) StopCoroutine(lidRoutine);
            lidRoutine = StartCoroutine(OpenLid());
            ShowMessage("VOYAGE COMPLETE\nThe Black Tide's treasure is yours.\nExplore the stars, fire a signal, or visit the lookout.");
        }
        IEnumerator OpenLid()
        {
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime;
                chestLid.localRotation = Quaternion.Euler(105f * Mathf.SmoothStep(0f, 1f, t), 0f, 0f);
                yield return null;
            }
            lidRoutine = null;
        }
        public void CycleLight()
        {
            colorIndex = (colorIndex + 1) % lightColors.Length;
            ceilingLight.color = lightColors[colorIndex];
            if (lightCrystal && lightMaterials.Length > colorIndex) lightCrystal.sharedMaterial = lightMaterials[colorIndex];
            ShowMessage("LANTERN COLOR " + (colorIndex + 1) + " / 3\nWarm amber / ocean blue / crimson red");
        }
        public void BreakOut()
        {
            player.ToggleLookout();
            ShowMessage(player.IsOutside ? "EXTERIOR LOOKOUT\nLook back at the ship and up at the surrounding sky.\nPress B / T to return to the cabin."
                : "BACK ABOARD THE BLACK TIDE\nContinue your search for the captain's treasure.");
        }
        public void SpawnSignal()
        {
            if (!player || !signalPrefab || Time.time < nextSpawn) return;
            nextSpawn = Time.time + .25f;
            Transform muzzle = player.AimTransform;
            Vector3 at = muzzle.position + muzzle.forward * .35f;
            Launch(at, muzzle.forward * 7f);
        }
        public void FireCannon(Transform cannon)
        {
            Launch(cannon.position + cannon.forward * 1.6f + Vector3.up * .15f, cannon.forward * 10f);
            ShowMessage("SIGNAL FIRED\nThe flare moves along the aiming direction with delta-time integration.");
        }
        void Launch(Vector3 at, Vector3 velocity)
        {
            while (liveSignals.Count > 0 && !liveSignals.Peek()) liveSignals.Dequeue();
            if (liveSignals.Count >= 20)
            {
                var oldest = liveSignals.Dequeue();
                if (oldest) Destroy(oldest.gameObject);
            }
            var ball = Instantiate(signalPrefab, at, Quaternion.identity);
            ball.velocity = velocity;
            ball.impact = spawnParticles;
            ball.impactSound = spawnSound;
            ball.gameObject.SetActive(true);
            liveSignals.Enqueue(ball);
            spawnParticles.transform.position = at;
            spawnParticles.Play();
            spawnSound.transform.position = at;
            spawnSound.Play();
        }
        public void ResetVoyage()
        {
            if (lidRoutine != null) StopCoroutine(lidRoutine);
            lidRoutine = null;
            collected.Clear();
            opened = false;
            chestLid.localRotation = Quaternion.identity;
            chestTreasure.SetActive(false);
            treasureParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            foreach (var seal in seals) { seal.SetActive(true); seal.GetComponent<PirateInteractable>().Highlight(false); }
            foreach (var signal in liveSignals) if (signal) Destroy(signal.gameObject);
            liveSignals.Clear();
            colorIndex = 0;
            ceilingLight.color = lightColors[0];
            if (lightCrystal) lightCrystal.sharedMaterial = lightMaterials[0];
            comet.ResetOrbit();
            player.ReturnToCabin();
            ShowMessage("NEW VOYAGE\nFind the three navigation seals and open the captain's chest.");
        }
        public void ShowMessage(string message) { if (status) status.text = message; }
        void Quit()
        {
            quitHeld = 0f;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#elif UNITY_WEBGL
            ShowMessage("VOYAGE ENDED\nThe browser controls this tab. Close the tab to exit.");
#else
            Application.Quit();
#endif
        }
    }
}
