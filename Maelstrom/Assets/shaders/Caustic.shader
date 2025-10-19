Shader "Custom/Caustic"
{
    Properties
    {
        _MainTex("Sprite Texture", 2D) = "white" {}
        _Time("Time", Float) = 0
        _Seed("Seed", Float) = 1
        _Maelstrom("Maelstrom", Float) = 1
        _Opacity("Opacity", Float) = 1
    }

    SubShader 
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha DstAlpha
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
 
            #pragma vertex vert 
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #define tau 6.28318530718

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
              //  float _Time;
                float _Seed;
                float _Maelstrom;
                float _Opacity;
            CBUFFER_END


            float sin01(float x) { return (sin(x * tau) + 1.0) * 0.5; }
            float cos01(float x) { return (cos(x * tau) + 1.0) * 0.5; }

            float2 rand01(float2 p)
            {
                float3 a = frac(p.xyx * float3(123.5, 234.34, 345.65));
                a += dot(a, a + 34.45);
                return frac(float2(a.x * a.y, a.y * a.z));
            }

            float2x2 rotate2d(float _angle){
                return float2x2(cos(_angle),-sin(_angle),
                            sin(_angle),cos(_angle));
            }

            float distFn(float2 from, float2 to)
            {
                float x = length(from - to);
                return pow(x, 4.0);
            }

            float voronoi(float2 uv, float t, float seed, float size)
            {
                float minDist = 100.0;
                float gridSize = size;
                float2 cellUv = frac(uv * gridSize) - 0.5;
                float2 cellCoord = floor(uv * gridSize);

                [loop]
                for (float x = -1.0; x <= 1.0; x++)
                {
                    [loop]
                    for (float y = -1.0; y <= 1.0; y++)
                    {
                        float2 cellOffset = float2(x, y);
                        float2 rand01Cell = rand01(cellOffset + cellCoord + seed);

                        // renamed from 'point' to 'pnt'
                        float2 pnt = cellOffset + sin(rand01Cell * (t + 10.0)) * 0.5;

                        float dist = distFn(cellUv, pnt);
                        minDist = min(minDist, dist);
                    }
                }
                return minDist;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.uv;
                float amplitude = pow(_Seed*2,2);

                float2 squareUv = float2(uv.x * (_ScreenParams.x / _ScreenParams.y), uv.y);
                squareUv = float2(squareUv.x, squareUv.y);
                squareUv = mul(rotate2d(30*_Seed), squareUv);

                float t = _Time * (1+pow(3*_Seed+3,3));
                float v = 0.0;
                float sizeDistortion = amplitude;

                v += voronoi(squareUv, t * 2.0, 0.5 + amplitude, 2.5 - sizeDistortion + amplitude);
                amplitude = amplitude/2;
              //  v += voronoi(squareUv, t * 2.0, 0.0 + amplitude, 4.0 - sizeDistortion + amplitude);


                float3 negColor = float3(1, 1, 1) +  float3(-1, -1, 0)*_Maelstrom;
                float3 happyColor = float3(0.3,0.3, 0.8) +  float3(-0.3,-0.3, 0.2)*_Maelstrom;

                //mix the colors based on the seed
                float3 col = v * lerp(negColor,happyColor , _Seed);
                float3 backgroundColor = float3(0.0, 0.0, _Maelstrom);

                float alpha = _Opacity * (col.x+col.y+col.z)/3;
                col += (1.0 - v) * backgroundColor;

                return float4(col, alpha);
            }

            ENDHLSL
        }
    }
}
