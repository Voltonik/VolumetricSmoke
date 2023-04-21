using UnityEngine;

namespace MyBox
{
	public class ActiveStateOnStart : MonoBehaviour
	{
		public bool Active;
		 public GameObject Target;

		private void Awake()
		{
			Target.gameObject.SetActive(Active);
		}
	}
}