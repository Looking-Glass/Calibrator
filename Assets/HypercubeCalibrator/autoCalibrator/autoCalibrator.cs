using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;


//this autocalibrator is a state machine
//since presumably we can have many different kinds of calibration types and layouts of slices, detecting them all with one piece
// of code is only sane if we have a different state that handles the different types of calibration.

//generic features are few, but include methods for detecting peaks in the light sensor data, and for displaying a gradient on screen

//a state is called a autoCalibratorModule

namespace hypercube
{

    public class sensorData
    {
        List<int> recordedData;
        List<float> pixel;
        List<float> timeRecorded;
        public sensorData()
        {
            recordedData = new List<int>();
            pixel = new List<float>();
            timeRecorded = new List<float>();
        }

        public void addDataPoint(int data, float pixelValue)
        {
            recordedData.Add(data);
            pixel.Add(pixelValue);
            timeRecorded.Add(Time.timeSinceLevelLoad);
        }

        public int size
        {
            get
            {
                return recordedData.Count;
            }
        }

        public int[] getAllData()
        {
            return recordedData.ToArray();
        }
        public float[] getAllPixelMeasurements()
        {
            return pixel.ToArray();
        }

        public int getData(int element)
        {
            if (element >= recordedData.Count)
                return 0;
            return recordedData[element];
        }
        public float getPixel(int element)
        {
            if (element >= pixel.Count)
                return -999f;
            return pixel[element];
        }
        public float getTime(int element)
        {
            if (element >= timeRecorded.Count)
                return 0f;
            return timeRecorded[element];
        }
    }

    public class autoCalibrator : MonoBehaviour
    {

        [Tooltip("How many sensors in the LED sensor array")]
        public int xArticulation = 3;
        public int yArticulation = 3;

        [Tooltip("These will hide and show based on what is being seen.")]
        public GameObject[] indicators;

        SerialController serial;

        //TODO these should be set from calibrator gui!
        public int minPeakFreq = 1;// The minimum 'height' a peak should be.</param>
        public int minPeakAmplitude = 60;
        public float allowedDeviation = .13f; //any data found outside of outlierBias * median will be ignored.

        public GameObject gradientLine;
        public GameObject gradientMesh;

        public castMesh canvas;

        public bool debug = true;

        autoCalibratorModule currentModule;

        //modules
        sampleLines_inline inline;

        sensorData[] records;

        // Use this for initialization
        void Awake()
        {
            inline = new sampleLines_inline();

            int childCount = transform.childCount;
            indicators = new GameObject[childCount];
            for (int c = 0; c < childCount; c++)
            {
                indicators[c] = transform.GetChild(c).gameObject;
            }

        }

        void Start()
        {
            records = new sensorData[xArticulation * yArticulation];
            resetCollectedData();

            setModule(inline);  //TEMP!!!  this should be set from a gui option

            //set up comm to our light sensors in the hardware
            //SerialController serial = gameObject.AddComponent<SerialController>();
            //serial.portName = getPortName();
            //serial.reconnectionDelay = 500;
            //serial.maxUnreadMessages = 100;
            //serial.maxFailuresAllowed = 3;
            //serial.enabled = true;

            //dataFileDict vars = canvas.GetComponent<dataFileDict> ();

        }

        public sensorData getRecord(int x, int y)
        {
            if (x >= xArticulation)
                return null;
            if (y >= yArticulation)
                return null;

            //the records are stored as (in a 3x3 example)  x0y0 x1y0 x2y0 x0y1 x1y1 x2y1 x0y2 x1y2 x2y2
            int i = (y * xArticulation) + x;

            if (i >= records.Length)
                return null;

            return records[i];
        }

        public void resetCollectedData()
        {
            for (int x = 0; x < records.Length; x++)
            {
                records[x] = new sensorData();
            }
        }

        static string getPortName()
        {
            string[] allSerialPorts = hypercube.input.getPortNames();
            foreach (string n in allSerialPorts)
            {
                if (n.Contains("usbmodem")) //TEMP! turn this into the real hardware name, not default arduino mega name!
                    return n;
            }
            return "NOT FOUND!";
        }

        public void setModule(autoCalibratorModule m)
        {
            if (currentModule != null)
                currentModule.end(this);

            currentModule = m;

            if (currentModule != null)
                currentModule.start(this);
        }

        // Update is called once per frame
        void Update()
        {
            if (!serial)
                return;

            //do we see a lit up screen? We only care about the most recent message.
            string data = serial.ReadSerialMessage();
            if (data == null) // not data this frame.
                return;

            string[] arr = data.Split(' ');

            if (arr.Length != xArticulation * yArticulation)
                Debug.LogError("Received " + arr.Length + " elements of data from the calibration hardware. This does not match the given " + xArticulation + " x " + yArticulation + " description of the sensor array.");


            for (int x = 0; x < records.GetLength(0); x++)
            {
                records[x].addDataPoint(dataFileDict.stringToInt(arr[x], 0), currentModule.getRelevantValue());
            }

            if (currentModule != null)
                currentModule.update(this);  //the module only gets updated after we get input from the serial
        }

        public void setLine(float posX, float posY, float w, float h, bool horizontal, bool onOff = true)
        {
            float screenX = 0f;
            float screenY = 0f;
            canvas.getScreenDims(ref screenX, ref screenY);
            float pixelX = 1f/screenX;
            float pixelY = 1f/screenY;

            float aspectRatio = canvas.getScreenAspectRatio();
            gradientLine.transform.localScale = new Vector3( 2f * pixelX * w * aspectRatio, 2f * pixelY * h, 1f);

            if (horizontal)
                gradientMesh.transform.localRotation = Quaternion.identity;
            else
                gradientMesh.transform.localRotation = Quaternion.Euler(0f,0f,90f);

            gradientLine.transform.localPosition = new Vector3((2f * posX * pixelX * aspectRatio) - ( pixelX * screenX *  aspectRatio), 1f-(posY * pixelY * 2f), 0f);

        }


        /// <param name="sensorData"> sensor x, sensor y, screenPixel  = value
        /// <param name="coordinates"> is returned as slice, x, y
  
