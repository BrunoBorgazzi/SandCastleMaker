Shader "SandCastle/SandSculpt"
{
    Properties
    {
        [PerRendererData] _MainTex ("Quad Base Texture", 2D) = "white" {}
        _CastleTex ("Castle Target Sprite", 2D) = "white" {}
        _SculptMaskTex ("Sculpt Mask (R:Dry G:Wet B:TideWet A:Ghost)", 2D) = "black" {}
        _CastleUVRect ("Castle UV Rect (MinX, MinY, SizeX, SizeY)", Vector) = (0.15, 0.15, 0.7, 0.7)

        [Header(Sand Palette)]
        _DrySandColor ("Dry Sculpted Sand Color", Color) = (0.93, 0.76, 0.46, 1.0)
        _DrySandHighlight ("Dry Sand Sun Highlight", Color) = (1.00, 0.90, 0.66, 1.0)
        _DrySandShadow ("Dry Sand Shadow Color", Color) = (0.68, 0.49, 0.26, 1.0)
        _WetSandColor ("Wet Compacted Sand Color", Color) = (0.66, 0.47, 0.27, 1.0)
        _WetSandShadow ("Wet Sand Shadow Color", Color) = (0.44, 0.29, 0.15, 1.0)
        _TideWetTrailColor ("Beach Wet Trail Color", Color) = (0.48, 0.34, 0.20, 0.68)
        _BuildZoneGuideColor ("Build Zone Guide Color", Color) = (0.52, 0.36, 0.18, 0.45)

        [Header(Relief and Grain)]
        _PixelGrid ("Pixel Art Resolution", Float) = 192.0
        _PixelGridXY ("Pixel Art Grid Size (W, H)", Vector) = (192.0, 192.0, 0.0, 0.0)
        _BevelStrength ("2.5D Relief Bevel Intensity", Range(0, 2.0)) = 1.15
        _GrainIntensity ("Grain Roughness", Range(0, 0.35)) = 0.12
        _ShowFaintGuide ("Show Silhouette Guide (0..1)", Range(0, 1)) = 0.08
        _UsePebbleGuide ("Use Dark Pebble Guide (0=Light Sand, 1=Dark Sand)", Range(0, 1)) = 0.0
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

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
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
            };

            sampler2D _MainTex;
            sampler2D _CastleTex;
            sampler2D _SculptMaskTex;
            float4 _CastleUVRect;

            fixed4 _DrySandColor;
            fixed4 _DrySandHighlight;
            fixed4 _DrySandShadow;
            fixed4 _WetSandColor;
            fixed4 _WetSandShadow;
            fixed4 _TideWetTrailColor;
            fixed4 _BuildZoneGuideColor;

            float _PixelGrid;
            float4 _PixelGridXY;
            half _BevelStrength;
            half _GrainIntensity;
            half _ShowFaintGuide;
            half _UsePebbleGuide;

            // Hash 2D en coordenadas locales [0..192] libre de overflow de precisión
            float Hash21(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // Coordenada cuantizada a grilla Pixel Art proporcional 1:1 (cuadrada en mundo)
                float2 gridSize = (_PixelGridXY.x > 1.0 && _PixelGridXY.y > 1.0)
                    ? _PixelGridXY.xy
                    : float2(_PixelGrid, _PixelGrid);
                float2 pixelPos = floor(IN.texcoord * gridSize);
                float2 qUV = (pixelPos + 0.5) / gridSize;
                float2 texelStep = 1.0 / gridSize;

                // Lectura del mapa de estado (R: Arena Seca, G: Arena Húmeda, B: Rastro Mojado de Ola, A: Silueta Objetivo)
                fixed4 mask = tex2D(_SculptMaskTex, qUV);
                half dryHeight = mask.r;
                half wetShield = mask.g;
                half tideWetness = mask.b;
                half targetAlpha = mask.a;

                half totalSculpt = saturate(max(dryHeight, wetShield));
                half wetRatio = saturate(wetShield * 1.15);

                // Grano de arena 2D genuino (sin bandas verticales)
                float grain = Hash21(pixelPos);
                float grain2 = Hash21(pixelPos * 0.5 + 17.0);
                half grainDelta = (grain - 0.5) * _GrainIntensity + (grain2 - 0.5) * (_GrainIntensity * 0.6);

                // --- CASO 1: ARENA ESCULPIDA POR EL DEDO DEL JUGADOR (DIBUJO LIBRE CON RELIEVE 2.5D) ---
                if (totalSculpt > 0.08)
                {
                    fixed4 maskL = tex2D(_SculptMaskTex, qUV + float2(-texelStep.x, 0.0));
                    fixed4 maskR = tex2D(_SculptMaskTex, qUV + float2( texelStep.x, 0.0));
                    fixed4 maskD = tex2D(_SculptMaskTex, qUV + float2(0.0, -texelStep.y));
                    fixed4 maskU = tex2D(_SculptMaskTex, qUV + float2(0.0,  texelStep.y));

                    half hL = max(maskL.r, maskL.g);
                    half hR = max(maskR.r, maskR.g);
                    half hD = max(maskD.r, maskD.g);
                    half hU = max(maskU.r, maskU.g);

                    // Derivada direccional: Sol arriba-izquierda (+), Sombra abajo-derecha (-)
                    half nx = (hL - hR) * _BevelStrength;
                    half ny = (hD - hU) * _BevelStrength;
                    half lightDiff = clamp(nx + ny, -1.0, 1.0);

                    // Color base según Arena Seca vs Arena Húmeda
                    fixed3 baseCol = lerp(_DrySandColor.rgb, _WetSandColor.rgb, wetRatio);
                    fixed3 shadowCol = lerp(_DrySandShadow.rgb, _WetSandShadow.rgb, wetRatio);
                    fixed3 highlightCol = lerp(_DrySandHighlight.rgb, _DrySandColor.rgb * 0.92, wetRatio);

                    // Señal visual de saturación y grietas húmedas antes de desmoronarse al absorber olas
                    half soakStress = saturate((0.68 - wetShield) / 0.55) * step(0.11, wetShield);
                    if (soakStress > 0.01)
                    {
                        float crackPulse = 0.75 + 0.25 * sin(_Time.y * 8.0 + pixelPos.x * 0.35);
                        float crackNoise = Hash21(floor(pixelPos * 0.5) + 41.0);
                        half isSoakCrack = step(1.0 - soakStress * 0.42, crackNoise);
                        baseCol = lerp(baseCol, shadowCol * 0.68, soakStress * 0.55);
                        baseCol = lerp(baseCol, fixed3(0.32, 0.52, 0.62) * crackPulse, isSoakCrack * 0.55);
                    }
                    else if (wetShield > 0.55 && hU < 0.08)
                    {
                        // Sutil brillo húmedo en el borde superior que sella contra la ola
                        highlightCol = lerp(highlightCol, fixed3(0.66, 0.86, 0.92), 0.35);
                    }

                    // Aplicar iluminación de relieve 2.5D
                    fixed3 litSand = baseCol;
                    if (lightDiff > 0.0)
                    {
                        litSand = lerp(baseCol, highlightCol, lightDiff * 0.75);
                    }
                    else
                    {
                        litSand = lerp(baseCol, shadowCol, (-lightDiff) * 0.85);
                    }

                    // Sutil textura de bloques/almenas talladas en la arena cuando hay buen espesor
                    float brickRow = step(0.88, frac(pixelPos.y * 0.125));
                    float brickCol = step(0.90, frac((pixelPos.x + floor(pixelPos.y * 0.125) * 4.0) * 0.0833));
                    half mortarHint = saturate(brickRow + brickCol) * step(0.65, totalSculpt) * 0.07;
                    litSand -= mortarHint;

                    // Contorno exterior oscuro en el borde donde la arena toca el suelo de la playa
                    half minNeighbor = min(min(hL, hR), min(hD, hU));
                    half isOuterRim = step(minNeighbor, 0.08);
                    litSand = lerp(litSand + grainDelta, shadowCol * 0.82, isOuterRim * 0.65);

                    return fixed4(saturate(litSand), 1.0);
                }

                // --- CASO 2: GUÍA SOLO DE LA SILUETA DEL CASTILLO (SIN MARCOS NI ESQUINAS) ---
                if (targetAlpha > 0.2 && _ShowFaintGuide > 0.001)
                {
                    fixed4 mL = tex2Dlod(_SculptMaskTex, float4(qUV + float2(-texelStep.x, 0.0), 0, 0));
                    fixed4 mR = tex2Dlod(_SculptMaskTex, float4(qUV + float2( texelStep.x, 0.0), 0, 0));
                    fixed4 mD = tex2Dlod(_SculptMaskTex, float4(qUV + float2(0.0, -texelStep.y), 0, 0));
                    fixed4 mU = tex2Dlod(_SculptMaskTex, float4(qUV + float2(0.0,  texelStep.y), 0, 0));
                    half minA1 = min(min(mL.a, mR.a), min(mD.a, mU.a));

                    if (_UsePebbleGuide > 0.5)
                    {
                        // Modo mapas de tierra/arena oscura (noche/tormenta): contorno orgánico de piedritas y tierra húmeda oscura
                        fixed3 deepEarth = _BuildZoneGuideColor.rgb * fixed3(0.82, 0.78, 0.74);
                        fixed3 darkSlate = fixed3(0.18, 0.17, 0.16);
                        fixed3 pebbleHighlight = fixed3(0.34, 0.28, 0.22);
                        fixed3 pebbleCol = lerp(deepEarth, darkSlate, step(0.55, grain));
                        pebbleCol = lerp(pebbleCol, pebbleHighlight, step(0.84, grain2) * 0.65);

                        // Segundo anillo sutil (2 texels de espesor en las piedritas para lectura clara en tormenta/noche)
                        fixed4 mL2 = tex2Dlod(_SculptMaskTex, float4(qUV + float2(-texelStep.x * 2.0, 0.0), 0, 0));
                        fixed4 mR2 = tex2Dlod(_SculptMaskTex, float4(qUV + float2( texelStep.x * 2.0, 0.0), 0, 0));
                        fixed4 mD2 = tex2Dlod(_SculptMaskTex, float4(qUV + float2(0.0, -texelStep.y * 2.0), 0, 0));
                        fixed4 mU2 = tex2Dlod(_SculptMaskTex, float4(qUV + float2(0.0,  texelStep.y * 2.0), 0, 0));
                        half minA2 = min(min(mL2.a, mR2.a), min(mD2.a, mU2.a));

                        bool isRimOuter = (minA1 <= 0.2);
                        bool isRimInner = (!isRimOuter && minA2 <= 0.2);

                        float pebbleCluster = frac((pixelPos.x + pixelPos.y) * 0.22 + grain2 * 0.14);
                        if (isRimOuter && pebbleCluster > 0.28)
                        {
                            half guideAlpha = saturate(_ShowFaintGuide * 1.05);
                            return fixed4(pebbleCol, guideAlpha);
                        }
                        if (isRimInner && pebbleCluster > 0.56)
                        {
                            return fixed4(deepEarth, saturate(_ShowFaintGuide * 0.62));
                        }

                        // Sutil sombra de arena apisonada dentro del molde (conservando rastro húmedo de ola si pasó agua)
                        half innerTintAlpha = 0.08 + step(0.86, grain) * 0.05;
                        if (tideWetness > 0.02)
                        {
                            half wetCurve = smoothstep(0.02, 0.95, tideWetness);
                            half wetAlpha = max(innerTintAlpha, wetCurve * _TideWetTrailColor.a);
                            fixed3 wetRGB = lerp(_TideWetTrailColor.rgb, deepEarth, 0.22);
                            return fixed4(saturate(wetRGB + grainDelta * 0.35), wetAlpha);
                        }
                        return fixed4(deepEarth, innerTintAlpha);
                    }
                    else
                    {
                        // Modo mapas de arena clara (soleado/atardecer): guía punteada cálida clásica solo sobre la silueta
                        half isOutline = step(minA1, 0.2);
                        if (isOutline > 0.5 && step(0.45, frac((pixelPos.x + pixelPos.y) * 0.25)) > 0.5)
                        {
                            return fixed4(_BuildZoneGuideColor.rgb, saturate(_ShowFaintGuide * 2.2));
                        }
                    }
                }

                // --- CASO 3: RASTRO DE PLAYA MOJADA CUANDO LA OLA RETROCEDE (CANAL B) ---
                if (tideWetness > 0.02)
                {
                    half wetCurve = smoothstep(0.02, 0.95, tideWetness);
                    half wetAlpha = wetCurve * _TideWetTrailColor.a;
                    half sheen = smoothstep(0.72, 0.98, tideWetness) * 0.09;
                    fixed3 wetBeachRGB = _TideWetTrailColor.rgb + grainDelta * 0.35 + fixed3(sheen, sheen * 0.9, sheen * 0.7);
                    return fixed4(saturate(wetBeachRGB), wetAlpha);
                }

                discard;
                return fixed4(0, 0, 0, 0);
            }
            ENDCG
        }
    }
    FallBack "Sprites/Default"
}
