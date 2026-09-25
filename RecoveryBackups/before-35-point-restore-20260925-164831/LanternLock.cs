using UnityEngine;
using System.Collections;

public class LanternLock : MonoBehaviour
{
    public GameObject flame;
    public Light lanternLight;
    public DarknessTimer darknessTimer;

    public float fadeDuration = 0.75f;
    public float targetIntensity = 2f;

    private bool isLit = false;

    public bool IsLit => isLit;

    private void Start()
    {
        if (flame != null)
            flame.SetActive(false);

        if (lanternLight != null)
        {
            lanternLight.enabled = true;
            lanternLight.intensity = 0f;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        TryLight(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryLight(other);
    }

    private void TryLight(Collider other)
    {
        if (darknessTimer != null && darknessTimer.HasFailed)
            return;

        MatchKey match = other.GetComponentInParent<MatchKey>();

        if (match != null && match.isLit && !isLit)
        {
            isLit = true;

            if (flame != null)
                flame.SetActive(true);

            if (darknessTimer != null)
                darknessTimer.StopTimer();

            StartCoroutine(FadeInLight());
        }
    }

    private IEnumerator FadeInLight()
    {
        if (lanternLight == null)
            yield break;

        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;

            float t = elapsed / fadeDuration;
            t = Mathf.SmoothStep(0f, 1f, t);

            lanternLight.intensity =
                Mathf.Lerp(0f, targetIntensity, t);

            yield return null;
        }

        lanternLight.intensity = targetIntensity;
    }
}
