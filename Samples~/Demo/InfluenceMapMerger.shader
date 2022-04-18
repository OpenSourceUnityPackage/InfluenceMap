Shader "InfluenceMapMerger"
{
    Properties
    {
        _InfluenceMap1 ("Texture", 2D) = "white" {}
        _InfluenceMap2 ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            sampler2D _InfluenceMap1;
            sampler2D _InfluenceMap2;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float i1 = tex2D(_InfluenceMap1, i.uv).r;
                float i2 = tex2D(_InfluenceMap2, i.uv).r;

                // Combine
                //return fixed4(i1, i2, 0.f, 1.f);

                // Max
                // i1 is priority if equality
                return fixed4(step(i2, i1) * i1, (i2 > i1) * i2, 0.f, 1.f);
            }
            ENDCG
        }
    }
}
