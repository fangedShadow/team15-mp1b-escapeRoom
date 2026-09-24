Shader "Custom/TMP_InvisibleWriting"
{
    Properties
    {
        _MainTex ("Font Atlas", 2D) = "white" {}
        _Color ("Text Color", Color) = (1,1,1,1)
        _ViewThreshold ("View Angle", Range(0, 1)) = 0.4
        _FacingDirectionWS ("Facing Direction WS", Vector) = (0,0,1,0)
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _ViewThreshold;
            float3 _FacingDirectionWS;

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float4 color : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);
                float3 facingDir = normalize(_FacingDirectionWS);
                float facing = abs(dot(facingDir, viewDir));

                if (facing < _ViewThreshold)
                    discard;

                fixed4 tex = tex2D(_MainTex, i.uv);
                return fixed4(_Color.rgb * i.color.rgb, tex.a * _Color.a * i.color.a);
            }
            ENDCG
        }
    }
}