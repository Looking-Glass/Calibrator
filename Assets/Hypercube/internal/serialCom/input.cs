﻿using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;


//the main Hypercube input class, use this to access all physical input from Volume
//note that Volume stores its calibration inside the touchscreen circuit board.  This calibration is read by this class and sent to the castMesh.

//regarding the i/o of calibration data into the touchscreen pcb, it is stored on the board in 3 areas:
//area 0 = the basic config data. stored in text form native to the dataFileDict class.  This is < 1k of data containg things such as projector resolution, touch screen resolution, does this hardware use an fpga, etc.
//area 1 = the unsullied slices.  < 1k of calibration data that conforms to an ideal perfect undistorted format if the projector could perfectly project onto slices without perspective or distortion of any kind.
//area 2 = the sullied slices. About 50k of data. This is a calibrated output of the slices in vertex positions.  These are typically cut up into 33 x 9 vertices of articulation per slice.

namespace hypercube
{
    public class input : MonoBehaviour
    {
        //singleton pattern
        private static input instance = null;
        public static input _get() { return instance; }
        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(this.gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(this.gameObject);
            //end singleton

            touchPanel = null;

            if (!searchForSerialComs())
                Debug.LogWarning("Can't get input from Volume because no ports were detected! Confirm that Volume is connected via USB.");
        }

#if HYPERCUBE_DEV
        public
#endif
        static bool forceStringRead = false; //can be used to force the string input manager to update instead of the regular streamed pcb input (used for calibration handshaking when writing to pcb)

        public int baudRate = 57600;
        public int reconnectionDelay = 500;
        public int maxUnreadMessage = 5;
        public int maxAllowedFailure = 3;
        public bool debug = false;

        public float touchPanelFirmwareVersion { get; private set; }
        public static touchScreenInputManager touchPanel { get; private set;}  
        serialPortFinder[] portSearches; //we wait for a handshake to know which serial port is which.

        protected stringInputManager touchPanelStringManager; //used to get data and settings from the touch panel

        //these keep track of all touchScreen targets, and hence the in input system can send them user input data as it is received.
        static HashSet<touchScreenTarget> eventTargets = new HashSet<touchScreenTarget>();
        public static void _setTouchScreenTarget(touchScreenTarget t, bool addRemove)
        {
            if (addRemove)
                eventTargets.Add(t);
            else
                eventTargets.Remove(t);
        }


        //use this instead of Start(),  that way we know we have our hardware settings info ready before we begin receiving data
        public static void init(dataFileDict d)
        {
            if (!d)
            {
                Debug.LogError("Input was passed bad hardware dataFileDict!");
                return;
            }

            if (!instance)
                return;

#if HYPERCUBE_INPUT

            if (touchPanel != null)
                touchPanel.setTouchScreenDims(d);
#endif
        }

#if HYPERCUBE_INPUT

        public static void _processTouchScreenEvent(touch t)
        {
            if (t == null)
            {
                Debug.LogWarning("Please report a bug in hypercube input. A null touch event was sent for processing.");
                return;
            }

            if (eventTargets.Count == 0)
                return;

            if (t.state == touch.activationState.TOUCHDOWN)
            {
                foreach (touchScreenTarget target in eventTargets)
                    target.onTouchDown(t);
            }
            else if (t.state == touch.activationState.ACTIVE)
            {
                foreach (touchScreenTarget target in eventTargets)
                    target.onTouchMoved(t);
            }
            else if (t.state == touch.activationState.TOUCHUP)
            {
                foreach (touchScreenTarget target in eventTargets)
                    target.onTouchUp(t);
            }
        }

        bool searchForSerialComs()
        {
            if (getIsStillSearchingForSerial()) //we are still searching.
                return false;

            serialComSearchTime = 0f;
            string[] names = getPortNames();

            if (names.Length == 0)
                return false;

            portSearches = new serialPortFinder[names.Length];
            for (int i = 0; i < portSearches.Length; i++)
            {
                portSearches[i] = new serialPortFinder();
                portSearches[i].debug = debug;
                portSearches[i].identifyPort(createInputSerialPort(names[i])); //add a component that manages every port, and set off to identify what it is.
            }

            serialComSearchTime = 0f;
            return true;
        }

        //are we still looking for serial comms and such?
        bool getIsStillSearchingForSerial()
        {
            if (portSearches == null || portSearches.Length == 0) 
                return false;

            for (int i = 0; i < portSearches.Length; i++)
            {
                if (portSearches[i] != null)
                    return true;
            }

            return false;
        }


