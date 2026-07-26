Shader "UI/DiagonalWipe"
{
    Properties
    {
        [PerRendererData] _MainTex ("From (A)", 2D) = "white" {}
        _SecondTex ("To (B)", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _Progress ("Wipe Progress", Range(0, 1)) = 0
        _Soft ("Soft Edge", Range(0.001, 0.25)) = 0.04
        _GlowColor ("Glow Color", Color) = (1, 0.95, 0.75, 1)
        _GlowStrength ("Glow Strength", Range(0, 2)) = 0.35
        _Angle ("Wipe Angle (Degrees)", Range(0, 360)) = 45

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
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float2 localUV : TEXCOORD1;
                float4 worldPosition : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            sampler2D _SecondTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;
            float _Progress;
            float _Soft;
            fixed4 _GlowColor;
            float _GlowStrength;
            float _Angle;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.localUV = v.texcoord;
                OUT.color = v.color * _Color;
                return OUT;
            }

            float WipeCoord(float2 uv, float angleDeg)
            {
                float rad = angleDeg * 0.01745329251;
                float2 dir = float2(cos(rad), sin(rad));
                float t = dot(uv, dir);
                // Remap projection over the unit square to 0..1 so Progress covers the full image.
                float dMin = min(min(0.0, dir.x), min(dir.y, dir.x + dir.y));
                float dMax = max(max(0.0, dir.x), max(dir.y, dir.x + dir.y));
                return saturate((t - dMin) / max(dMax - dMin, 1e-4));
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                fixed4 fromCol = tex2D(_MainTex, uv) + _TextureSampleAdd;
                fixed4 toCol = tex2D(_SecondTex, uv) + _TextureSampleAdd;

                float diagonal = WipeCoord(IN.localUV, _Angle);
                float soft = max(_Soft, 0.001);
                float w = smoothstep(diagonal - soft, diagonal + soft, _Progress);
                fixed4 col = lerp(fromCol, toCol, w);

                float band = 1.0 - saturate(abs(diagonal - _Progress) / soft);
                col.rgb += _GlowColor.rgb * (band * band) * _GlowStrength;

                col *= IN.color;

                #ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a - 0.001);
                #endif

                return col;
            }
            ENDCG
        }
    }
}
