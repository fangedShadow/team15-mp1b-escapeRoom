using UnityEngine;

namespace Team15.Storage
{
public class KeyItem : MonoBehaviour
{
	public bool IsClaimed { get; private set; }

	public bool TryClaim()
	{
		if (IsClaimed)
			return false;

		IsClaimed = true;
		return true;
	}
}

}
