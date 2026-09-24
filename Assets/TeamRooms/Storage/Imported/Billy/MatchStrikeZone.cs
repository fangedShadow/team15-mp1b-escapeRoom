using UnityEngine;

namespace Team15.Storage
{
public class MatchStrikeZone : MonoBehaviour
{
    public float minimumStrikeDistance = 0.08f;

    private MatchKey currentMatch;
    private Vector3 entryPosition;

    private void OnTriggerEnter(Collider other)
    {
        MatchKey match = other.GetComponentInParent<MatchKey>();

        if (match == null || match.isLit)
            return;

        currentMatch = match;
        entryPosition = match.transform.position;
    }

    private void OnTriggerStay(Collider other)
    {
        MatchKey match = other.GetComponentInParent<MatchKey>();

        if (match == null || match != currentMatch || match.isLit)
            return;

        float distanceMoved =
            Vector3.Distance(entryPosition, match.transform.position);

        if (distanceMoved >= minimumStrikeDistance)
        {
            match.LightMatch();
            currentMatch = null;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        MatchKey match = other.GetComponentInParent<MatchKey>();

        if (match == currentMatch)
            currentMatch = null;
    }
}
}
