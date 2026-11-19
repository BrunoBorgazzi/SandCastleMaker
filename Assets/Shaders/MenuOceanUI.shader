Shader "UI/MenuOceanPerspective"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _HorizonColor ("Horizon Deep Sea", Color) = (0.05, 0.42, 0.78, 1)
        _MidSeaColor ("Mid Sea Blue", Color) = (0.09, 0.65, 0.91, 1)
        _ShallowSeaColor ("Shallow Turquoise", Color) = (0.24, 0.84, 0.93, 1)
        _FoamColor ("Foam Cream", Color) = (1.0, 0.96, 0.84, 1)
        _WetSandColor ("Wet Shore Shadow", Color) = (0.58, 0.40, 0.17, 0.62)

        _ShaderTime ("Shader Time", Float) = 0
        _TiltX ("Device Tilt X", Float) = 0
        _PixelCols ("Pixel Columns", Float) = 180
        _PixelRows ("Pixel Rows", Float) = 84

        _Ripple0 ("Ripple 0 (u,v,startTime,amp)", Vector) = (0.5, 0.5, -20, 0)
        _Ripple1 ("Ripple 1 (u,v,startTime,amp)", Vector) = (0.5, 0.5, -20, 0)
        _Ripple2 ("Ripple 2 (u,v,startTime,amp)", Vector) = (0.5, 0.5, -20, 0)
        _Ripple3 ("Ripple 3 (u,v,startTime,amp)", Vector) = (0.5, 0.5, -20, 0)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
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

            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _HorizonColor;
            fixed4 _MidSeaColor;
            fixed4 _ShallowSeaColor;
            fixed4 _FoamColor;
            fixed4 _WetSandColor;
            float _ShaderTime;
            float _TiltX;
            float _PixelCols;
            float _PixelRows;
            float4 _Ripple0;
            float4 _Ripple1;
            float4 _Ripple2;
            float4 _Ripple3;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                return OUT;
            }

            half EvalRipple(float2 uv, float4 rip, float nowTime)
            {
                float age = nowTime - rip.z;
                if (rip.w <= 0.001 || age < 0.0 || age > 1.75) return 0.0;
                float radius = age * 0.21 + 0.014;
                // Elliptical perspective flattening (flatter near horizon, rounder near beach)
                float ySquash = 1.9 + saturate(rip.y) * 1.5;
                float2 d = float2(uv.x - rip.x, (uv.y - rip.y) * ySquash);
                float dist = length(d);
                float ring = 1.0 - smoothstep(0.0, 0.018, abs(dist - radius));
                float fade = (1.0 - age / 1.75) * (1.0 - age / 1.75) * rip.w;
                return ring * fade;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float cols = max(_PixelCols, 64.0);
                float rows = max(_PixelRows, 32.0);
                float2 qUV = floor(IN.texcoord * float2(cols, rows)) / float2(cols, rows);
                float nowT = _ShaderTime > 0.001 ? _ShaderTime : _Time.y;
                float tilt = clamp(_TiltX, -1.0, 1.0);

                // d = 0 at horizon (top), d = 1 at sandy shore (bottom)
                float d = saturate(1.0 - qUV.y);

                // True perspective foreshortening: tightly packed horizontal waves at horizon,
                // opening up wide and slowing gracefully as they approach the beach.
                float zPersp = 1.0 / (d + 0.14);

                // Subtle horizontal parallax shift on tilt (ZERO diagonal phase skew!)
                float xPos = qUV.x + tilt * 0.035 * d;

                // Slow, soothing real-ocean breathing swash cycle (~7.7 seconds per wave)
                float cycle = frac(nowT * 0.13);
                float swash;
                if (cycle < 0.38)
                {
                    // Gentle run-up onto the beach
                    swash = sin((cycle / 0.38) * 1.5707963);
                }
                else if (cycle < 0.50)
                {
                    // Soft natural pause at high-water mark
                    swash = 1.0;
                }
                else
                {
                    // Smooth slow backwash retreat revealing glistening wet sand
                    swash = 0.5 * (1.0 + cos(((cycle - 0.50) / 0.50) * 3.1415926));
                }

                // Gentle organic shoreline curve
                float curveA = sin(xPos * 5.8 + nowT * 0.32) * 0.020;
                float curveB = cos(xPos * 11.4 - nowT * 0.42) * 0.008;
                float tiltShore = tilt * (qUV.x - 0.5) * 0.016;
                float baseShoreY = 0.27 + curveA + curveB + tiltShore;
                float maxWetReach = 0.085 + curveA * 0.7 + tiltShore * 0.5;
                float shoreY = baseShoreY - swash * 0.145;

                // Below the live waterline: translucent receding wet sand trail
                if (qUV.y < shoreY)
                {
                    if (qUV.y >= maxWetReach)
                    {
                        float span = max(0.01, baseShoreY - maxWetReach);
                        float wetGrad = saturate((qUV.y - maxWetReach) / span);
                        // Pixel-stepped wet sheen that lingers as water recedes and dries gradually
                        float dryPulse = lerp(0.45, 0.95, swash);
                        float steppedWet = floor(wetGrad * 4.0 + 0.5) / 4.0;
                        fixed4 wetOut = _WetSandColor;
                        wetOut.a *= steppedWet * dryPulse * IN.color.a;
                        return wetOut;
                    }
                    return fixed4(0, 0, 0, 0);
                }

                // Horizontal wave trains rolling SLOWLY toward the viewer (+nowT moves crests toward larger d / lower qUV.y)
                float undul1 = sin(xPos * 9.5 + nowT * 0.28) * 0.28 + sin(xPos * 21.0 - nowT * 0.22) * 0.12;
                float undul2 = cos(xPos * 14.0 - nowT * 0.24) * 0.22;

                // Primary ocean swell (~6.6s period) + secondary calm sea texture (~9.5s)
                float swellA = sin(zPersp * 4.6 + nowT * 0.95 + undul1);
                float swellB = sin(zPersp * 7.8 + nowT * 0.66 + undul2);
                float waveField = swellA * 0.62 + swellB * 0.38;

                // 5-band stepped coastal pixel-art depth palette (deep sapphire -> cerulean -> azure -> turquoise -> aqua)
                float depthVal = saturate(d * 0.96 + waveField * 0.045);
                fixed3 c0 = _HorizonColor.rgb;
                fixed3 c1 = lerp(_HorizonColor.rgb, _MidSeaColor.rgb, 0.45);
                fixed3 c2 = _MidSeaColor.rgb;
                fixed3 c3 = lerp(_MidSeaColor.rgb, _ShallowSeaColor.rgb, 0.55);
                fixed3 c4 = _ShallowSeaColor.rgb;

                fixed3 seaRGB = c0;
                if (depthVal > 0.16) seaRGB = c1;
                if (depthVal > 0.36) seaRGB = c2;
                if (depthVal > 0.60) seaRGB = c3;
                if (depthVal > 0.80) seaRGB = c4;

                // Soft horizontal broken wave-crest highlights gliding slowly toward shore
                float patchMask1 = smoothstep(0.05, 0.65, sin(xPos * 13.0 + zPersp * 0.8 + nowT * 0.18) * 0.5 + 0.5);
                float patchMask2 = smoothstep(0.15, 0.75, cos(xPos * 23.0 - zPersp * 1.1 - nowT * 0.15) * 0.5 + 0.5);

                float brightCrest = step(0.68, swellA) * patchMask1 * smoothstep(0.06, 0.55, d);
                float midRipple   = step(0.76, swellB) * patchMask2 * (1.0 - d * 0.35);
                fixed3 crestTint  = lerp(_ShallowSeaColor.rgb * 1.12, _FoamColor.rgb, 0.38);
                seaRGB = lerp(seaRGB, crestTint, clamp(brightCrest * 0.48 + midRipple * 0.32, 0.0, 0.62));

                // Fine thin sunlit horizontal glints near the horizon (calm Reddit sea loop feel)
                float horizonSheen = step(0.84, sin(zPersp * 11.5 + nowT * 0.75 + sin(xPos * 18.0) * 0.6)) * (1.0 - smoothstep(0.0, 0.36, d)) * patchMask2;
                seaRGB = lerp(seaRGB, _FoamColor.rgb, horizonSheen * 0.42);

                // Soft, thin horizontal sunlit water streaks beneath sun-side (NO chunky square dots!)
                float sunBand = exp(-pow((qUV.x - 0.19 - tilt * 0.05) * 2.6, 2.0));
                float sunWave = step(0.86, sin(zPersp * 9.8 + nowT * 0.72 + cos(xPos * 14.0) * 0.45));
                float sunSpan = smoothstep(0.32, 0.86, sin(xPos * 20.0 + zPersp * 1.4 - nowT * 0.28) * 0.5 + 0.5);
                float sunStreak = sunWave * sunSpan * smoothstep(0.05, 0.24, d) * (1.0 - smoothstep(0.70, 0.94, d));
                seaRGB = lerp(seaRGB, _FoamColor.rgb, sunStreak * lerp(0.14, 0.34, sunBand));

                // Shoreline leading foam + broken trailing lacy foam ribbon
                float distToShore = qUV.y - shoreY;
                float laceNoise = sin(xPos * 34.0 + nowT * 0.65) * 0.007 + cos(xPos * 58.0 - nowT * 0.85) * 0.004;
                float foamLead = step(distToShore, 0.024 + laceNoise);
                float foamInner = step(distToShore, 0.044 + laceNoise * 1.2) * (1.0 - foamLead) * step(0.35, sin(xPos * 28.0 + nowT * 0.5) * 0.5 + 0.5);

                // Second delicate foam line rolling calmly behind the main surf
                float secondWavePos = 0.115 + 0.045 * sin(nowT * 0.82);
                float foamSecond = step(abs(distToShore - secondWavePos - laceNoise * 0.8), 0.009) * smoothstep(0.20, 0.72, sin(xPos * 15.0 - nowT * 0.35) * 0.5 + 0.5) * 0.70;

                // Interactive elliptical touch/drag ripples
                half ripSum = 0.0;
                ripSum += EvalRipple(qUV, _Ripple0, nowT);
                ripSum += EvalRipple(qUV, _Ripple1, nowT);
                ripSum += EvalRipple(qUV, _Ripple2, nowT);
                ripSum += EvalRipple(qUV, _Ripple3, nowT);

                float totalFoam = saturate(foamLead + foamInner * 0.55 + foamSecond + saturate(ripSum) * 0.85);
                seaRGB = lerp(seaRGB, _FoamColor.rgb, totalFoam);

                // Crisp deep horizon line separating sky & ocean cleanly
                if (qUV.y > 0.982)
                    seaRGB = lerp(seaRGB, _HorizonColor.rgb * 0.76, 0.70);

                return fixed4(seaRGB * IN.color.rgb, IN.color.a);
            }
            ENDCG
        }
    }
}