        float connectTimer = 0f;
        void Update()
        {
            if (touchPanel != null && touchPanel.serial.enabled && !forceStringRead) //normal path
                touchPanel.update(debug);
            else if (touchPanelStringManager != null && touchPanelStringManager.serial.enabled) //we are still getting config and calibration from pcb (or are being forced to by forceStringRead)
            {
                updateGetSettingsFromPCB();
            }
            else //still searching for serial ports.
            {                   
                connectTimer += Time.deltaTime;
                if (getIsStillSearchingForSerial())
                    findSearialComUpdate(Time.deltaTime);
                else if (connectTimer > 1f)
                {
                    searchForSerialComs(); //try searching again.
                    connectTimer = 0f;
                }
                return;
            }

        }

        //handle PCB during period where we are just getting config data from it.
        void updateGetSettingsFromPCB()
        {
            touchPanelStringManager.update(debug);

            string data = touchPanelStringManager.readMessage();

            while (data != null && data != "")
            {
                if (data.StartsWith("data0::") && data.EndsWith("::done"))
                {
                    string[] toks = data.Split(new string[] { "::" }, System.StringSplitOptions.None);
                    input._get().GetComponent<castMesh>().setPCBbasicSettings(toks[1]); //store it in the castMesh... it will use it if needed, ignore it if it already has USB settings.
                    if (toks[1].Contains("useFPGA=True"))
                        touchPanelStringManager.serial.SendSerialMessage("read1"); //give us the perfect slices.  If it uses an FPGA
                    else
                        touchPanelStringManager.serial.SendSerialMessage("read2"); //ask for the calibrated slices.
                }
                else if (data.StartsWith("data") && data.EndsWith("::done"))
                {
                    string[] toks = data.Split(new string[] { "::" }, System.StringSplitOptions.None);
                    Vector2[,,] verts = null;
                    if (utils.bin2Vert(toks[1], out verts))
                    {
                        castMesh cm = input._get().GetComponent<castMesh>();
                        if (!cm.hasCalibration) //don't push it through if we already have usb calibration
                        {
                            cm._setCalibration(verts);
#if HYPERCUBE_DEV
                            if (cm.calibratorV) cm.calibratorV.setLoadedVertices(verts, false); //if we are calibrating, the calibrator needs to know about previous calibrations                    
                        }
                        else
                        {
                            cm.calibratorBasic.pcbText.text = "<color=#00ff00>PCB</color>";  //let the dev know the pcb has viable data.
#endif
                        }
                    }
                    else if (data != "data1::::done" && data.StartsWith("data1::") )
                        Debug.LogWarning("Received faulty 'perfect' vertex data from PCB");
                    else if (data != "data2::::done" && data.StartsWith("data2::"))
                        Debug.LogWarning("Received faulty 'calibrated' vertex data from PCB");   
                        
                    touchPanel = new touchScreenInputManager(touchPanelStringManager.serial); //we have what we want, now with this input will only get data from here
                }
#if HYPERCUBE_DEV
                else if (data.StartsWith("mode::recording::"))
                {
                    _recordingMode = true;
                }
                else if (data.StartsWith("recording::done"))
                {
                    _recordingMode = false;
                }
#endif
                data = touchPanelStringManager.readMessage();
            }

        }


        //we haven't found all of our ports, keep trying.
        private float serialComSearchTime = 0f;
        void findSearialComUpdate(float deltaTime)
        {
            serialComSearchTime += deltaTime;
            for (int i = 0; i < portSearches.Length; i++)
            {
                if (portSearches[i] == null)
                    continue;

                serialPortType t = portSearches[i].update(deltaTime);
                if (t == serialPortType.SERIAL_UNKNOWN) //a timeout or some other problem.  This is likely not a port related to us.
                {
                    GameObject.Destroy(portSearches[i].getSerialInput().serial);
                    portSearches[i] = null;
                }
                else if (t == serialPortType.SERIAL_TOUCHPANEL)
                {
                    touchPanelFirmwareVersion = portSearches[i].firmwareVersion;
                    touchPanelStringManager = portSearches[i].getSerialInput(); //we found the touch panel, get calibration and settings data off of it, and then pass it off to the touchScreenInput handler after done.

                    touchPanelStringManager.serial.SendSerialMessage("read0"); //send for the config asap. 
                    portSearches[i] = null; //stop checking this port for relevance.                   
                    if (debug)
                        Debug.Log("Connected to and identified touch panel PCB hardware.");

                    endPortSearch(); //this version of the tools only knows how to use touchpanel serial port. we are done.
                }
                else if (t == serialPortType.SERIAL_WORKING)
                {
                    if (serialComSearchTime > 1f && !portSearches[i].getSerialInput().serial.isConnected) //timeout
                    {
                        Destroy(portSearches[i].getSerialInput().serial);
                        portSearches[i] = null; //stop bothering with this guy.
                    }
                    //do nothing
                }
            }
        }


        void endPortSearch()
        {
            for (int i = 0; i < portSearches.Length; i++)
            {
                if (portSearches[i] != null)
                {
                    GameObject.Destroy(portSearches[i].getSerialInput().serial);
                    portSearches[i] = null;
                }
            }
        }

