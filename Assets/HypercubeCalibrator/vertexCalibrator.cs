using UnityEngine;
using System.Collections;

namespace hypercube
{
	public class vertexCalibrator : calibrator {

		public Material dotMaterial;

		public int xArticulation;
		public int yArticulation;

		public WebCamTexture cam; //TEMP
		public Material aMaterial;
		public float threshold = .7f;

		public Material displayMat;
		Texture2D displayTex;
		public int textureRes = 1024;
		public int successPoint = 4;

		Vector4 lastSuccessfulStepValues;
		Vector4 currentStepValues;
		bool widthHeight;
		bool firstSecond; //which half are we cutting off.

		int frameDelay = 0; //a way to delay the test to check the endoscope since its super slow
		void Start()
		{
			cam = new WebCamTexture ();
			aMaterial.SetTexture ("_MainTex", cam);
			cam.Play ();

			assignNewDisplayTexture ();
		}

		void OnValidate()
		{
			reset ();
		}

		public void reset()
		{
			if (!dotMaterial)
				return;
			
			dotMaterial.mainTextureScale = new Vector2 (xArticulation, yArticulation);
			canvas.updateMesh (); //TODO really we only need to update the materials here.
		}

		public override Material[] getMaterials ()
		{
			Material[] mats = new Material[canvas.getSliceCount()];
			for(int i = 0; i < mats.Length; i ++)
			{
				mats [i] = dotMaterial;
			}
			return mats;
		}

		void Update()
		{
			if (!cam.didUpdateThisFrame)
				return;
			frameDelay++;

			if (frameDelay < 10)
				return;
			frameDelay = 0;

			//do we see a lit up screen?   TODO REPLACE THIS WITH THE SERIAL INPUT
			Color centerColor = cam.GetPixel(cam.width/2, cam.height/2);
			float c = (centerColor.r + centerColor.g + centerColor.b) / 3f;
			bool result = false;
			if (c > threshold)
				result = true;

			takeTestStep (result);
		}


		void takeTestStep(bool lastStepSuccess)
		{
			if (lastStepSuccess) 
			{
				lastSuccessfulStepValues = currentStepValues;
				widthHeight = !widthHeight;
				if (widthHeight) 
				{ //chop width wise
					currentStepValues.z /= 2f;
					if (firstSecond)
						currentStepValues.x += lastSuccessfulStepValues.z / 2f;
				} 
				else
				{
					currentStepValues.w /= 2f;
					if (firstSecond)
						currentStepValues.y += lastSuccessfulStepValues.w / 2f;
				}

				fill (currentStepValues);
				return;
			}

			//it failed last time.
			firstSecond = !firstSecond;
			if (widthHeight) 
			{ //chop width wise
				if (firstSecond)
					currentStepValues.x += lastSuccessfulStepValues.z / 2f;
				else
					currentStepValues.x -= lastSuccessfulStepValues.z / 2f;

				if (currentStepValues.y < 0 || currentStepValues.y + currentStepValues.z > textureRes) 
				{
					firstSecond = !firstSecond;
					currentStepValues = lastSuccessfulStepValues;
					return;
				}
			} 
			else
			{
				if (firstSecond)
					currentStepValues.y += lastSuccessfulStepValues.w / 2f;
				else 
					currentStepValues.y -= lastSuccessfulStepValues.w / 2f;
				
				if (currentStepValues.y < 0 || currentStepValues.y + currentStepValues.w > textureRes) 
				{
					firstSecond = !firstSecond;
					currentStepValues = lastSuccessfulStepValues;
					return;
				}
			}

			if (currentStepValues.x < 0f)
				Debug.LogWarning (currentStepValues.x + " - X is less than 0f!");
			if (currentStepValues.y < 0f)
				Debug.LogWarning (currentStepValues.y + " - Y is less than 0f!");

			fill (currentStepValues);
		}

		void startNewTest()
		{
			lastSuccessfulStepValues = currentStepValues = new Vector4 (0f, 0f, textureRes, textureRes);
		}


		void fill(Vector4 v)
		{
			fill ((int)v.x, (int)v.y, (int)v.z, (int)v.w);
		}
		void fill(int _x, int _y, int _w, int _h)
		{
			if (_w < successPoint || _h < successPoint) //we found our point.
			{
				//SUCCESS!
				startNewTest ();
				return;
			}

			currentStepValues = new Vector4 (_x, _y, _w, _h);
			if (displayTex.width != textureRes)
				assignNewDisplayTexture ();
				
			for (int x = 0; x < textureRes; x++) 
			{
				for (int y = 0; y < textureRes; y++) 
				{
					if (x >= _x && x < _x + _w && y >= _y && y < _y + _h)
						displayTex.SetPixel (x, y, Color.white);
					else
						displayTex.SetPixel (x, y, Color.black);
				}
			}
			displayTex.Apply ();
		}

		void assignNewDisplayTexture()
		{
			displayTex = new Texture2D (textureRes, textureRes);
			displayMat.SetTexture ("_MainTex", displayTex);
		}
			
	}
}
