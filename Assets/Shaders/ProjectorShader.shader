Shader "Unlit/NewUnlitShader"
{
    Properties
    {
        _Power("Power", Range(1,3)) = 2
        _Power2("Power2", Range(1,3)) = 2
        
        _Cutoff1("Thing1", Range(0,1)) = 1
        _Cutoff2("Thing", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100
        Cull Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 5.0

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float2 uv2 : TEXCOORD1;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float2 uv2 : TEXCOORD1;
                float4 vertex : SV_POSITION;
            };

            float _Power;
            float _Power2;
            
            float _Cutoff1;
            float _Cutoff2;

            SamplerState trilinear_clamp_sampler;

            Texture2D tex;
            float contrast;
            float brightness;
            float saturation;

            float enableCurve;
            float flipCurve;
            float crossOver;

            float vertical;

            static fixed3 W = fixed3(0.2125, 0.7154, 0.0721);

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.uv2 = v.uv2;
                return o;
            }

            float brightnessCurve(fixed2 uv)
            {
                if (flipCurve < 0.1)
                {
                    if (uv.x > 0.5)
                    {
                        float progress = (uv.x - 0.5) * 2;
                        return -lerp(0, 0.05, progress);
                    }
                }
                else
                {
                    if (uv.x < 0.5)
                    {
                        float progress = uv.x * 2;
                        return -lerp(0.05, 0, progress);
                    }
                }

                return 0.0;
            }

            float easeInOutCubic(float x)
            {
                return x < 0.5 ? 4.0 * x * x * x : 1.0 - pow(-2.0 * x + 2.0, 3.0) / 2.0;
            }

            float antiOverlap(fixed2 uv)
            {
                float x = clamp(uv.x, 0, 1);
                if (flipCurve < 0.5)
                {
                    if (x > 1.0 - _Cutoff1)
                    {
                        float progress = lerp(0.0, _Cutoff2, (1.0 - x) / _Cutoff1);
                        return easeInOutCubic(progress);
                    }
                }
                else
                {
                    if (x < _Cutoff1)
                    {
                        float progress = lerp(0.0, _Cutoff2, x / _Cutoff1);
                        return easeInOutCubic(progress);
                    }
                }

                return 1.0;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed2 uv = i.uv / i.uv2;
                
                if (flipCurve < 0.5) {
                    uv.x = 3.0 * uv.x - pow(uv.x, _Power2);
                    uv.x /= 2.0;
                }
                else {
                    uv.x = uv.x + pow(uv.x, _Power);
                    uv.x /= 2.0;
                }

                fixed4 col = tex.Sample(trilinear_clamp_sampler, uv);

                float bc = 0;
                // TODO: Vertical orientation
                // TODO: Variable control points
                if (enableCurve > 0.1) bc = brightnessCurve(uv);

                float intComp = dot(col.rgb, W);
                fixed3 intensity = fixed3(intComp, intComp, intComp);
                col.rgb = lerp(intensity, col.rgb, saturation);

                col.rgb = ((col.rgb - 0.5f) * max(contrast, 0)) + 0.5f;
                col.rgb += (brightness + bc);
                if (enableCurve > 0.1) col.rgb *= antiOverlap(uv);

                return col;
            }
            ENDCG
        }
    }
}
