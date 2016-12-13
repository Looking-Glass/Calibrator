using UnityEngine;
using System.Collections;
using UnityEditor;

namespace hypercube
{
	public class calibratorMenuOptions : MonoBehaviour 
	{
		#if HYPERCUBE_DEV
		//[MenuItem("Hypercube/Save Settings", false, 51)]
		//public static void saveCubeSettings()
		//{
		//hypercube.castMesh c = GameObject.FindObjectOfType<hypercube.castMesh>();
		//if (c)
		//    c.saveConfigSettings();
		//else
		//    Debug.LogWarning("No castMesh was found, and therefore no saving could occur.");
		//}

		//other menu stuff


		//[MenuItem("Hypercube/Copy current slice calibration", false, 300)]  //# is prio
		//public static void openCubeWindowPrefs()
		//{
		//	fineCalibrator c = GameObject.FindObjectOfType<fineCalibrator>();

		//if (c)
		//c.copyCurrentSliceCalibration();
		//else
		//Debug.LogWarning("No calibrator was found, and therefore no copying occurred.");
		//}


        //test the calibration data
        [MenuItem("Hypercube/Set Calibration from data", false, 300)]  //# is prio
        public static void openCubeWindowPrefs()
        {
            
            //detector x, detector y, pixel = value
            int[][][] dataX = new int[][][]
            {
                    new int[][]
                    {
                        new int [] {1,2,3}, //100
                        new int [] {1,2,3},
                        new int [] {1,2,3}
                    }, 
                    new int[][]
                    {
                        new int [] {1,2,3},
                        new int [] {1,2,3},
                        new int [] {1,2,3}
                    }
            };
            int[][][] dataY = new int[][][]
            {
            };

         

            Vector2[,,] calibration;
            hypercube.autoCalibrator.getCoordsFromData(10, dataX, dataY, out calibration);
            castMesh c = GameObject.FindObjectOfType<castMesh>();
            c.setCalibration(calibration);


        }


		#endif
	}
}
