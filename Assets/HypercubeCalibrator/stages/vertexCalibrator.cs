using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace hypercube
{

	public class vertexCalibrator : calibrator
    {
        public enum renderCondition
        {
            OFF = 0,
            CURRENT_SLICE,
            ALL_SLICES,         
            INVALID
        }

        public basicSettingsAdjustor basicSettings;  //make it possible to let it know if there were updates from the pcb

        public renderCondition renderDots;
        public renderCondition renderNum;
        public renderCondition renderBgImage;
        
        public Material[] bgMaterials;
        public int currentBgMaterial;

		public float sensitivity = .1f;
        public float dotSize;

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

        Vector2[,,] vertices = null;
        Vector2[,,] perfectVertices = null; //the untouched values. Instead of recalculating where on the mesh we are at each time if we need to reset some value(s), keeping them here is an easy and flexible way to do so.
        vertexCalibratorUndoManager undoMgr = null;


        int displayLevelX = 0;  //the current detail display level. Valid values are -articulations.size to 0, since it functions as an index to xOptions and yOptions
		int displayLevelY = 0;
        bool cubeMode = true; //cubeMode is basically displayLevel -1.  It only allows selection of the 4 corners of the first and last slice, and always interpolates everything across Z
        bool xOptionContains(int v)
        {
            foreach (int o in xOptions[displayLevelX])
            {
                if (v == o)
                    return true;
            }
            return false;
        }
        bool yOptionContains(int v)
        {
            foreach (int o in yOptions[displayLevelY])
            {
                if (v == o)
                    return true;
            }
            return false;
        }

        void getNearestOptionX(uint v, out uint less, out uint greater)
        {
            less = 0;
            greater = 0;
            foreach (uint o in xOptions[displayLevelX])
            {
                if (v == o)
                {
                    less = v;
                    greater = v;
                    return;
                }
                if (o < v)
                    less = o; 
                else
                {
                    greater = o;
                    return;
                }
            }
        }
        void getNearestOptionY(uint v, out uint less, out uint greater)
        {
            less = greater = 0;
            foreach (uint o in yOptions[displayLevelY])
            {
                if (v == o)
                {
                    less = v;
                    greater = v;
                    return;
                }
                if (o < v)
                    less = o; 
                else
                {
                    greater = o;
                    return;
                }
            }
        }


        public GameObject dotParent;
        public TextMesh sliceNumText;
        public GameObject background;
        public Camera renderCam;
        public float cameraOffset;
        Material[] sliceMaterials = null;
        RenderTexture[] sliceTextures = null;


        public static readonly int[] articulations = {3,5,9,17,33,65,129,257,513};
        public static int articulationLookup(int val)
        {
            for (int a = 0; a < articulations.Length; a++)
            {
                if (articulations[a] == val)
                    return a;
            }
            return -1; 
        }

        public override Material[] getMaterials()
        {
            return sliceMaterials;
        }

        public GameObject dotMesh;
        public GameObject selectionMesh;
        public LineRenderer cubeModeMesh;
        public float scaleSensitivity = .01f;

        GameObject[] dotMeshes;

        Vector3 lastMousePos; //used to calculate mouse controls

        void Awake()
        {
            undoMgr = new vertexCalibratorUndoManager();
        }

        public override void OnEnable()
        {

            dataFileDict d = canvas.GetComponent<dataFileDict>();

            //these are set by the basic settings, so we can read them out here.
            slicesX = d.getValueAsInt("slicesX", 1);
            slicesY = d.getValueAsInt("slicesY", 10);
            articulationX = d.getValueAsInt("articulationX", 33);
            articulationY = d.getValueAsInt("articulationY", 17);

            selectionS = selectionX = selectionY = 0;

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

            resetVertexData(true); //reset just perfect data data

            if (vertices == null || 
                slicesX * slicesY != vertices.GetLength(0) ||  //if any of these don't match, we need to rebuild the calibration from scratch.
                articulationX != vertices.GetLength(1) ||
                articulationY != vertices.GetLength(2)
                )
            {
                resetVertexData(false); //change the calibrated vertices
                messageWindow.showMessage("A previous calibration was loaded, but it was incompatible with the settings given in the menu so it will be ignored.");
            }


            //configure ourselves
            //the following lines will tell us what elements to highlight when we toggle between levels of detail
            setOptions(out xOptions, articulationX);
            setOptions(out yOptions, articulationY);

            castMesh.canvas.applyLoadedSettings(d); //in case texture res has changed.

            updateTextures();
            canvas._setCalibration(vertices);
            undoMgr.recordUndo(vertices);

            base.OnEnable();
        }

        //pcb just gave us a calibration... try to use it.
        public void setLoadedVertices(Vector2[,,] newVerts, bool pcbUsb)
        {
            if (newVerts == null)
                return;


            if (perfectVertices == null) //we haven't opened the vertexCalibration menu yet, just supply this data.
            {
                if (pcbUsb)
                    basicSettings.usbText.color = Color.green;
                else
                    basicSettings.pcbText.color = Color.green;

                vertices = newVerts;
                canvas._setCalibration(vertices);
            }
            else if ( slicesX * slicesY == newVerts.GetLength(0) &&  //we are currently editing vertices. if any of these don't match, ignore this new data.
            articulationX == newVerts.GetLength(1) &&
            articulationY == newVerts.GetLength(2))
            {                         
                if (pcbUsb)
                    basicSettings.usbText.color = Color.green;
                else
                    basicSettings.pcbText.color = Color.green;

                vertices = newVerts; //update the current verts with the new ones.
                canvas._setCalibration(vertices);
            }
            else //perfectVertices != null && the data doesn't match.
                messageWindow.showMessage("Calibration was found, but was incompatible with the settings from the menu so it will be ignored.");
         
        }


        public bool getXFlip(int slice) //is the current output flipped relative to the final output?
        {
            if (vertices[slice, 0, 0].x < vertices[slice, articulationX - 1, 0].x)
                return false;
            else
                return true;
        }
        public bool getYFlip(int slice) //is the current output flipped relative to the final output?
        {
            if (vertices[slice, 0, 0].y < vertices[slice, 0, articulationY -1].y)
                return false;
            else
                return true;
        }

        public void flipVertsX()
        {
            int slices = slicesX * slicesY;

            for (int i = 0; i < slices; i++)
                flipVertsX(i);
            canvas._setCalibration(vertices);
            updateTextures();
        }
        public void flipVertsX(int slice)
        {

            Vector2[,] originalSlice = new Vector2[articulationX,articulationY];
            //copy the data.
            for (int y = 0; y < articulationY; y++)
            {
                for (int x = 0; x < articulationX; x++)
                {
                    originalSlice[x, y] = new Vector2(vertices[slice, x, y].x, vertices[slice, x, y].y);
                }
            }

            //apply the inverse
            for (int y = 0; y < articulationY; y++)
            {
                for (int x = 0; x < articulationX; x++)
                {
                    vertices[slice, x, y].x = originalSlice[articulationX - x - 1, y].x;
                    vertices[slice, x, y].y = originalSlice[articulationX - x - 1, y].y;
                }
            }

            System.GC.Collect();
            
        }
        public void flipVertsY()
        {
            int slices = slicesX * slicesY;

            for (int i = 0; i < slices; i++)
                flipVertsY(i);
            canvas._setCalibration(vertices);
            updateTextures();
        }
        public void flipVertsY(int slice)
        {

            Vector2[,] originalSlice = new Vector2[articulationX, articulationY];
            //copy the data.
            for (int ay = 0; ay < articulationY; ay++)
            {
                for (int ax = 0; ax < articulationX; ax++)
                {
                    originalSlice[ax, ay] = new Vector2(vertices[slice, ax, ay].x, vertices[slice, ax, ay].y);
                }
            }

            //apply the inverse
            for (int ay = 0; ay < articulationY; ay++)
            {
                for (int ax = 0; ax < articulationX; ax++)
                {
                    vertices[slice, ax, ay].x = originalSlice[ax, articulationY - ay - 1].x;
                    vertices[slice, ax, ay].y = originalSlice[ax, articulationY - ay - 1].y;
                }
            }
            System.GC.Collect();
        }

        public void flipVertsZ()
        {
            int sliceCount = vertices.GetLength(0);
            Vector2[,,] originalSlice = new Vector2[vertices.GetLength(0), vertices.GetLength(1), vertices.GetLength(2)];
            //copy the data.
            for (int s = 0; s < sliceCount; s++)
            {
                for (int ay = 0; ay < articulationY; ay++)
                {
                    for (int ax = 0; ax < articulationX; ax++)
                    {
                        originalSlice[s, ax, ay] = new Vector2(vertices[s, ax, ay].x, vertices[s, ax, ay].y);
                    }
                }
            }

            //apply the inverse
            for (int s = 0; s < sliceCount; s++)
            {
                for (int ay = 0; ay < articulationY; ay++)
                {
                    for (int ax = 0; ax < articulationX; ax++)
                    {
                        vertices[s, ax, ay].x = originalSlice[sliceCount - s - 1, ax, ay].x;
                        vertices[s, ax, ay].y = originalSlice[sliceCount - s - 1, ax, ay].y;
                    }
                }
            }
            canvas._setCalibration(vertices);
            updateTextures();
        }
            


        //1 to reset perfect slices, 0 to reset calibrated slices
        public void resetVertexData(bool perfectSlices)
        {
            selectionS = selectionX = selectionY = 0; //reset selection

            int sliceCount = slicesX * slicesY;
            
            if (perfectSlices)
                perfectVertices = new Vector2[sliceCount, articulationX, articulationY];
            else
                vertices = new Vector2[sliceCount, articulationX, articulationY];

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
                            if (perfectSlices)
                                perfectVertices[sliceIndex, ax, ay ] =   new Vector2(sliceX + (ax * aW), sliceY + (ay * aH));
                            else
                                vertices[sliceIndex, ax, ay] =      new Vector2(sliceX + (ax * aW), sliceY + (ay * aH));
                        }
                    }
                }
            }
            undoMgr.clear(); //these will not work anymore for us.
        }



        //reset every point of articulation in this slice only
        public void resetVertexOffsets(int slice)
        {
            for (int y = 0; y < perfectVertices.GetLength(2); y++)
            {
                for (int x = 0; x < perfectVertices.GetLength(1); x++)
                {
                    resetVertexOffset(slice, x, y);
                }
            }
        }

        public void resetVertexOffset(int slice, int xVert, int yVert)
        {
            vertices[slice, xVert, yVert].x = perfectVertices[slice, xVert, yVert].x;
            vertices[slice, xVert, yVert].y = perfectVertices[slice, xVert, yVert].y;
        }


        void increaseDetail(bool updateTex)
        {
            if (cubeMode) //get out of cubeMode into normal editing
            {
                cubeMode = false;
                if (updateTex)
                    updateTextures();
                return;
            }

            int oldX = displayLevelX;
            int oldY = displayLevelY;
            displayLevelX++;
            displayLevelY++;

            validateDisplayLevel();
            if (displayLevelX != oldX)
                selectionX *= 2;
            if (displayLevelY != oldY)
                selectionY *= 2;
            if (updateTex)
                updateTextures();
        }
        void decreaseDetail(bool updateTex)
        {
            if (displayLevelX == 0 && displayLevelY == 0 && !cubeMode) //enter cubeMode
            {
                cubeMode = true;
                if (updateTex)
                    updateTextures();
                selectionS = selectionS < slicesX * slicesY / 2 ? 0 : slicesX * slicesY - 1;  //make the slice selection snap to either 0 or last depending on which is closer to current
                return;
            }

            int oldX = displayLevelX;
            int oldY = displayLevelY;
            displayLevelX--;
            displayLevelY--;

            validateDisplayLevel();
            if (displayLevelX != oldX)
                selectionX /= 2;
            if (displayLevelY != oldY)
                selectionY /= 2;

            if (updateTex)
                updateTextures();
        }


        bool vertChangeOccurred = false;
        void Update()
        {

            if (Input.GetKeyDown(KeyCode.Equals)) // increase detail
            {               
                increaseDetail(true);
            }
            else if (Input.GetKeyDown(KeyCode.Minus)) //decrease detail
            {
                decreaseDetail(true);
            }

            //reset all
            else if ((Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) &&
                (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) &&
                Input.GetKeyDown(KeyCode.R))
            {
                vertices = perfectVertices;
                canvas._setCalibration(vertices);
                undoMgr.recordUndo(vertices);
            }
            //reset verts below current articulation level
            else if ((Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) && Input.GetKeyDown(KeyCode.R))
            {
                for (int y = 0; y < articulationY; y++)
                {                  
                    for (int x = 0; x < articulationX; x++)
                    {
                        if (!xOptionContains(x) || !yOptionContains(y))
                        {
                            // "reset" this vert by moving it to its proper position based on the nearest active x values
                            uint less;
                            uint greater;
                            getNearestOptionY((uint)y, out less, out greater);
                            vertices[selectionS, x, y].x = Mathf.Lerp(vertices[selectionS, x, less].x, vertices[selectionS, x, greater].x, Mathf.InverseLerp((float)less, (float)greater, (float)y));
                            getNearestOptionX((uint)y, out less, out greater);
                            vertices[selectionS, x, y].y = Mathf.Lerp(vertices[selectionS, less, y].y, vertices[selectionS, greater, y].y, Mathf.InverseLerp((float)less, (float)greater, (float)x));
                        }
                    }
                }
                undoMgr.recordUndo(vertices);
                canvas._setCalibration(vertices);
            }


            else if (Input.GetKeyDown(KeyCode.S))
            {
                              
                selectionY--;
                if (selectionY < 0 || cubeMode)
                    selectionY = 0;

                updateTextures();
            }
            else if (Input.GetKeyDown(KeyCode.W))
            {
                selectionY++;
                if (selectionY >= articulationY || cubeMode)
                    selectionY = articulationY - 1;

                updateTextures();
            }
            else if (Input.GetKeyDown(KeyCode.A))
            {
                selectionX--;
                if (selectionX < 0 || cubeMode)
                    selectionX = 0;
                updateTextures();
            }
            else if (Input.GetKeyDown(KeyCode.D))
            {
                selectionX++;
                if (selectionX >= articulationX || cubeMode)
                    selectionX = articulationX - 1;
                updateTextures();
            }
            else if (Input.GetKeyDown(KeyCode.R))
            {
                if (cubeMode)
                {
                    selectionS = slicesX * slicesY - 1;
                    updateTextures();
                    return;
                }

                selectionS++;
                if (selectionS >= slicesX * slicesY) //loop around
                    selectionS = 0;
                updateTextures();
            }
            else if (Input.GetKeyDown(KeyCode.F))
            {
                if (cubeMode)
                {
                    selectionS = 0;
                    updateTextures();
                    return;
                }
                
                selectionS--;
                if (selectionS < 0)
                    selectionS = (slicesX * slicesY) - 1; //loop around
                updateTextures();
            }

            if (Input.GetKey(KeyCode.Mouse1)) //resize dots
            {
                Vector3 diff = lastMousePos - Input.mousePosition;
                if (diff != Vector3.zero)
                {
                    dotSize += (-diff.x - diff.y ) * Time.deltaTime * scaleSensitivity; 
                    updateTextures();
                }
            }


            //undo
            if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) ||
                Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand)
                ) && Input.GetKeyDown(KeyCode.Z))
            {
                Vector2[,,] verts = undoMgr.undo();
                if (verts == null)
                    return;
                vertices = verts;
                canvas._setCalibration(vertices);
                return;
            }
            //redo
            if ((Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)
            ) && Input.GetKeyDown(KeyCode.Z))
            {
                Vector2[,,] verts = undoMgr.redo();
                if (verts == null)
                    return;
                vertices = verts;
                canvas._setCalibration(vertices);
                return;
            }



            //move a vert with mouse
            if (Input.GetKey(KeyCode.Mouse0))
			{
				Vector3 diff = Input.mousePosition - lastMousePos;
				if (diff != Vector3.zero)
				{
					diff *= Time.deltaTime * sensitivity;

                    if (Input.GetKey(KeyCode.E))  //adjust slice spacing.
                    {
                        for (int s = 0; s < slicesX * slicesY; s++)
                        {
                            int yMod = s / slicesX; //each row of slices moves up more
                            float yOffset = diff.y * yMod;
                            for (int y = 0; y < articulationY; y++)
                            {
                                for (int x = 0; x < articulationX; x++)
                                {
                                    vertices[s, x, y].y += yOffset;
                                }
                            }
                        }
                    }
                    else if (Input.GetKey(KeyCode.C) || Input.GetKey(KeyCode.Y)) //move all verts along this x
                    {
                        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))//current slice only.
                        {
                            for (int optY = 0; optY < yOptions[displayLevelY].Length; optY++)
                            {
                                moveVert(selectionS, displayLevelX, selectionX, displayLevelY, optY, diff.x, diff.y);
                            }
                        }
                        else //interpolate the y movements along their sister y's
                        {
                            for (int optY = 0; optY < yOptions[displayLevelY].Length; optY++)
                            {
                                moveDeepVert(selectionS, displayLevelX, selectionX, displayLevelY, optY, diff.x, diff.y);
                            }
                        }
                    }
                    else if (Input.GetKey(KeyCode.X)) //move all verts along this y
                    {
                        
                        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))//current slice only.
                        {
                            for (int optX = 0; optX < xOptions[displayLevelX].Length; optX++)
                            {
                                moveVert(selectionS, displayLevelX, optX, displayLevelY, selectionY, diff.x, diff.y);
                            }
                        }
                        else //interpolate the y movements along their sister y's,
                        {
                            for (int optX = 0; optX < xOptions[displayLevelX].Length; optX++)
                            {
                                moveDeepVert(selectionS, displayLevelX, optX, displayLevelY, selectionY, diff.x, diff.y);
                            }
                        }
                    }
                    else if (Input.GetKey(KeyCode.A) && 
                            (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) &&
                             (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))) //move every vert /offset all
                    {
                        if (getXFlip(selectionS))
                            diff.x = -diff.x;
                        if (getYFlip(selectionS))
                            diff.y = -diff.y;
                        for (int s = 0; s < slicesX * slicesY; s++)
                        {
                            for (int y = 0; y < articulationY; y++)
                            {
                                for (int x = 0; x < articulationX; x++)
                                {
                                    vertices[s, x, y].x += diff.x;
                                    vertices[s, x, y].y += diff.y;
                                }
                            }
                        }
                    }

                    else if ((Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) && //move the whole slice
                        (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
                    {
                        if (getXFlip(selectionS))
                            diff.x = -diff.x;
                        if (getYFlip(selectionS))
                            diff.y = -diff.y;
                        for (int y = 0; y < articulationY; y++)
                        {
                            for (int x = 0; x < articulationX; x++)
                            {
                                vertices[selectionS, x, y].x += diff.x;
                                vertices[selectionS, x, y].y += diff.y;
                            }
                        }
                    }
                    else if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))  //move this vert on all slices
                    {
                        int slices = slicesX * slicesY;
                        for (int s = 0; s < slices; s++)
                        {
                            moveVert(s, diff.x, diff.y);
                        }
                    }
                    else if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) //move this vert only
                    {                       
                        moveVert(selectionS, diff.x, diff.y);
                    }
                /*    else if (cubeMode)
                    {
                        //cube mode is really a variant of the lowest mode. Here we lerp movement along the z edges
                        int slices = slicesX * slicesY;

                        if (selectionS == 0)
                        {
                            for (int s = 0; s < slices; s++)
                            {
                                float lerp = 1f - ((float)s / (float)slices);
                                moveVert(s, diff.x * lerp, diff.y * lerp);
                            }
                        }
                        else //we have a back slice selected, so slice 0 gets no influence here
                        {
                            for (int s = 0; s < slices; s++)
                            {
                                float lerp = (float)s / (float)slices;
                                moveVert(s, diff.x * lerp, diff.y * lerp);
                            }
                        }
                    }
                    */
                    else
                        moveDeepVert(selectionS, diff.x, diff.y); //move this vert softly, on all slices (the default)

                    vertChangeOccurred = true;
					canvas._setCalibration (vertices);
				}
			}
            else if (vertChangeOccurred && Input.GetKeyUp(KeyCode.Mouse0)) //record undo on mouseup
            {
                undoMgr.recordUndo(vertices);
                vertChangeOccurred = false;
            }


            //modifiers
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                //Toggle different bg display
                if (Input.GetKeyDown(KeyCode.Alpha1))
                { renderDots++;   if (renderDots == renderCondition.INVALID) renderDots = 0; updateTextures(); }
                if (Input.GetKeyDown(KeyCode.Alpha2))
                { renderNum++; if (renderNum == renderCondition.INVALID) renderNum = 0; updateTextures(); }
                if (Input.GetKeyDown(KeyCode.Alpha3))
                { renderBgImage++; if (renderBgImage == renderCondition.INVALID) renderBgImage = 0; updateTextures(); }
                if (Input.GetKeyDown(KeyCode.Alpha4))
                { currentBgMaterial++; if (currentBgMaterial >= bgMaterials.Length) currentBgMaterial = 0; background.GetComponent<MeshRenderer>().material = bgMaterials[currentBgMaterial]; updateTextures(); }

                //flip only the current slice verts
                if (Input.GetKeyDown(KeyCode.Alpha8))
                { flipVertsX(selectionS); canvas._setCalibration(vertices);  updateTextures(); undoMgr.recordUndo(vertices);}
                if (Input.GetKeyDown(KeyCode.Alpha9))
                { flipVertsY(selectionS); canvas._setCalibration(vertices);  updateTextures(); undoMgr.recordUndo(vertices);}
            }
            else // no modifier
            {
                if (Input.GetKeyDown(KeyCode.Alpha1))
                    sensitivity = .00002f;
                else if (Input.GetKeyDown(KeyCode.Alpha2))
                    sensitivity = .0002f;
                else if (Input.GetKeyDown(KeyCode.Alpha3))
                    sensitivity = .007f;
                else if (Input.GetKeyDown(KeyCode.Alpha4))
                    sensitivity = .015f;
                else if (Input.GetKeyDown(KeyCode.Alpha5))
                    sensitivity = .15f;

                if (Input.GetKeyDown(KeyCode.Alpha8))
                { flipVertsX(); undoMgr.recordUndo(vertices);}
                if (Input.GetKeyDown(KeyCode.Alpha9))
                { flipVertsY(); undoMgr.recordUndo(vertices);}
                if (Input.GetKeyDown(KeyCode.Alpha0))
                { flipVertsZ(); undoMgr.recordUndo(vertices);}
            }


                lastMousePos = Input.mousePosition;
        }

        //lerps the appropriate unselected vertices and also it's equivalent among sister slices
        void moveDeepVert(int slice, float xAmount, float yAmount)
        {
            moveDeepVert(slice, displayLevelX, selectionX, displayLevelY, selectionY, xAmount, yAmount);
        }
        void moveDeepVert(int slice, int dispX, int selX, int dispY, int selY, float xAmount, float yAmount)
        {
            
            int sliceCount = slicesX * slicesY;
            for (int s = 0; s < sliceCount; s++)
            {
                if (s == slice)
                    moveVert(slice, dispX, selX, dispY, selY, xAmount, yAmount); //move the main slice 
                else if (s < slice)
                {
                    float lerp = Mathf.Lerp(0f, 1f, (float)s/(float)slice);
                    moveVert(s, dispX, selX, dispY, selY, xAmount * lerp, yAmount * lerp);
                }
                else //s > slice
                {
                    float lerp = Mathf.Lerp(0f, 1f, 1f -((float)(s-slice)/(float)(sliceCount - slice)));
                    moveVert(s, dispX, selX, dispY, selY, xAmount * lerp, yAmount * lerp);
                }
            }
        }


        //lerps the appropriate unselected vertices
        void moveVert(int slice, float xAmount, float yAmount)
        {
            moveVert(slice, displayLevelX, selectionX, displayLevelY, selectionY, xAmount, yAmount);
        }
            
        void moveVert(int slice, int dispX, int selX, int dispY, int selY, float xAmount, float yAmount)
		{
            if (xAmount == 0 && yAmount == 0)
                return;
            
            if (getXFlip(slice))
                xAmount = -xAmount;
            if (getYFlip(slice))
                yAmount = -yAmount;

			int middleX = (int)xOptions[dispX][selX];
			int middleY = (int)yOptions[dispY][selY];

			//translate the middle vert 100% (and then don't change it later)
			vertices [slice, middleX, middleY].x += xAmount; 
			vertices [slice, middleX, middleY].y += yAmount;
	
			int left = middleX;
			if (selX != 0)
				left = (int)xOptions [dispX] [selX - 1];
			int right = middleX; 
			if (selX < xOptions [dispX].Length - 1)
				right = (int)xOptions [dispX] [selX + 1];

			int top = middleY;
			if (selY != 0)
				top = (int)yOptions [dispY] [selY - 1];
			int bottom = middleY; 
			if (selY < yOptions [dispY].Length - 1)
				bottom = (int)yOptions [dispY] [selY + 1];

			//now we know all verts that will be affected by this move.
			//imagine now 4 quadrants, with the 'middle' vert in the center
			//we will lerp all sister verts in those quadrants accordingly.

			float lerpVal = 0f;
			for (int vy = top; vy <= bottom; vy++) 
			{  
				for (int vx = left; vx <= right; vx++) 
				{  
                    if (vx < middleX) //left
                    { 
                        if (vy < middleY)//top left
                        { 
                            lerpVal = Mathf.InverseLerp(left, middleX, vx) * Mathf.InverseLerp(top, middleY, vy);
                            vertices[slice, vx, vy].x += Mathf.Lerp(0, xAmount, lerpVal); 
                            vertices[slice, vx, vy].y += Mathf.Lerp(0, yAmount, lerpVal); 
                        }
                        else if (vy > middleY)//bottom left
                        { 
                            lerpVal = Mathf.InverseLerp(left, middleX, vx) * Mathf.InverseLerp(bottom, middleY, vy);
                            vertices[slice, vx, vy].x += Mathf.Lerp(0, xAmount, lerpVal); 
                            vertices[slice, vx, vy].y += Mathf.Lerp(0, yAmount, lerpVal); 
                        }
                        else if (vy == middleY)//middle left
                        { 
                            lerpVal = Mathf.InverseLerp(left, middleX, vx);
                            vertices[slice, vx, vy].x += Mathf.Lerp(0, xAmount, lerpVal); 
                            vertices[slice, vx, vy].y += Mathf.Lerp(0, yAmount, lerpVal); 
                        }
                    }
                    else if (vx > middleX)//right
                    { 
                        if (vy < middleY) //top right
                        {  
                            lerpVal = Mathf.InverseLerp(right, middleX, vx) * Mathf.InverseLerp(top, middleY, vy);
                            vertices[slice, vx, vy].x += Mathf.Lerp(0, xAmount, lerpVal); 
                            vertices[slice, vx, vy].y += Mathf.Lerp(0, yAmount, lerpVal);
                        }
                        else if (vy > middleY)//bottom right 
                        { 
                            lerpVal = Mathf.InverseLerp(right, middleX, vx) * Mathf.InverseLerp(bottom, middleY, vy);
                            vertices[slice, vx, vy].x += Mathf.Lerp(0, xAmount, lerpVal); 
                            vertices[slice, vx, vy].y += Mathf.Lerp(0, yAmount, lerpVal);
                        }
                        else if (vy == middleY) //middle right
                        { 
                            lerpVal = Mathf.InverseLerp(right, middleX, vx);
                            vertices[slice, vx, vy].x += Mathf.Lerp(0, xAmount, lerpVal); 
                            vertices[slice, vx, vy].y += Mathf.Lerp(0, yAmount, lerpVal); 
                        }
                    }
                    else if (vx == middleX) 
                    { 
                        if (vy < middleY) //middle top
                        {  
                            lerpVal = Mathf.InverseLerp(top, middleY, vy);
                            vertices[slice, vx, vy].x += Mathf.Lerp(0, xAmount, lerpVal); 
                            vertices[slice, vx, vy].y += Mathf.Lerp(0, yAmount, lerpVal);
                        }
                        else if (vy > middleY) //middle bottom
                        {
                            lerpVal = Mathf.InverseLerp(bottom, middleY, vy);
                            vertices[slice, vx, vy].x += Mathf.Lerp(0, xAmount, lerpVal); 
                            vertices[slice, vx, vy].y += Mathf.Lerp(0, yAmount, lerpVal);
                        }
                    }
				}
			}				
		}


        /// <summary>
        /// This applies a bowing offset to verts at the most detailed level
        /// </summary>
        /// <param name="top"></param>
        /// <param name="bottom"></param>
        /// <param name="left"></param>
        /// <param name="right"></param>
      public void bowVerts(float top, float bottom, float left, float right)
        {
            /*
             *           int middleX = (vertices.GetLength(1) - 1) / 2;
                     int middleY = (vertices.GetLength(2) - 1) / 2;

                     int w = vertices.GetLength(2);
                     int h = vertices.GetLength(1);
                     Vector2 bow = new Vector2();
                     for (int vy = 0; vy < h; vy++)
                     {
                         for (int vx = 0; vx < w; vx++)
                         {

                             float rowLerpValue = vx/(float)w;
                             float columnLerpValue = vy / (float)h;

                             if (vx < middleX) //left
                             {
                                 if (vy < middleY)//top left
                                 {
                                     //phase 1 1
                                     bow.x = (1f - Mathf.Cos(1f - rowLerpValue)) * xBowAmount;
                                     bow.y = (1f - Mathf.Cos(1f - columnLerpValue)) * yBowAmount;
                                 }
                                 else if (vy > middleY)//bottom left
                                 {
                                     //phase: 0 1
                                     bow.x = (1f - Mathf.Cos(0f - rowLerpValue)) * xBowAmount;
                                     bow.y = (1f - Mathf.Cos(1f - columnLerpValue)) * yBowAmount;
                                 }
                                 else if (vy == middleY)//middle left
                                 {
                                 }
                             }
                             else if (vx > middleX)//right
                             {
                                 if (vy < middleY) //top right
                                 {
                                     //phase: 1 0 
                                     bow.x = (1f - Mathf.Cos(1f - rowLerpValue)) * xBowAmount;
                                     bow.y = (1f - Mathf.Cos(0f - columnLerpValue)) * yBowAmount;
                                 }
                                 else if (vy > middleY)//bottom right 
                                 {
                                     //phase: 0 0
                                     bow.x = (1f - Mathf.Cos(0f - rowLerpValue)) * xBowAmount;
                                     bow.y = (1f - Mathf.Cos(0f - columnLerpValue)) * yBowAmount;
                                 }
                                 else if (vy == middleY) //middle right
                                 {
                                 }
                             }
                             else if (vx == middleX)
                             {
                                 if (vy < middleY) //middle top
                                 {
                                 }
                                 else if (vy > middleY) //middle bottom
                                 {
                                 }
                             }
                         }
                     }
         */
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
            if (sliceMaterials == null || sliceTextures == null || 
                sliceMaterials.Length != sliceCount || 
                sliceTextures.Length != sliceCount ||
                sliceTextures[0].width != castMesh.rttResX ||
                sliceTextures[0].height != castMesh.rttResY
            ) 
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
                    sliceTextures[s] = new RenderTexture(castMesh.rttResX, castMesh.rttResY, 24, RenderTextureFormat.ARGBFloat);
                    sliceTextures[s].filterMode = FilterMode.Trilinear;
                    sliceTextures[s].wrapMode = TextureWrapMode.Clamp;
                    sliceTextures[s].antiAliasing = 1;
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

/*        void blackOutTexture(int slice)
        {
            dotParent.SetActive(false);
            sliceNumText.gameObject.SetActive(false);
            background.SetActive(false);

            renderCam.targetTexture = sliceTextures[slice];
            renderCam.rect = new Rect(0f, 0f, 1f, 1f);
            renderCam.Render();
            renderCam.targetTexture = null;
        }
*/
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
                    dotMeshes[dot] = (GameObject)Instantiate(dotMesh, dotParent.transform);
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


            //decide what to draw and what not to draw
            if (cubeMode && slice != 0 && slice != (slicesX * slicesY) - 1) //only draw dots for first and last
                dotParent.SetActive(false);
            else if (renderDots == renderCondition.ALL_SLICES || (renderDots == renderCondition.CURRENT_SLICE && slice == selectionS))
                dotParent.SetActive(true);
            else
                dotParent.SetActive(false);

            if (renderNum == renderCondition.ALL_SLICES || (renderNum == renderCondition.CURRENT_SLICE && slice == selectionS))
            {
                sliceNumText.gameObject.SetActive(true);
                sliceNumText.text = (slice + 1).ToString(); //start counting from 1, not 0
            }             
            else
                sliceNumText.gameObject.SetActive(false);

            if (renderBgImage == renderCondition.ALL_SLICES || (renderBgImage == renderCondition.CURRENT_SLICE && slice == selectionS))
                background.SetActive(true);
            else
                background.SetActive(false);

            if (cubeMode && slice == selectionS)
            {
                cubeModeMesh.gameObject.SetActive(true);
                Vector3 cmPos = selectionMesh.transform.localPosition;
                cmPos.z = 1.5f;
                cubeModeMesh.SetPosition(0, cmPos);
                cubeModeMesh.widthMultiplier = dotSize * 2f;
            }
            else
                cubeModeMesh.gameObject.SetActive(false);


            renderCam.targetTexture = sliceTextures[slice];
            renderCam.aspect = 1f; //(float)castMesh.rttResX / (float)castMesh.rttResY; //make the render take up the whole texture, even if it is not square
            renderCam.orthographicSize = 1f; //.5f * (float)castMesh.rttResY;
            renderCam.rect = new Rect(0f, 0f, 1f, 1f);
            renderCam.Render();
            renderCam.targetTexture = null;
        }

        //this will convert the articulated slices into slices that just define the corners
        //it is intended to be used only on the 'virgin' slices, so they can be stored in a tiny format on the PCB
        static Vector2[,,]  simplifySlices(Vector2[,,] articulatedSlices )
        {
            Vector2[,,] output = new Vector2[articulatedSlices.GetLength(0), 2, 2];

            for (int s = 0; s < articulatedSlices.GetLength(0); s++)
            {
                output[s, 0, 0] = articulatedSlices[s,0,0];
                output[s, 0, 1] = articulatedSlices[s, 0, articulatedSlices.GetLength(2) -1];
                output[s, 1, 0] = articulatedSlices[s, articulatedSlices.GetLength(1) - 1, 0];
                output[s, 1, 1] = articulatedSlices[s, articulatedSlices.GetLength(1) - 1, articulatedSlices.GetLength(2) - 1];
            }
            return output;
        }


        public IEnumerator saveSettings()
        {
            messageWindow.showMessage("Attempting to SAVE...");

            System.Text.StringBuilder sb = new System.Text.StringBuilder();

            dataFileDict d = canvas.GetComponent<dataFileDict>();
            if (d)
                d.setValue("calibratorVersion", basicSettings.calibratorVer.text);

            //touch panel
            if (!basicSettings.saveToPCB.isOn)
                sb.Append("Save to PCB: <color=#dddddd>N/A</color>\n");
            else if (input.touchPanel == null || !d)
                sb.Append("Save to PCB: <color=#ff0000>FAIL</color>\n");
            else
            {
                input.touchPanel.serial.readDataAsString = true;
           
                string basicSettingsStr = d.getDataAsString();
                yield return input._get()._writeSettings(basicSettingsStr);
                if (input.pcbIoState == input.pcbState.SUCCESS)
                    sb.Append("Save settings to to PCB: <color=#00ff00>SUCCESS</color>\n");
                else
                    sb.Append("Save settings to PCB: <color=#ff0000>FAIL</color>\n");

                if (vertices == null || perfectVertices == null)
                    sb.Append("Save slices to PCB: <color=#ff0000>FAIL: NO VERT DATA (Visit step 2 first)</color>\n"); // Continue to stage 2, and try again.
                else
                {
                    yield return input._get()._writeSlices(simplifySlices(perfectVertices), false);//perfect slices
                    if (input.pcbIoState == input.pcbState.SUCCESS)
                        sb.Append("Save perfect slices to PCB: <color=#00ff00>SUCCESS</color>\n");
                    else
                        sb.Append("Save perfect slices to PCB: <color=#ff0000>FAIL</color>\n");


                      
                    yield return input._get()._writeSlices(vertices, true);//calibrated slices
                    if (input.pcbIoState == input.pcbState.SUCCESS)
                        sb.Append("Save calibrated slices to PCB: <color=#00ff00>SUCCESS</color>\n");
                    else
                        sb.Append("Save calibrated slices to PCB: <color=#ff0000>FAIL</color>\n");
                }

                input.touchPanel.serial.readDataAsString = false;
            }


            //usb save
            string configPath = "";
            if (!utils.getConfigPathToFile(canvas.usbConfigPath + "/" + canvas.basicSettingsFileName, out configPath))
                sb.Append("Save to USB: <color=#ff0000>FAIL</color>");
            else
            {
                d.fileName = configPath;
                if (d.save())
                    sb.Append("Save settings to USB: <color=#00ff00>SUCCESS</color>\n");
                else
                {
                    sb.Append("Save settings to USB: <color=#ff0000>FAIL</color>\n");
                    yield break; //stop trying to save, if we couldn't do the regular settings
                }

                if (vertices == null || perfectVertices == null)
                    sb.Append("Save slices to USB: <color=#ff0000>FAIL: NO VERT DATA (Visit step 2 first)</color>\n");
                else
                {

                    string basePath = System.IO.Path.GetDirectoryName(configPath);

                    configPath = utils.formatPathToOS(basePath + "/" + canvas.perfectSlicesFileName);
                    //if (!utils.getConfigPath(canvas.usbConfigPath + "/" + canvas.perfectSlicesFileName, out configPath))
                    //    sb.Append("Save perfect slices to USB: <color=#ff0000>FAIL</color>\n");
                    //else
                    {
                        byte[] fileBytes = utils.vert2Bin(perfectVertices);
                        System.IO.File.WriteAllBytes(configPath, fileBytes);
                        sb.Append("Save perfect slices to USB: <color=#00ff00>SUCCESS</color>\n");
                    }

                    configPath = utils.formatPathToOS(basePath + "/" + canvas.calibratedSlicesFileName);
                    //if (!utils.getConfigPath(canvas.usbConfigPath + "/" + canvas.calibratedSlicesFileName, out configPath))
                    //    sb.Append("Save calibrated slices to USB: <color=#ff0000>FAIL</color>\n");
                    //else
                    {                   
                        byte[] fileBytes = utils.vert2Bin(vertices);
                        System.IO.File.WriteAllBytes(configPath, fileBytes);
                        sb.Append("Save calibrated slices to USB: <color=#00ff00>SUCCESS</color>\n");


                        //sully textures
                        string rawPath = utils.formatPathToOS(basePath);
                        int w = d.getValueAsInt("volumeResX", 1920);
                        int h = d.getValueAsInt("volumeResY", 1080);
                        if (canvas.generateSullyTextures(w, h, rawPath))
                            sb.Append("Wrote sully textures to USB: <color=#00ff00>" + rawPath + "</color>\n");
                        else
                            sb.Append("Write sully textures to USB: <color=#ff0000>FAIL</color>\n");
                    }
                }

            }

            //castMesh.canvas.GetComponent<AudioSource>().Play(); //play sound
            messageWindow.showMessage(sb.ToString());
        }

    }
}
