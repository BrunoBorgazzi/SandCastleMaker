Shader "SandCastle/TideWater"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        
        [Header(Pixel Art Water Palette)]
        _DeepWaterColor ("Deep Ocean Color", Color) = (0.07, 0.44, 0.68, 0.94)
        _ShallowWaterColor ("Shallow Swash Color", Color) = (0.18, 0.72, 0.82, 0.82)
        _FoamColor ("Breaking Foam Color", Color) = (0.98, 0.99, 0.96, 0.97)

        [Header(Tide and Wave Parameters)]
        _CurrentShoreY ("Current Shoreline World Y", Float) = 561.5
        _WavePhase ("Wave Animation Phase", Float) = 0.0
        _WaveCurveAmp ("Horizontal Wave Undulation Amp", Float) = 0.18
        _FoamThickness ("White Foam Band Thickness (World)", Float) = 0.26
        _ShallowThickness ("Shallow Turquoise Band Thickness (World)", Float) = 1.45
        _WindIntensity ("Wind Chop Intensity", Range(0, 1)) = 0.0
        _RandomSeed ("Wave Random Seed", Float) = 0.0
        _PixelGrid ("Pixel Grid Resolution", Float) = 160.0

        [Header(Wet Sand Wave Barrier)]
        _WaveBarrierTex ("Wave Barrier Map (RG 16bit Height, B Impact)", 2D) = "black" {}
        _UseWaveBarrier ("Use Wave Barrier (0 or 1)", Float) = 0.0
        _CanvasWorldX ("Canvas World X (MinX, InvWidth)", Vector) = (0, 1, 0, 0)
        _BarrierRangeY ("Barrier Y Range (MinY, SpanY)", Vector) = (550, 20, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent+10"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float2 worldPos : TEXCOORD1;
            };

            fixed4 _DeepWaterColor;
            fixed4 _ShallowWaterColor;
            fixed4 _FoamColor;

            float _CurrentShoreY;
            float _WavePhase;
            float _WaveCurveAmp;
            float _FoamThickness;
            float _ShallowThickness;
            half _WindIntensity;
            float _RandomSeed;
            float _PixelGrid;

            sampler2D _WaveBarrierTex;
            float _UseWaveBarrier;
            float4 _CanvasWorldX;
            float4 _BarrierRangeY;

            // Hash 2D aperiódico (sin patrón repetitivo en coordenadas enteras de grilla)
            float Hash21(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.worldPos = mul(unity_ObjectToWorld, IN.vertex).xy;
                OUT.color = IN.color;
                return OUT;
            }

            // Misma curva exacta que usa TideController en C# para sincronizar visual y erosión
            float ComputeShoreY(float u)
            {
                float uWrapped = u * 6.283185;
                float primary = sin(uWrapped + _WavePhase) * _WaveCurveAmp;
                float secondary = cos(uWrapped * 2.0 - _WavePhase * 1.35 + _RandomSeed) * (_WaveCurveAmp * 0.45);
                float windPacketA = sin(uWrapped * 3.0 + _WavePhase * 2.1 + _RandomSeed * 1.7) * (0.20 * _WindIntensity);
                float windPacketB = cos(uWrapped * 5.0 - _WavePhase * 2.9 + _RandomSeed * 0.6) * (0.12 * _WindIntensity);
                return _CurrentShoreY + primary + secondary + windPacketA + windPacketB;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // Grilla Pixel Art uniforme en espacio de mundo (evita estiramientos y aliasing vertical)
                float pixelWorldStep = 0.055;
                float2 pCell = floor(IN.worldPos / pixelWorldStep);
                float2 qWorld = pCell * pixelWorldStep;
                float qU = floor(IN.texcoord.x * _PixelGrid) / _PixelGrid;

                float shoreY = ComputeShoreY(qU);
                half impactForce = 0.0;

                // Consultar barrera de Arena Húmeda: frena la ola y activa oleaje de rebote
                if (_UseWaveBarrier > 0.5)
                {
                    float uCanvas = (qWorld.x - _CanvasWorldX.x) * _CanvasWorldX.y;
                    if (uCanvas >= 0.0 && uCanvas <= 1.0)
                    {
                        fixed4 bar = tex2D(_WaveBarrierTex, float2(uCanvas, 0.5));
                        float normBarY = bar.r * 0.9961089 + bar.g * 0.0038911;
                        float barWorldY = _BarrierRangeY.x + normBarY * _BarrierRangeY.y;
                        if (barWorldY > shoreY)
                        {
                            shoreY = barWorldY;
                        }
                        impactForce = bar.b;
                    }
                }

                float distAboveShore = qWorld.y - shoreY;

                // Si estamos por debajo del frente de la ola (o detrás del dique húmedo), es arena seca/protegida
                if (distAboveShore < 0.0)
                {
                    discard;
                }

                // 1. CRESTA DE ESPUMA BLANCA PIXEL ART (orgánica, más agitada con viento y borboteante al chocar)
                float animTick = floor(_Time.y * (5.0 + _WindIntensity * 5.0 + impactForce * 6.0));
                float foamNoise = Hash21(pCell + float2(floor(_RandomSeed * 13.0), animTick));
                float foamWarp = (ValueNoise(pCell * 0.16 + float2(_RandomSeed, animTick * 0.12)) - 0.5) * 0.10;
                float impactFoamBoost = impactForce * 0.24;
                float dynamicFoamWidth = max(0.04, _FoamThickness * (1.0 + _WindIntensity * 0.45) + impactFoamBoost + foamWarp);

                if (distAboveShore < dynamicFoamWidth)
                {
                    half isBubbleGap = step(0.68 - _WindIntensity * 0.12 + impactForce * 0.18, distAboveShore / dynamicFoamWidth) * step(0.56, foamNoise);
                    fixed4 foamCol = lerp(_FoamColor, _ShallowWaterColor, isBubbleGap);
                    return foamCol;
                }

                // Ondas de rebote contracorriente al golpear contra Arena Húmeda
                half reboundWave = 0.0;
                if (impactForce > 0.04 && distAboveShore < 1.55)
                {
                    float bouncePhase = distAboveShore * 13.5 - _Time.y * 12.5;
                    float bounceEnvelope = saturate(1.0 - distAboveShore / 1.55);
                    float stepRipple = step(0.58, sin(bouncePhase) * 0.5 + 0.5);
                    reboundWave = stepRipple * bounceEnvelope * impactForce * 0.85;
                }

                // 2. AGUA BAJA TURQUESA Y MAR PROFUNDO CON PROFUNDIDAD ORGÁNICA
                float depthJitter = (ValueNoise(pCell * float2(0.075, 0.12) + float2(_RandomSeed * 0.37, -_WavePhase * 0.18)) - 0.5) * 0.38;
                float shallowT = saturate((distAboveShore - dynamicFoamWidth + depthJitter) / max(_ShallowThickness, 0.01));
                float steppedT = floor(shallowT * 4.0) / 4.0;
                fixed4 waterCol = lerp(_ShallowWaterColor, _DeepWaterColor, steppedT);

                // 3. ONDAS DE SUPERFICIE APERIÓDICAS (2 octavas de ruido elíptico hacia la costa)
                float2 rippleUV1 = pCell * float2(0.095, 0.34) + float2(_RandomSeed * 1.3 + _Time.y * (0.28 + _WindIntensity * 0.35), _WavePhase * 0.35);
                float2 rippleUV2 = pCell * float2(0.145, 0.42) + float2(-_RandomSeed * 0.7 - _Time.y * 0.22, _WavePhase * 0.24 + 5.1);
                float wavePatch = ValueNoise(rippleUV1) * 0.62 + ValueNoise(rippleUV2) * 0.38;

                float nearShoreFade = 1.0 - smoothstep(0.15, 3.8, distAboveShore) * (0.55 - _WindIntensity * 0.30);
                half organicWave = step(0.67 - _WindIntensity * 0.07, wavePatch) * nearShoreFade;

                // Destellos puntuales suaves y naturales del sol sobre crestas
                float sparkleTick = floor(_Time.y * (3.2 + _WindIntensity * 3.5));
                float sparkleNoise = Hash21(floor(pCell * float2(0.5, 1.0)) + float2(sparkleTick * 1.7, _RandomSeed));
                half glint = step(0.985 - _WindIntensity * 0.02, sparkleNoise) * step(0.54, wavePatch);

                // Cabrillas blancas sueltas y rachas de viento cuando _WindIntensity > 0.12
                float whitecapNoise = ValueNoise(pCell * float2(0.16, 0.26) + float2(_Time.y * (0.7 + _WindIntensity), _RandomSeed * 2.3));
                half whitecaps = step(0.79 - _WindIntensity * 0.10, whitecapNoise) * step(0.12, _WindIntensity);

                float windScroll = _Time.y * (2.5 + _WindIntensity * 4.0);
                float2 windCell = float2(pCell.x + windScroll, pCell.y + pCell.x * 0.38);
                float streakBand = ValueNoise(windCell * float2(0.045, 0.38) + _RandomSeed);
                half windStreak = step(0.84 - _WindIntensity * 0.09, streakBand) * smoothstep(0.15, 0.50, _WindIntensity);

                fixed3 rippleColor = lerp(_ShallowWaterColor.rgb, _FoamColor.rgb, 0.52);
                waterCol.rgb = lerp(waterCol.rgb, rippleColor, saturate(organicWave * 0.52));
                waterCol.rgb = lerp(waterCol.rgb, _FoamColor.rgb, saturate(glint * 0.48 + whitecaps * 0.72 + windStreak * 0.42 + reboundWave * 0.88));

                return waterCol;
            }
            ENDCG
        }
    }
    FallBack "Sprites/Default"
}
