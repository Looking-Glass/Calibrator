using UnityEngine;
using System.Collections;
using System;

namespace hypercube
{
    public class sampleLines_inline : autoCalibratorModule
    {
        float lineSpeed = 1f;
        float lineThickness = 30f;
        float pos;

        public float bias;

        bool horz = true;

        Vector2[,,] output; //what we think is the corresponding positions of every sensor

        public override float getRelevantValue()
        {
            return pos + (lineThickness/2f);
        }

        public override void start(autoCalibrator a)
        {
            //init the output vector.
            output = new Vector2[a.canvas.getSliceCount(), a.xArticulation, a.yArticulation];
            for (int x = 0; x < output.GetLength(0); x++)
                for (int y = 0; y < output.GetLength(1); y++)
                    for (int z = 0; z < output.GetLength(2); z++)
                        output[x,y,z] = new Vector2();

            float screenW = 0f;
            float screenH = 0f;
            a.canvas.getScreenDims(ref screenW, ref screenH);

            pos = screenH;
            horz = true;
            a.setLine(0f,0f, 1f,0f, true, false); //the line is off.

            a.resetCollectedData();
        }

        public override void update(autoCalibrator a)
        {
            float screenW = 0f;
            float screenH = 0f;
            a.canvas.getScreenDims(ref screenW, ref screenH);

            if (horz)
            {
                if (pos < -lineThickness) //we're done, let's collect the data.
                {
           //         handleData(a, false);
                    horz = false;
                    pos = screenW;
                }

                pos -= lineSpeed;
                a.setLine(0f, pos, screenW, lineThickness, horz);       
            }
            else
            {
                if (pos < -lineThickness) //we finished.
                {                  
           //         handleData(a, true);
                    a.setModule(null); //exit ourselves.
                }

                pos -= lineSpeed;
                a.setLine(pos, 0f, lineThickness, screenH, horz);
            }
        }


  
        /*
        void handleData(autoCalibrator a, bool xData)
        {

            #if CALIBRATOR_DEBUG
            int recordSize = a.getRecord(0,0).size;
            System.Text.StringBuilder[] lines = new System.Text.StringBuilder[recordSize + 1]; //debug .. the +1 allows for a header line at the top of the csv
            lines[0].Append("Pixel Value, ");
            for (int r = 1; r <= recordSize; r++)
            {
                lines[r].Append(a.getRecord(0,0).getPixel(r - 1) + ", "); 
            }
            #endif
            for (int x = 0; x < output.GetLength(0); x++)
            {
                for (int y = 0; y < output.GetLength(1); y++)
                {
                    float[] values = a.findDataPeaks(a.canvas.getSliceCount(), x, y);
                    if (values.Length != a.canvas.getSliceCount())
                        Debug.LogError("Received peak data of incorrect length! Not all slices were accounted for.");               

                    for (int z = 0; z < values.Length; z++)
                    {
                        if (xData)
                            output[x, y, z].x = values[z];
                        else
                            output[x, y, z].y = values[z];                     
                    }

                    #if CALIBRATOR_DEBUG
                    sensorData d = a.getRecord(x, y);
                    lines[0].Append("Sensor x" + x + " y" + y +", ");
                    for (int r = 1; r <= d.size; r++)
                    {
                        lines[r].Append(a.getRecord(x, y).getData(r-1) + ", "); //debug only
                    }
                    #endif                   
                }
            }

            #if CALIBRATOR_DEBUG
            System.Text.StringBuilder CSVoutput = new System.Text.StringBuilder();
            foreach (System.Text.StringBuilder s in lines)
            {
                CSVoutput.Append(s + "\n");
            }
            if (horz)
            {
                System.IO.File.WriteAllText("Y Detections", CSVoutput.ToString());
                Debug.Log("Wrote data to CSV: Y Detections");
            }
            else
            {
                System.IO.File.WriteAllText("X Detections", CSVoutput.ToString());
                Debug.Log("Wrote data to CSV: X Detections");
            }
            #endif
        }
        */

        public override void end(autoCalibrator a)
        {
       /*     //let's line up the pieces to see if they line up

            int i = 0;
            for (int x = 0; x < output.GetLength(0); x++)
            {
                for (int y = 0; y < output.GetLength(1); y++)
                {
                    for (int z = 0; z < output.GetLength(2); z++)
                    {
                        Vector3 pos = a.screenCoordToLocalCoord(output[x,y,z].x, output[x,y,z].y);
                        pos.z = z * .1f; //put them back so we can visualize if we have things right.
                        a.indicators[i].transform.localPosition = pos;
                        i++;
                    }
                }
            }
            */
        }
    }
}