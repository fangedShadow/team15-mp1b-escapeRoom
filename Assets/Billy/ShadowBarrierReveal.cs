using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ShadowBarrierReveal : MonoBehaviour
{
    public GameObject shadowBarrier;

    public float dissolveDuration = 1.5f;
    public float riseDistance = 0.5f;

    private bool revealed = false;
    public bool IsRevealed { get; private set; }
    private readonly List<Material> fadeMaterials = new List<Material>();
    private readonly List<Color> originalColors = new List<Color>();

    private void OnTriggerEnter(Collider other)
    {
        TryReveal(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryReveal(other);
    }

    private void TryReveal(Collider other)
    {
        if (revealed || shadowBarrier == null)
            return;

        LanternLock lantern =
            other.GetComponentInParent<LanternLock>();

        // The grab Rigidbody belongs to LanternPivot; the ignition lock is a child.
        if (lantern == null && other.attachedRigidbody != null)
            lantern = other.attachedRigidbody.GetComponentInChildren<LanternLock>();

        if (lantern == null)
            return;

        if (!lantern.IsLit)
            return;

        revealed = true;

        StartCoroutine(RemoveBarrier());
    }

    private IEnumerator RemoveBarrier()
    {
        if (shadowBarrier == null)
            yield break;

        Transform barrierTransform = shadowBarrier.transform;

        Vector3 startScale = barrierTransform.localScale;
        Vector3 startPosition = barrierTransform.localPosition;

        Vector3 targetScale =
            new Vector3(
                startScale.x * 0.85f,
                startScale.y * 0.05f,
                startScale.z * 0.85f
            );

        Vector3 targetPosition =
            startPosition + Vector3.up * riseDistance;

        Renderer[] renderers =
            shadowBarrier.GetComponentsInChildren<Renderer>();

        foreach (Renderer barrierRenderer in renderers)
        {
            foreach (Material material in barrierRenderer.materials)
            {
                fadeMaterials.Add(material);
                originalColors.Add(material.color);
            }
        }

        float elapsed = 0f;

        while (elapsed < dissolveDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / dissolveDuration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            barrierTransform.localScale =
                Vector3.Lerp(startScale, targetScale, smoothT);

            barrierTransform.localPosition =
                Vector3.Lerp(startPosition, targetPosition, smoothT);

            float alpha = Mathf.Lerp(1f, 0f, smoothT);

            for (int i = 0; i < fadeMaterials.Count; i++)
            {
                Color color = originalColors[i];
                color.a *= alpha;
                fadeMaterials[i].color = color;
            }

            yield return null;
        }

        IsRevealed = true;
        shadowBarrier.SetActive(false);
    }

    private void OnDestroy()
    {
        foreach (Material material in fadeMaterials)
            if (material != null)
                Destroy(material);
    }
}
