using UnityEngine;

namespace Team15.Deck
{
[ExecuteAlways]
public class HangingBreeze : MonoBehaviour
{
    [Header("Breeze")]
    [SerializeField] private float swayAmount = 8f;
    [SerializeField] private float swaySpeed = 1f;
    [SerializeField] private float secondarySway = 3f;

    [Header("Motion")]
    [SerializeField] private float damping = 0.15f;
    [SerializeField] private bool useWorldRotation = false;

    private float seed;

    private void OnEnable()
    {
        seed = Random.Range(0f, 100f);
    }

    private void Update()
    {
        // A couple of sine waves create less mechanical movement
        // than using a single sine wave.
        float time = Application.isPlaying
            ? Time.time
            : Time.realtimeSinceStartup;

        float primary = Mathf.Sin((time + seed) * swaySpeed);
        float secondary = Mathf.Sin((time + seed * 1.37f) * swaySpeed * 1.7f);

        float x = primary * swayAmount;
        float z = secondary * secondarySway;

        Quaternion breezeRotation = Quaternion.Euler(x, 0f, z);

        if (useWorldRotation)
        {
            transform.rotation = breezeRotation;
        }
        else
        {
            transform.localRotation = breezeRotation;
        }
    }
}
}
