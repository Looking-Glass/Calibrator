using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace hypercube
{
	public class vertexCalibrator : calibrator
    {
		public float sensitivity = .1f;
        public float dotSize;
		public bool flipX = false;
		public bool flipY = false;

	    int articulationX;
	    int articulationY;
	    int slicesX;
	    int slicesY;

        public Shader sliceShader;
        public int renderResolution = 1024;

        int selectionS;
        int selectionX;
        int selectionY;
   
        uint[][] xOptions;
        uint[][] yOptions;

        Vector2[,,] vertices;
        Vector2[,,] virginVertices; //the untouched values. Instead of recalculating where on the mesh we are at each time if we need to reset some value(s), keeping them here is an easy and flexible way to do so.

        int displayLevelX = 0;  //the current detail display level. Valid values are -articulations.size to 0, since it functions as an index to xOptions and yOptions
		int displayLevelY = 0;

        public Camera renderCam;
        public float cameraOffset;
        Material[] sliceMaterials = null;
        RenderTexture[] sliceTextures = null;

//        float dotAspect = 1f;

        public static int[] articulations = {3,5,9,17,33,65,129,257,513};
        public static int articulationLookup(int val)
        {
            for (int a = 0; a < articulations.Length; a++)
            {
                if (articulations[a] == val)
                    return a;
            }
            return -1; 
        }
        //static int getVertsFromDivLevel(int d)
        //{
        //    if (d == 0)
        //        return 0;
        //    if (d == 1)
        //        return 2;

        //    return (getVertsFromDivLevel(d - 1) * 2) - 1;
        //}

        public override Material[] getMaterials()
        {
            return sliceMaterials;
        }

        public GameObject dotMesh;
        public GameObject selectionMesh;
        public float scaleSensitivity = .01f;

        GameObject[] dotMeshes;

        Vector3 lastMousePos; //used to calculate mouse controls

        public override void OnEnable()
        {

            dataFileDict d = canvas.GetComponent<dataFileDict>();


            if (castMesh.loadCalibrationData(out slicesX, out slicesY, out articulationX, out articulationY, out vertices, d))
            {
                resetOriginalVertexOffsets(); //reset just 'virgin' data
            }
            else
            {
                //these are set by the basic settings, so we can read them out here.
                slicesX = d.getValueAsInt("slicesX", 1);
                slicesY = d.getValueAsInt("slicesY", 10);
                articulationX = d.getValueAsInt("articulationX", 33);
                articulationY = d.getValueAsInt("articulationY", 17);

                //if something is wrong or incoherent, reset everything to some default.
                //this should almost never happen.
                if (slicesX < 1)
                    slicesX = 1;
                if (slicesY < 1)
                    slicesY = 1;

                if (articulationLookup(articulationX) == -1) //ensure that our articulations are only of the allowed, otherwise calibration will be too confusing
                    articulationX = articulations[4];
                if (articulationLookup(articulationY) == -1)
                    articulationY = articulations[3];

                resetOriginalVertexOffsets(true); //change both vertices and virginVertices

            }

            //configure ourselves
            //the following lines will tell us what elements to highlight when we toggle between levels of detail
            setOptions(out xOptions, articulationX);
            setOptions(out yOptions, articulationY);

            updateTextures();
            canvas.setCalibration(vertices);

            base.OnEnable();
        }

        //public float[] getCalibrationData()
        //{
        //    int sliceCount = slicesX * slicesY ;
        //    float[] data = new float[sliceCount * articulationX * articulationY * 2];
        //    uint d = 0;
        //    for (int s = 0; s < sliceCount; s++)
        //    {
        //        for (int y = 0; y <= articulationY; y++)
        //        {
        //            for (int x = 0; x <= articulationX; x++)
        //            {
        //                data[d] = vertices[s, x, y].x;
        //                d++;
        //                data[d] = vertices[s, x, y].y;                     
        //                d++;
        //            }
        //        }
        //    }
        //    return data;
        //}




        //public void skew()
        //{
        //    //skews
        //    topM.x += skews[s].x;
        //    lowM.x -= skews[s].x;
        //    midL.y += skews[s].y;
        //    midR.y -= skews[s].y;

        //    //interpolate the alternate axis on the skew so that edges will always be straight ( fix elbows caused when we skew)
        //    topM.y = Mathf.Lerp(topL.y, topR.y, Mathf.InverseLerp(topL.x, topR.x, topM.x));
        //    lowM.y = Mathf.Lerp(lowL.y, lowR.y, Mathf.InverseLerp(lowL.x, lowR.x, lowM.x));
        //    midL.x = Mathf.Lerp(topL.x, lowL.x, Mathf.InverseLerp(topL.y, lowL.y, midL.y));
        //    midR.x = Mathf.Lerp(topR.x, lowR.x, Mathf.InverseLerp(topR.y, lowR.y, midR.y));
        //}

        //public void bow()
        //{
        //    //add bow distortion compensation
        //    //bow is stored as top,bottom,left,right  = x y z w
        //    float bowX = 0f;
        //    float bowY = 0f;
        //    float xBowAmount = 0f;
        //    float yBowAmount = 0f;
        //    float averageBowX = (bow.z + bow.w) / 2f;
        //    float averageBowY = (bow.x + bow.y) / 2f;
        //    if (o == shardOrientation.UL)//phase: 1 1
        //    {
        //        xBowAmount = Mathf.Lerp(bow.z, averageBowX, columnLerpValue); //left
        //        yBowAmount = Mathf.Lerp(bow.x, averageBowY, rowLerpValue);  //top
        //        bowX = (1f - Mathf.Cos(1f - rowLerpValue)) * xBowAmount;
        //        bowY = (1f - Mathf.Cos(1f - columnLerpValue)) * yBowAmount;
        //    }
        //    else if (o == shardOrientation.UR)//phase: 1 0
        //    {
        //        xBowAmount = Mathf.Lerp(bow.w, averageBowX, 1f - columnLerpValue); //right
        //        yBowAmount = Mathf.Lerp(bow.x, averageBowY, rowLerpValue);  //top
        //        bowX = (1f - Mathf.Cos(1f - rowLerpValue)) * xBowAmount;
        //        bowY = (1f - Mathf.Cos(0f - columnLerpValue)) * yBowAmount;
        //    }
        //    else if (o == shardOrientation.LL)//phase: 0 1
        //    {
        //        xBowAmount = Mathf.Lerp(bow.z, averageBowX, columnLerpValue); // *rowLerpValue; //left
        //        yBowAmount = Mathf.Lerp(bow.y, averageBowY, 1f - rowLerpValue);  //bottom
        //        bowX = (1f - Mathf.Cos(0f - rowLerpValue)) * xBowAmount;
        //        bowY = (1f - Mathf.Cos(1f - columnLerpValue)) * yBowAmount;
        //    }
        //    else if (o == shardOrientation.LR)//phase: 0 0
        //    {
        //        xBowAmount = Mathf.Lerp(bow.w, averageBowX, 1f - columnLerpValue);//right
        //        yBowAmount = Mathf.Lerp(bow.y, averageBowY, 1f - rowLerpValue);  //bottom
        //        bowX = (1f - Mathf.Cos(0f - rowLerpValue)) * xBowAmount;
        //        bowY = (1f - Mathf.Cos(0f - columnLerpValue)) * yBowAmount;
        //    }

        //    bowX -= xBowAmount * .5f; //the lines above pivot the bowing on the centerpoint of the slice. The two following lines change the pivot to the corner points of articulation so that the center is what moves.
        //    bowY -= yBowAmount * .5f;
        //    lerpedVector.x += bowX;
        //    lerpedVector.y += bowY;
        //    //end bow distortion compensation
        //}


        public void resetOriginalVertexOffsets(bool alsoNonOriginal = false)
        {
            selectionS = selectionX = selectionY = 0; //reset selection

            int sliceCount = slicesX * slicesY;
            vertices = new Vector2[sliceCount, articulationX, articulationY];
            virginVertices = new Vector2[sliceCount, articulationX, articulationY];

            float sliceW = 1f/ slicesX;
            float sliceH = 1f/ slicesY;
            float aW = 1f/(articulationX - 1); //the -1 is because we want that last vert to end up on the edge
            float aH = 1f/(articulationY - 1);
            aW *= sliceW; //normalize the articulation w/h to the 'world' space
            aH *= sliceH;
//            dotAspect = sliceW/sliceH;

            int sliceIndex = 0;
            for (int y = 0; y < slicesY; y++)
            {
                for (int x = 0; x < slicesX; x++)
                {
                    float sliceX = x * sliceW;
                    float sliceY = y * sliceH;
                    for (int ay = 0; ay < articulationY; ay++)
                    {
                        for (int ax = 0; ax < articulationX; ax++)
                        {
                            sliceIndex = x + (y * slicesX);
                            virginVertices[sliceIndex, ax, ay ] =   new Vector2(sliceX + (ax * aW), sliceY + (ay * aH));
                            if (alsoNonOriginal)
                                vertices[sliceIndex, ax, ay] =      new Vector2(sliceX + (ax * aW), sliceY + (ay * aH));
                        }
                    }
                }
            }
        }



        //reset every point of articulation in this slice only
        public void resetVertexOffsets(int slice)
        {
            for (int y = 0; y < virginVertices.GetLength(2); y++)
            {
                for (int x = 0; x < virginVertices.GetLength(1); x++)
                {
                    resetVertexOffset(slice, x, y);
                }
            }
        }

        public void resetVertexOffset(int slice, int xVert, int yVert)
        {
            vertices[slice, xVert, yVert].x = virginVertices[slice, xVert, yVert].x;
            vertices[slice, xVert, yVert].y = virginVertices[slice, xVert, yVert].y;
        }

        void Update()
        {
			if (Input.GetKeyDown (KeyCode.Alpha1))
				sensitivity = .00002f;
			else if (Input.GetKeyDown (KeyCode.Alpha2))
				sensitivity = .0002f;
			else if (Input.GetKeyDown (KeyCode.Alpha3))
				sensitivity = .002f;
			else if (Input.GetKeyDown (KeyCode.Alpha4))
				sensitivity = .015f;
			else if (Input.GetKeyDown (KeyCode.Alpha5))
				sensitivity = .15f;
            else if (Input.GetKeyDown(KeyCode.Equals)) // increase detail
            {
				int oldX = displayLevelX;
				int oldY = displayLevelY;
				displayLevelX++;
				displayLevelY++;

				validateDisplayLevel ();
				if (displayLevelX != oldX) 
					selectionX *= 2;
				if (displayLevelY != oldY)
					selectionY *= 2;
				
				updateTextures ();                 
            }
            else if (Input.GetKeyDown(KeyCode.Minus)) //decrease detail
            {
				int oldX = displayLevelX;
				int oldY = displayLevelY;
				displayLevelX--;
				displayLevelY--;

				validateDisplayLevel ();
				if (displayLevelX != oldX) 
					selectionX /= 2;
				if (displayLevelY != oldY)
					selectionY /= 2;

				updateTextures ();
            }
            else if (Input.GetKeyDown(KeyCode.W))
            {
                selectionY--;
                if (selectionY < 0)
                    selectionY = 0;

                updateTextures();
            }
            else if (Input.GetKeyDown(KeyCode.S))
            {
                selectionY++;
                if (selectionY >= articulationY)
                    selectionY = articulationY - 1;
                updateTextures();
            }
            else if (Input.GetKeyDown(KeyCode.A))
            {
                selectionX--;
                if (selectionX < 0)
                    selectionX = 0;
                updateTextures();
            }
            else if (Input.GetKeyDown(KeyCode.D))
            {
                selectionX++;
                if (selectionX >= articulationX)
                    selectionX = articulationX - 1;
                updateTextures();
            }
            else if (Input.GetKeyDown(KeyCode.R))
            {
                selectionS++;
                if (selectionS >= slicesX * slicesY)
                    selectionS = 0;
                updateTextures();
            }
            else if (Input.GetKeyDown(KeyCode.F))
            {
                selectionS--;
                if (selectionS < 0)
                    selectionS = (slicesX * slicesY) - 1;
                updateTextures();
            }


            if (Input.GetKey(KeyCode.Mouse1))
            {
                Vector3 diff = lastMousePos - Input.mousePosition;
                if (diff != Vector3.zero)
                {
                    dotSize += (-diff.x - diff.y ) * Time.deltaTime * scaleSensitivity; 
                    updateTextures();
                }
            }

			//move a vert with mouse
			if (Input.GetKey(KeyCode.Mouse0))
			{
				Vector3 diff = lastMousePos - Input.mousePosition;
				if (diff != Vector3.zero)
				{
					diff *= Time.deltaTime * sensitivity;
					if (flipY)
						diff.y = -diff.y;
					if (flipX)
						diff.x = -diff.x;
					vertices [selectionS, xOptions [displayLevelX][selectionX], yOptions [displayLevelY][selectionY]].x += diff.x;
					vertices [selectionS, xOptions [displayLevelX][selectionX], yOptions [displayLevelY][selectionY]].y += diff.y;
					canvas.setCalibration (vertices);
				}
			}

            lastMousePos = Input.mousePosition;
        }



        //this method returns an array containing what elements should be highlighted given each possible detail level
        static void setOptions(out uint[][] option, int maxOption)
        {
            int lookup = articulationLookup(maxOption);
            if (lookup == -1)
                Debug.LogError("Invalid maxOption provided when setting up the vertex calibrator, the choices must be one of the provided values.");

            List<uint[]> allOptions = new List<uint[]>();

            //start the first element, it holds every single element allowed at the given max
            uint[] current = new uint[maxOption];
            for (uint i = 0; i < maxOption; i++)
            {
                current[i] = i; //this array holds every single element
            }
            allOptions.Add(current);

            //now we will add lists that only contain alternating members from the previous list, down to 2.
            while(allOptions[allOptions.Count - 1].Length > 2)
            {
                bool odd = true;
                List<uint> newOption= new List<uint>();
                int count = allOptions[allOptions.Count - 1].Length; //length of the last option
                for (int i = 0; i < count; i++)
                {
                    if (odd)
                    {
                        newOption.Add(allOptions[allOptions.Count - 1][i]); //add the alternate elements                       
                    }
                    odd = !odd;
                }
                allOptions.Add(newOption.ToArray());
            }

            allOptions.Reverse(); //its just handier to work with from low to high.
            option = allOptions.ToArray();
        }

        void validateDisplayLevel()
        {
			if (displayLevelX < 0) //low limit
				displayLevelX = 0;
			if (displayLevelY < 0) //low limit
				displayLevelY = 0;

			displayLevelX = Mathf.Min(displayLevelX, xOptions.Length - 1); //account for different limits between x/y
			displayLevelY = Mathf.Min(displayLevelY, yOptions.Length - 1);

        }

        void updateTextures()
        {
            validateDisplayLevel();

			selectionX = Mathf.Min(selectionX, xOptions[displayLevelX].Length - 1);
			selectionY = Mathf.Min(selectionY, yOptions[displayLevelY].Length - 1);

            //begin mesh creation
            //build one mesh per slice, to not run out of verts
            int sliceCount = slicesX * slicesY;

            //first, recreate the gameObject and mesh arrays if our slice number has changed
            if (sliceMaterials == null || sliceTextures == null || sliceMaterials.Length != sliceCount || sliceTextures.Length != sliceCount) 
            {
                //clean up any last run
                if (sliceMaterials != null || sliceTextures != null)
                {
                    foreach (Material m in sliceMaterials)
                    {
                        Destroy(m);
                    }
                    foreach(RenderTexture r in sliceTextures)
                    {
                        Destroy(r);
                    }
                }

                sliceMaterials = new Material[sliceCount];
                sliceTextures = new RenderTexture[sliceCount];
                for (int s = 0; s < sliceCount; s++)
                {
                    sliceMaterials[s] = new Material(sliceShader);
                    sliceTextures[s] = new RenderTexture(renderResolution, renderResolution, 24, RenderTextureFormat.ARGB32);
                    sliceMaterials[s].SetTexture("_MainTex", sliceTextures[s]);
                }
            }

            renderCam.gameObject.SetActive(true);
            for (int s = 0; s < sliceCount; s++)
            {
                renderSlice(s, xOptions[displayLevelX].Length, yOptions[displayLevelY].Length);
            }
            renderCam.gameObject.SetActive(false);
        }

        void renderSlice(int slice, int xDiv, int yDiv)
        {
            //first make sure we have the required number of dots.
            int dotCount = xDiv * yDiv;
            if (dotMeshes == null || dotMeshes.Length != dotCount)
            {
                if (dotMeshes != null) //clear it out if it exists.
                {
                    foreach (GameObject g in dotMeshes)
                    {
                        g.transform.localPosition = new Vector3(0f, 0f, -100f); //make sure they don't show up in the new render.
                        Destroy(g);
                    }
                        
                }
                
                dotMeshes = new GameObject[dotCount];
                for (int dot = 0; dot < dotCount; dot++)
                {
                    dotMeshes[dot] = (GameObject)Instantiate(dotMesh, renderCam.transform.parent);
                    dotMeshes[dot].name = "dot " + dot;
                }
            }

            if (dotSize < 0f)
                dotSize = 0f;
            if (dotSize > .75f)
                dotSize = .75f;

            for (int dot = 0; dot < dotCount; dot++)
            {
                dotMeshes[dot].transform.localScale = new Vector3(dotSize, dotSize, dotSize); //y * dotOFfset
            }

            //lay out the dots
            float w = 1f / (xDiv - 1); //the -1 is because we want that last vert to end up on the edge
            float h = 1f / (yDiv - 1);
            int d =0;
            for (int y = 0; y < yDiv; y++)
            {
                for (int x = 0; x < xDiv; x++)
                {
                    dotMeshes[d].transform.localPosition = new Vector3(x * w * 2f, y * h *2f, cameraOffset); //the 2f sizes the content to a camera of size 1.
                    d++;
                }
            }


            //put the selection
            if (slice == selectionS)
                selectionMesh.transform.localPosition = new Vector3(selectionX * w * 2f, selectionY * h * 2f, cameraOffset);
            else
                selectionMesh.transform.localPosition = new Vector3(0f, 0f, -10f); //hide it behind the camera

            selectionMesh.transform.localScale = new Vector3(dotSize, dotSize, dotSize); //y * dotOFfset

            renderCam.targetTexture = sliceTextures[slice];
            renderCam.rect = new Rect(0f, 0f, 1f, 1f);
            renderCam.Render();
            renderCam.targetTexture = null;
        }



    }
}
