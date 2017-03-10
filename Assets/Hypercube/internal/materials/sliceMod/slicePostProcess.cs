using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class slicePostProcess : MonoBehaviour 
{
    public enum blending
    {
        NONE = 0,
        OVER,
        UNDER,
        ADD,
        MULTIPLY
    }

    public blending blend;
    public Texture tex;

    public Material alphaBlend;
    public Material adding;
    public Material multiplying;

    // Postprocess the image
    void OnRenderImage (RenderTexture source, RenderTexture destination)
    {   
        if (blend == blending.NONE)
        {
            Graphics.Blit (source, destination);
            return;
        }

        if (blend == blending.UNDER)
        {
            Graphics.Blit(source, destination, alphaBlend);
        }
        else if (blend == blending.OVER)
        {
            Graphics.Blit(source, destination, alphaBlend);
        }
        else if (blend == blending.ADD)
        {
            Graphics.Blit(source, destination, adding);
        }
        else if (blend == blending.MULTIPLY)
        {
            Graphics.Blit(source, destination, multiplying);
        }

        //add other blending type here if needed...
    }
}
