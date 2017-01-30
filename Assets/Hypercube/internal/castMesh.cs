﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;


//This script manages the final mesh that is displayed on Volume (the castMesh)
//the surface of the castMesh translates the rendered slices into a form that the Volume can display properly.

namespace hypercube
{

    [ExecuteInEditMode]
    [RequireComponent(typeof(dataFileDict))]
    public class castMesh : MonoBehaviour
    {
        public Shader sullyColorShader;

        public readonly string usbConfigPath = "volumeCalibrationData";
        public readonly string basicSettingsFileName = "settings_basic.txt";
        public readonly string perfectSlicesFileName = "settings_perfectSlices.txt";
        public readonly string calibratedSlicesFileName = "settings_calibratedSlices.txt";

        public string volumeModelName { get; private set; }
        public float volumeHardwareVer { get; private set; }

        //stored aspect ratio multipliers, each with the corresponding axis set to 1
        public Vector3 aspectX { get; private set; }
        public Vector3 aspectY { get; private set; }
        public Vector3 aspectZ { get; private set; }

        private bool hasUSBCalibration = false;
        private bool hasPCBCalibration = false;
        private bool hasPCBBasic = false;

        public int getSliceCount() { if (calibrationData == null) return 1; return calibrationData.GetLength(0); } //a safe accessor, since its accessed constantly.
        Vector2[,,] calibrationData = null;

        public bool flipX = false;  //modifier values, by the user.
        public bool flipY = false;
        public bool flipZ = false;


        private static bool _drawOccludedMode = false; 
        public bool drawOccludedMode
        {
            get
            {
                return _drawOccludedMode;
            }
            set
            {
                _drawOccludedMode = value;
                updateMesh();
            }
        }



        public float zPos = .01f;
        [Range(1, 20)]

        public GameObject sliceMesh;

        [Tooltip("The materials set here will be applied to the dynamic mesh")]
        public List<Material> canvasMaterials = new List<Material>();
        public Material occlusionMaterial;

        [HideInInspector]
        public bool usingCustomDimensions = false; //this is an override so that the canvas can be told to obey the dimensions of some particular output w/h screen other than the game window

        float customWidth;
        float customHeight;

        

        public hypercubePreview preview = null;

#if HYPERCUBE_DEV
        public calibrator currentCalibrator = null;
#endif

        public void setCalibration(Vector2[,,] data)
        {        
            if (data == null)
            {
                Debug.LogWarning("Bad calibration sent to castMesh!");
                return;
            }

            calibrationData = data;
            updateMesh();
        }

        public Material casterMaterial;


        void Start()
        {
#if !UNITY_EDITOR
            Debug.Log("Loading Hypercube Tools v" + hypercubeCamera.version + " on  Unity v" + Application.unityVersion);
#endif

            if (!preview)
                preview = GameObject.FindObjectOfType<hypercubePreview>();

            if (!loadSettingsFromUSB())
            {
                hasUSBCalibration = false;

                hasPCBBasic = false;
                hasPCBCalibration = false;

#if !HYPERCUBE_INPUT
                
#if UNITY_EDITOR               
            Debug.LogWarning("HYPERCUBE: Can't load settings. Please run: Hypercube > Load Volume Friendly Unity Prefs to allow Hypercube to read settings off the Volume USB.");
#else
            //TODO show only an interactive preview?
            Debug.LogWarning("No calibration found on USB drive, (and PCB read is not allowed due to preprocessor settings!)");
#endif
#endif
                //poll, and try to use the settings on the PCB once they come in.
            }
        }

        public void setCustomWidthHeight(float w, float h)
        {
            if (w == 0 || h == 0) //bogus values. Possible, if the window is still setting up.
                return;

            usingCustomDimensions = true;
            customWidth = w;
            customHeight = h;
        }



        public bool loadSettingsFromUSB()
        {
            hasUSBCalibration = false;

            dataFileDict d = GetComponent<dataFileDict>();

            d.clear();
           
            if (hypercube.utils.getConfigPath(usbConfigPath + "/" + basicSettingsFileName, out d.fileName) && d.load()) // (ie   G:/volumeConfigurationData/prefs.txt)
            {

                string calibrationFile = "";
                if (d.getValueAsBool("FPGA", false))
                {
                    if (hypercube.utils.getConfigPath(usbConfigPath + "/" + perfectSlicesFileName, out calibrationFile))
                        hasUSBCalibration = true;
                }
                else
                {
                    calibrationFile = calibratedSlicesFileName;
                    if (hypercube.utils.getConfigPath(usbConfigPath + "/" + calibratedSlicesFileName, out calibrationFile))
                        hasUSBCalibration = true;
                }
                    
                if (hasUSBCalibration)
                {
                    #if UNITY_EDITOR
                    UnityEditor.Undo.RecordObject(this, "Loading settings from usb file."); //these force the editor to mark the canvas as dirty and save what is loaded.
                    #endif
                    applyLoadedSettings(d);

                    //TODO apply the usb calibration
                    Vector2[,,] v ;
                    byte[] fileContents = System.IO.File.ReadAllBytes(calibrationFile);
                    castMesh.getVerticesFromBinary(fileContents, out v);
                    setCalibration(v);
                }
            }
            else 
            {
                //we failed to load the file!  ...the calling method will try the PCB now
                hasUSBCalibration = false;
            }
                
            return hasUSBCalibration;
        }


