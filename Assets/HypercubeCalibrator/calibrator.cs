using UnityEngine;
using System.Collections;

namespace hypercube
{
	public class calibrator : MonoBehaviour 
	{
		public castMesh canvas;

		void OnEnable()
		{
			canvas.calibrator = this;
			canvas.updateMesh ();
		}

		public virtual Material[] getMaterials()
		{
			return null;
		}
	}
}
