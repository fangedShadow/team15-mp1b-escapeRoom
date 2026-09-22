using UnityEngine;

public class LanternSway : MonoBehaviour
{
    public float swayAngleX = 2f;
    public float swayAngleZ = 3f;

    public float swaySpeedX = 0.8f;
    public float swaySpeedZ = 1.1f;

    private float timer;

    void Update()
    {
        timer += Time.deltaTime;

        float x = Mathf.Sin(timer * swaySpeedX) * swayAngleX;
        float z = Mathf.Sin(timer * swaySpeedZ + 1.7f) * swayAngleZ;

        transform.localRotation = Quaternion.Euler(x, 0f, z);
    }
}