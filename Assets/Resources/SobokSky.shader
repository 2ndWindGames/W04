Shader "Sobok/IllustratedSky"
{
    Properties { _Phase ("Time of day", Float) = 0 }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Background" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            CBUFFER_START(UnityPerMaterial)
                float _Phase;
                float _Aspect;
            CBUFFER_END
            Varyings vert(Attributes v)
            {
                Varyings o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.uv = v.uv; return o;
            }
            float3 palette(float3 day, float3 sunset, float3 night, float3 dawn, float phase)
            {
                float p = fmod(phase, 4.0);
                float t = smoothstep(0, 1, frac(p));
                if (p < 1) return lerp(day, sunset, t);
                if (p < 2) return lerp(sunset, night, t);
                if (p < 3) return lerp(night, dawn, t);
                return lerp(dawn, day, t);
            }
            float disc(float2 uv, float2 center, float radius)
            {
                float2 d = uv - center; d.x *= _Aspect;
                return 1 - smoothstep(radius - 0.002, radius + 0.002, length(d));
            }
            half4 frag(Varyings i) : SV_Target
            {
                float2 uv = i.uv;
                float3 top = palette(float3(.67,.83,.85), float3(.59,.48,.68), float3(.075,.10,.22), float3(.47,.51,.71), _Phase);
                float3 bottom = palette(float3(.98,.94,.82), float3(.98,.72,.55), float3(.25,.29,.44), float3(.96,.79,.72), _Phase);
                float3 col = lerp(bottom, top, smoothstep(0,1,uv.y));
                float night = palette(0,0,1,.25,_Phase).x;
                float sun = disc(uv, float2(.78,.74), .038);
                col = lerp(col, float3(1,.92,.69), sun*(1-night)*.8);
                float moon = disc(uv,float2(.78,.74),.033) * (1-disc(uv,float2(.795,.752),.031));
                col = lerp(col,float3(.96,.94,.81),moon*night);
                // Sparse fixed stars: no flashing or random changes between frames.
                float2 cell = floor(uv * float2(17,25));
                float seed = frac(sin(dot(cell,float2(127.1,311.7)))*43758.5453);
                float star = (1-smoothstep(.025,.07,length(frac(uv*float2(17,25))-.5))) * step(.78,seed) * step(.45,uv.y);
                col = lerp(col,float3(.98,.94,.81),star*night*.75);
                float cloud = disc(uv,float2(.20,.69),.026) + disc(uv,float2(.24,.70),.034) + disc(uv,float2(.29,.69),.025);
                col = lerp(col,float3(1,.97,.90),saturate(cloud)*.25*(1-night));
                // Broad flat silhouettes resemble layered paper illustration.
                float farHill = .23 + .04*sin(uv.x*7+1) + .018*sin(uv.x*15);
                float nearHill = .12 + .035*sin(uv.x*8+3);
                float frontHill = .055 + .023*sin(uv.x*9);
                float3 farColor = palette(float3(.68,.76,.71),float3(.62,.54,.65),float3(.18,.23,.35),float3(.56,.56,.68),_Phase);
                float3 nearColor = palette(float3(.53,.66,.61),float3(.48,.44,.57),float3(.12,.18,.28),float3(.42,.49,.57),_Phase);
                float3 frontColor = palette(float3(.40,.56,.51),float3(.37,.37,.49),float3(.08,.13,.22),float3(.31,.42,.46),_Phase);
                col = lerp(col,farColor,1-smoothstep(farHill-.002,farHill+.002,uv.y));
                col = lerp(col,nearColor,1-smoothstep(nearHill-.002,nearHill+.002,uv.y));
                col = lerp(col,frontColor,1-smoothstep(frontHill-.002,frontHill+.002,uv.y));
                return half4(col,1);
            }
            ENDHLSL
        }
    }
}
