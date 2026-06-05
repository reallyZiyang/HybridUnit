Shader "Hybrid/Baked Effect Atlas"
{
    Properties
    {
        _MainTex ("Atlas", 2D) = "white" {}
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _FrameUVRect ("Frame UV Rect", Vector) = (0, 0, 1, 1)
        _FrameUVClamp ("Frame UV Clamp", Vector) = (0, 0, 1, 1)
        _FrameTransform ("Frame Transform", Vector) = (0, 0, 1, 1)
        _InstanceColor ("Instance Color", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _FrameUVRect)
                UNITY_DEFINE_INSTANCED_PROP(float4, _FrameUVClamp)
                UNITY_DEFINE_INSTANCED_PROP(float4, _FrameTransform)
                UNITY_DEFINE_INSTANCED_PROP(float4, _InstanceColor)
            UNITY_INSTANCING_BUFFER_END(Props)

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float4 frameTransform = UNITY_ACCESS_INSTANCED_PROP(Props, _FrameTransform);
                float3 positionOS = input.positionOS.xyz;
                positionOS.xy = positionOS.xy * frameTransform.zw + frameTransform.xy;

                float4 frameUVRect = UNITY_ACCESS_INSTANCED_PROP(Props, _FrameUVRect);
                float4 frameUVClamp = UNITY_ACCESS_INSTANCED_PROP(Props, _FrameUVClamp);
                output.uv = clamp(frameUVRect.xy + input.uv * frameUVRect.zw, frameUVClamp.xy, frameUVClamp.zw);
                output.positionCS = TransformObjectToHClip(positionOS);
                output.color = input.color * _Tint * UNITY_ACCESS_INSTANCED_PROP(Props, _InstanceColor);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
                return color;
            }
            ENDHLSL
        }
    }
}