        //sensorData z element is expected to be the length of the resolution.
        public static bool getCoordsFromData(int sliceCount, int[][][]sensorDataX, int[][][]sensorDataY, out Vector2[,,] coordinates)
        {
            if (sensorDataX.Length != sensorDataY.Length || sensorDataX[0].Length != sensorDataY[0].Length)
            {
                Debug.LogWarning("Sensor Array length doesn't match!");
                coordinates = null;
                return false;
            }

            float resX = sensorDataX[0][0].Length;
            float resY = sensorDataY[0][0].Length;

            coordinates = new Vector2[sliceCount, sensorDataX.Length, sensorDataX[0].Length];
            //fill in the data
            for (int s = 0; s < sliceCount; s++)
            {
                for (int x = 0; x < sensorDataX.Length; x++)
                {
                    for (int y = 0; y < sensorDataX[0].Length; y++)
                    {
                        coordinates[s, x, y] = new Vector2();
                    }
                }
            }

            for (int x = 0; x < sensorDataX.Length; x++)
            {
                for (int y = 0; y < sensorDataX[0].Length; y++)
                {
                    int[] xData = sensorDataX[x][y];
                    int[] yData = sensorDataY[x][y];
                    float[] valuesX = autoCalibrator.getPeaksFromData(10, 80, sliceCount, .13f, xData);  //TODO settings!
                    float[] valuesY = autoCalibrator.getPeaksFromData(10, 80, sliceCount, .13f, yData);
                    if (valuesX.Length != sliceCount || valuesY.Length != sliceCount )
                    {
                        Debug.LogWarning("Received peak data of incorrect length! Not all slices were accounted for.");   
                        return false;
                    }

                    for (int s = 0; s < sliceCount; s++) 
                    {
                        coordinates[s, x, y].x = valuesX[s]/resX;  
                        coordinates[s, x, y].y = valuesY[s]/resY; 
                    }                                      
                }
            }
            return true;
        }

        /// <param name="minPeakAmplitude">
        /// <param name="minPeakFreq">The minimum 'width' of expected peaks. This will keep it from returning noise as peaks.</param>
        public float[] findDataPeaks(int expectedPeaks, int xSensor, int ySensor)
        {
            sensorData d = getRecord(xSensor, ySensor);
            if (d == null)
                return null;

            return getPeaksFromData(minPeakAmplitude, minPeakFreq, expectedPeaks, allowedDeviation, d.getAllData());
        }

