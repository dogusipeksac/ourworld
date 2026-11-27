Shader "OurWorld/Skybox/PanoramicBlend"
{
    Properties
    {
        [NoScaleOffset] _TexA ("Panorama A", 2D) = "black" {}
        [NoScaleOffset] _TexB ("Panorama B", 2D) = "black" {}
        _Blend ("Blend", Range(0.0, 1.0)) = 0.0
        _Exposure ("Exposure", Range(0.0, 8.0)) = 1.0
        _Tint ("Tint Color", Color) = (1, 1, 1, 1)
        _Rotation ("Rotation", Range(0.0, 360.0)) = 0.0
        _HorizonColor ("Horizon Color", Color) = (0.5, 0.55, 0.65, 1)
        _HorizonStrength ("Horizon Strength", Range(0.0, 1.0)) = 0.0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Background"
            "RenderType" = "Background"
            "PreviewType" = "Skybox"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_TexA);
            SAMPLER(sampler_TexA);
            TEXTURE2D(_TexB);
            SAMPLER(sampler_TexB);

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half4 _HorizonColor;
                float _Blend;
                float _Exposure;
                float _Rotation;
                float _HorizonStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 dir : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                float rad = _Rotation * 0.01745329252; // deg to rad
                float s, c;
                sincos(rad, s, c);

                float3 p = IN.positionOS.xyz;
                // Rotate object-space direction around Y axis by _Rotation so azimuth = _Rotation maps to (0, y, +1) -> u = 0.5
                OUT.dir = float3(p.x * c - p.z * s, p.y, p.x * s + p.z * c);
                OUT.positionCS = TransformObjectToHClip(p);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 d = normalize(IN.dir);
                float u = 0.5 + atan2(d.x, d.z) * 0.15915494309; // 1 / (2 * PI)
                float v = 0.5 + asin(clamp(d.y, -1.0, 1.0)) * 0.31830988618; // 1 / PI
                float2 uv = float2(u, v);

                half3 colA = SAMPLE_TEXTURE2D_LOD(_TexA, sampler_TexA, uv, 0).rgb;
                half3 colB = SAMPLE_TEXTURE2D_LOD(_TexB, sampler_TexB, uv, 0).rgb;
                half3 col = lerp(colA, colB, (half)_Blend) * _Tint.rgb * (half)_Exposure;

                half h = 1.0h - saturate(abs((half)d.y) * 6.0h);
                col = lerp(col, _HorizonColor.rgb, h * (half)_HorizonStrength);

                return half4(col, 1.0h);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
