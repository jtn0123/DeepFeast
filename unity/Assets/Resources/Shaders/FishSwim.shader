Shader "DeepFeast/FishSwim"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Phase ("Swim phase", Float) = 0
        _Energy ("Swim energy", Float) = 1
        _Mouth ("Jaw anticipation", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" "DisableBatching"="True" }
        Cull Off Lighting Off ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            sampler2D _MainTex;
            fixed4 _Color;
            float _Phase, _Energy, _Mouth;
            v2f vert(appdata v)
            {
                v2f o;
                // Head stays steady; the caudal peduncle and tail flex along a travelling wave.
                float rear = saturate((0.4 - v.vertex.x) / 2.2);
                v.vertex.y += sin(_Phase - v.vertex.x * 2.0) * rear * rear * 0.12 * _Energy;
                // A local, gentle pectoral flutter gives painted fins motion without adding a duplicate fin.
                float fin = exp(-pow((v.vertex.x - 0.12) * 4.0, 2.0)) * saturate(-v.vertex.y * 2.0);
                v.vertex.y += sin(_Phase * 1.3) * fin * 0.026;
                v.vertex.x += saturate(v.vertex.x) * sin(_Mouth * 3.14159265) * 0.025;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target { return tex2D(_MainTex, i.uv) * i.color; }
            ENDCG
        }
    }
}