        static string[] getPortNames()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            return System.IO.Ports.SerialPort.GetPortNames();
#else
            //this code is from http://answers.unity3d.com/questions/643078/serialportsgetportnames-error.html
            int p = (int)Environment.OSVersion.Platform;
            List<string> serial_ports = new List<string>();

            // Are we on Unix?
            if (p == 4 || p == 128 || p == 6)
            {
                string[] ttys = System.IO.Directory.GetFiles("/dev/", "tty.*");  //In the GetPortNames function, it looks for ports that begin with "/dev/ttyS" or "/dev/ttyUSB" . However, OS X ports begin with "/dev/tty.".
                foreach (string dev in ttys)
                {
                    if (dev.StartsWith("/dev/tty."))
                        serial_ports.Add(dev);
                }
            }

            return serial_ports.ToArray();
#endif
        }



        SerialController createInputSerialPort(string comName)
        {
            SerialController sc = gameObject.AddComponent<SerialController>();
            sc.portName = comName;
            sc.baudRate = baudRate;
            sc.reconnectionDelay = reconnectionDelay;
            sc.maxUnreadMessages = maxUnreadMessage;
            sc.maxFailuresAllowed = maxAllowedFailure;
            sc.enabled = true;
            //sc.readDataAsString = true;
            return sc;
        }


        //code related to i/o of config and calibration data on the pcb
        #region IO to PCB


#if HYPERCUBE_DEV

        bool _recordingMode = false;
        float serialTimeoutIO = 5f;
        public enum pcbState
        {
            INVALID = 0,
            SUCCESS,
            FAIL,
            WORKING
        }
        public static pcbState pcbIoState = pcbState.INVALID;
        public IEnumerator _writeSettings(string settingsData)
        {
            float startTime = Time.timeSinceLevelLoad;
            if (touchPanelStringManager != null && touchPanelStringManager.serial.isConnected)
            {
                pcbIoState = pcbState.WORKING;
                _recordingMode = false;

                //prepare the pcb to accept our data
                touchPanelStringManager.serial.SendSerialMessage("write0"); //perfect slices

                while (!_recordingMode)
                {
                    if (_serialTimeOutCheck(startTime))
                        yield break;
                    yield return pcbIoState;
                }

                touchPanelStringManager.serial.SendSerialMessage(settingsData);

                while (_recordingMode)//don't exit until we are done.
                {
                    if (_serialTimeOutCheck(startTime))
                        yield break;
                    yield return pcbIoState;
                }
                pcbIoState = pcbState.SUCCESS;
                yield break;
            }
            pcbIoState = pcbState.FAIL;
        }

        public IEnumerator _writeSlices(Vector2[,,] d, bool sullied)
        {

            float startTime = Time.timeSinceLevelLoad;
            if (touchPanelStringManager != null && touchPanelStringManager.serial.isConnected)
            {
                pcbIoState = pcbState.WORKING;
                _recordingMode = false;

                //prepare the pcb to accept our data
                if (sullied)
                    touchPanelStringManager.serial.SendSerialMessage("write2");
                else
                    touchPanelStringManager.serial.SendSerialMessage("write1"); //perfect slices

                while (!_recordingMode)
                {
                    if (_serialTimeOutCheck(startTime))
                        yield break;
                    yield return pcbIoState;
                }

                string saveData;
                utils.vert2Bin(d, out saveData);
                touchPanelStringManager.serial.SendSerialMessage(saveData);

                while (_recordingMode)//don't exit until we are done.
                {
                    if (_serialTimeOutCheck(startTime))
                        yield break;
                    yield return pcbIoState;
                }
                pcbIoState = pcbState.SUCCESS;
                yield break;
            }
            pcbIoState = pcbState.FAIL;
        }

        public bool _serialTimeOutCheck(float startTime)
        {
            if (Time.timeSinceLevelLoad - startTime > serialTimeoutIO)
            {
                pcbIoState = pcbState.FAIL;
                _recordingMode = false;
                return true;
            }

            return false;
        }
#endif

        #endregion



#else //We use HYPERCUBE_INPUT because I have to choose between this odd warning below, or immediately throwing a compile error for new users who happen to have the wrong settings (IO.Ports is not included in .Net 2.0 Subset).  This solution is odd, but much better than immediately failing to compile.
    
        void searchForSerialComs()
        {
            printWarning();
        }

        public static bool isHardwareReady() //can the touchscreen hardware get/send commands?
        {
            return false;
        }
        public static void sendCommandToHardware(string cmd)
        {

        }
    
        void Start () 
        {
            printWarning();
            this.enabled = false;
        }

        static void printWarning()
        {
            Debug.LogWarning("TO USE HYPERCUBE INPUT: \n1) Go To - Edit > Project Settings > Player    2) Set Api Compatability Level to '.Net 2.0'    3) Add HYPERCUBE_INPUT to Scripting Define Symbols (separate by semicolon, if there are others)");
        }
#endif

    }


}
