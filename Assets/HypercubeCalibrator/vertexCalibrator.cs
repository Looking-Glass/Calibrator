using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace hypercube
{
	public class vertexCalibrator : calibrator
    {
       public float dotSize;

       public int articulationX;
       public int articulationY;
       public int slicesX;
       public int slicesY;
   
        uint[][] xOptions;
        uint[][] yOptions;

        Vector2[,,] vertices;
        Vector2[,,] virginVertices; //the untouched values. Instead of recalculating where on the mesh we are at each time if we need to reset some value(s), keeping them here is an easy and flexible way to do so.

        int displayLevel = 0;  //the current detail display level. Valid values are -articulations.size to 0, since it functions as an index to xOptions and yOptions

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

        public GameObject calibrationMesh;


        public override void OnEnable()
        {

            dataFileDict d = canvas.GetComponent<dataFileDict>();
            slicesX = d.getValueAsInt("slicesX", 0);
            slicesY = d.getValueAsInt("slicesY", 0);
            articulationX = d.getValueAsInt("articulationX", 0);
            articulationY = d.getValueAsInt("articulationY", 0);

            if (slicesX < 1)
                slicesX = 1;
            if (slicesY < 1)
                slicesY = 1;

            if (articulationLookup(articulationX) == -1) //ensure that our articulations are only of the allowed, otherwise calibration will be too confusing
                articulationX = articulations[4];
            if (articulationLookup(articulationY) == -1)
                articulationY = articulations[3];

            //configure ourselves
            //the following lines will tell us what elements to highlight when we toggle between levels of detail
            setOptions(out xOptions, articulationX);
            setOptions(out yOptions, articulationY);

            updateMesh();

            base.OnEnable();
        }


        public void resetAllVertexOffsets()
        {
            vertices = new Vector2[slicesX * slicesY, articulationX, articulationY];

            float sliceW = 1f/ slicesX;
            float sliceH = 1f/ slicesY;
            float aW = 1f/(articulationX - 1); //the -1 is because we want that last vert to end up on the edge
            float aH = 1f/(articulationY - 1);

            for (int y = 0; y < slicesY; y++)
            {
                for (int x = 0; x < slicesX; x++)
                {
                    float sliceX = x * sliceW;
                    float sliceY = y * sliceH;
                    for (int ay = 0; ay <= articulationY; ay++)
                    {
                        for (int ax = 0; ax <= articulationX; ax++)
                        {                       
                            vertices[x + (y * slicesX), ax, ay ] = 
                                new Vector2(sliceX + (ax * aW), sliceY + (ay * aH));
                        }
                    }
                }
            }

            virginVertices = new Vector2[slicesX * slicesY, articulationX, articulationY];
            vertices.CopyTo(virginVertices, 0);
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
            if (Input.GetKeyDown(KeyCode.Equals)) // increase detail
            {
                displayLevel ++;
                updateMesh();

            }
            else if (Input.GetKeyDown(KeyCode.Minus))
            {
                displayLevel --;
                updateMesh();
            }
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

            option = allOptions.ToArray();
        }

        void updateMesh()
        {

            if (displayLevel < 0) //low limit
                displayLevel = 0;

            int maxArticulation = Mathf.Max(xOptions.Length, yOptions.Length) - 1;
            displayLevel = Mathf.Min(displayLevel, maxArticulation); //high limit

            int displayLevelX = Mathf.Min(displayLevel, xOptions.Length - 1); //account for different limits between x/y
            int displayLevelY = Mathf.Min(displayLevel, yOptions.Length - 1);

            //begin mesh creation
            //recommend to build one mesh per slice, to not run out of verts

            List<Vector3> verts = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<Color> colors = new List<Color>();
            List<int[]> submeshes = new List<int[]>(); //the triangle list(s)

            //////
            for (int s = 0; s < vertices.GetLength(0); s++)
            {
                for (int y = 0; y < vertices.GetLength(2); y++)
                {
                    for (int x = 0; x  < vertices.GetLength(1); x++)
                    {
                        Vector2 centerPoint = vertices[s,x,y];
                       // verts.Add(new Vector3(.x, vertices[s, x, y].y, 0f));
                    }
                }
            }


                            //////

                            MeshRenderer r = calibrationMesh.GetComponent<MeshRenderer>();
            if (!r)
                r = calibrationMesh.AddComponent<MeshRenderer>();

            MeshFilter mf = calibrationMesh.GetComponent<MeshFilter>();
            if (!mf)
                mf = calibrationMesh.AddComponent<MeshFilter>();

            Mesh m = mf.sharedMesh;
            if (!m)
                return; //probably some in-editor state where things aren't init.
            m.Clear();

            m.SetVertices(verts);
            m.SetUVs(0, uvs);

            m.subMeshCount = 1;

            //normals are necessary for the transparency shader to work (since it uses it to calculate camera facing)
            Vector3[] normals = new Vector3[verts.Count];
            for (int n = 0; n < verts.Count; n++)
                normals[n] = Vector3.forward;

            m.normals = normals;
            m.colors = colors.ToArray();
        }

        public override Material[] getMaterials()
        {
            return base.getMaterials();
        }
    }
}
