Shader "Team15/StartSea"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _UnscaledTime ("Animation time", Float) = 0
        _Foreground ("Foreground water", Float) = 0
        _Waterline ("Waterline in UV", Float) = 0.6
        _BoatX ("Boat center in UV", Float) = 0.3
        _BoatWidth ("Boat width in UV", Float) = 0.4
        _BoatBob ("Boat height offset in UV", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" }
        Cull Off
        ZWrite Off
        ZTest Always
        Blend Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            float _UnscaledTime;
            float _Foreground;
            float _Waterline;
            float _BoatX;
            float _BoatWidth;
            float _BoatBob;

            float3 DisplayColor(float3 srgb)
            {
                #if defined(UNITY_COLORSPACE_GAMMA)
                    return srgb;
                #else
                    return GammaToLinearSpace(srgb);
                #endif
            }

            float4 frag(v2f_img input) : SV_Target
            {
                float2 uv = input.uv;
                float t = _UnscaledTime;
                float boatDistance = (uv.x - _BoatX) / max(_BoatWidth * 0.65, 0.025);
                float nearBoat = exp2(-boatDistance * boatDistance * 2.0);

                // Long swells cross the whole screen. Only the water beside the hull
                // follows a small part of its motion; the ocean does not translate.
                float horizon = _Waterline
                              + sin(uv.x * 6.283 - t * 0.55) * 0.022
                              + sin(uv.x * 12.566 + t * 0.23) * 0.009
                              + _BoatBob * nearBoat * 0.22;
                float depth = max(horizon - uv.y, 0.0);
                float surface = 1.0 - smoothstep(horizon - 0.035, horizon + 0.050, uv.y);
                float deep = saturate(depth / max(_Waterline, 0.1));

                // A few broad, slightly diagonal wave fronts replace small ripples.
                float phase = depth * 22.0 + uv.x * 2.7 - t * 0.48
                            + sin(uv.x * 5.0 - t * 0.15) * 0.55;
                float secondary = depth * 13.0 - uv.x * 4.2 + t * 0.31;
                float swell = sin(phase);
                float broadLight = saturate(swell * 0.5 + 0.5);
                float crest = smoothstep(0.47, 0.96, swell)
                            * (0.52 + 0.48 * (sin(secondary) * 0.5 + 0.5));
                float shoulder = smoothstep(-0.10, 0.80, sin(phase - 0.62));
                float3 sea = lerp(float3(0.040, 0.068, 0.091),
                                  float3(0.027, 0.049, 0.069), deep);
                sea += broadLight * float3(0.020, 0.026, 0.033);
                sea += shoulder * float3(0.014, 0.021, 0.027);
                sea += crest * float3(0.062, 0.079, 0.094);

                if (_Foreground > 0.5)
                {
                    // The foreground extends to every edge and softly immerses the keel.
                    float wash = 1.0 - smoothstep(horizon - 0.028, horizon + 0.033, uv.y);
                    float washAlpha = lerp(0.24, 0.83, saturate(depth / 0.25)) * wash;
                    float3 water = DisplayColor(sea * 0.88);

                    // A faint irregular horizontal wake, never a closed ring around the ship.
                    float wakeY = (uv.y - horizon + 0.045
                                 + sin(uv.x * 10.0 - t * 0.32) * 0.012) / 0.028;
                    float wakeBand = exp2(-wakeY * wakeY * 2.0);
                    float wakeBreaks = smoothstep(0.05, 0.88,
                        sin(uv.x * 23.0 - t * 0.28) * 0.5 + 0.5);
                    float wakeAlpha = wakeBand * nearBoat * wakeBreaks * 0.09;
                    float alpha = wakeAlpha + washAlpha * (1.0 - wakeAlpha);
                    float3 foam = DisplayColor(float3(0.15, 0.19, 0.22));
                    float3 color = (foam * wakeAlpha + water * washAlpha * (1.0 - wakeAlpha))
                                 / max(alpha, 0.0001);
                    return float4(color, alpha);
                }

                return float4(DisplayColor(sea), surface * 0.97);
            }
            ENDCG
        }
    }
    Fallback Off
}