        bool applyLoadedSettings(dataFileDict d)
        {             
            volumeModelName = d.getValue("volumeModelName", "UNKNOWN!");
            volumeHardwareVer = d.getValueAsFloat("volumeHardwareVersion", -9999f);


            if (!loadCalibrationData(out calibrationData, d))
                return false;

            Shader.SetGlobalInt("_sliceCount", getSliceCount()); //let any shaders that need slice count, know what it is currently.
  
            updateMesh();       
  
            //setup input to take into account touchscreen hardware config
            input.init(d);

            //setup aspect ratios, for constraining cube scales
			setProjectionAspectRatios (
				d.getValueAsFloat ("projectionCentimeterWidth", 10f),
				d.getValueAsFloat ("projectionCentimeterHeight", 5f),
				d.getValueAsFloat ("projectionCentimeterDepth", 7f));


            //TODO these can come from the hardware
            Shader.SetGlobalFloat("_hardwareContrastMod", 1f);
            Shader.SetGlobalFloat("_sliceBrightnessR", 1f);
            Shader.SetGlobalFloat("_sliceBrightnessG", 1f);
            Shader.SetGlobalFloat("_sliceBrightnessB", 1f);

            return true;
        }

        public static bool loadCalibrationData (out Vector2[,,] _vertData, dataFileDict d)
        {
            int x,y, _articulationX, _articulationY = 0;
            return loadCalibrationData(out x, out y, out _articulationX, out _articulationY, out _vertData, d);
        }
        public static bool loadCalibrationData(out int _slicesX, out int _slicesY, out int _articulationX, out int _articulationY, out Vector2[,,] _vertData, dataFileDict d)
        {
            _slicesX = _slicesY = _articulationX = _articulationY = 0;
            _vertData = null;
            
            _slicesX = d.getValueAsInt("slicesX", 1);
            _slicesY = d.getValueAsInt("slicesY", 10);
            _articulationX = d.getValueAsInt("articulationX", 33);
            _articulationY = d.getValueAsInt("articulationY", 17);
        

            if (_slicesX < 1 || _slicesY < 1)
                return false;

            int sliceCount = _slicesX * _slicesY;


            //TODO get the vert calibration out of the file

            string[] floatDataStr = vertStr.Split(',');
            if (floatDataStr.Length != _articulationX * _articulationY * sliceCount * 2) //the 2 accounts for both x,y values
                return false;

            //recover the values from the datafile
            float[] floatData = new float[floatDataStr.Length];
            for (int f = 0; f < floatDataStr.Length; f++)
            {
                floatData[f] = dataFileDict.stringToFloat(floatDataStr[f], 0f);
            }

            _vertData = new Vector2[sliceCount, _articulationX, _articulationY];
            int c = 0;
            for (int s = 0; s < sliceCount; s++)
            {
                for (int y = 0; y <= _articulationY; y++)
                {
                    for (int x = 0; x <= _articulationX; x++)
                    {
                        _vertData[s, x, y] = new Vector2(floatData[c], floatData[c + 1]);
                        c++;
                        c++;
                    }
                }
            }

            return true;
        }

		//requires the physical dimensions of the projection, in Centimeters. Should not be public except for use by calibration tools or similar. 
#if HYPERCUBE_DEV
		public 
#endif
		void setProjectionAspectRatios(float xCm, float yCm, float zCm) 
		{
            if (xCm == 0f) //sanity check
            {
                xCm = 1f;
                Debug.LogWarning("Bad aspect ratio was given! Fixing...");
            }

            if (yCm == 0f)
            {
                yCm = 1f;
                Debug.LogWarning("Bad aspect ratio was given! Fixing...");
            }

            if (zCm == 0f)
            {
                zCm = 1f;
                Debug.LogWarning("Bad aspect ratio was given! Fixing...");
            }
                

			aspectX = new Vector3(1f, yCm/xCm, zCm/xCm);
			aspectY = new Vector3(xCm/yCm, 1f, zCm / yCm);
			aspectZ = new Vector3(xCm/zCm, yCm / zCm, 1f);
		}



