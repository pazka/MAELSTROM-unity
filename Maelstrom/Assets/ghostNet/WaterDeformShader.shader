Shader "Custom/URP_WaterDistortion"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _DistortionStrength ("Distortion Strength", Float) = 0.05
        _NoiseScale ("Noise Scale", Float) = 2.0
        _Speed ("Speed", Float) = 1.0
        _Tint ("Water Tint", Color) = (1,1,1,0.5)
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "RenderPipeline"="UniversalRenderPipeline"
        }

        Pass
        {
            Name "Forward"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
            };

            TEXTURE2D(_CameraOpaqueTexture);
            SAMPLER(sampler_CameraOpaqueTexture);

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            float _DistortionStrength;
            float _NoiseScale;
            float _Speed;
            float4 _Tint;

            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);

                float a = hash(i);
                float b = hash(i + float2(1, 0));
                float c = hash(i + float2(0, 1));
                float d = hash(i + float2(1, 1));

                float2 u = f * f * (3.0 - 2.0 * f);

                return lerp(a, b, u.x) +
                    (c - a) * u.y * (1.0 - u.x) +
                    (d - b) * u.x * u.y;
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.screenPos = ComputeScreenPos(o.positionHCS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                // Screen UV (0..1)
                float2 screenUV = i.screenPos.xy / i.screenPos.w;

                // Animated noise
                float time = _Time.y * _Speed;

                // Use screen UV for distortion pattern
                float nX = noise(screenUV * _NoiseScale + float2(time, 0));
                float nY = noise(screenUV * _NoiseScale + float2(0, time));

                float2 distortion = float2(nX, nY) * _DistortionStrength;

                float2 distortedUV = screenUV + distortion;

                float4 sceneColor = SAMPLE_TEXTURE2D(
                    _CameraOpaqueTexture,
                    sampler_CameraOpaqueTexture,
                    distortedUV
                );

                return sceneColor * _Tint;
            }
            ENDHLSL
        }
    }
}