using UnityEngine;
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

        public int baudRate = 57600;
        public int reconnectionDelay = 500;
        public int maxUnreadMessage = 5;
        public int maxAllowedFailure = 3;
        public bool debug = false;

        public static touchScreenInputManager touchPanel { get; private set;}  
        serialPortFinder[] portSearches; //we wait for a handshake to know which serial port is which.

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

#if HYPERCUBE_DEV //allow access to calibrator
        public 
#endif
        bool searchForSerialComs()
        {
            if (portSearches != null && portSearches.Length > 0) //we are still searching.
                return false;

            serialComSearchTime = 0f;
            string[] names = getPortNames();

            if (names.Length == 0)
                return false;

            portSearches = new serialPortFinder[names.Length];
            for (int i = 0; i < portSearches.Length; i++)
            {
                portSearches[i] = new serialPortFinder();
                portSearches[i].identifyPort(createInputSerialPort(names[i])); //add a component that manages every port, and set off to identify what it is.
            }

            serialComSearchTime = 0f;
            return true;
        }


        void Update()
        {
            if (touchPanel == null)
            {
                if (portSearches != null && portSearches.Length > 0)
                    findSearialComUpdate(Time.deltaTime);
                return;
            }
            else if (touchPanel.serial.enabled)
                touchPanel.update(debug);
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
                    GameObject.Destroy(portSearches[i].getSerialInput());
                    portSearches[i] = null;
                }
                else if (t == serialPortType.SERIAL_TOUCHPANEL)
                {
                    touchPanel = new touchScreenInputManager(portSearches[i].getSerialInput(), portSearches[i].firmwareVersion); //we found the touch panel. Hand off the serialInput component to it's proper, custom handler
                    portSearches[i] = null; //stop checking this port for relevance.
                    touchPanel.serial.readDataAsString = true;
                    touchPanel.serial.SendSerialMessage("read0"); //send for the config asap. 
                    if (debug)
                        Debug.Log("Connected to and identified touch panel PCB hardware.");
                    endPortSearch(); //this version of the tools only knows how to use touchpanel serial port. we are done.
                }
                else if (t == serialPortType.SERIAL_WORKING)
                {
                    if (serialComSearchTime > 1f && !portSearches[i].getSerialInput().isConnected) //timeout
                    {
                        Destroy(portSearches[i].getSerialInput());
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
                    GameObject.Destroy(portSearches[i].getSerialInput());
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
            sc.readDataAsString = true;
            return sc;
        }


        //public static bool isHardwareReady() //can the touchscreen hardware get/send commands?
        //{
        //    if (!instance)
        //        return false;

        //    if (touchPanel != null)
        //    {
        //        if (!touchPanel.serial.enabled)
        //            touchPanel.serial.readDataAsString = true; //we must wait for another init:done before we give the go-ahead to get raw data again.
        //        else if (touchPanel.serial.readDataAsString == false)
        //            return true;
        //    }

        //    return false;
        //}

        //static bool sendCommandToHardware(string cmd)
        //{
        //    if (isHardwareReady())
        //    {
        //        touchPanel.serial.SendSerialMessage(cmd + "\n\r");
        //        return true;
        //    }
        //    else
        //        Debug.LogWarning("Can't send message to hardware, it is either not yet initialized, disconnected, or malfunctioning.");

        //    return false;
        //}

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
