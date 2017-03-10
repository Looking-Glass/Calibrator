using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace hypercube
{
    [System.Serializable]
    public class sliceModifier
    {
        [Tooltip("What slice should this slice modifier apply to.\nDifferent devices have different amounts of slices, so use this to let the tools choose the appropriate slice between front (0) and back(1).")]
        [Range(0f, 1f)]
        public float depth; //0-1 front to back
        private float _lastDepth = -1; //used to detect change in value, while still allowing depth editability in inspector (don't use get/set)
        [Tooltip("How should this modification be blended with the rendered slice?")]
        public slicePostProcess.blending blend;
        [Tooltip("The modification. Put the texture that you want to blend with the desired slice. It can be a renderTexture.")]
        public Texture tex;

        //fast static lookup so that any slice mods can be found asap when iterating through them in hypercubeCamera.render()
        //static List<sliceModifier> sliceLookup = null;
        //public static sliceModifier getSliceMod(int sliceNum, int totalSlices)
        //{
        //    if (sliceLookup == null)
        //        sliceLookup = new List<sliceModifier>();

        //    if (hypercubeCamera.mainCam) //try to fill out the array.
        //    {
        //        foreach(sliceModifier m in hypercubeCamera.mainCam.sliceMods)
        //        {
        //            m.updateSlice(totalSlices);
        //        }
        //    }

        //    if (sliceNum >= sliceLookup.Count)
        //        return null;
        //    return sliceLookup[sliceNum];
        //}

        public static sliceModifier getSliceMod(int sliceNum, int sliceCount, sliceModifier[] mods) //TODO this iterates every slice on every frame, check if optimization is in order.
        {
            foreach (sliceModifier m in mods)
            {
                if (m.getSlice(sliceCount) == sliceNum)
                    return m;
            }
            return null;
        }

        int slice = -1;
        public int updateSlice()
        {
            if (!hypercube.castMesh.canvas)
            {
                slice = -1;
                return slice;
            }
            return updateSlice(hypercube.castMesh.canvas.getSliceCount());
        }
        public int updateSlice(int totalSlices)
        {
            if (depth == 0f)
                slice = 0;
            else if (depth == 1f)
                slice = totalSlices - 1;
            else
                slice = Mathf.RoundToInt(Mathf.Lerp(0, totalSlices - 1, depth));

            _lastDepth = depth;

            return slice;
        }

        public int getSlice(int totalSlices)
        {
            if (depth == _lastDepth)
                return slice;

            return updateSlice(totalSlices);
        }
    }
}
