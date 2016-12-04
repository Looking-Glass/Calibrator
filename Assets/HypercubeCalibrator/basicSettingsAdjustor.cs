using UnityEngine;
using System.Collections;

namespace hypercube
{
    public class basicSettingsAdjustor : MonoBehaviour
    {

        public UnityEngine.UI.InputField modelName;
        public UnityEngine.UI.InputField versionNumber;

        public UnityEngine.UI.InputField resX;
        public UnityEngine.UI.InputField resY;
        public UnityEngine.UI.InputField slicesX;
        public UnityEngine.UI.InputField slicesY;
        public UnityEngine.UI.Dropdown articulationX;
        public UnityEngine.UI.Dropdown articulationY;

        public castMesh canvas;

        public UnityEngine.UI.Text vertCalculation;
        public UnityEngine.UI.Text sliceInfo;
        public UnityEngine.UI.Text sliceCount;

        void OnEnable()
        {
            if (!canvas)
                return;

            dataFileDict d = canvas.GetComponent<dataFileDict>();

            modelName.text = d.getValue("volumeModelName", "UNKNOWN!");
            versionNumber.text = d.getValue("volumeHardwareVersion", "-9999");

            resX.text = d.getValueAsInt("volumeResX", 1920).ToString();
            resY.text = d.getValueAsInt("volumeResY", 1080).ToString();

            slicesX.text = d.getValueAsInt("slicesX", 1).ToString();
            slicesY.text = d.getValueAsInt("slicesY", 10).ToString();

            //add the given options defined in vertexCalibrator.articulations
            System.Collections.Generic.List<UnityEngine.UI.Dropdown.OptionData> options = new System.Collections.Generic.List<UnityEngine.UI.Dropdown.OptionData>();
            foreach(int a in vertexCalibrator.articulations)
                options.Add(new UnityEngine.UI.Dropdown.OptionData(a.ToString()));
            articulationX.options = options;
            articulationY.options = options;

            articulationX.value = vertexCalibrator.articulationLookup(d.getValueAsInt("articulationX", vertexCalibrator.articulations[4])); //convert the number to the index in the array of possible choices
            articulationY.value = vertexCalibrator.articulationLookup(d.getValueAsInt("articulationY", vertexCalibrator.articulations[3]));

            updateDisplayInfo();
        }

        //this also validates the current values
        public void updateDisplayInfo()
        {
            //this will update the printouts to make sure we put in sane info

            sliceCount.text = "";

            calibrationStager s = GameObject.FindObjectOfType<calibrationStager>();
            s.allowNextStage = false;

            int resXint = dataFileDict.stringToInt(resX.text, 0);
            int resYint = dataFileDict.stringToInt(resY.text, 0);
            int slicesXint = dataFileDict.stringToInt(slicesX.text, 0);
            int slicesYint = dataFileDict.stringToInt(slicesY.text, 0);
            int xArticulation = vertexCalibrator.articulations[articulationX.value];
            int yArticulation = vertexCalibrator.articulations[articulationY.value];

            if (slicesXint < 1 || slicesYint < 1)
            {
                vertCalculation.text = "<color=#ff0000>Incoherent slice layout!</color>";
                return;
            }

            if (xArticulation < 2 || yArticulation < 2)
            {
                vertCalculation.text = "<color=#ff0000>Bad Articulation values!</color>";
                return;
            }

            if (resXint < 2 || resYint < 2)
            {
                vertCalculation.text = "<color=#ff0000>Bad Resolution!</color>";
                return;
            }

            sliceCount.text = " = " + (slicesXint * slicesYint).ToString() + " slices";
            sliceInfo.text = ((float)resXint / (float)slicesXint).ToString() + " x " + ((float)resYint / (float)slicesYint).ToString();

            int vertPrediction = xArticulation * yArticulation * slicesXint * slicesYint ;
            if (vertPrediction > 62000)
            {
                vertCalculation.text = "<color=#ff0000>" + vertPrediction.ToString() + " (TOO MANY!)</color>";
                return; //don't allow next stage switch
            }              
            else if (vertPrediction > 50000)
                vertCalculation.text = "<color=#ffff00>" + vertPrediction.ToString() + "</color>";
            else
                vertCalculation.text = vertPrediction.ToString();

            
            s.allowNextStage = true;
        }


        void OnDisable()
        {
            if (!canvas)
                return;

            dataFileDict d = canvas.GetComponent<dataFileDict>();

            d.setValue("volumeModelName", modelName.text);
            d.setValue("volumeHardwareVersion", dataFileDict.stringToFloat(versionNumber.text, -9999f));

            d.setValue("volumeResX", dataFileDict.stringToInt(resX.text, 1920));
            d.setValue("volumeResY", dataFileDict.stringToInt(resY.text, 1080));

            d.setValue("slicesX", dataFileDict.stringToInt(slicesX.text, 1));
            d.setValue("slicesY", dataFileDict.stringToInt(slicesY.text, 10));

            d.setValue("articulationX", vertexCalibrator.articulationLookup(articulationX.value));
            d.setValue("articulationY", vertexCalibrator.articulationLookup(articulationY.value));

            canvas.slices = dataFileDict.stringToInt(slicesX.text, 1) * dataFileDict.stringToInt(slicesY.text, 1);

            //set the res, if it is different.
            int resXpref =  d.getValueAsInt("volumeResX", 1920);
            int resYpref = d.getValueAsInt("volumeResY", 1080);
            bool forceFullScreen = true;
    #if UNITY_EDITOR
            forceFullScreen = false;
    #endif
            if (Screen.width != resXpref || Screen.height != resYpref)
                Screen.SetResolution(resXpref, resYpref, forceFullScreen);
        }
    }
}


