using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Unity.XR.CoreUtils;
using System.Collections.Generic;

public class DarknessTimer : MonoBehaviour
{
    public float timeLimit = 60f;

    public TMP_Text warningText;
    public TMP_Text timerText;
    public TMP_Text failText;
    public GameObject restartButton;

    private float timeRemaining;
    private bool timerRunning = true;
    private bool hasFailed = false;
    private readonly List<Light> failedRoomLights = new List<Light>();

    public bool HasFailed => hasFailed;

    private void Start()
    {
        timeRemaining = Mathf.Max(0f, timeLimit);

        if (restartButton != null)
            restartButton.SetActive(false);

        if (warningText != null)
            warningText.text = "Find the light before the darkness reaches you.";

        if (failText != null)
            failText.gameObject.SetActive(false);

        UpdateTimerText();
    }

    private void Update()
    {
        if (!timerRunning || hasFailed)
            return;

        timeRemaining -= Time.deltaTime;

        if (timeRemaining <= 0f)
        {
            timeRemaining = 0f;
            UpdateTimerText();
            Fail();
            return;
        }

        UpdateTimerText();
    }

    private void UpdateTimerText()
    {
        if (timerText == null)
            return;

        int seconds = Mathf.CeilToInt(timeRemaining);

        timerText.text =
            (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00");
    }

    public void StopTimer()
    {
        if (hasFailed)
            return;

        timerRunning = false;

        if (warningText != null)
            warningText.gameObject.SetActive(false);

        if (timerText != null)
            timerText.gameObject.SetActive(false);

        Debug.Log("Darkness timer stopped");
    }

    private void Fail()
    {
        hasFailed = true;
        timerRunning = false;

        if (restartButton != null)
            restartButton.SetActive(true);

        if (warningText != null)
            warningText.gameObject.SetActive(false);

        if (timerText != null)
            timerText.gameObject.SetActive(false);

        if (failText != null)
            failText.gameObject.SetActive(true);

        ShowDarknessAndRestart();

        Debug.Log("Player lost to darkness");
    }

    private void ShowDarknessAndRestart()
    {
        Camera view = Camera.main;
        Canvas panel = restartButton != null
            ? restartButton.GetComponentInParent<Canvas>(true)
            : null;
        XROrigin rig = view != null ? view.GetComponentInParent<XROrigin>() : null;

        // Baked lighting and emissive materials can remain visible with lights off.
        // Hide room visuals only; keep the XR rig, UI and interaction objects alive.
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
        {
            foreach (Light roomLight in root.GetComponentsInChildren<Light>(true))
            {
                roomLight.enabled = false;
                failedRoomLights.Add(roomLight);
            }

            foreach (Renderer roomRenderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (rig != null && roomRenderer.transform.IsChildOf(rig.transform))
                    continue;
                if (panel != null && roomRenderer.transform.IsChildOf(panel.transform))
                    continue;
                roomRenderer.forceRenderingOff = true;
            }
        }

        if (view != null)
        {
            view.clearFlags = CameraClearFlags.SolidColor;
            view.backgroundColor = Color.black;
        }

        if (panel != null && view != null)
        {
            Vector3 forward = Vector3.ProjectOnPlane(view.transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.001f)
                forward = Vector3.ProjectOnPlane(view.transform.up, Vector3.up);
            forward.Normalize();

            // Position once on failure: remain world-space for comfortable VR pointing.
            panel.transform.SetParent(null, true);
            panel.renderMode = RenderMode.WorldSpace;
            panel.worldCamera = view;
            panel.transform.SetPositionAndRotation(
                view.transform.position + forward * 2f - Vector3.up * 0.15f,
                Quaternion.LookRotation(forward, Vector3.up));
            panel.transform.localScale = Vector3.one * 0.015f;
        }

        if (restartButton != null)
        {
            Button button = restartButton.GetComponent<Button>();
            if (button != null)
            {
                ColorBlock colors = button.colors;
                colors.normalColor = new Color(1f, 0.8f, 0.3f);
                colors.highlightedColor = new Color(1f, 0.95f, 0.7f);
                colors.selectedColor = colors.highlightedColor;
                button.colors = colors;
            }
        }
    }

    private void LateUpdate()
    {
        // A match or light animation must not relight the room after time runs out.
        if (!hasFailed)
            return;
        foreach (Light roomLight in failedRoomLights)
            if (roomLight != null)
                roomLight.enabled = false;
    }

    public void RestartRoom()
    {
        // Reload this room, even when another scene is active in an additive setup.
        Scene room = gameObject.scene;
#if UNITY_EDITOR
        if (room.buildIndex < 0)
        {
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                room.path, new LoadSceneParameters(LoadSceneMode.Single));
            return;
        }
#endif
        SceneManager.LoadScene(room.path);
    }
}
