Shader "Custom/Day07UnlitColor"
{
     // Material Inspector에 노출할 값을 선언.
    Properties
    {
        // _BaseColor 기본값을 하늘색
        _BaseColor ("Base Color", Color) = (0.2, 0.7, 1.0, 1.0)
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "RenderPipeline" = "UniversalPipeline" 
        }

        Pass
        {
            // Pass 이름 정의
            Name "ForwardUnlit"

            // URP의 일반적인 전방 렌더러 단계에서 실행
            Tags { "LightMode" = "UniversalForward" }
            
            HLSLPROGRAM // 셰이더 코드 시작

            #pragma vertex vert

            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                // float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                // float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return _BaseColor;
            }

            ENDHLSL // 셰이더 코드 종료
        }
    }
}
