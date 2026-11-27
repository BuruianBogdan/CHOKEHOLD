// Orbirea de la grenada cu ace: o parte a ecranului se intuneca, cu granita curbata (pe linia ochilor zona
// vizibila e mai lata, ca umbra nasului), estompata si usor tremurata. Crucea din centru ramane vizibila.
//   _Side      1 = dreapta, -1 = stanga
//   _Coverage  0 = nimic, 1 = cat mai mult (granita la ~0,52 din ecran pe partea lovita)
//   _Pulse     inrosirea de la impact (1 -> 0)
Shader "ChokeHold/NeedleBlind"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Side ("Side", Float) = 1
        _Coverage ("Coverage", Range(0, 1)) = 0
        _Pulse ("Pulse", Range(0, 1)) = 0
        _Aspect ("Aspect", Float) = 1.7778
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float _Side;
            float _Coverage;
            float _Pulse;
            float _Aspect;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            float hash (float n) { return frac(sin(n) * 43758.5453); }

            float noise1 (float x)
            {
                float i = floor(x);
                float f = frac(x);
                return lerp(hash(i), hash(i + 1.0), f * f * (3.0 - 2.0 * f));
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float x = _Side > 0 ? i.uv.x : 1.0 - i.uv.x;     // 0 = partea sanatoasa, 1 = marginea lovita
                float y = i.uv.y * 2.0 - 1.0;

                // granita: 0,52 sus/jos, 0,62 pe linia ochilor; cu _Coverage mica sta la margine
                float curve = 0.52 + 0.10 * (1.0 - y * y);
                float limit = lerp(1.08, curve, _Coverage);

                // tremur ca o pleoapa: zgomot lent pe verticala
                float wobble = (noise1(i.uv.y * 7.0 + _Time.y * 1.7) - 0.5) * 0.025;
                float edge = x - limit + wobble;

                float mask = smoothstep(0.0, 0.09, edge);
                // chiar la granita o tenta rosie-inchisa (sange in pleoapa), apoi negru
                float depth = smoothstep(0.0, 0.12, edge);
                float3 color = lerp(float3(0.13, 0.012, 0.012), float3(0.006, 0.0, 0.0), depth);

                // inrosirea de la impact, pe partea lovita
                float pulse = _Pulse * smoothstep(-0.25, 0.1, edge) * 0.55;
                color = lerp(color, float3(0.6, 0.05, 0.03), pulse * (1.0 - mask));

                float alpha = saturate(mask * 0.97 + pulse * (1.0 - mask));
                return fixed4(color, alpha * i.color.a);
            }
            ENDCG
        }
    }
}
