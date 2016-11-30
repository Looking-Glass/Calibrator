
Shader "Hidden/sullyTest"
{
	Properties
	{
		_MainTex("Texture", 2D) = "white" {}  //the incoming, 'virgin' slice image
		_sullyX("Sully X", 2D) = "black" {}
		_sullyY("Sully Y", 2D) = "black" {}
	}
		SubShader
	{
		// No culling or depth
		//Cull Off ZWrite Off ZTest Always

		Pass
	{
		CGPROGRAM
#pragma vertex vert
#pragma fragment frag

#include "UnityCG.cginc"


		struct appdata
	{
		float4 vertex : POSITION;
		float2 uv : TEXCOORD0;
	};

	struct v2f
	{
		float2 uv : TEXCOORD0;
		float4 vertex : SV_POSITION;
	};

	v2f vert(appdata v)
	{
		v2f o;
		o.vertex = mul(UNITY_MATRIX_MVP, v.vertex);
		o.uv = v.uv;
		return o;
	}

	sampler2D _MainTex;  //this is the raw, unsullied image
	sampler2D _sullyX;
	sampler2D _sullyY;

	fixed4 frag(v2f i) : SV_Target
	{
		//return tex2D(_MainTex, i.uv);

		float4 sx = tex2D(_sullyX, i.uv);
		float4 sy = tex2D(_sullyY, i.uv);

		sx *= 256; //sx come in as 0-1, this normalizes them into our 8 bit per channel value space
		sy *= 256;

		float2 xy; //this will hold the lookup pixel

		float x = ((int)sx.r << 4) + ((int)sx.g >> 4);  //get the x lookup from the x sully texture
		x += ((((int)sx.g & 15) << 8) + sx.b)/4096;
		xy.x = x;

		float y = ((int)sy.r << 4) + ((int)sy.g >> 4); //get the y lookup fro the y sully texture
		y += ((((int)sy.g & 15) << 8) + sy.b) / 4096;
		xy.y = y;

		xy.x /= 1920 ; //convert the normalized 0-1 values into the desired pixel by mapping it to the resolution of the screen and sully textures
		xy.y /= 1080 ;
		//xy.x /= 1280 ;
		//xy.y /= 720 ;

		return tex2D(_MainTex, xy); //use the values from the sully textures as a UV lookup on the raw image

	}
		ENDCG
	}
	}
}
