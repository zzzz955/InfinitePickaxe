Shader "UI/InfiniteMineMineralEffect"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _OutlineColor ("Outline Color", Color) = (1,1,1,1)
        _OutlineWidth ("Outline Width", Range(0,5)) = 1
        _AlphaThreshold ("Alpha Threshold", Range(0,1)) = 0.1
        _GlowColor ("Glow Color", Color) = (1,0.5,0.1,1)
        _GlowStrength ("Glow Strength", Range(0,2)) = 0
        _GlowNoiseScale ("Glow Noise Scale", Range(0,30)) = 10
        _GlowSpeed ("Glow Speed", Range(0,10)) = 2
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
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile __ UNITY_UI_CLIP_RECT
            #pragma multi_compile __ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 worldPosition : TEXCOORD1;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            fixed4 _OutlineColor;
            float _OutlineWidth;
            float _AlphaThreshold;
            fixed4 _GlowColor;
            float _GlowStrength;
            float _GlowNoiseScale;
            float _GlowSpeed;
            float4 _ClipRect;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color = v.color * _Color;
                o.worldPosition = v.vertex;
                return o;
            }

            float SampleAlpha(float2 uv)
            {
                if (uv.x < 0 || uv.x > 1 || uv.y < 0 || uv.y > 1)
                {
                    return 0.0;
                }
                return tex2D(_MainTex, uv).a;
            }

            float Hash21(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));
                float x1 = lerp(a, b, u.x);
                float x2 = lerp(c, d, u.x);
                return lerp(x1, x2, u.y);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float padding = _OutlineWidth * max(_MainTex_TexelSize.x, _MainTex_TexelSize.y);
                float safeScale = max(0.0001, 1.0 - padding * 2.0);
                float2 uv = (i.uv - 0.5) / safeScale + 0.5;

                fixed4 baseCol = 0;
                if (uv.x >= 0 && uv.x <= 1 && uv.y >= 0 && uv.y <= 1)
                {
                    baseCol = tex2D(_MainTex, uv) * i.color;
                }
                float alpha = baseCol.a;
                float threshold = _AlphaThreshold;

                float2 texel = _MainTex_TexelSize.xy * _OutlineWidth;
                float maxNeighbor = 0.0;
                maxNeighbor = max(maxNeighbor, SampleAlpha(uv + float2(texel.x, 0)));
                maxNeighbor = max(maxNeighbor, SampleAlpha(uv + float2(-texel.x, 0)));
                maxNeighbor = max(maxNeighbor, SampleAlpha(uv + float2(0, texel.y)));
                maxNeighbor = max(maxNeighbor, SampleAlpha(uv + float2(0, -texel.y)));
                maxNeighbor = max(maxNeighbor, SampleAlpha(uv + float2(texel.x, texel.y)));
                maxNeighbor = max(maxNeighbor, SampleAlpha(uv + float2(-texel.x, texel.y)));
                maxNeighbor = max(maxNeighbor, SampleAlpha(uv + float2(texel.x, -texel.y)));
                maxNeighbor = max(maxNeighbor, SampleAlpha(uv + float2(-texel.x, -texel.y)));

                float outlineMask = step(threshold, maxNeighbor) * (1.0 - step(threshold, alpha));

                float glow = 0.0;
                if (_GlowStrength > 0.0001)
                {
                    float2 flameUv = uv * _GlowNoiseScale;
                    flameUv.y -= _Time.y * _GlowSpeed;
                    float n1 = Noise(flameUv);
                    float n2 = Noise(flameUv * 1.9 + float2(13.2, 7.9));
                    float flame = saturate(n1 * 0.65 + n2 * 0.35);
                    flame = pow(flame, 1.6);
                    float flicker = 0.7 + 0.3 * sin((_Time.y * (_GlowSpeed * 1.2 + 0.5)) + n2 * 6.2831853);
                    glow = _GlowStrength * flame * flicker;
                }

                fixed4 outCol = baseCol;
                outCol.rgb = lerp(outCol.rgb, _OutlineColor.rgb, outlineMask);
                outCol.rgb += _GlowColor.rgb * glow * outlineMask;
                outCol.a = max(outCol.a, _OutlineColor.a * outlineMask);
                outCol.a = saturate(outCol.a);

                #ifdef UNITY_UI_CLIP_RECT
                outCol.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(outCol.a - 0.001);
                #endif

                return outCol;
            }
            ENDCG
        }
    }
}
