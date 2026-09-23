using UnityEngine;

namespace BlackTide
{
    // The editor replaces this bootstrap with an editable, saved scene on first import.
    // This fallback also makes the included scene work if automatic authoring is skipped.
    public sealed class PirateBootstrap : MonoBehaviour
    {
        void Awake()
        {
            if (FindFirstObjectByType<PirateGame>() == null) PirateSceneFactory.Build();
            Destroy(gameObject);
        }
    }
}
