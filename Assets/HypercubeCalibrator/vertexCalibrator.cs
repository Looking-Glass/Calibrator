using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace hypercube
{
	public class vertexCalibrator : calibrator
    {
       public float dotSize;

       public int articulationX;
       public int articulationY;
       public int slices;
   
        uint[][] xOptions;
        uint[][] yOptions;

        //Vector2[,,] vertices;

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


        public void resetVertexOffsets()
        {
        }

        public override void OnEnable()
        {
           
            dataFileDict d = canvas.GetComponent<dataFileDict>();
            articulationX = d.getValueAsInt("articulationX", 0);
            articulationY = d.getValueAsInt("articulationY", 0);

            if (articulationLookup(articulationX) == 0) //ensure that our articulations are only of the allowed, otherwise calibration will be too confusing
                articulationX = articulations[4];
            if (articulationLookup(articulationY) == 0)
                articulationY = articulations[3];


            //configure ourselves
            //the following lines will tell us what elements to highlight when we toggle between levels of detail
            setOptions(out xOptions, articulationX);
            setOptions(out yOptions, articulationY);

            updateMesh();

            base.OnEnable();
        }

        //this method returns an array containing what elements should be highlighted given each possible detail level
        static void setOptions(out uint[][] option, int maxOption)
        {
            int lookup = articulationLookup(maxOption);
            if (lookup == 0)
                Debug.LogError("Invalid maxOption provided when setting up the vertex calibrator, the choices must be one of the provided values.");

            List<uint[]> allOptions = new List<uint[]>();

            //start the first element, it holds every single element allowed at the given max
            uint[] current = new uint[maxOption];
            for (uint i = 0; i < maxOption; i++)
            {
                current[i] = i; //this array holds every single element
            }
            allOptions.Add(current);

            //now we will add lists that only contain alternating members from the previous list, down to 2.
            while(allOptions[allOptions.Count - 1].Length > 2)
            {
                bool odd = true;
                List<uint> newOption= new List<uint>();
                int count = allOptions[allOptions.Count - 1].Length; //length of the last option
                for (int i = 0; i < count; i++)
                {
                    if (odd)
                    {
                        newOption.Add(allOptions[allOptions.Count - 1][i]); //add the alternate elements                       
                    }
                    odd = !odd;
                }
                allOptions.Add(newOption.ToArray());
            }

            option = allOptions.ToArray();
        }

        void updateMesh()
        {

        }

        public override Material[] getMaterials()
        {
            return base.getMaterials();
        }
    }
}
