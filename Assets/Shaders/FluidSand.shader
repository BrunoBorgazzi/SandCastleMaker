Shader "SandCastle/FluidSand"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint Color", Color) = (0.95, 0.78, 0.45, 1.0)
        _WetColor ("Wet Sand Color", Color) = (0.65, 0.48, 0.28, 1.0)
        _WetBlend ("Wetness Ratio", Range(0, 1)) = 0.0
        
        [Header(Granular Noise)]
        _GrainScale ("Grain Texture Scale", Float) = 45.0
        _GrainIntensity ("Grain Roughness Intensity", Range(0, 1)) = 0.35
        _EdgeFluidity ("Fluid Edge Softness", Range(0, 0.5)) = 0.12
        
        [Header(Shimmer and Motion)]
        _ShimmerSpeed ("Shimmer Pulse Speed", Float) = 1.5
        _ShimmerIntensity ("Shimmer Glint", Range(0, 0.5)) = 0.1
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

            fixed4 _Color;
            fixed4 _WetColor;
            half _WetBlend;
            float _GrainScale;
            half _GrainIntensity;
            half _EdgeFluidity;
            float _ShimmerSpeed;
            half _ShimmerIntensity;
            sampler2D _MainTex;

            // Pseudo-random noise function for mobile performance (no textures needed)
            float Hash21(float2 p)
            {
                p = frac(p * float2(234.34, 435.345));
                p += dot(p, p + 34.23);
                return frac(p.x * p.y);
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

            fixed4 frag(v2f IN) : SV_Target
            {
                // Muestreo de textura base (Sprite)
                fixed4 tex = tex2D(_MainTex, IN.texcoord);
                if (tex.a < 0.01) discard;

                // Color interpolado seco / húmedo
                fixed4 baseSandColor = lerp(_Color, _WetColor, _WetBlend) * IN.color;

                // Cálculo de ruido granular procedural basado en posición de mundo
                float2 grainUV = IN.worldPos * _GrainScale;
                float grain = Hash21(floor(grainUV));
                float grainFactor = 1.0 - (grain * _GrainIntensity);

                // Destello sutil / shimmer para simular microcristales de sílice
                float shimmer = sin(_Time.y * _ShimmerSpeed + (IN.worldPos.x + IN.worldPos.y) * 8.0) * 0.5 + 0.5;
                shimmer = pow(shimmer, 4.0) * _ShimmerIntensity;

                // Suavizado orgánico del contorno
                float distFromCenter = length(IN.texcoord - 0.5) * 2.0;
                float edgeAlpha = smoothstep(1.0, 1.0 - _EdgeFluidity, distFromCenter);

                // Composición final
                fixed3 finalRGB = (baseSandColor.rgb * grainFactor) + shimmer;
                fixed finalAlpha = tex.a * baseSandColor.a * edgeAlpha;

                return fixed4(finalRGB, finalAlpha);
            }
            ENDCG
        }
    }
    FallBack "Sprites/Default"
}

