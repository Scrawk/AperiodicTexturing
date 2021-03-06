Shader "AperiodicTexturing/AperiodicTexturingShader_Debug"
{
    Properties
    {
        _Color("Color", Color) = (1,1,1,1)
        _TilesTexture("Tiles Texture", 2D) = "white" {}
        _TilesColorTexture("Tiles Color Texture", 2D) = "white" {}
        _TileMappingTexture("Tile Mapping Texture", 2D) = "black" {}
        _TileScale("Tile Scale", Vector) = (4, 4, 0, 0)
        _DebugColorAlpha("Debug Color Alpha", Range(0, 1)) = 0.5
        _Glossiness("Smoothness", Range(0, 1)) = 0.0
        _Metallic("Metallic", Range(0,1)) = 0.0
    }
        SubShader
        {
            Tags { "RenderType" = "Opaque" }
            LOD 200

            CGPROGRAM

            #pragma surface surf Standard fullforwardshadows
            #pragma target 3.0

            sampler2D _TilesTexture, _TilesColorTexture, _TileMappingTexture;
            float2 _TileMappingScale, _TileScale;
            float4 _TileMappingTexture_TexelSize;

            struct Input
            {
                float2 uv_TilesTexture;
                float2 uv_TileMappingTexture;
            };

            half _Glossiness;
            half _Metallic;
            fixed4 _Color;
            float _DebugColorAlpha;

            UNITY_INSTANCING_BUFFER_START(Props)
                // put more per-instance properties here
            UNITY_INSTANCING_BUFFER_END(Props)

            float4 GetTileUV(float2 uv, float2 mappingSize)
            {
                float2 tileIndex = tex2D(_TileMappingTexture, uv) * 255.0;
                float2 invScale = 1.0 / _TileScale;

                float2 mappingUV = uv * mappingSize;
                float2 mappingScaledUV = mappingUV * invScale;

                float4 tileUV;
                tileUV.xy = (tileIndex + frac(mappingUV)) * invScale;
                tileUV.z = ddx(mappingScaledUV);
                tileUV.w = ddy(mappingScaledUV);

                return tileUV;
            }

            float4 SampleAperiodicTexture(float4 uv)
            {
                float4 col0 = tex2D(_TilesTexture, uv.xy, uv.z, uv.w);
                float4 col1 = tex2D(_TilesColorTexture, uv.xy, uv.z, uv.w);

                return lerp(col0, col1, _DebugColorAlpha * col1.a);
            }

            void surf(Input IN, inout SurfaceOutputStandard o)
            {
                float2 uv = IN.uv_TileMappingTexture.xy;
                float2 mappingSize = _TileMappingTexture_TexelSize.zw;

                float4 tileUV = GetTileUV(uv, mappingSize);

                float4 result = SampleAperiodicTexture(tileUV);

                o.Albedo = result.rgb * _Color.rgb;
                o.Alpha = result.a * _Color.a;
                o.Metallic = _Metallic;
                o.Smoothness = _Glossiness;

            }

            ENDCG
        }
            FallBack "Diffuse"
}
