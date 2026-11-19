Shader "UI/StoreCardLive"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _CardMode ("Card Mode (0=Local 1=Brasil 2=Japon 3=Islandia)", Float) = 0
        _WinRect ("Portrait Window (uMin,vMin,uMax,vMax)", Vector) = (0.12, 0.30, 0.91, 0.90)
        _ShaderTime ("Shader Time", Float) = 0
        _TiltX ("Device Tilt X", Float) = 0
        _SheenStrength ("Collectible Foil Sheen Strength", Float) = 1.0
        _FrostAmount ("Coming Soon Frost Amount", Float) = 0.0

        _Ripple0 ("Ripple 0 (u,v,startTime,amp)", Vector) = (0.5, 0.5, -20, 0)
        _Ripple1 ("Ripple 1 (u,v,startTime,amp)", Vector) = (0.5, 0.5, -20, 0)
        _Ripple2 ("Ripple 2 (u,v,startTime,amp)", Vector) = (0.5, 0.5, -20, 0)

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
            float _CardMode;
            float4 _WinRect;
            float _ShaderTime;
            float _TiltX;
            float _SheenStrength;
            float _FrostAmount;
            float4 _Ripple0;
            float4 _Ripple1;
            float4 _Ripple2;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                return OUT;
            }

            half EvalCardRipple(float2 uv, float4 rip, float nowTime)
            {
                float age = nowTime - rip.z;
                if (rip.w <= 0.001 || age < 0.0 || age > 1.45) return 0.0;
                float radius = age * 0.16 + 0.012;
                float2 d = float2(uv.x - rip.x, (uv.y - rip.y) * 2.1);
                float dist = length(d);
                float ring = 1.0 - smoothstep(0.0, 0.014, abs(dist - radius));
                float fade = (1.0 - age / 1.45) * (1.0 - age / 1.45) * rip.w;
                return ring * fade;
            }

            float CardHash(float2 cell)
            {
                return frac(sin(dot(cell, float2(127.1, 311.7))) * 43758.5453);
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                float nowT = _ShaderTime > 0.001 ? _ShaderTime : _Time.y;
                float tilt = clamp(_TiltX, -1.0, 1.0);
                int mode = (int)(_CardMode + 0.5);

                fixed4 baseCol = tex2D(_MainTex, uv);
                if (baseCol.a < 0.01) return fixed4(0, 0, 0, 0);

                fixed3 rgb = baseCol.rgb;

                // Quantized pixel grid matching crisp card pixel art
                float2 qUV = floor(uv * float2(140.0, 220.0)) / float2(140.0, 220.0);

                // Strictly gate all landscape animation inside the exact portrait window (_WinRect)
                bool inWin = (uv.x >= _WinRect.x && uv.x <= _WinRect.z && uv.y >= _WinRect.y && uv.y <= _WinRect.w);

                if (inWin)
                {
                    half ripSum = 0.0;
                    ripSum += EvalCardRipple(qUV, _Ripple0, nowT);
                    ripSum += EvalCardRipple(qUV, _Ripple1, nowT);
                    ripSum += EvalCardRipple(qUV, _Ripple2, nowT);

                    // ==========================================================
                    // MODE 0: CARD PLAYA LOCAL
                    // Sun: (0.515, 0.815) | Ocean: v 0.520..0.706 | 4 Red Castle Flags: v 0.45..0.70
                    // ==========================================================
                    if (mode == 0)
                    {
                        // 1. Calm turquoise ocean + white breaking waves behind the solid sandcastle
                        bool inSea = (uv.y >= 0.520 && uv.y <= 0.706);
                        bool isBlueSea = inSea && (baseCol.b > baseCol.r + 0.10 && baseCol.g > 0.48 && baseCol.b > 0.54);
                        bool isSurf = inSea && (baseCol.r > 0.72 && baseCol.g > 0.86 && baseCol.b > 0.84);
                        if (isBlueSea || isSurf)
                        {
                            float d = saturate((0.706 - qUV.y) / 0.186);
                            float zPersp = 1.0 / (d + 0.20);
                            float sx = sin(zPersp * 4.2 + nowT * 0.88 + qUV.x * 9.0) * 0.0055 * (0.45 + 0.55 * d);
                            float2 sUV = float2(clamp(uv.x + sx + tilt * 0.004 * d, _WinRect.x + 0.004, _WinRect.z - 0.004), uv.y);
                            fixed4 shifted = tex2Dlod(_MainTex, float4(sUV, 0, 0));
                            bool shiftOk = (shifted.b > shifted.r + 0.08 && shifted.g > 0.44 && shifted.b > 0.50) || (shifted.r > 0.72 && shifted.g > 0.86 && shifted.b > 0.84);
                            if (shiftOk) rgb = shifted.rgb;

                            float crest = step(0.76, sin(zPersp * 4.8 + nowT * 0.90 + sin(qUV.x * 12.0) * 0.35));
                            float cMask = smoothstep(0.20, 0.78, sin(qUV.x * 16.0 - nowT * 0.22) * 0.5 + 0.5);
                            rgb = lerp(rgb, fixed3(0.68, 0.99, 0.99), crest * cMask * (isBlueSea ? 0.26 : 0.0));
                            rgb = lerp(rgb, fixed3(1.0, 0.97, 0.84), saturate(ripSum) * 0.82);
                        }

                        // 2. Subtle breeze fluttering & light-rippling the 4 tiny crimson castle flags!
                        bool isRedFlag = (uv.y >= 0.45 && uv.y <= 0.70) && (baseCol.r > 0.58 && baseCol.g < 0.25 && baseCol.b < 0.26);
                        if (isRedFlag)
                        {
                            float waveF = sin(nowT * 5.2 - qUV.x * 52.0);
                            float flutterY = waveF * 0.0028;
                            fixed4 fS = tex2Dlod(_MainTex, float4(uv.x, clamp(uv.y + flutterY, _WinRect.y, _WinRect.w), 0, 0));
                            if (fS.r > 0.45 && fS.g < 0.30) rgb = fS.rgb;
                            rgb = lerp(rgb, fixed3(0.96, 0.28, 0.22), saturate(waveF) * 0.30);
                        }

                        // 3. Warm golden breathing glow around the smiling sun (preserves cute face!)
                        if (uv.y > 0.71)
                        {
                            float2 sRel = (uv - float2(0.515, 0.815)) * float2(1.0, 1.55);
                            float sDist = length(sRel);
                            if (sDist < 0.25)
                            {
                                float ang = atan2(sRel.y, sRel.x);
                                float pulse = 0.5 + 0.5 * sin(nowT * 1.25);
                                float rays = 0.5 + 0.5 * sin(ang * 12.0 + nowT * 0.70);
                                bool isSunWarm = (baseCol.r > 0.85 && baseCol.g > 0.50 && baseCol.b < 0.55);
                                if (isSunWarm)
                                    rgb += fixed3(0.05, 0.04, 0.01) * (pulse * 0.7 + rays * 0.3);
                                else if (baseCol.b > 0.75)
                                    rgb += fixed3(0.08, 0.06, 0.01) * smoothstep(0.25, 0.11, sDist) * (0.45 + 0.55 * rays);
                            }
                        }
                    }
                    // ==========================================================
                    // MODE 1: CARD BRASIL (RIO CALIENTE)
                    // Sun halo: (0.61, 0.72) | Palms: green fronds | Copacabana Bay: v 0.288..0.556
                    // ==========================================================
                    else if (mode == 1)
                    {
                        // 1. Radiant golden sun halo pulsing behind solid Cristo Redentor
                        if (uv.y > 0.56)
                        {
                            bool isWarmSky = (baseCol.r > 0.80 && baseCol.g > 0.74 && baseCol.b > 0.45);
                            if (isWarmSky)
                            {
                                float2 rel = (uv - float2(0.61, 0.72)) * float2(1.0, 1.50);
                                float rad = length(rel);
                                float ang = atan2(rel.y, rel.x);
                                float pulse = 0.5 + 0.5 * sin(nowT * 1.05 - rad * 5.0);
                                float rays = 0.5 + 0.5 * sin(ang * 12.0 + nowT * 0.40);
                                rgb += fixed3(0.08, 0.06, 0.01) * smoothstep(0.42, 0.05, rad) * (pulse * 0.65 + rays * 0.35);
                            }
                        }

                        // 2. Copacabana turquoise bay waves (safely preserves swimmers & warm sand!)
                        bool inBay = (uv.y >= 0.288 && uv.y <= 0.556);
                        bool isBayWater = inBay && (baseCol.b > baseCol.r + 0.10 && baseCol.g > 0.42 && baseCol.b > 0.46);
                        bool isBayFoam = inBay && (baseCol.r > 0.75 && baseCol.g > 0.88 && baseCol.b > 0.84);
                        if (isBayWater || isBayFoam)
                        {
                            float d = saturate((0.556 - qUV.y) / 0.268);
                            float zPersp = 1.0 / (d + 0.22);
                            float sx = sin(zPersp * 4.4 + nowT * 0.86 + qUV.x * 8.5) * 0.0050;
                            float2 sUV = float2(clamp(uv.x + sx + tilt * 0.004 * d, _WinRect.x + 0.004, _WinRect.z - 0.004), uv.y);
                            fixed4 shifted = tex2Dlod(_MainTex, float4(sUV, 0, 0));
                            bool shiftOk = (shifted.b > shifted.r + 0.08 && shifted.g > 0.40) || (shifted.r > 0.74 && shifted.g > 0.86 && shifted.b > 0.82);
                            if (shiftOk) rgb = shifted.rgb;

                            float crest = step(0.76, sin(zPersp * 5.0 + nowT * 0.88 + sin(qUV.x * 11.0) * 0.35));
                            float cMask = smoothstep(0.22, 0.76, sin(qUV.x * 15.0 - nowT * 0.22) * 0.5 + 0.5);
                            rgb = lerp(rgb, fixed3(0.66, 0.98, 0.96), crest * cMask * (isBayWater ? 0.24 : 0.0));
                            rgb = lerp(rgb, fixed3(1.0, 0.96, 0.82), saturate(ripSum) * 0.80);
                        }

                        // 3. Tropical breeze swaying green palm fronds (top-left & right side palms)
                        bool isGreenPalm = (baseCol.g > baseCol.r + 0.05 && baseCol.g > baseCol.b + 0.06);
                        if (isGreenPalm)
                        {
                            float sway = sin(nowT * 1.20 + qUV.y * 12.0 + (uv.x > 0.5 ? 1.8 : 0.0)) * 0.0042;
                            fixed4 leafS = tex2Dlod(_MainTex, float4(clamp(uv.x + sway, _WinRect.x + 0.002, _WinRect.z - 0.002), uv.y, 0, 0));
                            if (leafS.g > leafS.r + 0.03 && leafS.g > leafS.b + 0.04)
                                rgb = leafS.rgb;
                        }
                    }
                    // ==========================================================
                    // MODE 2: CARD JAPON (PLAYA OKINAWA)
                    // Lagoon + Torii: v 0.242..0.422 | Sun: (0.29, 0.46) | Sakura branch: u < 0.45, v > 0.62
                    // ==========================================================
                    else if (mode == 2)
                    {
                        // 1. Calm Okinawa turquoise lagoon & sunset reflection around rock-solid crimson Torii
                        bool inLagoon = (uv.y >= 0.242 && uv.y <= 0.422);
                        bool isTorii = (baseCol.r > baseCol.g + 0.16) || (baseCol.r < 0.22 && baseCol.g < 0.24 && baseCol.b < 0.35);
                        bool isWater = inLagoon && !isTorii && (baseCol.g > 0.60 && baseCol.b > 0.60 && (baseCol.b - baseCol.r) > 0.04);
                        bool isSunRefl = inLagoon && !isTorii && (uv.x < 0.46 && uv.y > 0.33 && baseCol.r > 0.84 && baseCol.g > 0.82);
                        if (isWater || isSunRefl)
                        {
                            float d = saturate((0.422 - qUV.y) / 0.180);
                            float zPersp = 1.0 / (d + 0.20);
                            float wx = sin(zPersp * 4.4 + nowT * 0.78 + qUV.x * 8.5) * 0.0046;
                            float2 sUV = float2(clamp(uv.x + wx + tilt * 0.003 * d, _WinRect.x + 0.004, _WinRect.z - 0.004), uv.y);
                            fixed4 shifted = tex2Dlod(_MainTex, float4(sUV, 0, 0));
                            bool shiftTorii = (shifted.r > shifted.g + 0.16) || (shifted.r < 0.22 && shifted.g < 0.24 && shifted.b < 0.35);
                            if (!shiftTorii && (shifted.b > 0.56 || shifted.g > 0.76))
                                rgb = shifted.rgb;

                            float glint = step(0.77, sin(zPersp * 5.0 + nowT * 0.82 + cos(qUV.x * 13.0) * 0.35));
                            float gMask = smoothstep(0.22, 0.76, sin(qUV.x * 14.0 + nowT * 0.18) * 0.5 + 0.5);
                            fixed3 tint = isSunRefl ? fixed3(1.0, 0.96, 0.84) : fixed3(0.72, 0.98, 0.97);
                            rgb = lerp(rgb, tint, glint * gMask * 0.28);
                            rgb = lerp(rgb, fixed3(1.0, 0.96, 0.88), saturate(ripSum) * 0.78);
                        }

                        // 2. Soft low sunrise warm breath around sun disc (u ~ 0.29, v ~ 0.46)
                        float sunD = length((uv - float2(0.29, 0.46)) * float2(1.0, 1.45));
                        if (sunD < 0.20 && uv.y > 0.415)
                        {
                            float pulse = 0.5 + 0.5 * sin(nowT * 0.92);
                            rgb += fixed3(0.06, 0.04, 0.02) * smoothstep(0.20, 0.03, sunD) * pulse;
                        }

                        // 3. Gentle spring breeze rustling cherry blossom clusters inside top-left window (never touches wooden header!)
                        if (uv.x < 0.46 && uv.y > 0.62 && uv.y < 0.78)
                        {
                            bool isSakura = (baseCol.r > 0.78 && baseCol.b > 0.68 && baseCol.g < 0.88) || (baseCol.r < 0.45 && baseCol.g < 0.35 && baseCol.b < 0.35);
                            if (isSakura)
                            {
                                float sway = sin(nowT * 1.10 + qUV.y * 11.0) * 0.0028;
                                fixed4 s = tex2Dlod(_MainTex, float4(clamp(uv.x + sway, _WinRect.x + 0.002, _WinRect.z - 0.002), uv.y, 0, 0));
                                rgb = s.rgb;
                            }
                        }
                    }
                    // ==========================================================
                    // MODE 3: CARD ISLANDIA (PLAYA HELADA)
                    // Aurora & Stars: v 0.560..0.862 | Glacial Fjord: v 0.455..0.568
                    // ==========================================================
                    else if (mode == 3)
                    {
                        // 1. Living undulating Aurora Borealis curtains + twinkling real painted arctic stars!
                        if (uv.y > 0.560)
                        {
                            // Protect crisp white/pale-blue hanging icicles along the top corners!
                            bool isIcicle = (baseCol.r > 0.55 && baseCol.g > 0.72 && baseCol.b > 0.82);
                            if (!isIcicle)
                            {
                                float aurX = sin(qUV.y * 18.0 - nowT * 0.82 + qUV.x * 5.5) * 0.0080;
                                float aurY = cos(qUV.x * 9.5 + nowT * 0.58) * 0.0045;
                                float2 aurUV = float2(clamp(uv.x + aurX, _WinRect.x + 0.004, _WinRect.z - 0.004), clamp(uv.y + aurY, 0.565, _WinRect.w - 0.004));
                                fixed4 aurSample = tex2Dlod(_MainTex, float4(aurUV, 0, 0));
                                bool isAurora = (aurSample.g > 0.28 && aurSample.g > aurSample.r + 0.10 && aurSample.r < 0.55)
                                             || (baseCol.g > 0.28 && baseCol.g > baseCol.r + 0.10 && baseCol.r < 0.55);
                                if (isAurora)
                                {
                                    float shimmer = 0.80 + 0.30 * sin(qUV.x * 11.0 - nowT * 1.15 + qUV.y * 8.0);
                                    rgb = aurSample.rgb * shimmer;
                                    rgb += fixed3(0.01, 0.09, 0.07) * saturate(aurSample.g - 0.30) * (0.5 + 0.5 * sin(nowT * 1.45 + qUV.x * 8.0));
                                }
                                else if (baseCol.b > 0.38 && baseCol.g > 0.28 && baseCol.r > 0.14)
                                {
                                    // Twinkle ONLY the real painted arctic stars already in the artwork
                                    float phase = CardHash(floor(uv * float2(34.0, 56.0))) * 40.0;
                                    float twinkle = 0.78 + 0.34 * sin(nowT * 2.3 + phase);
                                    rgb = saturate(baseCol.rgb * twinkle);
                                }
                            }
                        }

                        // 2. Glacial fjord rippling gently between snowy icebergs
                        bool inFjord = (uv.y >= 0.455 && uv.y <= 0.568);
                        bool isGlacialSea = inFjord && (baseCol.b > 0.38 && baseCol.g > 0.28 && baseCol.r < 0.36);
                        if (isGlacialSea)
                        {
                            float d = saturate((0.568 - qUV.y) / 0.113);
                            float zPersp = 1.0 / (d + 0.22);
                            float sx = sin(zPersp * 4.5 + nowT * 0.74 + qUV.x * 8.0) * 0.0044;
                            fixed4 shifted = tex2Dlod(_MainTex, float4(clamp(uv.x + sx, _WinRect.x + 0.004, _WinRect.z - 0.004), uv.y, 0, 0));
                            if (shifted.r < 0.42 && shifted.b > 0.35) rgb = shifted.rgb;

                            float crest = step(0.76, sin(zPersp * 5.0 + nowT * 0.78 + cos(qUV.x * 11.0) * 0.35));
                            float cMask = smoothstep(0.22, 0.76, sin(qUV.x * 14.0 - nowT * 0.18) * 0.5 + 0.5);
                            rgb = lerp(rgb, fixed3(0.32, 0.90, 0.84), crest * cMask * 0.26);
                            rgb = lerp(rgb, fixed3(0.84, 0.96, 1.0), saturate(ripSum) * 0.78);
                        }
                    }
                }

                // ==========================================================
                // SUBTLE COLLECTIBLE CARD FOIL / HOLO SHEEN SWEEP
                // Glides diagonally every ~5.4s + reacts softly to phone tilt!
                // Kept very light so pixel art & text stay 100% crisp and legible.
                // ==========================================================
                if (_SheenStrength > 0.01)
                {
                    float diag = uv.x * 0.85 + uv.y * 0.55;
                    float cyclePos = frac(nowT * 0.185) * 2.8 - 0.85;
                    float tiltPos = 0.65 + tilt * 0.55;
                    float bandA = smoothstep(0.11, 0.0, abs(diag - cyclePos)) * 0.15;
                    float bandB = smoothstep(0.16, 0.0, abs(diag - tiltPos)) * (abs(tilt) * 0.12);
                    float sheenAmt = (bandA + bandB) * _SheenStrength;

                    fixed3 foilCol = fixed3(1.0, 0.94, 0.76);
                    if (mode == 2) foilCol = fixed3(1.0, 0.88, 0.94);
                    else if (mode == 3) foilCol = fixed3(0.72, 0.98, 1.0);

                    // Slightly stronger glint along the collectible card frame, gentler over the portrait window
                    float frameBoost = inWin ? 0.55 : 1.0;
                    rgb = saturate(rgb + foilCol * sheenAmt * frameBoost);
                }

                // Pixel-exact Coming Soon frosted veil (applied ONLY on opaque card silhouette — ZERO ghost rectangle!)
                if (_FrostAmount > 0.001)
                {
                    rgb = lerp(rgb, fixed3(0.84, 0.92, 1.0), saturate(_FrostAmount));
                }

                return fixed4(rgb * IN.color.rgb, baseCol.a * IN.color.a);
            }
            ENDCG
        }
    }
}
