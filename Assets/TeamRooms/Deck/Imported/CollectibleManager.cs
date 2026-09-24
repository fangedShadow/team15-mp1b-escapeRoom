using System.Collections.Generic;
using UnityEngine;

namespace Team15.Deck
{
    public class CollectibleManager : MonoBehaviour
    {
        public GameObject objectToAppear;
        private readonly HashSet<Collectible> collectedItems = new HashSet<Collectible>();
        private readonly HashSet<Collectible> requiredItems = new HashSet<Collectible>();
        private bool initialized;
        public int CollectedCount => collectedItems.Count;
        public int TotalCount => requiredItems.Count;
        public bool IsComplete => initialized && TotalCount > 0 && CollectedCount == TotalCount;

        private void Start() => Initialize();

        private void Initialize()
        {
            if (initialized) return;
            initialized = true;
            foreach (GameObject sceneRoot in gameObject.scene.GetRootGameObjects())
                foreach (Collectible collectible in sceneRoot.GetComponentsInChildren<Collectible>(true))
                    if (collectible.manager == this) requiredItems.Add(collectible);
            if (objectToAppear) objectToAppear.SetActive(false);
        }

        public bool CollectItem(Collectible item)
        {
            Initialize();
            if (!item || item.manager != this || !requiredItems.Contains(item) ||
                !collectedItems.Add(item)) return false;
            if (IsComplete && objectToAppear) objectToAppear.SetActive(true);
            return true;
        }
    }
}
