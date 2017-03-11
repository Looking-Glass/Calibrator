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
        [Tooltip("How should this modification be blended with the rendered slice?\n\nNONE:\nNo blending, the normal slice is used and the given texture is ignored.\n\nOVER:\nThe given texture is alpha blended on top of the chosen slice.\n\nUNDER:\nThe given texture is alpha blended below the chosen slice.\n\nADD:\nThe pixel value of the given texture and the slice are added together (made brighter).\n\nMULTIPLY:\nThe pixel value of the given texture and the slice are multiplied (made darker).\n\nREPLACE:\nThe rendered slice is ignored, and the given texture is used instead.")]
        public slicePostProcess.blending blend;
        [Tooltip("The modification. Put the texture that you want to blend with the desired slice. It can be a renderTexture.")]
        public Texture tex;

        public static sliceModifier getSliceMod(int sliceNum, int sliceCount, sliceModifier[] mods) //TODO this iterates every slice on every frame, check if optimization is in order.
        {
            if (sliceCount < 1 || mods == null)
                return null;

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
