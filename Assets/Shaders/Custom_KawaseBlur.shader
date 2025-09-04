Shader "Custom/KawaseBlur"
{
    Properties { _MainTex ("Texture", 2D) = "white" {} _Offset ("Offset", Float) = 1 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        ZWrite Off Cull Off Blend One Zero

        Pass
        {
            Name "KAWASE"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;   // set by Unity
            float _Offset;

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 positionHCS: SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes v) {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target {
                float2 t = _MainTex_TexelSize.xy * _Offset;
                half4 c  = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * 0.227027;
                c += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + t * float2( 1, 0)) * 0.316216;
                c += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + t * float2(-1, 0)) * 0.316216;
                c += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + t * float2( 0, 1)) * 0.070270;
                c += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + t * float2( 0,-1)) * 0.070270;
                return c;
            }
            ENDHLSL
        }
    }
}
