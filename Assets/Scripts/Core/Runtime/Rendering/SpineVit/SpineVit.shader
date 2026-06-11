Shader "Hybrid/Spine VIT"
{
    Properties
    {
        _MainTex ("Spine Atlas", 2D) = "white" {}
        _PositionTex ("Position VIT", 2D) = "black" {}
        _ColorTex ("Color VIT", 2D) = "white" {}
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _FrameIndex ("Frame Index", Float) = 0
        _InstanceColor ("Instance Color", Color) = (1, 1, 1, 1)
        _RenderTrans ("Render Transform", Vector) = (0, 0, 1, 1)
        _RenderRotation ("Render Rotation", Vector) = (1, 0, 0, 0)
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
            TEXTURE2D(_PositionTex);
            TEXTURE2D(_ColorTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float, _FrameIndex)
                UNITY_DEFINE_INSTANCED_PROP(float4, _InstanceColor)
                UNITY_DEFINE_INSTANCED_PROP(float4, _RenderTrans)
                UNITY_DEFINE_INSTANCED_PROP(float4, _RenderRotation)
            UNITY_INSTANCING_BUFFER_END(Props)

            struct Attributes
            {
                float2 uv : TEXCOORD0;
                uint vertexID : SV_VertexID;
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

                int frameIndex = max(0, (int)round(UNITY_ACCESS_INSTANCED_PROP(Props, _FrameIndex)));
                // VIT 布局：x = vertexID，y = frameIndex，避免 CPU 每帧重建 Spine 网格。
                float4 bakedPosition = LOAD_TEXTURE2D(_PositionTex, int2(input.vertexID, frameIndex));
                float4 renderTrans = UNITY_ACCESS_INSTANCED_PROP(Props, _RenderTrans);
                float4 renderRotation = UNITY_ACCESS_INSTANCED_PROP(Props, _RenderRotation);
                float2 renderPosition = bakedPosition.xy * renderTrans.zw;
                bakedPosition.xy = float2(
                    renderPosition.x * renderRotation.x - renderPosition.y * renderRotation.y,
                    renderPosition.x * renderRotation.y + renderPosition.y * renderRotation.x) + renderTrans.xy;
                float4 bakedColor = LOAD_TEXTURE2D(_ColorTex, int2(input.vertexID, frameIndex));

                output.positionCS = TransformObjectToHClip(float3(bakedPosition.xyz));
                output.uv = input.uv;
                output.color = bakedColor * bakedPosition.w * _Tint * UNITY_ACCESS_INSTANCED_PROP(Props, _InstanceColor);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
            }
            ENDHLSL
        }
    }
}
