using UnityEngine;
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

    public float firmwareVersion { get; private set; }

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

    

#if HYPERCUBE_INPUT

    static readonly byte[] emptyByte = new byte[] { 0 };

    public touchScreenInputManager( SerialController _serial) : base(_serial, new byte[]{255,255}, 1024)
    {
        firmwareVersion = -9999f;
        touchScreens = new touchScreen[8]; //start with a blank array.  When we get info from the serial port we will know how many touch panels to enact.

        _serial.readDataAsString = true; //start with data
    }

    public void setTouchScreenDims(dataFileDict d)
    {
        if (d == null)
            return;

        if (!d.hasKey("touchScreenResX") ||
            !d.hasKey("touchScreenResY") ||
            !d.hasKey("projectionCentimeterWidth") ||
            !d.hasKey("projectionCentimeterHeight") ||
            !d.hasKey("projectionCentimeterDepth") ||
            !d.hasKey("touchScreenMapTop") ||
            !d.hasKey("touchScreenMapBottom") ||
            !d.hasKey("touchScreenMapLeft") ||
            !d.hasKey("touchScreenMapRight")  //this one is necessary to keep the hypercube aspect ratio
            )
            Debug.LogWarning("Volume config file lacks touch screen hardware specs!"); //these must be manually entered, so we should warn if they are missing.

        screenResX = d.getValueAsFloat("touchScreenResX", screenResX);
        screenResY = d.getValueAsFloat("touchScreenResY", screenResY);
        projectionWidth = d.getValueAsFloat("projectionCentimeterWidth", projectionWidth);
        projectionHeight = d.getValueAsFloat("projectionCentimeterHeight", projectionHeight);
        projectionDepth = d.getValueAsFloat("projectionCentimeterDepth", projectionDepth);

        topLimit = d.getValueAsFloat("touchScreenMapTop", topLimit); //use averages.
        bottomLimit = d.getValueAsFloat("touchScreenMapBottom", bottomLimit);
        leftLimit = d.getValueAsFloat("touchScreenMapLeft", leftLimit);
        rightLimit = d.getValueAsFloat("touchScreenMapRight", rightLimit);

        touchScreenWidth = projectionWidth * (1f/(rightLimit - leftLimit));
        touchScreenHeight = projectionHeight * (1f/(topLimit - bottomLimit));
    }



    public override void update(bool debug)
    {

        string data = serial.ReadSerialMessage();
        while (data != null)
        {
            if (debug)
                Debug.Log("touchScreenInputMgr: "+ data);

            if (serial.readDataAsString)
            {
                if (data.StartsWith("firmwareVersion::"))
                {
                    string[] toks = data.Split("::".ToCharArray());
                    firmwareVersion = dataFileDict.stringToFloat(toks[1], firmwareVersion);
                }

                if (data == "init::done" || data.Contains("init::done"))
                {
                    serial.readDataAsString = false; //start capturing data
                    Debug.Log("Touch Screen(s) is ready and initialized.");
                }

                return; //still initializing
            }
            

            addData(System.Text.Encoding.Unicode.GetBytes(data));
     
            data = serial.ReadSerialMessage();
        }

        postProcessData();

#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        //this will tell the chip to give us an init, even if it isn't mechanically resetting (just in case, for example on osx which does not mechanically reset the chip on connection)
        if (!hasInit && serial.isConnected) //will run the first time we have connection.
        {
            serial.SendSerialMessage("reping"); 
            hasInit = true;
        }
#endif
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
        //for (int i = 0; i < touchPoolSize; i++)
        //{
        //    interfaces[i].active = false;
        //}     


        rawTouchData d = new rawTouchData();

        for (int i = 2; i < dataChunk.Length; i = i + 5) //start at 1 and jump 5 each time.
        {
                d.id = dataChunk[i];
                d.o = (touchScreenOrientation) touchScreenId;
                d.iX = System.BitConverter.ToUInt16(dataChunk, i + 1);
                d.iY = System.BitConverter.ToUInt16(dataChunk, i + 3);
                d.x = (float)d.iX;
                d.y = (float)d.iY;


                //TODO   INJECT INTO THE TOUCHSCREEN'S  public void _interface(rawTouchData d)

            }

        }



#endif

     static float angleBetweenPoints(Vector2 v1, Vector2 v2)
    {      
        return Mathf.Atan2(v1.x - v2.x, v1.y - v2.y) * Mathf.Rad2Deg;
    }

     public static void mapToRange(float x, float y, float top, float right, float bottom, float left, out float outX, out float outY)
     {
         outX = map(x, left, right, 0f, 1.0f);
         outY = map(y, bottom, top, 0f, 1.0f);
     }
     static float map(float s, float a1, float a2, float b1, float b2)
     {
         return b1 + (s - a1) * (b2 - b1) / (a2 - a1);
     }

}



}