        void OnValidate()
        {
            if (!sliceMesh)
                return;

            if (preview)
            {
                preview.sliceCount = getSliceCount();
                preview.sliceDistance = 1f / (float)preview.sliceCount;
                preview.updateMesh();
            }

            updateMesh();
            resetTransform();
        }

        void Update()
        {
            if (!hasPCBCalibration && !hasUSBCalibration)
            {
                if (!hasPCBBasic && input.touchPanel != null) //the touch panel pcb can store our calibration.  keep trying to get it.
                {
                    dataFileDict d = GetComponent<dataFileDict>();
                    if (input.touchPanel._getConfigData(ref d))
                    {
                        if (applyLoadedSettings(d))
                        {
                            hasPCBBasic = true;
                            if (d.getValueAsBool("useFPGA", false))
                                input.touchPanel.serial.SendSerialMessage("read1"); //ask the serial panel for the 'perfect' slices
                            else
                                input.touchPanel.serial.SendSerialMessage("read2"); //ask for calibrated slices (much larger)
                        }
                    }                      
                }
                else if (hasPCBBasic && input.touchPanel._applyHardwareCalibration(this))
                {
                    hasPCBCalibration = true;
                }                  
            }

            if (transform.hasChanged)
            {
                resetTransform();
            }
        }

        public float getScreenAspectRatio()
        {
            float w = 0f;
            float h = 0f;
            getScreenDims(ref w, ref h);
            return w / h;
        }
        public void getScreenDims(ref float w, ref float h)
        {
            if (usingCustomDimensions && customWidth > 2 && customHeight > 2)
            {
                w = customWidth;
                h = customHeight;
                return;            
            }
            w = (float)Screen.width;
            h = (float)Screen.height;
        }

        void resetTransform() //size the mesh appropriately to the screen
        {
            if (!sliceMesh)
                return;

            if (Screen.width < 1 || Screen.height < 1)
                return; //wtf.


            float xPixel = 1f / (float)Screen.width;
           // float yPixel = 1f / (float)Screen.height;

            float outWidth = (float)Screen.width;  //used in horizontal slicer
            if (usingCustomDimensions && customWidth > 2 && customHeight > 2)
            {
                xPixel = 1f / customWidth;
                //yPixel = 1f / customHeight;
                outWidth = customWidth; //used in horizontal slicer
            }

            float aspectRatio = getScreenAspectRatio();
            sliceMesh.transform.localPosition = new Vector3(-(xPixel * aspectRatio * outWidth), -1f, zPos); //this puts the pivot of the mesh at the upper left 
            sliceMesh.transform.localScale = new Vector3( aspectRatio * 2f, 2f, 1f); //the camera size is 1f, therefore the view is 2f big.  Here we scale the mesh to match the camera's view 1:1

        }

        //this is part of the code that tries to map the player to a particular screen (this appears to be very flaky in Unity)
   /*     public void setToDisplay(int displayNum)
        {
            if (displayNum == 0 || displayNum >= Display.displays.Length)
                return;

            GetComponent<Camera>().targetDisplay = displayNum;
            Display.displays[displayNum].Activate();
        }
*/


        public void setTone(float value)
        {
            if (!sliceMesh)
                return;

            MeshRenderer r = sliceMesh.GetComponent<MeshRenderer>();
            if (!r)
                return;
            foreach (Material m in r.sharedMaterials)
            {
                m.SetFloat("_Mod", value);
            }
        }


