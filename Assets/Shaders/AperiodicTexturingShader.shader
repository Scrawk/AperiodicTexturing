Shader "AperiodicTexturing/AperiodicTexturingShader"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _TilesTexture("TilesTexture", 2D) = "white" {}
        _TileMappingTexture("TileMappingTexture", 2D) = "black" {}
        _TileScale("TileScale", Vector) = (0, 0, 0, 0)
        _TileMappingScale("TileMappingScale", Vector) = (0, 0, 0, 0)
        _Glossiness ("Smoothness", Range(0, 1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM

        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _TilesTexture, _TileMappingTexture;
        float2 _TileMappingScale, _TileScale;

        struct Input
        {
            float2 uv_TilesTexture;
            float2 uv_TileMappingTexture;
        };

        half _Glossiness;
        half _Metallic;
        fixed4 _Color;

        UNITY_INSTANCING_BUFFER_START(Props)
            // put more per-instance properties here
        UNITY_INSTANCING_BUFFER_END(Props)

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float2 uv = IN.uv_TileMappingTexture.xy;
            float2 tileIndex = tex2D(_TileMappingTexture, uv) * 255.0;

            float2 mappingAddress = uv * _TileMappingScale;
            float2 tileScaledTex = uv * _TileMappingScale * (1.0 / _TileScale);

            float4 result = tex2D(_TilesTexture, (tileIndex + frac(mappingAddress)) / _TileScale, ddx(tileScaledTex), ddy(tileScaledTex));

            o.Albedo = result.rgb * _Color.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
