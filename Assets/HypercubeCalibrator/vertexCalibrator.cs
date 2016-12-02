using UnityEngine;
using System.Collections;

namespace hypercube
{
	public class vertexCalibrator : calibrator
    {
       public float dotSize;

       public int articulationX;
       public int articulationY;
       public int slices;

      //  Vector2[,,] vertices;

        public static int[] articulations = {3,5,9,17,33,65,129,257,513};
        public static int articulationLookup(int val)
        {
            for (int a = 0; a < articulations.Length; a++)
            {
                if (articulations[a] == val)
                    return a;
            }
            return 0; 
        }


        public override void OnEnable()
        {
           
            dataFileDict d = canvas.GetComponent<dataFileDict>();
            articulationX = d.getValueAsInt("articulationX", articulations[4]);
            articulationY = d.getValueAsInt("articulationY", articulations[3]);

            if (articulationLookup(articulationX) == 0) //ensure that our articulations are only of the allowed, otherwise calibration will be too confusing
                articulationX = articulations[4];
            if (articulationLookup(articulationY) == 0)
                articulationX = articulations[3];


            updateMesh();

            base.OnEnable();
        }

        void updateMesh()
        {

        }
	}
}
