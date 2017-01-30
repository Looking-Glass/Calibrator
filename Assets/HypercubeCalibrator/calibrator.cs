using UnityEngine;
using System.Collections;
using System.IO;

//a base class for different kinds of things that can modify the hypercube.

namespace hypercube
{
	public class calibrator : MonoBehaviour 
	{
		public castMesh canvas;

		public virtual void OnEnable()
		{
			canvas.currentCalibrator = this;
			canvas.updateMesh ();

		}

		public virtual Material[] getMaterials()
		{
			return null;
		}
              
	}
}
