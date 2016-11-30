using UnityEngine;
using System.Collections;
using System;

namespace hypercube
{


    [ImageEffectOpaque]
    [ExecuteInEditMode]
    [RequireComponent(typeof(Camera))]
    [AddComponentMenu("")]
    public class sullyTest : MonoBehaviour
    {

        public Material material;


        protected virtual void Start()
        {
            // Disable if we don't support image effects
            if (!SystemInfo.supportsImageEffects)
            {
                enabled = false;
                return;
            }

        }


        void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
             Graphics.Blit(source, destination, material);
        }
    }

}