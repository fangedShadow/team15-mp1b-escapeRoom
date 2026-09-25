using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

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

        Debug.Log("Player lost to darkness");
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
