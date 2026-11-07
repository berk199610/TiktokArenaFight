// Silhouette of characters hidden behind the arena walls (user request): drawn only where something is in front
// of the character (ZTest Greater) and not where another character is (stencil), above the feet.
Shader "Arena/XRay"
{
    Properties
    {
        _Color ("Color", Color) = (0.55, 0.85, 1, 0.55)
        _MinY ("Min world height", Float) = 0.35
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "XRay"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Greater
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _MinY;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; };
            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }
            half4 frag (Varyings i) : SV_Target
            {
                clip(i.positionWS.y - _MinY);
                float3 view = normalize(GetCameraPositionWS() - i.positionWS);
                float rim = 1.0 - saturate(dot(normalize(i.normalWS), view));
                half4 c = _Color;
                c.a = saturate(_Color.a * (0.45 + rim * 1.2));
                c.rgb = _Color.rgb * (0.7 + rim * 0.8);
                return c;
            }
            ENDHLSL
        }
    }
}