        public void updateMesh()
        {
            if (!sliceMesh || calibrationData == null)
                return;


            if (canvasMaterials.Count == 0)
            {

                //TODO this should be dynamic... filling in any gaps with the default material, connected to the default texture
                Debug.LogError("Canvas materials have not been set!  Please define what materials you want to apply to each slice in the hypercubeCanvas component.");
                return;
            }


            if (getSliceCount() > canvasMaterials.Count)
            {
                Debug.LogWarning("Can't add more than " + canvasMaterials.Count + " slices, because only " + canvasMaterials.Count + " canvas materials are defined.");
                return;
            }

            int slices = getSliceCount();

            List<Vector3> verts = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<Color> colors = new List<Color>();
            List<int[]> submeshes = new List<int[]>(); //the triangle list(s)
            Material[] faceMaterials = new Material[slices];

            //create the mesh
            int vertCount = 0;

            for (int s = 0; s < slices; s++)
            {

                //we generate each slice mesh out of 4 interpolated parts.
                List<int> tris = new List<int>();

                vertCount += generateSlice(vertCount, s, ref verts, ref tris, ref uvs, ref colors); 

                submeshes.Add(tris.ToArray());

                //every face has a separate material/texture  
                if (_drawOccludedMode)
                    faceMaterials[s] = occlusionMaterial; //here it just uses 1 material, but the slices have different uv's if we are in occlusion mode
                else if (!flipZ)
                    faceMaterials[s] = canvasMaterials[s]; //normal
                else
                    faceMaterials[s] = canvasMaterials[slices - s - 1]; //reverse z
            }


            MeshRenderer r = sliceMesh.GetComponent<MeshRenderer>();
            if (!r)
                r = sliceMesh.AddComponent<MeshRenderer>();

            MeshFilter mf = sliceMesh.GetComponent<MeshFilter>();
            if (!mf)
                mf = sliceMesh.AddComponent<MeshFilter>();

            Mesh m = mf.sharedMesh;
            if (!m)
                return; //probably some in-editor state where things aren't init.
            m.Clear();

            m.SetVertices(verts);
            m.SetUVs(0, uvs);

            m.subMeshCount = slices;
            for (int s = 0; s < slices; s++)
            {
                m.SetTriangles(submeshes[s], s);
            }

            //normals are necessary for the transparency shader to work (since it uses it to calculate camera facing)
            Vector3[] normals = new Vector3[verts.Count];
            for (int n = 0; n < verts.Count; n++)
                normals[n] = Vector3.forward;

            m.normals = normals;

#if HYPERCUBE_DEV

            if (currentCalibrator && currentCalibrator.gameObject.activeSelf && currentCalibrator.enabled)
                r.materials = currentCalibrator.getMaterials();
            else
#endif
                r.materials = faceMaterials; //normal path

            m.RecalculateBounds();
        }

        //this is used to generate each of 4 sections of every slice.
        //therefore 1 central column and 1 central row of verts are overlapping per slice, but that is OK.  Keeping the interpolation isolated to this function helps readability a lot
        //returns amount of verts created
        int generateSlice(int startingVert, int slice, ref  List<Vector3> verts, ref List<int> triangles, ref List<Vector2> uvs, ref List<Color> colors)
        {
            int vertCount = 0;
            int xTesselation = calibrationData.GetLength(1);
            int yTesselation = calibrationData.GetLength(2);
            for (var y = 0; y < yTesselation; y++)
            {
                for (var x = 0; x < xTesselation; x++)
                {
                    //add it
                    verts.Add(new Vector3(calibrationData[slice, x, y].x, calibrationData[slice, x, y].y, 0f)); //note these values are 0-1
                    vertCount++;
                }
            }

            //triangles
            //we only want < tesselation because the very last verts in both directions don't need triangles drawn for them.
            int currentTriangle = 0;
          //  int xspot = xTesselation - 1;
         //   int yspot = yTesselation - 1;
            for (var y = 0; y < yTesselation -1; y++)
            {
                for (int x = 0; x < xTesselation -1; x++)
                {
                    currentTriangle = startingVert + x;
                    triangles.Add(currentTriangle + ((y + 1) * xTesselation)); //bottom left
                    triangles.Add((currentTriangle + 1) + (y * xTesselation));  //top right
                    triangles.Add(currentTriangle + (y * xTesselation)); //top left

                    triangles.Add(currentTriangle + ((y + 1) * xTesselation)); //bottom left
                    triangles.Add((currentTriangle + 1) + ((y + 1) * xTesselation)); //bottom right
                    triangles.Add((currentTriangle + 1) + (y * xTesselation)); //top right
                }
            }

            //uvs
            float UVW = 1f / (float)(xTesselation -1); //-1 makes sure the UV gets to the end
            float UVH = 1f / (float)(yTesselation -1 );
            for (var y = 0; y < yTesselation; y++)
            {
                for (var x = 0; x < xTesselation; x++)
                {
                    Vector2 targetUV = new Vector2(x * UVW, y * UVH);  //0-1 UV target

                    //TODO handle flipping! preferably in the materials
                    //add lerped uv
                   // float xLerp = Mathf.Lerp(topLeftUV.x, bottomRightUV.x, targetUV.x);
                   // float yLerp = Mathf.Lerp(topLeftUV.y, bottomRightUV.y, targetUV.y);
                    uvs.Add(targetUV);

                    colors.Add(new Color(targetUV.x, targetUV.y, slice * .001f, 1f)); //note the current slice is stored in the blue channel
                }
            }

            return vertCount;
        }


