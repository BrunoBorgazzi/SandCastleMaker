Shader "UI/WorldBgLive"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _BiomeMode ("Biome Mode (0=Local 1=Brasil 2=Japon 3=Islandia 4=Tienda)", Float) = 0
        _ShaderTime ("Shader Time", Float) = 0
        _TiltX ("Device Tilt X", Float) = 0

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
            float _BiomeMode;
            float _ShaderTime;
            float _TiltX;
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
                if (rip.w <= 0.001 || age < 0.0 || age > 1.6) return 0.0;
                float radius = age * 0.14 + 0.010;
                float2 d = float2(uv.x - rip.x, (uv.y - rip.y) * 2.4);
                float dist = length(d);
                float ring = 1.0 - smoothstep(0.0, 0.012, abs(dist - radius));
                float fade = (1.0 - age / 1.6) * (1.0 - age / 1.6) * rip.w;
                return ring * fade;
            }

            float PixelHash(float2 cell)
            {
                return frac(sin(dot(cell, float2(127.1, 311.7))) * 43758.5453);
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                float nowT = _ShaderTime > 0.001 ? _ShaderTime : _Time.y;
                float tilt = clamp(_TiltX, -1.0, 1.0);
                int mode = (int)(_BiomeMode + 0.5);

                // Quantized pixel grid matching crisp 180x320 pixel-art resolution
                float2 qUV = floor(uv * float2(180.0, 320.0)) / float2(180.0, 320.0);
                fixed4 baseCol = tex2D(_MainTex, uv);
                fixed3 rgb = baseCol.rgb;

                // Subtle interactive touch ripples (masked inside water zones per biome)
                half ripSum = 0.0;
                ripSum += EvalRipple(qUV, _Ripple0, nowT);
                ripSum += EvalRipple(qUV, _Ripple1, nowT);
                ripSum += EvalRipple(qUV, _Ripple2, nowT);
                ripSum += EvalRipple(qUV, _Ripple3, nowT);

                // ==========================================================
                // MODE 0: PLAYA LOCAL (BG_Playa_Local.png)
                // Sky: v > 0.555 | Ocean + Foam: v 0.35..0.555 | Beach: v < 0.38
                // ==========================================================
                if (mode == 0)
                {
                    bool inSeaBand = (uv.y >= 0.355 && uv.y <= 0.555);
                    bool isBlueWater = inSeaBand && (baseCol.b > baseCol.r + 0.14 && baseCol.b > 0.42);
                    bool isSurfFoam = (uv.y >= 0.355 && uv.y <= 0.465) && (baseCol.r > 0.80 && baseCol.g > 0.86 && baseCol.b > 0.88);

                    if (isBlueWater || isSurfFoam)
                    {
                        // Calm perspective swell rolling slowly toward shore (~6.8s)
                        float d = saturate((0.555 - qUV.y) / 0.20);
                        float zPersp = 1.0 / (d + 0.18);
                        float waveShiftX = sin(zPersp * 4.2 + nowT * 0.88 + qUV.x * 8.0) * 0.0045 * (0.4 + 0.6 * d);
                        float swashY = sin(nowT * 0.82 + qUV.x * 5.0) * 0.0038 * smoothstep(0.45, 1.0, d);

                        float2 sampleUV = float2(clamp(uv.x + waveShiftX + tilt * 0.004 * d, 0.002, 0.998), clamp(uv.y + swashY, 0.356, 0.554));
                        fixed4 shifted = tex2Dlod(_MainTex, float4(sampleUV, 0, 0));
                        // Preserve sand & castle cleanly if shifted UV crosses shore edge
                        bool shiftedIsWater = (shifted.b > shifted.r + 0.12 && shifted.b > 0.38) || (shifted.r > 0.80 && shifted.g > 0.86 && shifted.b > 0.88);
                        if (shiftedIsWater) rgb = shifted.rgb;

                        // Gentle bright wave crest bands advancing toward the beach
                        float crest = step(0.76, sin(zPersp * 4.8 + nowT * 0.92 + sin(qUV.x * 11.0 + nowT * 0.25) * 0.35));
                        float crestPatch = smoothstep(0.18, 0.76, sin(qUV.x * 16.0 - nowT * 0.20 + zPersp) * 0.5 + 0.5);
                        rgb = lerp(rgb, fixed3(0.42, 0.86, 0.96), crest * crestPatch * (isBlueWater ? 0.26 : 0.0));

                        // Thin, natural horizontal sunlit water streaks (NO chunky square dots!)
                        float sheenWave = step(0.86, sin(zPersp * 9.5 + nowT * 0.72 + cos(qUV.x * 14.0) * 0.45));
                        float sheenLen  = smoothstep(0.35, 0.85, sin(qUV.x * 22.0 + zPersp * 1.5 - nowT * 0.28) * 0.5 + 0.5);
                        rgb = lerp(rgb, fixed3(0.68, 0.94, 0.99), sheenWave * sheenLen * (isBlueWater ? 0.22 : 0.0));

                        // Interactive ripples on water
                        rgb = lerp(rgb, fixed3(1.0, 0.96, 0.84), saturate(ripSum) * 0.82);
                    }
                    else if (uv.y >= 0.335 && uv.y <= 0.405 && baseCol.r > baseCol.b + 0.18)
                    {
                        // Subtle breathing wet-sand sheen right along shoreline edge
                        float wetBreath = 0.5 + 0.5 * sin(nowT * 0.82 + qUV.x * 4.5);
                        float nearShore = smoothstep(0.335, 0.395, uv.y) * (1.0 - smoothstep(0.395, 0.415, uv.y));
                        rgb = lerp(rgb, rgb * fixed3(0.84, 0.78, 0.68), nearShore * wetBreath * 0.38);
                    }
                    else if (uv.y > 0.58)
                    {
                        // Soft sunlight shimmer across top-right sunbeams
                        float sunGlow = smoothstep(0.55, 1.0, uv.x) * smoothstep(0.65, 1.0, uv.y);
                        float rayWave = 0.5 + 0.5 * sin((uv.x * 10.0 - uv.y * 8.0) + nowT * 0.45);
                        rgb += fixed3(0.05, 0.04, 0.01) * sunGlow * rayWave;
                    }
                }
                // ==========================================================
                // MODE 1: BRASIL - RIO CALIENTE (BG_Brasil_RioCaliente.png)
                // Sunbeams behind Cristo: v > 0.73 | Turquoise Bay: v 0.33..0.60 | Framing Palms: v < 0.46 edges
                // ==========================================================
                else if (mode == 1)
                {
                    // 1. Radiant golden sunbeams pulsing smoothly behind Cristo Redentor (preserve dark Cristo silhouette!)
                    bool isSunSky = (uv.y > 0.72) && (baseCol.r > 0.68 && baseCol.g > 0.60);
                    if (isSunSky)
                    {
                        float2 rel = uv - float2(0.50, 0.85);
                        float ang = atan2(rel.y, rel.x);
                        float rad = length(rel);
                        float beamPulse = 0.5 + 0.5 * sin(ang * 14.0 + nowT * 0.32);
                        float glowBreath = 0.5 + 0.5 * sin(nowT * 0.9 - rad * 5.0);
                        rgb += fixed3(0.08, 0.06, 0.01) * (beamPulse * 0.65 + glowBreath * 0.35) * smoothstep(0.55, 0.06, rad);
                    }

                    // 2. Curved turquoise Copacabana bay water (safely preserves green mountains & warm sand!)
                    bool inBay = (uv.y >= 0.33 && uv.y <= 0.61);
                    bool isBayWater = inBay && (baseCol.b > baseCol.r + 0.08 && baseCol.g > 0.30 && baseCol.b > 0.36);
                    bool isBayFoam = inBay && (baseCol.r > 0.76 && baseCol.g > 0.85 && baseCol.b > 0.82);
                    if (isBayWater || isBayFoam)
                    {
                        float d = saturate((0.61 - qUV.y) / 0.28);
                        float zPersp = 1.0 / (d + 0.20);
                        float sx = sin(zPersp * 4.5 + nowT * 0.85 + qUV.x * 7.5) * 0.0040;
                        float sy = cos(nowT * 0.78 + qUV.x * 6.0) * 0.0022;
                        fixed4 shifted = tex2Dlod(_MainTex, float4(clamp(uv.x + sx, 0.002, 0.998), clamp(uv.y + sy, 0.33, 0.61), 0, 0));
                        bool okShift = (shifted.b > shifted.r + 0.06 && shifted.b > 0.34) || (shifted.r > 0.74 && shifted.g > 0.82 && shifted.b > 0.78);
                        if (okShift) rgb = shifted.rgb;

                        float crest = step(0.76, sin(zPersp * 5.0 + nowT * 0.88 + sin(qUV.x * 12.0) * 0.4));
                        float maskP = smoothstep(0.22, 0.78, sin(qUV.x * 15.0 - nowT * 0.22) * 0.5 + 0.5);
                        rgb = lerp(rgb, fixed3(0.55, 0.96, 0.94), crest * maskP * (isBayWater ? 0.24 : 0.0));

                        // Soft sunlit horizontal sheen on the bay (no square white dots)
                        float sheenB = step(0.87, sin(zPersp * 9.0 + nowT * 0.68 + cos(qUV.x * 13.0) * 0.4)) * smoothstep(0.35, 0.85, sin(qUV.x * 20.0 - nowT * 0.24) * 0.5 + 0.5);
                        rgb = lerp(rgb, fixed3(0.80, 0.98, 0.96), sheenB * (isBayWater ? 0.20 : 0.0));
                        rgb = lerp(rgb, fixed3(1.0, 0.96, 0.84), saturate(ripSum) * 0.80);
                    }

                    // 3. Gentle warm sea breeze swaying the left/right framing palm & monstera leaves
                    bool isFoliageZone = (uv.y < 0.45) && (uv.x < 0.26 || uv.x > 0.74);
                    bool isGreenLeaf = isFoliageZone && (baseCol.g > baseCol.r + 0.06 && baseCol.g > baseCol.b + 0.06);
                    if (isGreenLeaf)
                    {
                        float sway = sin(nowT * 1.15 + qUV.y * 12.0 + (uv.x > 0.5 ? 1.8 : 0.0)) * 0.0035;
                        fixed4 leafS = tex2Dlod(_MainTex, float4(clamp(uv.x + sway, 0.001, 0.999), uv.y, 0, 0));
                        if (leafS.g > leafS.r + 0.04 && leafS.g > leafS.b + 0.04)
                            rgb = leafS.rgb;
                    }
                }
                // ==========================================================
                // MODE 2: JAPON - PLAYA OKINAWA (BG_Japon_Okinawa.png)
                // Sakura branch: u<0.46, v>0.68 | Lagoon + Torii: v 0.24..0.56 (exclude red Torii!)
                // ==========================================================
                else if (mode == 2)
                {
                    // 1. Calm Okinawa turquoise water + warm sun reflection shimmering around the rock-solid crimson Torii
                    bool inLagoon = (uv.y >= 0.24 && uv.y <= 0.565);
                    bool isRedTorii = (baseCol.r > baseCol.g + 0.18) || (baseCol.r < 0.20 && baseCol.g < 0.22 && baseCol.b < 0.34);
                    bool isTurqWater = inLagoon && !isRedTorii && (baseCol.g > 0.62 && baseCol.b > 0.62 && (baseCol.b - baseCol.r) > 0.04);
                    bool isSunRefl = inLagoon && !isRedTorii && (uv.x < 0.45 && uv.y > 0.41 && uv.y < 0.555 && baseCol.r > 0.85 && baseCol.g > 0.82);

                    if (isTurqWater || isSunRefl)
                    {
                        float d = saturate((0.565 - qUV.y) / 0.325);
                        float zPersp = 1.0 / (d + 0.18);
                        float wx = sin(zPersp * 4.4 + nowT * 0.78 + qUV.x * 8.0) * 0.0042;
                        fixed4 shifted = tex2Dlod(_MainTex, float4(clamp(uv.x + wx, 0.002, 0.998), uv.y, 0, 0));
                        bool shiftTorii = (shifted.r > shifted.g + 0.18) || (shifted.r < 0.20 && shifted.g < 0.22 && shifted.b < 0.34);
                        if (!shiftTorii && (shifted.b > 0.58 || shifted.g > 0.75))
                            rgb = shifted.rgb;

                        float glint = step(0.77, sin(zPersp * 5.2 + nowT * 0.82 + cos(qUV.x * 13.0) * 0.35));
                        float gMask = smoothstep(0.22, 0.76, sin(qUV.x * 14.0 + nowT * 0.18) * 0.5 + 0.5);
                        fixed3 glintTint = isSunRefl ? fixed3(1.0, 0.95, 0.82) : fixed3(0.70, 0.98, 0.96);
                        rgb = lerp(rgb, glintTint, glint * gMask * 0.28);

                        rgb = lerp(rgb, fixed3(1.0, 0.96, 0.88), saturate(ripSum) * 0.78);
                    }

                    // 2. Soft low sunrise warm breath around sun disc (u~0.24, v~0.60)
                    float sunDist = length((uv - float2(0.23, 0.60)) * float2(1.0, 1.7));
                    if (sunDist < 0.28 && uv.y > 0.55)
                    {
                        float pulse = 0.5 + 0.5 * sin(nowT * 0.85);
                        rgb += fixed3(0.06, 0.04, 0.02) * smoothstep(0.28, 0.04, sunDist) * pulse;
                    }

                    // 3. Gentle spring breeze rustling top-left cherry blossom branch (exclude flat pink sky)
                    if (uv.x < 0.45 && uv.y > 0.68)
                    {
                        bool isBranchOrPetal = (baseCol.r < 0.82) || (baseCol.g > 0.82);
                        if (isBranchOrPetal)
                        {
                            float sway = sin(nowT * 1.05 + qUV.y * 10.0) * 0.0026;
                            fixed4 s = tex2Dlod(_MainTex, float4(clamp(uv.x + sway, 0.001, 0.999), uv.y, 0, 0));
                            if ((s.r < 0.84) || (s.g > 0.82))
                                rgb = s.rgb;
                        }
                    }
                }
                // ==========================================================
                // MODE 3: ISLANDIA - PLAYA HELADA (BG_Islandia_Helada.png)
                // Aurora & Starry Sky: v > 0.72 | Glacial Sea: v 0.29..0.64 | Ice Crystals: v < 0.26
                // ==========================================================
                else if (mode == 3)
                {
                    // 1. Living undulating Aurora Borealis curtains + twinkling arctic stars in night sky!
                    if (uv.y > 0.70)
                    {
                        // Smooth vertical/horizontal curtain wave along the green-cyan aurora ribbons
                        float aurWaveX = sin(qUV.y * 16.0 - nowT * 0.75 + qUV.x * 5.0) * 0.0065;
                        float aurWaveY = cos(qUV.x * 9.0 + nowT * 0.55) * 0.0040;
                        float2 aurUV = float2(clamp(uv.x + aurWaveX, 0.002, 0.998), clamp(uv.y + aurWaveY, 0.70, 0.998));
                        fixed4 aurSample = tex2Dlod(_MainTex, float4(aurUV, 0, 0));
                        bool isAurora = (aurSample.g > 0.30 && aurSample.g > aurSample.r + 0.12) || (baseCol.g > 0.30 && baseCol.g > baseCol.r + 0.12);
                        if (isAurora)
                        {
                            float shimmer = 0.78 + 0.32 * sin(qUV.x * 11.0 - nowT * 1.1 + qUV.y * 8.0);
                            rgb = aurSample.rgb * shimmer;
                            // Subtle luminous emerald-cyan glow
                            rgb += fixed3(0.01, 0.08, 0.06) * saturate(aurSample.g - 0.35) * (0.5 + 0.5 * sin(nowT * 1.4 + qUV.x * 8.0));
                        }
                        else if (baseCol.b > 0.42 && baseCol.g > 0.32 && baseCol.r > 0.18)
                        {
                            // Twinkle ONLY the real painted arctic stars already in the artwork (no fake squares!)
                            float phase = PixelHash(floor(uv * float2(36.0, 64.0))) * 40.0;
                            float twinkle = 0.78 + 0.32 * sin(nowT * 2.2 + phase);
                            rgb = saturate(baseCol.rgb * twinkle);
                        }
                    }

                    // 2. Glacial fjord water gently rippling around floating icebergs (icebergs are bright white-cyan -> untouched!)
                    bool inFjord = (uv.y >= 0.29 && uv.y <= 0.645);
                    bool isGlacialSea = inFjord && (baseCol.b > 0.36 && baseCol.g > 0.26 && baseCol.r < 0.34);
                    if (isGlacialSea)
                    {
                        float d = saturate((0.645 - qUV.y) / 0.355);
                        float zPersp = 1.0 / (d + 0.20);
                        float sx = sin(zPersp * 4.4 + nowT * 0.72 + qUV.x * 7.0) * 0.0038;
                        fixed4 shifted = tex2Dlod(_MainTex, float4(clamp(uv.x + sx, 0.002, 0.998), uv.y, 0, 0));
                        if (shifted.r < 0.38 && shifted.b > 0.34) rgb = shifted.rgb;

                        float crest = step(0.76, sin(zPersp * 5.0 + nowT * 0.76 + cos(qUV.x * 11.0) * 0.35));
                        float maskC = smoothstep(0.2, 0.75, sin(qUV.x * 14.0 - nowT * 0.18) * 0.5 + 0.5);
                        // Subtle green-cyan aurora reflection dancing on the dark glacial fjord!
                        rgb = lerp(rgb, fixed3(0.32, 0.86, 0.82), crest * maskC * 0.26);
                        rgb = lerp(rgb, fixed3(0.84, 0.96, 1.0), saturate(ripSum) * 0.78);
                    }

                    // 3. Smooth diagonal crystalline sheen across foreground ice crystals (v < 0.27, no square dots)
                    if (uv.y < 0.27 && baseCol.b > 0.62 && baseCol.g > 0.55)
                    {
                        float sweep = smoothstep(0.88, 0.99, sin(qUV.x * 14.0 + qUV.y * 10.0 - nowT * 1.45));
                        rgb = lerp(rgb, fixed3(0.92, 0.99, 1.0), sweep * 0.32);
                    }
                }
                // ==========================================================
                // MODE 4: TIENDA DE PLAYAS (BG_Tienda_Playas.png)
                // Giant Sun: (0.32, 0.835) | Tropical Palms: v > 0.635 | Ocean + Surf: v 0.456..0.632 | Beach: v < 0.46
                // ==========================================================
                else if (mode == 4)
                {
                    // 1. Calm turquoise tropical ocean + sunlit reflection + breaking white surf rolling slowly to shore
                    bool inSea = (uv.y >= 0.456 && uv.y <= 0.632);
                    bool isBlueSea = inSea && (baseCol.b > baseCol.r + 0.08 && baseCol.g > 0.44 && baseCol.b > 0.52);
                    bool isSurfOrGlint = inSea && (baseCol.r > 0.68 && baseCol.g > 0.84 && baseCol.b > 0.78);
                    if (isBlueSea || isSurfOrGlint)
                    {
                        float d = saturate((0.632 - qUV.y) / 0.176);
                        float zPersp = 1.0 / (d + 0.18);
                        float sx = sin(zPersp * 4.4 + nowT * 0.86 + qUV.x * 8.0) * 0.0044 * (0.45 + 0.55 * d);
                        float sy = sin(nowT * 0.80 + qUV.x * 5.2) * 0.0034 * smoothstep(0.45, 1.0, d);
                        float2 sUV = float2(clamp(uv.x + sx + tilt * 0.004 * d, 0.002, 0.998), clamp(uv.y + sy, 0.457, 0.631));
                        fixed4 shifted = tex2Dlod(_MainTex, float4(sUV, 0, 0));
                        bool shiftOk = (shifted.b > shifted.r + 0.06 && shifted.g > 0.42 && shifted.b > 0.48) || (shifted.r > 0.68 && shifted.g > 0.84 && shifted.b > 0.78);
                        if (shiftOk) rgb = shifted.rgb;

                        // Gentle perspective wave crests gliding slowly toward the sandy shore
                        float crest = step(0.76, sin(zPersp * 4.8 + nowT * 0.88 + sin(qUV.x * 11.0 + nowT * 0.24) * 0.35));
                        float crestMask = smoothstep(0.18, 0.76, sin(qUV.x * 15.0 - nowT * 0.20 + zPersp) * 0.5 + 0.5);
                        rgb = lerp(rgb, fixed3(0.62, 0.98, 0.99), crest * crestMask * (isBlueSea ? 0.24 : 0.0));

                        // Thin sunlit horizontal shimmer along the tropical sun reflection path (NO square dots!)
                        float sheen = step(0.86, sin(zPersp * 9.2 + nowT * 0.70 + cos(qUV.x * 14.0) * 0.45));
                        float sheenSpan = smoothstep(0.35, 0.85, sin(qUV.x * 20.0 + zPersp * 1.4 - nowT * 0.26) * 0.5 + 0.5);
                        rgb = lerp(rgb, fixed3(0.92, 1.0, 0.94), sheen * sheenSpan * (isBlueSea ? 0.22 : 0.0));

                        // Interactive water touch ripples
                        rgb = lerp(rgb, fixed3(1.0, 0.97, 0.86), saturate(ripSum) * 0.82);
                    }
                    else if (uv.y >= 0.432 && uv.y <= 0.468 && baseCol.r > 0.80 && baseCol.b < 0.48)
                    {
                        // Subtle breathing wet shoreline sheen synchronized with gentle wave swash
                        float wetBreath = 0.5 + 0.5 * sin(nowT * 0.80 + qUV.x * 5.0);
                        float nearEdge = smoothstep(0.432, 0.460, uv.y) * (1.0 - smoothstep(0.460, 0.472, uv.y));
                        rgb = lerp(rgb, rgb * fixed3(0.86, 0.78, 0.66), nearEdge * wetBreath * 0.34);
                    }

                    // 2. Radiant tropical sun gentle pulse & subtle rotating ray shimmer (center ~0.32, 0.835)
                    if (uv.y > 0.69 && uv.x > 0.08 && uv.x < 0.58)
                    {
                        float2 sRel = (uv - float2(0.32, 0.835)) * float2(1.0, 1.78);
                        float sDist = length(sRel);
                        if (sDist < 0.26)
                        {
                            float sAng = atan2(sRel.y, sRel.x);
                            float raySpin = 0.5 + 0.5 * sin(sAng * 16.0 + nowT * 0.65);
                            float sunPulse = 0.5 + 0.5 * sin(nowT * 1.15);
                            bool isSunBody = (baseCol.r > 0.82 && baseCol.g > 0.45 && baseCol.b < 0.52);
                            if (isSunBody)
                            {
                                rgb += fixed3(0.06, 0.05, 0.01) * (sunPulse * 0.65 + raySpin * 0.35);
                            }
                            else if (baseCol.b > 0.75 && baseCol.g > 0.75)
                            {
                                // Soft golden halo breathing in the cyan sky just outside the sun's spikes
                                rgb += fixed3(0.08, 0.06, 0.01) * smoothstep(0.26, 0.12, sDist) * (0.45 + 0.55 * raySpin);
                            }
                        }
                    }

                    // 3. Gentle tropical sea breeze swaying the lush green framing palm fronds (preserve brown trunks & sky!)
                    if (uv.y > 0.635)
                    {
                        bool isPalmGreen = (baseCol.g > baseCol.r + 0.04 && baseCol.g > baseCol.b + 0.08);
                        if (isPalmGreen)
                        {
                            float sidePhase = uv.x > 0.46 ? 1.7 : 0.0;
                            float tipWeight = smoothstep(0.98, 0.66, uv.y) * 0.65 + 0.35;
                            float sway = sin(nowT * 1.15 + qUV.y * 10.0 + sidePhase) * 0.0036 * tipWeight;
                            fixed4 leafS = tex2Dlod(_MainTex, float4(clamp(uv.x + sway, 0.001, 0.999), uv.y, 0, 0));
                            if (leafS.g > leafS.r + 0.02 && leafS.g > leafS.b + 0.04)
                                rgb = leafS.rgb;
                        }
                    }
                }

                return fixed4(rgb * IN.color.rgb, baseCol.a * IN.color.a);
            }
            ENDCG
        }
    }
}
