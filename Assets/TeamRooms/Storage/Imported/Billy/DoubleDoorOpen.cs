using UnityEngine;
using System.Collections;

namespace Team15.Storage
{
public class DoubleDoorOpen : MonoBehaviour
{
    public Transform leftDoor;
    public Transform rightDoor;

    public float openAngle = 90f;
    public float openDuration = 1f;

    private bool isOpen = false;
    public bool IsOpen => isOpen;

    public void OpenDoor()
    {
        if (isOpen || leftDoor == null || rightDoor == null)
            return;

        isOpen = true;
        StartCoroutine(OpenRoutine());
    }

    private IEnumerator OpenRoutine()
    {
        Quaternion leftStart = leftDoor.localRotation;
        Quaternion rightStart = rightDoor.localRotation;

        Quaternion leftTarget =
            leftStart * Quaternion.Euler(0f, -openAngle, 0f);

        Quaternion rightTarget =
            rightStart * Quaternion.Euler(0f, openAngle, 0f);

        float elapsed = 0f;

        while (elapsed < openDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / openDuration);
            t = Mathf.SmoothStep(0f, 1f, t);

            leftDoor.localRotation =
                Quaternion.Slerp(leftStart, leftTarget, t);

            rightDoor.localRotation =
                Quaternion.Slerp(rightStart, rightTarget, t);

            yield return null;
        }

        leftDoor.localRotation = leftTarget;
        rightDoor.localRotation = rightTarget;
    }
}
}
