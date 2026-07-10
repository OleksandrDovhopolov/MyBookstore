Shader "UI/Decor/AlphaOutline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _OutlineColor ("Outline Color", Color) = (1,1,1,1)
        _OutlineWidth ("Outline Width", Range(0, 16)) = 8

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _OutlineColor;
            float _OutlineWidth;
            float4 _MainTex_TexelSize;
            float4 _ClipRect;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 baseColor = tex2D(_MainTex, IN.texcoord) * IN.color;
                float2 stepUv = _MainTex_TexelSize.xy * _OutlineWidth;

                fixed centerAlpha = baseColor.a;
                fixed aroundAlpha = 0;

                aroundAlpha = max(aroundAlpha, tex2D(_MainTex, IN.texcoord + float2( stepUv.x, 0)).a);
                aroundAlpha = max(aroundAlpha, tex2D(_MainTex, IN.texcoord + float2(-stepUv.x, 0)).a);
                aroundAlpha = max(aroundAlpha, tex2D(_MainTex, IN.texcoord + float2(0,  stepUv.y)).a);
                aroundAlpha = max(aroundAlpha, tex2D(_MainTex, IN.texcoord + float2(0, -stepUv.y)).a);
                aroundAlpha = max(aroundAlpha, tex2D(_MainTex, IN.texcoord + float2( stepUv.x,  stepUv.y)).a);
                aroundAlpha = max(aroundAlpha, tex2D(_MainTex, IN.texcoord + float2(-stepUv.x,  stepUv.y)).a);
                aroundAlpha = max(aroundAlpha, tex2D(_MainTex, IN.texcoord + float2( stepUv.x, -stepUv.y)).a);
                aroundAlpha = max(aroundAlpha, tex2D(_MainTex, IN.texcoord + float2(-stepUv.x, -stepUv.y)).a);

                fixed outlineAlpha = saturate(aroundAlpha - centerAlpha);
                fixed4 outlineColor = _OutlineColor;
                outlineColor.a *= outlineAlpha * IN.color.a;

                fixed4 result = baseColor;
                result.rgb = lerp(outlineColor.rgb, result.rgb, centerAlpha);
                result.a = max(result.a, outlineColor.a);

                #ifdef UNITY_UI_CLIP_RECT
                result.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(result.a - 0.001);
                #endif

                return result;
            }
            ENDCG
        }
    }
}
