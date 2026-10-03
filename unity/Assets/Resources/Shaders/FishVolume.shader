Shader "DeepFeast/FishVolume"
{
    Properties { _MainTex ("Painted skin", 2D) = "white" {} }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "DisableBatching"="True" }
        Cull Back ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "PaintedLighting.cginc"
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; fixed4 color : COLOR; float2 flex : TEXCOORD1; };
            struct v2f { float4 position : SV_POSITION; float3 local : TEXCOORD0; float3 normal : TEXCOORD1; fixed4 color : COLOR; float3 view : TEXCOORD2; float2 flex : TEXCOORD3; float front : TEXCOORD4; };
            sampler2D _MainTex;
            float4 _Frame, _SpriteBounds, _Eye, _EyeCenter;
            fixed4 _Base, _Dark, _Belly, _FogColor;
            float _Phase, _Energy, _TailFlex, _TurnBend, _Flutter, _Mouth, _Height, _Fog, _Visibility, _Style, _Pattern, _Part, _Expression, _FrontView, _Facing;
            v2f vert(appdata v)
            {
                v2f o;
                if (_Part > 1.5 && _Part < 2.5 && _Expression == 1) v.vertex.y = _EyeCenter.y + (v.vertex.y-_EyeCenter.y)*.08;
                if (_Part > 2.5) v.vertex.y = -_Height*.17 + (v.vertex.y+_Height*.17)*(1+_Mouth*2.8);
                float rear = saturate((0.45 - v.vertex.x) / 1.5);
                float wave = _Phase - v.vertex.x * 3;
                float flex = sin(wave) * _TailFlex * _Energy + _TurnBend;
                v.vertex.z += rear * rear * flex;
                float slope = -2 * rear / 1.5 * flex - rear * rear * cos(wave) * 3 * _TailFlex * _Energy;
                v.normal.x -= v.normal.z * slope;
                v.vertex.z += sin(_Phase * 1.3) * v.flex.x * _Flutter * (v.flex.y == 0 ? 1 : v.flex.y);
                if (_Part < 0.5) v.vertex.y -= _Mouth * saturate((v.vertex.x - 0.6) * 2) * saturate(-v.vertex.y / max(_Height, 0.1)) * 0.1;
                o.local = v.vertex.xyz;
                o.position = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal(normalize(v.normal));
                float3 world = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.view = _WorldSpaceCameraPos - world;
                o.color = v.color; o.flex = v.flex;
                o.front = smoothstep(.40,.85, v.normal.x) * smoothstep(.55,.95,v.vertex.x); return o;
            }
            fixed3 procedural(float3 p)
            {
                float y = p.y / max(_Height, 0.1);
                fixed3 skin = lerp(_Base.rgb, _Belly.rgb, smoothstep(0.1, -0.65, y) * 0.78);
                skin = lerp(skin, _Dark.rgb, smoothstep(0.30, 0.95, y) * 0.55);
                if (_Pattern == 1) { float band = max(max(1-smoothstep(.10,.15,abs(p.x-.66)),1-smoothstep(.09,.14,abs(p.x+.18))),1-smoothstep(.08,.12,abs(p.x+.75))); skin = lerp(skin,_Dark.rgb,band); skin=lerp(skin,fixed3(.94,.95,.84),smoothstep(.45,.75,band)); }
                if (_Pattern == 2 || _Pattern == 6) skin = lerp(skin,_Dark.rgb,smoothstep(.78,.94,sin(p.x*15+y*.4))*.7);
                if (_Pattern == 3) skin = lerp(skin, fixed3(.02,.09,.27), smoothstep(.1,.2,y) * (1-smoothstep(.6,.83,p.x)) * .75);
                if (_Pattern == 4) { float2 cell=floor(p.xy*11); float noise=frac(sin(dot(cell,float2(12.9898,78.233)))*43758.5453); float spot=(1-smoothstep(.10,.23,length(frac(p.xy*11)-.5)))*step(.5,noise); skin=lerp(skin,_Dark.rgb,spot*.65); }
                if (_Pattern == 5 || _Pattern == 7) { float scale=pow(saturate(cos(p.x*27 + floor(y*12)*1.5)*cos(y*35)),14); skin+=scale*.06; }
                // Small recessed gill crease follows the curved skin on both sides.
                float gill = exp(-pow((p.x-.48+y*.13)*38,2)) * (1-smoothstep(.30,.65,abs(y)));
                return skin * (1-gill*.16);
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float3 normal = normalize(i.normal), view = normalize(i.view);
                fixed3 rgb = i.color.rgb;
                if (_Part < .5)
                {
                    float2 uv=(i.local.xy-_SpriteBounds.xy)/_SpriteBounds.zw;
                    fixed4 painted=tex2D(_MainTex,_Frame.xy+saturate(uv)*_Frame.zw);
                    // Remove the baked eye before adding paired globe eyes, including the occluded side.
                    float mask=1-smoothstep(.78,1.12,length((i.local.xy-_Eye.xy)/max(_Eye.zw,.02)));
                    float2 clean=(float2(_Eye.x-.34,_Eye.y-.20)-_SpriteBounds.xy)/_SpriteBounds.zw;
                    fixed4 replacement=tex2D(_MainTex,_Frame.xy+saturate(clean)*_Frame.zw);
                    painted.rgb=lerp(painted.rgb,lerp(_Base.rgb,replacement.rgb,replacement.a),mask);
                    float finMask=1-smoothstep(.7,1.08,length((i.local.xy-float2(.22,-_Height*.23))/float2(.42,_Height*.36)));
                    float2 finSkin=(i.local.xy+float2(0,_Height*.55)-_SpriteBounds.xy)/_SpriteBounds.zw;
                    fixed4 finReplacement=tex2D(_MainTex,_Frame.xy+saturate(finSkin)*_Frame.zw);
                    painted.rgb=lerp(painted.rgb,lerp(_Base.rgb,finReplacement.rgb,finReplacement.a),finMask*.9);
                    // The frontal cap needs clean skin rather than the side illustration's mouth outline.
                    painted.rgb=lerp(painted.rgb,lerp(_Base.rgb,_Belly.rgb,smoothstep(.1,-.65,i.local.y/max(_Height,.1))*.55),i.front);
                    painted.rgb=lerp(painted.rgb,procedural(i.local),_FrontView*.96);
                    rgb=lerp(lerp(_Base.rgb,painted.rgb,painted.a),procedural(i.local),_Style);
                }
                else if (_Part < 1.5) rgb *= .92 + .08 * cos(i.local.x*45 + i.local.y*22);
                else if (_Part < 2.5)
                {
                    if (_Expression == 1 && dot(rgb,1) > 1.5) rgb = _Base.rgb;
                    if (_Expression == 2 && i.local.y > _EyeCenter.y + .025 - (i.local.x-_EyeCenter.x)*.35) rgb = _Dark.rgb;
                    if (_Expression == 3 && i.local.y < _EyeCenter.y - .015) rgb = _Base.rgb;
                }
                float diffuse=saturate(dot(normal,normalize(float3(-.3,.65,-.7))));
                float ndv=saturate(dot(normal,view));
                float lighting=lerp(.78,.60,_Style)+diffuse*lerp(.26,.47,_Style);
                float specular=pow(saturate(dot(normal,normalize(view+normalize(float3(-.3,.65,-.7))))),32)*lerp(.035,.13,_Style);
                rgb=PaintedLighting(rgb, i.local.y / max(_Height*2,.1)+.5)*lighting;
                rgb+=specular*fixed3(.65,.85,.9);
                rgb*=lerp(.88,1,smoothstep(.02,.30,ndv));
                float visibility=_Visibility;
                if (_Part > 2.5)
                {
                    if (i.local.x > 1.18) visibility *= smoothstep(.18,.60,sqrt(saturate(1-_Facing*_Facing)));
                    else visibility *= smoothstep(-.12,.12,-sign(i.local.z)*_Facing);
                }
                return fixed4(lerp(rgb,_FogColor.rgb,_Fog),visibility);
            }
            ENDCG
        }
    }
}
