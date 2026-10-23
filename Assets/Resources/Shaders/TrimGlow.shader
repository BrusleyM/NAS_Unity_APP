Shader "NAS/TrimGlow"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base colour", Color) = (0.1, 0.1, 0.1, 1)
        [HDR] _GlowColor ("Glow colour", Color) = (0, 0.8, 1, 1)
        _GlowIntensity ("Glow intensity", Range(0, 8)) = 2.5
        // 0 steady glow, 1 pulse, 2 flowing colours
        _Mode ("Mode", Float) = 0
        _Speed ("Speed", Range(0.1, 4)) = 0.6
        _FlowScale ("Flow scale", Float) = 3
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _GlowColor;
                half _GlowIntensity;
                half _Mode;
                half _Speed;
                half _FlowScale;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 viewDirWS : TEXCOORD2;
                float3 positionOS : TEXCOORD3;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewDirWS = GetWorldSpaceViewDir(position.positionWS);
                output.positionOS = input.positionOS.xyz;
                return output;
            }

            half3 FlowPalette(float t)
            {
                return 0.5 + 0.5 * cos(6.28318 * (t + float3(0.0, 0.33, 0.67)));
            }

            half4 Frag(Varyings input, bool isFrontFace : SV_IsFrontFace) : SV_Target
            {
                half3 normal = normalize(input.normalWS);
                if (!isFrontFace) normal = -normal;
                half3 viewDir = normalize(input.viewDirWS);

                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;
                Light light = GetMainLight();
                half3 diffuse = albedo * (light.color * saturate(dot(normal, light.direction)) + SampleSH(normal));

                half pulse = 0.55 + 0.45 * sin(_Time.y * _Speed * 6.28318);
                half3 glow = _GlowColor.rgb;
                half pattern = 1.0;
                if (_Mode > 1.5)
                    glow = FlowPalette(input.positionOS.x * _FlowScale + _Time.y * _Speed);
                else if (_Mode > 0.5)
                    pattern = pulse;

                half rim = pow(1.0 - saturate(dot(normal, viewDir)), 2.0);
                half3 emission = glow * _GlowIntensity * pattern * (0.45 + 0.9 * rim);
                // Soft roll-off so a strong glow keeps the part's shape instead of clipping to a flat colour.
                emission = 1.0 - exp(-emission);

                return half4(diffuse + emission, 1.0);
            }
            ENDHLSL
        }
    }
}
