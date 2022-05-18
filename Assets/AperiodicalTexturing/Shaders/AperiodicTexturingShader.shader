Shader "AperiodicTexturing/AperiodicTexturingShader"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _Albedo("Albedo", 2D) = "white" {}
        _Glossiness("Smoothness", 2D) = "black" {}
        _BumpMap("Bumpmap", 2D) = "bump" {}
        _TileMappingTexture("Tile Mapping Texture", 2D) = "black" {}
        _TileScale("Tile Scale", Vector) = (4, 4, 0, 0)
        _TileMappingScale("Tile Mapping Scale", Vector) = (256, 256, 0, 0)
        _MetallicScale("Metallic Scale", Range(0,1)) = 0.0
        _GlossinessScale("Smoothness Scale", Range(0,1)) = 0.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM

        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _Albedo, _Glossiness, _BumpMap, _TileMappingTexture;
        float2 _TileMappingScale, _TileScale;

        struct Input
        {
            float2 uv_TilesTexture;
            float2 uv_TileMappingTexture;
        };

        half _MetallicScale, _GlossinessScale;
        fixed4 _Color;

        UNITY_INSTANCING_BUFFER_START(Props)
            // put more per-instance properties here
        UNITY_INSTANCING_BUFFER_END(Props)

        float4 GetTileUV(float2 uv)
        {
            float2 tileIndex = tex2D(_TileMappingTexture, uv) * 255.0;
            float2 invScale = 1.0 / _TileScale;

            float2 mappingUV = uv * _TileMappingScale;
            float2 mappingScaledUV = mappingUV * invScale;

            float4 tileUV;
            tileUV.xy = (tileIndex + frac(mappingUV)) * invScale;
            tileUV.z = ddx(mappingScaledUV);
            tileUV.w = ddy(mappingScaledUV);

            return tileUV;
        }

        float4 SampleAlbedoTexture(float4 uv)
        {
            return tex2D(_Albedo, uv.xy, uv.z, uv.w) * _Color;
        }

        float SampleSmoothnessTexture(float4 uv)
        {
            return tex2D(_Glossiness, uv.xy, uv.z, uv.w).r * _GlossinessScale;
        }

        float3 SampleBumpTexture(float4 uv)
        {
            return UnpackNormal(tex2D(_BumpMap, uv));
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float2 uv = IN.uv_TileMappingTexture.xy;

            float4 tileUV = GetTileUV(uv);

            float4 albedo = SampleAlbedoTexture(tileUV);
            float smoothness = SampleSmoothnessTexture(tileUV);
            float3 normal = SampleBumpTexture(tileUV);

            o.Albedo = albedo.rgb;
            o.Alpha = albedo.a;
            o.Normal = normal;
            o.Metallic = _MetallicScale;
            o.Smoothness = smoothness;

        }

        ENDCG
    }
    FallBack "Diffuse"
}