        /// <summary>
        /// This method will extract 'peaks' from noisy data, returning the positions in the array where it thinks the peaks occurred.
        /// Note that the returned values are floats: It uses averages to figure out where it thinks the true peak is which may lie 'in between' the given data points.
        /// finally, it will return the given number of expected peaks, using -9999 to represent peaks it could not determine.
        /// </summary>
        /// <param name="minPeakAmplitude">The minimum 'height' a peak should be.</param>
        /// <param name="minPeakFreq">The minimum 'width' of expected peaks. This will keep it from returning noise as peaks.</param>
        /// <param name="data">The data to obtain the peaks from.</param>
        /// <returns></returns>
        public static float[] getPeaksFromData(int minPeakAmplitude, int minPeakFreq, int expectedPeaks, float allowedDeviation, int[] data)
        {
            /*            data = new int[] //600ms
                {
                    75, 76, 75, 74, 76, 74, 77, 77, 79, 79, 80, 80, 81, 81, 80, 80, 80, 82, 82, 81, 83, 83, 84, 84, 82,
                    83, 85, 82, 83, 84, 86, 85, 85, 83, 84, 84, 81, 78, 77, 78, 78, 77, 78, 76, 75, 75, 79, 79, 76, 74,
                    73, 75, 75, 77, 76, 74, 74, 74, 74, 76, 74, 76, 76, 75, 74, 76, 76, 73, 73, 74, 77, 78, 79, 79, 77,
                    74, 71, 74, 74, 77, 75, 75, 79, 80, 78, 75, 79, 80, 77, 77, 75, 79, 79, 79, 79, 79, 76, 77, 76, 80,
                    76, 77, 75, 78, 78, 77, 77, 78, 78, 78, 77, 75, 77, 77, 76, 75, 76, 76, 78, 77, 79, 78, 76, 79, 80,
                    79, 81, 81, 83, 82, 84, 84, 85, 87, 88, 88, 88, 89, 88, 89, 87, 87, 86, 87, 86, 86, 85, 82, 83, 82,
                    82, 81, 79, 80, 80, 78, 77, 76, 76, 74, 72, 74, 73, 71, 74, 74, 74, 75, 74, 74, 74, 76, 77, 78, 79,
                    78, 79, 79, 75, 75, 76, 75, 75, 77, 77, 77, 76, 77, 79, 78, 80, 82, 82, 81, 82, 83, 83, 82, 84, 84,
                    84, 83, 83, 83, 85, 83, 83, 85, 85, 84, 85, 81, 80, 82, 82, 81, 83, 85, 83, 84, 85, 86, 86, 88, 88,
                    89, 90, 89, 88, 90, 91, 91, 91, 92, 93, 94, 94, 93, 93, 93, 94, 97, 97, 99, 100, 101, 101, 102, 102,
                    101, 101, 99, 98, 97, 99, 98, 95, 95, 94, 93, 92, 90, 89, 88, 89, 88, 87, 89, 89, 88, 89, 88, 84, 86,
                    83, 84, 84, 85, 86, 81, 79, 76, 79, 81, 85, 85, 85, 85, 85, 85, 85, 86, 86, 86, 86, 87, 87, 86, 84,
                    83, 84, 85, 86, 85, 85, 86, 86, 84, 84, 85, 85, 83, 82, 86, 88, 86, 84, 83, 89, 89, 89, 89, 89, 88,
                    86, 86, 88, 90, 88, 89, 89, 91, 90, 89, 91, 89, 87, 88, 88, 89, 87, 90, 94, 93, 95, 96, 96, 97, 99,
                    101, 100, 102, 102, 103, 106, 107, 107, 104, 104, 102, 99, 99, 97, 93, 93, 91, 88, 89, 88, 88, 86, 85,
                    86, 86, 86, 87, 87, 88, 88, 89, 88, 87, 86, 88, 85, 84, 85, 84, 86, 85, 82, 81, 82, 81, 82, 81, 82,
                    81, 83, 83, 85, 88, 86, 87, 87, 88, 87, 88, 83, 85, 87, 84, 87, 86, 84, 84, 88, 89, 88, 87, 88, 86,
                    88, 91, 92, 92, 91, 85, 87, 89, 90, 90, 89, 89, 88, 91, 91, 91, 91, 91, 90, 92, 91, 87, 90, 91, 93,
                    92, 93, 95, 95, 96, 96, 96, 98, 99, 98, 99, 102, 103, 105, 106, 107, 109, 110, 111, 112, 110, 109,
                    110, 108, 107, 105, 102, 102, 99, 98, 96, 95, 95, 94, 92, 91, 91, 91, 91, 90, 91, 91, 90, 90, 89, 89,
                    88, 90, 88, 89, 90, 90, 90, 90, 89, 88, 88, 88, 90, 89, 88, 88, 88, 89, 88, 89, 89, 89, 88, 90, 90,
                    90, 88, 90, 90, 90, 90, 89, 90, 88, 89, 90, 90, 90, 90, 90, 90, 90, 88, 88, 90, 88, 88, 89, 89, 88,
                    90, 90, 88, 88, 90, 88, 88, 90, 89, 90, 90, 90, 90, 89, 91, 91, 90, 90, 91, 91, 94, 94, 95, 95, 97,
                    99, 98, 99, 102, 104, 104, 106, 105, 106, 107, 108, 103, 105, 100, 100, 99, 99, 97, 96, 95, 94, 93,
                    92, 91, 90, 89, 88, 88, 87, 87, 86, 87, 85, 84, 85, 87, 86, 84, 84, 85, 86, 82, 84, 82, 83, 83, 84,
                    83, 84, 86, 85, 85, 85, 85, 85, 86, 83, 83, 84, 83, 85, 85, 88, 87, 81, 81, 83, 86, 85, 81, 78, 79,
                    80, 82, 85, 83, 82, 86, 85, 86, 82, 83, 84, 84, 84, 84, 84, 82, 80, 84, 82, 83, 81, 81, 82, 83, 83,
                    83, 85, 86, 87, 90, 87, 89, 88, 95, 96, 97, 97, 101, 103, 103, 106, 108, 111, 114, 114, 117, 118, 115,
                    115, 113, 111, 109, 106, 102, 98, 96, 95, 93, 93, 93, 90, 88, 88, 87, 85, 85, 86, 85, 85, 84, 83, 83,
                    83, 83, 84, 85, 85, 85, 85, 85, 83, 84, 85, 84, 81, 81, 83, 80, 81, 82, 80, 82, 82, 82, 81, 79, 80,
                    81, 85, 84, 81, 81, 82, 82, 82, 82, 82, 84, 83, 81, 82, 84, 82, 83, 83, 83, 83, 80, 83, 85, 82, 81,
                    81, 81, 79, 79, 77, 79, 77, 80, 80, 79, 79, 83, 84, 82, 83, 81, 84, 83, 84, 85, 84, 87, 90, 88, 88,
                    91, 94, 95, 96, 98, 99, 102, 100, 104, 102, 105, 105, 102, 98, 97, 97, 94, 91, 87, 85, 82, 82, 81, 82,
                    81, 80, 79, 79, 78, 78, 77, 76, 74, 77, 79, 76, 74, 72, 73, 75, 75, 75, 74, 73, 74, 73, 77, 79, 76,
                    76, 79, 79, 80, 80, 81, 81, 81, 81, 81, 80, 80, 80, 80, 80, 81, 81, 81, 81, 81, 81, 80, 81, 81, 80,
                    80, 81, 81, 82, 80, 80, 80, 78, 77, 80, 77, 81, 81, 79, 79, 78, 78, 77, 79, 77, 79, 81, 79, 78, 76,
                    77, 76, 80, 80, 79, 76, 76, 77, 82, 81, 83, 86, 92, 89, 95, 96, 101, 104, 108, 111, 113, 116, 117,
                    116, 115, 112, 108, 100, 97, 96, 92, 87, 85, 83, 80, 77, 75, 76, 73, 72, 73, 73, 74, 73, 72, 75, 75,
                    73, 74, 74, 75, 73, 73, 73, 72, 72, 73, 74, 74, 69, 70, 71, 71, 68, 68, 70, 71, 68, 73, 73, 73, 73,
                    73, 73, 71, 72, 73, 73, 72, 73, 73, 74, 71, 71, 71, 71, 71, 73, 70, 69, 72, 74, 72, 72, 73, 72, 73,
                    73, 71, 73, 73, 71, 71, 73, 71, 74, 73, 73, 73, 73, 72, 69, 68, 69, 70, 72, 72, 71, 72, 74, 74, 76,
                    80, 83, 85, 88, 92, 94, 96, 100, 106, 107, 110, 108, 107, 105, 101, 97, 94, 93, 88, 85, 84, 81, 78,
                    76, 73, 72, 71, 70, 70, 69, 68, 68, 66, 66, 66, 67, 67, 67, 65, 63, 64, 64, 62, 62, 64, 64, 65, 65,
                    64, 65, 65, 65, 65, 65, 64, 65, 64, 65, 63, 64, 64, 64, 64, 64, 62, 61, 63, 62, 59, 59, 57, 58
                };

            data = new int[] //200 ms
                {
                    93, 93, 92, 93, 94, 94, 94, 95, 95, 95, 94, 95, 96, 96, 96, 95, 95, 95, 95, 96, 95, 96, 96, 96, 96, 97, 97, 96, 96, 97, 97, 96, 97, 96, 96, 96, 96, 95, 95, 94,
                    95, 94, 95, 95, 95, 95, 95, 95, 95, 95, 95, 95, 95, 94, 94, 93, 93, 94, 94, 96, 96, 95, 94, 94, 95, 95, 95, 95, 95, 95, 95, 95, 94, 94, 95, 95, 95, 95, 95, 93,
                    90, 87, 86, 85, 86, 86, 87, 87, 88, 88, 87, 87, 87, 87, 88, 88, 87, 87, 88, 88, 88, 87, 88, 87, 87, 87, 86, 87, 86, 87, 88, 92, 95, 95, 95, 96, 97, 96, 96, 96,
                    96, 97, 97, 98, 98, 98, 98, 98, 98, 99, 100, 100, 100, 101, 99, 99, 99, 100, 100, 100, 100, 100, 100, 99, 99, 99, 98, 98, 98, 98, 97, 97, 96, 97, 97, 96, 96, 96,
                    96, 96, 96, 96, 96, 96, 96, 96, 96, 96, 95, 96, 96, 97, 97, 96, 97, 97, 97, 97, 97, 96, 96, 97, 97, 98, 98, 98, 97, 97, 98, 97, 98, 98, 98, 98, 99, 99, 98, 98,
                    99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 98, 98, 98, 99, 98, 99, 99, 99, 99, 99, 99, 99, 99, 100, 99, 99, 100, 100, 101, 101, 101, 101, 102, 101, 102, 102,
                    103, 103, 103, 104, 104, 103, 104, 104, 105, 104, 105, 105, 105, 105, 105, 105, 105, 104, 104, 103, 103, 102, 102, 101, 101, 100, 100, 101, 100, 100, 100, 100,
                    99, 99, 99, 99, 99, 99, 99, 99, 100, 100, 99, 99, 100, 99, 99, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100,
                    100, 100, 101, 101, 100, 101, 101, 101, 101, 101, 101, 100, 100, 100, 100, 101, 101, 101, 101, 101, 101, 102, 102, 103, 102, 103, 102, 102, 103, 103, 103, 103,
                    103, 103, 104, 103, 103, 104, 105, 105, 105, 105, 106, 106, 107, 107, 107, 107, 108, 109, 109, 109, 110, 109, 109, 109, 109, 108, 107, 108, 107, 107, 106, 106,
                    106, 105, 104, 102, 102, 102, 102, 102, 102, 102, 101, 101, 101, 101, 101, 101, 101, 101, 101, 101, 101, 101, 101, 101, 101, 101, 100, 100, 101, 101, 100, 101,
                    101, 101, 101, 101, 101, 101, 101, 101, 100, 100, 101, 101, 101, 101, 101, 102, 102, 102, 101, 101, 100, 101, 101, 101, 101, 101, 101, 101, 101, 101, 101, 101,
                    101, 102, 102, 102, 102, 102, 101, 102, 102, 102, 102, 102, 102, 102, 102, 102, 102, 102, 102, 102, 103, 103, 103, 104, 104, 104, 105, 105, 105, 105, 106, 107,
                    107, 108, 107, 108, 108, 108, 108, 107, 107, 106, 106, 105, 104, 104, 103, 103, 102, 103, 102, 102, 102, 101, 101, 101, 100, 101, 101, 101, 101, 101, 101, 101,
                    100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 101, 101, 101, 100, 101, 101, 101, 100, 101, 101, 100, 101, 101, 101,
                    101, 101, 101, 101, 101, 101, 101, 101, 101, 101, 101, 102, 102, 102, 101, 101, 102, 102, 102, 101, 102, 102, 102, 102, 102, 102, 102, 102, 102, 101, 102, 102,
                    102, 102, 103, 103, 103, 103, 103, 103, 105, 105, 105, 106, 106, 107, 107, 107, 108, 108, 108, 107, 107, 107, 106, 105, 105, 104, 103, 103, 102, 102, 102, 102,
                    102, 101, 101, 101, 101, 101, 100, 100, 101, 101, 101, 100, 101, 100, 101, 101, 101, 100, 101, 100, 100, 101, 100, 100, 101, 101, 101, 101, 101, 101, 101, 101,
                    101, 100, 101, 101, 101, 101, 101, 101, 101, 101, 101, 101, 101, 101, 101, 101, 101, 101, 101, 101, 101, 101, 101, 101, 101, 101, 101, 101, 101, 101, 101, 101,
                    101, 102, 101, 102, 102, 102, 102, 101, 102, 101, 102, 101, 101, 102, 103, 102, 103, 104, 104, 105, 105, 106, 106, 107, 108, 109, 109, 110, 110, 111, 112, 112,
                    112, 111, 111, 109, 109, 108, 107, 106, 105, 104, 104, 103, 103, 102, 102, 101, 101, 100, 100, 100, 100, 100, 100, 100, 99, 100, 100, 100, 99, 99, 99, 99, 98,
                    98, 99, 98, 98, 98, 98, 97, 98, 98, 97, 97, 97, 96, 97, 97, 96, 96, 96, 95, 96, 95, 96, 96, 95, 96, 95, 96, 96, 96, 96, 96, 96, 96, 96, 95, 96, 95, 95, 96, 95,
                    95, 96, 96, 96, 96, 97, 97, 97, 96, 97, 97, 97, 97, 98, 98, 98, 98, 99, 98, 99, 99, 99, 100, 101, 101, 102, 102, 103, 103, 105, 105, 106, 107, 107, 108, 108,
                    108, 108, 107, 107, 105, 105, 104, 104, 103, 103, 102, 101, 100, 100, 100, 99, 99, 99, 99, 99, 99, 99, 98, 99, 99, 98, 99, 98, 99, 99, 99, 98, 98, 98, 99, 99,
                    99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 100, 100, 100, 99, 100, 100, 100, 100, 100, 99, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 99,
                    99, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 99, 99, 100, 99, 100, 99, 100, 100, 100, 100, 100, 100, 101, 100, 101, 102, 102, 103, 104, 104, 105, 106,
                    107, 108, 110, 110, 111, 112, 112, 112, 111, 110, 109, 107, 107, 106, 105, 103, 102, 101, 101, 100, 100, 99, 99, 98, 99, 98, 99, 98, 98, 98, 98, 98, 98, 97, 98,
                    97, 98, 98, 98, 98, 98, 98, 97, 98, 97, 98, 98, 98, 97, 98, 99, 99, 99, 99, 99, 98, 99, 99, 99, 99, 99, 99, 99, 99, 98, 98, 98, 97, 98, 98, 98, 97, 97, 97, 97,
                    98, 98, 98, 97, 98, 98, 97, 97, 97, 98, 97, 97, 98, 98, 97, 97, 97, 97, 97, 98, 98, 98, 97, 98, 98, 98, 99, 99, 100, 100, 101, 101, 102, 104, 104, 106, 107, 108,
                    109, 109, 110, 111, 111, 110, 109, 107, 106, 105, 103, 102, 101, 100, 99, 99, 98, 97, 97, 96, 96, 96, 95, 95, 95, 95, 94, 94, 94, 95, 95, 95, 95, 95, 95, 95, 95,
                    95, 94, 95, 95, 94, 94, 94, 94, 95, 95, 94, 94, 95, 94, 94, 94, 95, 95, 95, 95, 94, 94, 94, 94, 95, 94, 93, 94, 94, 93, 93, 93
                };

            data = new int[] //100
                {
                    44, 45, 45, 45, 46, 45, 46, 45, 46, 45, 46, 46, 46, 46, 46, 46, 46, 46, 46, 46, 46, 46, 47, 46, 47, 46, 46, 47, 47, 46, 47, 47, 47, 47, 46, 46, 47, 46, 46, 46,
                    46, 46, 45, 46, 46, 46, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 46, 46, 46, 46, 46, 45, 45, 45, 46, 46, 46, 45, 46, 45, 46, 46, 46, 45, 46,
                    45, 46, 46, 46, 46, 45, 46, 46, 46, 46, 45, 46, 45, 46, 46, 46, 46, 46, 46, 46, 46, 46, 46, 46, 46, 46, 46, 46, 46, 46, 47, 47, 47, 46, 47, 46, 47, 46, 46, 47,
                    46, 47, 47, 47, 48, 47, 47, 47, 48, 48, 48, 47, 48, 48, 49, 48, 49, 48, 49, 49, 49, 49, 49, 49, 49, 48, 48, 48, 47, 48, 48, 48, 47, 47, 46, 47, 47, 46, 47, 47,
                    47, 47, 46, 46, 46, 47, 47, 47, 47, 47, 47, 47, 47, 46, 47, 47, 46, 46, 47, 47, 47, 47, 47, 47, 46, 47, 47, 47, 47, 47, 47, 47, 47, 47, 47, 48, 47, 48, 48, 47,
                    48, 47, 47, 47, 48, 48, 47, 47, 48, 48, 47, 48, 48, 48, 47, 48, 48, 48, 48, 48, 47, 48, 47, 48, 48, 48, 48, 48, 48, 47, 47, 48, 48, 48, 48, 48, 49, 49, 48, 49,
                    49, 49, 50, 50, 50, 50, 50, 50, 50, 50, 50, 49, 48, 48, 48, 47, 47, 47, 47, 47, 47, 47, 46, 46, 46, 46, 45, 46, 46, 46, 45, 46, 46, 46, 46, 46, 47, 47, 47, 47,
                    47, 47, 47, 47, 47, 47, 46, 46, 47, 47, 47, 47, 47, 47, 47, 47, 48, 48, 47, 47, 48, 48, 47, 48, 47, 48, 48, 48, 47, 48, 47, 47, 47, 48, 48, 48, 48, 48, 47, 48,
                    47, 47, 47, 48, 48, 47, 48, 48, 48, 48, 48, 48, 48, 48, 48, 48, 48, 48, 49, 49, 49, 49, 48, 49, 50, 50, 50, 50, 51, 51, 51, 51, 51, 52, 52, 52, 52, 51, 51, 51,
                    50, 50, 50, 49, 49, 48, 48, 47, 48, 47, 47, 47, 46, 47, 46, 45, 46, 45, 45, 45, 46, 46, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 44, 45, 45, 44, 45, 45,
                    45, 45, 45, 44, 45, 45, 45, 45, 45, 45, 45, 44, 45, 45, 44, 45, 45, 45, 44, 44, 45, 45, 44, 44, 45, 44, 44, 45, 45, 45, 45, 44, 45, 45, 45, 45, 45, 45, 45, 45,
                    45, 45, 45, 45, 45, 46, 46, 46, 46, 46, 46, 46, 47, 46, 47, 47, 48, 47, 48, 48, 49, 49, 49, 49, 49, 49, 48, 49, 49, 48, 48, 48, 47, 47, 46, 47, 46, 46, 46, 46,
                    46, 46, 45, 45, 45, 45, 45, 45, 45, 45, 44, 45, 44, 45, 45, 45, 45, 45, 45, 45, 45, 45, 44, 45, 45, 45, 44, 44, 45, 44, 45, 44, 45, 44, 44, 44, 44, 45, 44, 44,
                    44, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 44, 44, 44, 44, 44, 44, 45, 44, 45, 44, 44, 44, 44, 44, 44, 44, 44, 44, 44, 44, 44, 44, 44, 44, 44, 44, 44, 44, 44,
                    45, 45, 44, 45, 45, 44, 44, 45, 45, 46, 45, 46, 46, 45, 45, 46, 46, 45, 44, 45, 44, 43, 44, 44, 43, 43, 42, 43, 43, 42, 41, 42, 41, 42, 42, 41, 42, 42, 41, 41,
                    42, 42, 42, 41, 42, 42, 42, 42, 42, 42, 43, 43, 42, 43, 43, 42, 43, 42, 42, 42, 43, 43, 43, 43, 42, 42, 42, 42, 42, 43, 43, 42, 43, 43, 43, 43, 43, 43, 43, 43,
                    43, 43, 42, 42, 43, 43, 43, 43, 42, 42, 43, 43, 43, 43, 43, 43, 43, 43, 43, 43, 44, 43, 44, 44, 44, 44, 44, 44, 44, 45, 44, 44, 46, 46, 46, 47, 46, 47, 48, 48,
                    48, 48, 48, 47, 48, 47, 46, 46, 46, 45, 43, 44, 42, 42, 42, 42, 42, 41, 40, 41, 41, 40, 40, 40, 40, 40, 39, 40, 38, 39, 38, 38, 38, 39, 39, 39, 39, 38, 38, 38,
                    37, 37, 38, 37, 38, 38, 37, 38, 37, 37, 38, 38, 38, 37, 37, 38, 37, 38, 38, 38, 38, 38, 37, 38, 37, 37, 37, 37, 37, 37, 37, 37, 37, 36, 37, 37, 36, 37, 36, 38,
                    38, 38, 38, 37, 37, 38, 37, 38, 37, 38, 38, 38, 37, 38, 38, 39, 39, 39, 39, 40, 40, 40, 40, 41, 41, 42, 42, 43, 42, 43, 43, 44, 43, 43, 42, 42, 42, 41, 41, 41,
                    40, 40, 40, 39, 39, 39, 39, 38, 38, 39, 39, 39, 39, 39, 39, 38, 39, 39, 38, 38, 38, 39, 39, 39, 39, 39, 39, 39, 39, 40, 40, 40, 40, 39, 40, 40, 40, 39, 40, 40,
                    40, 40, 40, 40, 40, 40, 40, 40, 40, 40, 40, 39, 39, 39, 40, 40, 40, 39, 40, 40, 39, 40, 40, 40, 40, 40, 40, 40, 40, 40, 40, 40, 40, 40, 41, 41, 41, 41, 40, 41,
                    41, 40, 41, 40, 41, 41, 41, 42, 42, 43, 43, 44, 44, 45, 45, 46, 46, 46, 48, 48, 48, 48, 48, 47, 47, 46, 46, 45, 44, 44, 44, 43, 43, 43, 42, 42, 42, 41, 41, 41,
                    41, 41, 41, 41, 41, 41, 41, 41, 41, 41, 41, 41, 41, 41, 41, 42, 41, 41, 41, 41, 41, 41, 41, 42, 42, 42, 41, 42, 42, 42, 42, 42, 42, 42, 42, 42, 42, 42, 42, 42,
                    41, 41, 42, 42, 42, 42, 41, 42, 41, 42, 41, 42, 42, 41, 41, 42, 41, 42, 42, 41, 41, 42, 42, 42, 42, 41, 42, 42, 41, 42, 42, 42, 41, 42, 42, 42, 43, 43, 44, 44,
                    45, 45, 45, 46, 47, 47, 48, 49, 49, 49, 50, 49, 48, 48, 47, 46, 46, 45, 43, 43, 42, 42, 42, 41, 41, 40, 39, 39, 38, 38, 37, 38, 37, 37, 39, 38, 39, 39, 39, 38,
                    38, 39, 38, 38, 39, 38, 39, 39, 39, 39, 39, 39, 39, 38, 39, 39, 39, 39, 39, 39, 39, 39, 39, 38, 38, 38, 38, 38, 38, 38, 37, 38, 38, 37, 37, 36, 37, 36, 37, 36
                };


                     data = new int[] //100 with bugs inserted
{
44, 45, 45, 45, 46, 45, 46, 45, 46, 45, 46, 46, 46, 46, 46, 46, 46, 46, 46, 46, 46, 46, 47, 46, 47, 46, 46, 47, 47, 46, 47, 47, 47, 47, 46, 46, 47, 46, 46, 46,
46, 46, 45, 46, 46, 46, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 46, 46, 46, 46, 46, 45, 45, 45, 46, 46, 46, 45, 46, 45, 46, 46, 46, 45, 46,
45, 46, 46, 46, 46, 45, 46, 46, 46, 46, 45, 46, 45, 46, 46, 46, 46, 46, 46, 46, 46, 46, 46, 46, 46, 46, 46, 46, 46, 46, 47, 47, 47, 46, 47, 46, 47, 46, 46, 47,
46, 47, 47, 47, 48, 47, 47, 47, 48, 48, 48, 47, 48, 48, 49, 48, 49, 48, 49, 49, 49, 49, 49, 49, 49, 48, 48, 48, 47, 48, 48, 48, 47, 47, 46, 47, 47, 46, 47, 47,
47, 47, 46, 46, 46, 47, 47, 47, 47, 47, 47, 47, 47, 46, 47, 47, 46, 46, 47, 47, 47, 47, 47, 47, 46, 47, 47, 47, 47, 47, 47, 47, 47, 47, 47, 48, 47, 48, 48, 47,
48, 47, 47, 47, 48, 48, 47, 47, 48, 48, 47, 48, 48, 48, 47, 48, 48, 48, 48, 48, 47, 48, 47, 48, 48, 48, 48, 48, 48, 47, 47, 48, 48, 48, 48, 48, 49, 49, 48, 49,
49, 49, 50, 50, 50, 50, 50, 50, 50, 50, 50, 49, 48, 48, 48, 47, 47, 47, 47, 47, 47, 47, 46, 46, 46, 46, 45, 46, 46, 46, 45, 46, 46, 46, 46, 46, 47, 47, 47, 47,
47, 47, 47, 47, 47, 47, 46, 46, 47, 47, 47, 47, 47, 47, 47, 47, 48, 48, 47, 47, 48, 48, 47, 48, 47, 48, 48, 48, 47, 48, 47, 47, 47, 48, 48, 48, 48, 48, 47, 48,
47, 47, 47, 48, 48, 47, 48, 48, 48, 48, 48, 48, 48, 48, 48, 48, 48, 48, 49, 49, 49, 49, 48, 49, 50, 50, 50, 50, 51, 51, 51, 51, 51, 52, 52, 52, 52, 51, 51, 51,
50, 50, 50, 49, 49, 48, 48, 47, 48, 47, 47, 47, 46, 47, 46, 45, 46, 45, 45, 45, 46, 46, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 44, 45, 45, 44, 45, 45,
45, 45, 45, 44, 45, 45, 45, 45, 45, 45, 45, 44, 45, 45, 44, 45, 45, 45, 44, 44, 45, 45, 44, 44, 45, 44, 44, 45, 45, 45, 45, 44, 45, 45, 45, 45, 45, 45, 45, 45,
37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37,
37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37,
44, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 44, 44, 44, 44, 44, 44, 45, 44, 45, 44, 44, 44, 44, 44, 44, 44, 44, 44, 44, 44, 44, 44, 44, 44, 44, 44, 44, 44, 44,
45, 45, 44, 45, 45, 44, 44, 45, 45, 46, 45, 46, 46, 45, 45, 46, 46, 45, 44, 45, 44, 43, 44, 44, 43, 43, 42, 43, 43, 42, 41, 42, 41, 42, 42, 41, 42, 42, 41, 41,
42, 42, 42, 41, 42, 42, 42, 42, 42, 42, 43, 43, 42, 43, 43, 42, 43, 42, 42, 42, 43, 43, 43, 43, 42, 42, 42, 42, 42, 43, 43, 42, 43, 43, 43, 43, 43, 43, 43, 43,
43, 43, 42, 42, 43, 43, 43, 43, 42, 42, 43, 43, 43, 43, 43, 43, 43, 43, 43, 43, 44, 43, 44, 44, 44, 44, 44, 44, 44, 45, 44, 44, 46, 46, 46, 47, 46, 47, 48, 48,
48, 48, 48, 47, 48, 47, 46, 46, 46, 45, 43, 44, 42, 42, 42, 42, 42, 41, 40, 41, 41, 40, 40, 40, 40, 40, 39, 40, 38, 39, 38, 38, 38, 39, 39, 39, 39, 38, 38, 38,
37, 37, 38, 37, 38, 38, 37, 38, 37, 37, 38, 38, 38, 37, 37, 38, 37, 38, 38, 38, 38, 38, 37, 38, 37, 37, 37, 37, 37, 37, 37, 37, 37, 36, 37, 37, 36, 37, 36, 38,
38, 38, 38, 37, 37, 38, 37, 38, 37, 38, 38, 38, 37, 38, 38, 39, 39, 39, 39, 40, 40, 40, 40, 41, 41, 42, 42, 43, 42, 43, 43, 44, 43, 43, 42, 42, 42, 41, 41, 41,
40, 40, 40, 39, 39, 39, 39, 38, 38, 39, 39, 39, 39, 39, 39, 38, 39, 39, 38, 38, 38, 39, 39, 39, 39, 39, 39, 39, 39, 40, 40, 40, 40, 39, 40, 40, 40, 39, 40, 40,
40, 40, 40, 40, 40, 40, 40, 40, 40, 40, 40, 39, 39, 39, 40, 40, 40, 39, 40, 40, 39, 40, 40, 40, 40, 40, 40, 40, 40, 40, 40, 40, 40, 40, 41, 41, 41, 41, 40, 41,
41, 40, 41, 40, 41, 41, 41, 42, 42, 43, 43, 41, 41, 41, 41, 41, 41, 41, 41, 41, 41, 41, 41, 41, 41, 41, 41, 41, 44, 44, 44, 41, 41, 41, 41, 41, 41, 41, 41, 41,
41, 41, 41, 41, 41, 41, 41, 41, 41, 41, 41, 41, 41, 41, 41, 42, 41, 41, 41, 41, 41, 41, 41, 42, 42, 42, 41, 42, 42, 42, 42, 42, 42, 42, 42, 42, 42, 42, 42, 42,
41, 41, 42, 42, 42, 42, 41, 42, 41, 42, 41, 42, 42, 41, 41, 42, 41, 42, 42, 41, 41, 42, 37, 37, 37, 37, 37, 37, 37, 42, 37, 37, 41, 37, 42, 37, 37, 37, 37, 37,
45, 45, 45, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 39, 39, 38, 38, 37, 38, 37, 37, 37, 38, 37, 37, 37, 38,
38, 39, 38, 38, 39, 38, 39, 39, 39, 39, 39, 39, 39, 38, 39, 39, 39, 39, 39, 39, 39, 39, 39, 38, 38, 38, 38, 38, 38, 38, 37, 38, 38, 37, 37, 36, 37, 36, 37, 36
};*/

            /*          data = new int[] //100 take 2
            {
                69, 69, 69, 69, 69, 70, 70, 70, 70, 70, 70, 70, 70, 70, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 72, 72, 72, 72, 72, 72, 72, 71, 71, 71, 71, 71, 71, 71,
                70, 70, 70, 70, 70, 69, 69, 69, 69, 69, 69, 69, 69, 69, 69, 69, 69, 69, 69, 69, 69, 69, 69, 69, 69, 69, 69, 69, 69, 69, 69, 69, 69, 69, 70, 70, 70, 70, 70, 70,
                70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 71, 71, 71, 71, 71, 71,
                71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 72, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 70, 70, 70, 70, 70, 70, 70, 70, 69, 70, 69, 69,
                69, 69, 69, 69, 69, 69, 69, 69, 69, 69, 69, 69, 69, 69, 69, 69, 69, 69, 69, 69, 70, 70, 70, 70, 70, 70, 70, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71,
                71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 72, 72, 72, 72, 72, 72, 72, 72, 73,
                73, 73, 73, 73, 74, 74, 74, 74, 74, 74, 74, 74, 74, 74, 73, 73, 73, 73, 72, 72, 72, 72, 72, 72, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71,
                71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71,
                71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 72, 72, 72, 72, 72, 72, 72, 72, 72, 73, 73, 73, 73, 73, 74, 74, 74, 74, 75, 75, 75, 75,
                74, 74, 74, 74, 73, 73, 73, 73, 72, 72, 72, 72, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 70, 71, 71, 70,
                70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 71, 71, 71, 71, 70, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71,
                71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 72, 72, 72, 72, 72, 72, 73, 73, 73, 74, 74, 74, 74, 74, 74, 74, 74, 74, 73, 73, 73, 72, 72, 72, 72, 71,
                71, 71, 71, 71, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70,
                70, 71, 70, 70, 71, 71, 70, 71, 71, 71, 71, 70, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 72,
                72, 72, 72, 72, 72, 72, 72, 73, 73, 73, 73, 73, 74, 74, 74, 74, 75, 75, 75, 75, 74, 74, 74, 74, 73, 73, 73, 73, 72, 72, 72, 72, 72, 72, 71, 71, 71, 71, 71, 71,
                71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71,
                71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 72, 72, 72, 72, 72, 72, 72, 72, 72, 72, 73, 73, 73, 74, 74, 74,
                74, 75, 75, 76, 76, 77, 77, 77, 77, 77, 76, 76, 75, 75, 74, 74, 74, 73, 73, 72, 72, 72, 72, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71,
                71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71,
                71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 72, 72, 72, 72, 72, 72, 73, 73, 73, 74, 74, 74, 75, 75, 75, 76, 76, 76, 76, 76,
                76, 75, 75, 74, 74, 73, 73, 72, 72, 72, 71, 71, 71, 71, 71, 71, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 71, 70, 71, 71, 71, 71,
                71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71,
                71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 72, 72, 72, 73, 73, 74, 74, 75, 75, 76, 77, 77, 78, 78, 78, 78, 78, 78, 77, 76, 76, 75, 74, 74, 73, 73,
                72, 72, 72, 71, 71, 71, 71, 71, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70,
                70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 71, 70, 70, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71, 71,
                71, 71, 71, 71, 72, 72, 72, 72, 73, 73, 74, 74, 75, 75, 76, 76, 77, 78, 78, 78, 78, 77, 77, 76, 75, 74, 74, 73, 73, 72, 72, 71, 71, 71, 70, 70, 70, 70, 70, 70,
                70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 70, 69, 70, 70, 70, 70, 70, 70, 69, 69, 69, 69, 69, 69, 69, 69, 69, 69, 68, 68, 68, 68, 68
            };
            */

            float badValue = -9999f; //what value do we return in the array when we think there is a missing element there?

            //first lets bottom out the data. This will help with figuring out the center of the peaks later.
            int minVal = int.MaxValue;
            foreach (int i in data)
            {
                if (i < minVal)
                    minVal = i;
            }
            for (int i = 0; i < data.Length; i++)
            {
                data[i] -= minVal;
            }


            //then, we will filter out what parts of the data count as a part of a peak.
            int halfFreq = Mathf.RoundToInt((float)minPeakFreq/2f);
            int first = 0;
            int last = 0;
            List<int> peakData = new List<int>(); //store here any values that meet basic peak criteria
            for (int i = 0; i < data.Length; i++)
            {
                first = i - halfFreq;
                last = i + halfFreq;

                if (first < 0 && last >= data.Length)
                    Debug.LogError("Data set is too small to analyze using a minPeakFreq of " + minPeakFreq);

                if (first < 0) //near start of analysis
                {
                    if (checkPeak(0, data[i], data[last], minPeakAmplitude))
                        peakData.Add(i);
                }
                else if (last >= data.Length) //near end of analysis
                {
                    if (checkPeak(data[first], data[i], 0, minPeakAmplitude))
                        peakData.Add(i);
                }
                else //normal path
                {
                    if (checkPeak(data[first], data[i], data[last], minPeakAmplitude))
                        peakData.Add(i);
                }
            }

            if (peakData.Count == 0) //sanity check
            {
                Debug.LogWarning("Could not find any peaks in the given data. Is the sensor broken? Printing raw data to log...");
                Debug.Log(data);
                return new float[0]; //could not find peaks
            }


            //now we know what parts of the data are part of peaks. Let's arrange them into groups (peaks).
            //maxAllowedMissingDataPoints will be used to separate out the peaks from each other.
            List<List<int>> noisyPeaks = new List<List<int>>();    
            int maxAllowedMissingDataPoints =  Mathf.RoundToInt(minPeakFreq / 5);
            if (maxAllowedMissingDataPoints < 5)
                maxAllowedMissingDataPoints = 5;
            int currentPeak = 0;
            noisyPeaks.Add(new List<int>());
            for (int d = 0; d < peakData.Count -1; d++)
            {
                noisyPeaks[currentPeak].Add(peakData[d]);

                if (peakData[d + 1] - peakData[d] >= maxAllowedMissingDataPoints)
                {
                    noisyPeaks.Add(new List<int>());
                    currentPeak++;
                }               
            }

            //lets, ignore any peaks with very few data points (which are probably noise)
            List<List<int>> peaks = new List<List<int>>();
            for (int p = 0; p < noisyPeaks.Count; p++)
            {
                if (noisyPeaks[p].Count >= 1)   //it's 1, so this is 'off' for now.
                    peaks.Add(noisyPeaks[p]);
            }


            //now lets do a biased average of the data contained in each peak, and prepare to return that as our final values.
            List<float> candidates = new List<float>();
            foreach (List<int> p in peaks)
            {
                candidates.Add(findPeakInDataPoints(p, data, 10f));
            }


            //finally, lets do a check to make sure that the peaks are evenly distributed, removing or replacing any that are not;
            //so first we do a diff comparison between all of them.
            //then remove any where the differences were more than 15% of the rest and do it again.
            List<float> differences = new List<float>();
            for (int i = 0; i < candidates.Count -1; i ++)
            {
                differences.Add(candidates[i + 1] - candidates[i]);
            }
            float[] outliers;
            int medianIndex = 0;
            float medianDiff = findOutliers(differences, allowedDeviation, out outliers, out medianIndex);

            //now lets go through them again, this time having some statistical info in hand (median values)                  
            float startingOffset = candidates[medianIndex] % medianDiff; //as best we can know, the medianDiff and medianIndex are well placed data, so lets use them as a base point to check the rest.

            //compare our found peaks to where we expect them to be based on the median info. namely, at a mostly steady distance from each other (within allowedDeviation).
            List<float> output = new List<float>();            
            for (int i = 0; i < expectedPeaks; i++)
            {
                float expectedVal = startingOffset + (i * medianDiff);
                bool foundMatch = false;
                for (int c = 0; c < candidates.Count; c++)
                {
                    float testDiff = candidates[c] > expectedVal ? candidates[c] - expectedVal : expectedVal - candidates[c];
                    if (testDiff <= medianDiff * allowedDeviation)
                    {
                        output.Add(candidates[c]);
                        foundMatch = true;
                        break;
                    }
                }

                if (!foundMatch)
                    output.Add(badValue); 
            }

            int missingElements = expectedPeaks - output.Count; //add any missing.
            for (int i = 0; i < missingElements; i++)
            {
                output.Add(badValue);
            }

            return output.ToArray();
        }


