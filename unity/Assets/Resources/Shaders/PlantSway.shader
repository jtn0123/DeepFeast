Shader "DeepFeast/PlantSway"
{
    Properties
    {
        _MainTex ("Painted atlas", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Phase ("Current phase", Float) = 0
        _Bend ("Current strength", Float) = 0.055
        _Height ("Plant height", Float) = 1
        _Fog ("Water fog", Range(0,1)) = 0
        _FogColor ("Water color", Color) = (0.05,0.3,0.4,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "DisableBatching"="True" }
        Cull Off Lighting Off ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "PaintedLighting.cginc"
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; float height : TEXCOORD1; };
            sampler2D _MainTex;
            fixed4 _Color, _FogColor;
            float _Phase, _Bend, _Height, _Fog;
            v2f vert(appdata v)
            {
                v2f o;
                // Zero displacement at the footing; current grows smoothly toward the crown.
                float h = saturate(v.vertex.y / max(_Height, 0.001));
                float rooted = h * h;
                v.vertex.x += (sin(_Phase + h * 2.8) + sin(_Phase * 1.7 - h * 4.3) * 0.22) * rooted * _Bend * _Height;
                v.vertex.y += sin(_Phase * 1.4 + v.vertex.x * 8.0) * rooted * _Bend * _Height * 0.12;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv; o.color = v.color * _Color;
                o.height = h;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv) * i.color;
                c.rgb = lerp(PaintedLighting(c.rgb, i.height), _FogColor.rgb, _Fog);
                return c;
            }
            ENDCG
        }
    }
}
