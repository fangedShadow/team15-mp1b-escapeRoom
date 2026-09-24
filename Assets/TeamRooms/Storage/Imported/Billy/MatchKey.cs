using UnityEngine;

namespace Team15.Storage
{
public class MatchKey : MonoBehaviour
{
    public bool isLit = false;
    public GameObject matchFlame;
    public Light matchLight;

    void Start()
    {
        isLit = false;

        if (matchFlame != null)
            matchFlame.SetActive(false);

        if (matchLight != null)
            matchLight.enabled = false;
    }

    public void LightMatch()
    {
        isLit = true;

        if (matchFlame != null)
            matchFlame.SetActive(true);

        if (matchLight != null)
            matchLight.enabled = true;
    }
}
}