        static float findOutliers(List<float> data, float allowedDeviation, out float[] outliers, out int medianIndex)
        {
            //returns the median difference. outlying values are given in 'outliers'
            float[] sorted = data.ToArray();
            System.Array.Sort(sorted);

            int medianElement = Mathf.RoundToInt(sorted.Length/2f);
            float median = sorted[medianElement];

            List<float> badElements = new List<float>();
            foreach (float f in sorted)
            {
                float diff = f > median ? f - median : median - f;
                if (diff > median * allowedDeviation)
                    badElements.Add(f);
            }

            outliers = badElements.ToArray();

            medianIndex = 0;
            for (int d = 0; d < data.Count; d++)
            {
                if (data[d] == median)
                    medianIndex = d;
            }

            return median;
        }
        static bool checkPeak(int first, int middle, int last, int threshold)
        {
            if (middle - first > threshold && middle - last > threshold)
                return true;
            return false;
        }       
        static float findPeakInDataPoints(List<int> dataPoints, int[] allData, float bias)
        {
            // use a weighted average to find the peak
            double total = 0;
            double div = 0;
            foreach (int d in dataPoints)
            {
                double pow = Mathf.Pow(allData[d], bias);
                total += d * pow;
                div += pow;
            }
            return (float)(total/div);
        }

    }
}