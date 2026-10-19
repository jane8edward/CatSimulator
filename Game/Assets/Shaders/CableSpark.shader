Shader "CatSimulator/CableSpark"
{
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Input {float4 positionOS:POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;};
            struct Output {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;};
            Output Vert(Input v){Output o;o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.uv=v.uv;o.color=v.color;return o;}
            half4 Frag(Output i):SV_Target
            {
                float2 p=i.uv*2-1;
                half glow=saturate(1-dot(p,p));
                return half4(lerp(i.color.rgb,half3(1,1,.9),glow*glow)*2,glow*glow*i.color.a);
            }
            ENDHLSL
        }
    }
}
