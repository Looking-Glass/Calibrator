﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//manages the saving of settings to the USB and/or PCB

namespace hypercube
{
    public class settingsFileIO : MonoBehaviour {

        public castMesh canvas;
        public vertexCalibrator vc;
        public basicSettingsAdjustor basic;

        void Update()
        {
            if (Time.timeSinceLevelLoad < 5f || basic.isTextInFocus()) //allow the pcb plenty of time to load before allowing saves.
                return;

            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                if (Input.GetKeyDown(KeyCode.S)) 
                {
                    StartCoroutine(vc.saveSettings());
    
                }
            }
        }
    }
}
