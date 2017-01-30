﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace hypercube
{

    public enum touchScreenOrientation
    {
        INVALID_TOUCHSCREEN = -1,

        //if a device has multiple touch screens, this is the way to distinguish between them, and give appropriate world cords
        FRONT_TOUCHSCREEN = 0,
        BACK_TOUCHSCREEN,
        LEFT_TOUCHSCREEN, //relative to the device itself
        RIGHT_TOUCHSCREEN,
        TOP_TOUCHSCREEN,
        BOTTOM_TOUCHSCREEN,

        OTHER_TOUCHSCREEN_1, //possible non-space related screens
        OTHER_TOUCHSCREEN_2
    }


    public class rawTouchData
    {
        public int id = 0;
        public touchScreenOrientation o;
        public float x = 0;
        public float y = 0;
        public uint iX = 0;
        public uint iY = 0;
    }


public class touchScreenInputManager  : streamedInputManager
{

    public readonly float firmwareVersion;

    float projectionWidth = 20f; //the physical size of the projection, in centimeters
    float projectionHeight = 12f;
    float projectionDepth = 20f;

    touchScreen[] touchScreens = null;
    public touchScreen getPanel(touchScreenOrientation p)
    {
        int intP = (int)p;
        if (intP < 0 || intP >= touchScreens.Length)
            return null;

        return touchScreens[intP];
    }
    public touchScreen front { get {return touchScreens[0]; } } //quick access
    public touchScreen back { get {return touchScreens[1]; } }

    public touchScreen this[int screenIndex] //index access to a particular touch screen
    {
        get
        {
            if (screenIndex < 0 || screenIndex >= touchScreens.Length)
                return null;
            return touchScreens[screenIndex];
        }
    }
    public touchScreen this[touchScreenOrientation o] //index access to a particular touch screen
    {
        get
        {
            return getPanel(o);
        }
    }

    //easy accessors to the data of screen 0, which will be what is used 95% of the time.
    public touch[] touches { get {return touchScreens[0].touches;}}
    public uint touchCount { get {return touchScreens[0].touchCount;}}
    public Vector2 averagePos { get {return touchScreens[0].averagePos;}} //The 0-1 normalized average position of all touches on touch screen 0
    public Vector2 averageDiff { get {return touchScreens[0].averageDiff;}} //The normalized distance the touch moved 0-1 on touch screen 0
    public Vector2 averageDist { get {return touchScreens[0].averageDist;}} //The distance the touch moved, in Centimeters  on touch screen 0
    public float twist { get {return touchScreens[0].twist;}}
    public float pinch { get {return touchScreens[0].pinch;}}//0-1
    public float touchSize { get {return touchScreens[0].touchSize;}} //0-1
    public float touchSizeCm { get {return touchScreens[0].touchSizeCm;}}


#if HYPERCUBE_INPUT

    static readonly byte[] emptyByte = new byte[] { 0 };

    //constructor
    public touchScreenInputManager( SerialController _serial, float _firmwareVersion) : base(_serial, new byte[]{255,255}, 1024)
    {
        firmwareVersion = _firmwareVersion;

        touchScreens = new touchScreen[8]; 
        for (int t = 0; t < touchScreens.Length; t++)
        {
            touchScreens[t] = new touchScreen((touchScreenOrientation)t);
        }
        
        //note do not set here whether to read string or data from the serial.  Whoever just gave use the serial will know what is best.
    }

    public void setTouchScreenDims(dataFileDict d)
    {
        if (d == null)
            return;

        if (!d.hasKey("projectionCentimeterWidth") ||
            !d.hasKey("projectionCentimeterHeight") ||
            !d.hasKey("projectionCentimeterDepth") 
            )
            Debug.LogWarning("Volume config file lacks touch screen hardware specs!"); //these must be manually entered, so we should warn if they are missing.


        projectionWidth = d.getValueAsFloat("projectionCentimeterWidth", projectionWidth);
        projectionHeight = d.getValueAsFloat("projectionCentimeterHeight", projectionHeight);
        projectionDepth = d.getValueAsFloat("projectionCentimeterDepth", projectionDepth);

        touchScreens[0]._init(0, d, projectionWidth, projectionHeight); //front
        touchScreens[1]._init(1, d, projectionWidth, projectionHeight); //back
        touchScreens[2]._init(2, d, projectionDepth, projectionHeight); //left
        touchScreens[3]._init(3, d, projectionDepth, projectionHeight); //right
        touchScreens[4]._init(4, d, projectionWidth, projectionDepth); //top
        touchScreens[5]._init(5, d, projectionWidth, projectionDepth); //bottom
        touchScreens[6]._init(6, d, 0f, 0f);
        touchScreens[7]._init(7, d, 0f, 0f);
    }



    public override void update(bool debug)
    {

        string data = serial.ReadSerialMessage();
        while (data != null)
        {
            if (debug)
                Debug.Log("touchScreenInputMgr: "+ data);

                //this is now handled by the port finder
                if (serial.readDataAsString)
                {
                    if (data.StartsWith("data0::") && data.EndsWith("::done"))
                    {
                        string[] toks = data.Split("::".ToCharArray());
                        serial.readDataAsString = false; //we got what we want now lets go back
                        basicConfigData = toks[1];
                    }
                    else if (data.StartsWith("data1::") && data.EndsWith("::done"))
                    {
                        string[] toks = data.Split("::".ToCharArray());
                        serial.readDataAsString = false; //we got what we want now lets go back
                        readSlices(toks[1]);
                        //in principle we could store here whether the data we have is sullied or perfect, but there are no known cases where the software will need to know both.
                    }
                    else if (data.StartsWith("data2::") && data.EndsWith("::done"))
                    {
                        string[] toks = data.Split("::".ToCharArray());
                        serial.readDataAsString = false; //we got what we want now lets go back
                        readSlices(toks[1]);
                    }
                    
                    return;
                }


                addData(System.Text.Encoding.Unicode.GetBytes(data));
     
            data = serial.ReadSerialMessage();
        }

        for (int t = 0; t < touchScreens.Length; t++)
        {
            if (touchScreens[t].active)
                touchScreens[t].postProcessData();
        }
                
   }


     protected override void processData(byte[] dataChunk)
    {
        /*  the expected data here is ..
         * 1 byte = total touches
         * 
         * 1 byte = touch id
         * 2 bytes = touch x
         * 2 bytes = touch y
         * 
         *  1 byte = touch id for next touch  (optional)
         *  ... etc
         *  
         * */

        if (dataChunk == emptyByte)
            return;
     
        uint touchScreenId = dataChunk[0]; //if a device has multiple screens, this is how we distinguish them. (this also tells us the orientation of the given screen)
        uint totalTouches = dataChunk[1];

        if (dataChunk.Length != (totalTouches * 5) + 2)  //unexpected chunk length! Assume it is corrupted, and dump it.
        return;

        //assume no one is touched.
        //this code assumes that touches for all touch screens are sent together during every frame (not a Unity frame, but a hardware frame).
        //as opposed to touch panel A sends something frame 1, then panel B sends it's own thing the next frame.  This will not work.
        for (int t = 0; t < touchScreens.Length; t++)
        {
            if (touchScreens[t].active)
                touchScreens[t].prepareNextFrame();
        }

        rawTouchData d = new rawTouchData();

        for (int i = 2; i < dataChunk.Length; i = i + 5) //start at 1 and jump 5 each time.
        {
            d.id = dataChunk[i];
            d.o = (touchScreenOrientation) touchScreenId;
            d.iX = System.BitConverter.ToUInt16(dataChunk, i + 1);
            d.iY = System.BitConverter.ToUInt16(dataChunk, i + 3);
            d.x = (float)d.iX;
            d.y = (float)d.iY;

            touchScreens[touchScreenId]._interface(d);
        }

    }

    //code related to i/o of config and calibration data on the pcb
    #region 

    //the basic settings stored on the PCB
    string basicConfigData = null;
    public bool _getConfigData(ref dataFileDict d)
    {
        if (basicConfigData == null)
            return false;

        if (d.loadFromString(basicConfigData))
            return true;

        return false;
    }


    Vector2[,,] sliceData = null;
    public bool _applyHardwareCalibration(castMesh c )
    {
            if (sliceData == null)
                return false;

            c.setCalibration(sliceData);
            return true;        
    }

       


#if HYPERCUBE_DEV

     public static byte[] _convertCalibrationToData(Vector2[,,] d)
    {
        List<byte> outData = new List<byte>();

        outData.AddRange(System.BitConverter.GetBytes(d.GetLength(0))); //header
        outData.AddRange(System.BitConverter.GetBytes(d.GetLength(1)));
        outData.AddRange(System.BitConverter.GetBytes(d.GetLength(2)));

        for (int s = 0; s < d.GetLength(0); s++)  //data
        {
            for (int y = 0; y < d.GetLength(1); y++)
            {
                for (int x = 0; x < d.GetLength(2); x++)
                {
                    outData.AddRange(System.BitConverter.GetBytes(d[s, x, y].x));
                    outData.AddRange(System.BitConverter.GetBytes(d[s, x, y].y));
                }
            }
        }
        return outData.ToArray();
    }

    public bool _writeSlices(Vector2[,,] d, bool sullied)
    {
        if (serial == null || !serial.isConnected)
            return false;

        //prepare the pcb to accept our data
        if (sullied)
            serial.SendSerialMessage("write2");
        else
            serial.SendSerialMessage("write1"); //perfect slices

         serial.SendSerialMessage(_convertCalibrationToData(d).ToString());

        return true;
    }
#endif
        #endregion


#endif



    }



}
