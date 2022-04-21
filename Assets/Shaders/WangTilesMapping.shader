
Shader "WangTiles/TileMappingShader" 
{
	Properties 
	{
		_MainTex ("Base (RGB)", 2D) = "white" {}
	}
	SubShader 
	{
		Tags { "RenderType"="Opaque" }
		LOD 200
		
		CGPROGRAM
		#pragma surface surf Lambert
		#pragma target 3.0
		#pragma glsl

		sampler2D _MainTex;
		sampler2D _TilesTexture, _TileMappingTexture;
		float2 _TileMappingScale, _TileScale;

		struct Input 
		{
			float2 uv_MainTex;
		};

		void surf (Input IN, inout SurfaceOutput o) 
		{
			float2 uv = IN.uv_MainTex.xy;
        	float4 whichTile = tex2D(_TileMappingTexture, uv) * 255.0;

			float2 mappingAddress = uv * _TileMappingScale;
			float2 tileScaledTex = uv * _TileMappingScale * (1.0 / _TileScale);
        	float4 result = tex2D(_TilesTexture, (whichTile.xy + frac(mappingAddress)) / _TileScale, ddx(tileScaledTex), ddy(tileScaledTex));
        	
			o.Albedo = result.rgb;
			o.Alpha = result.a;
			
		}
		ENDCG
	} 
	FallBack "Diffuse"
}