        public void generateSullyTextures(int w, int h, string filePath)
        {
            Camera c = GetComponent<Camera>();
            RenderTexture rtt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGBFloat);
            c.targetTexture = rtt;
            c.RenderWithShader(sullyColorShader, "");


            Texture2D rTex = new Texture2D(w, h, TextureFormat.RGBAFloat, false);
            RenderTexture.active = rtt;
            rTex.ReadPixels(new Rect(0f, 0f, w, h), 0, 0, false);
            rTex.Apply();

            int slices = getSliceCount();
            Color clr = new Color();
            Color32[] xColors = new Color32[w * h];
            Color32[] yColors = new Color32[w * h];
            int n = 0;
            for (int y = h - 1; y >= 0; y--)
            {
                for (int x = 0; x < w; x++)
                {
                    clr = rTex.GetPixel(x, y);
                    xColors[n] = floatToColor(clr.r * w);
                    yColors[n] = floatToColor(
                        ((clr.g / slices) * h) +  //the position within the current slice
                        (((clr.b * 1000) / (float)slices) * (float)h));  //plus the slice's height position within the entire image. The 10 is because the slice number is encoded as .1 per slice

                    n++;
                }
            }

            Texture2D xTex = new Texture2D(w, h, TextureFormat.RGBAFloat, false);
            xTex.SetPixels32(xColors);
            xTex.Apply();
            Texture2D yTex = new Texture2D(w, h, TextureFormat.RGBAFloat, false);
            yTex.SetPixels32(yColors);
            yTex.Apply();

            c.targetTexture = null;

            System.IO.File.WriteAllBytes(filePath + "/xSullyTex.png", xTex.EncodeToPNG());
            System.IO.File.WriteAllBytes(filePath + "/ySullyTex.png", yTex.EncodeToPNG());

            // testMaterial.SetTexture("_MainTex", xTex);  //test
            // testMaterial2.SetTexture("_MainTex", yTex);
        }


        //converts stored binary calibration data into a proper array for the castMesh
        public static bool getVerticesFromBinary(string inBinaryData, out Vector2[,,] outData)
        {
            byte[] rawBytes = System.Text.Encoding.Unicode.GetBytes(inBinaryData);
            return getVerticesFromBinary(rawBytes, out outData);
        }
        public static bool getVerticesFromBinary(byte[] rawBytes, out Vector2[,,] outData)
        {
            
            int sliceCount = System.BitConverter.ToInt32(rawBytes, 0);
            int xArticulation = System.BitConverter.ToInt32(rawBytes, 4);
            int yArticulation = System.BitConverter.ToInt32(rawBytes, 8);

            if (rawBytes.Length % 4 != 0)
            {
                Debug.LogWarning("The data received for the slices is malformed.");
                outData = null;
                return false;
            }

            int count = rawBytes.Length / 4;
            if (count - 3 != sliceCount * xArticulation * yArticulation * 2) //the count -3 = don't count the header info   ... the *2 = x and also y
            {
                Debug.LogWarning("The data received for the slices does not match the expected array length.");
                outData = null;
                return false;
            }

            outData = new Vector2[sliceCount, xArticulation, yArticulation];

            for (int s = 0; s < sliceCount; s++)
            {
                for (int y = 0; y < yArticulation; y++)
                {
                    for (int x = 0; x < xArticulation; x++)
                    {
                        int i = 12;  //start at the end of the header.
                        i += (s * xArticulation * yArticulation * 2) + (y * xArticulation * 2) + (x * 2);

                        outData[s, x, y] = new Vector2();
                        outData[s, x, y].x = System.BitConverter.ToSingle(rawBytes, i);
                        outData[s, x, y].y = System.BitConverter.ToSingle(rawBytes, i + 1);
                    }
                }
            }

            return true;
        }

        //this method maps a float into a rgb 24 bit color space with max value of 4096
        static Color32 floatToColor(float v)
        {
            if (v < 0 || v > 4096)
                Debug.LogError("Invalid value of " + v + " passed into the sully calibration texture generation! Must be 0-4096");

            byte r = (byte)((int)v >> 4); //this assumes the maximum value will never pass 0-4096 (12 bits)
            byte g = (byte)(((int)v & 0x0F) << 4); //the least significant part of the integer part of the float stored at the start of g
            double rawFrac = v - (int)v;
            int fracV = (int)(4096 * rawFrac);  //just the fractional part, scaled up to 12 bits
            g |= (byte)(fracV >> 8); //the most significant part of fractional part of the float stored in the back of g
            byte b = (byte)(fracV & 0xFF);
            return new Color32(r, g, b, byte.MaxValue);

        }

    }

}