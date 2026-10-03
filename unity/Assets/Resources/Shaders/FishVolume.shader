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
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; fixed4 color : COLOR; float2 surface : TEXCOORD0; float2 flex : TEXCOORD1; };
            struct v2f { float4 position : SV_POSITION; float3 local : TEXCOORD0; float3 normal : TEXCOORD1; fixed4 color : COLOR; float3 view : TEXCOORD2; float2 surface : TEXCOORD3; float front : TEXCOORD4; float3 skin : TEXCOORD5; };
            sampler2D _MainTex;
            float4 _Frame, _SpriteBounds, _Eye, _EyeCenter, _Head, _FinRoot;
            fixed4 _Base, _Dark, _Belly, _FogColor, _Accent;
            float _Phase, _Energy, _TailFlex, _TurnBend, _Flutter, _Mouth, _Height, _Fog, _Visibility, _Style, _Pattern, _Part, _Expression, _FrontView, _Facing;

            void jaw(inout float3 p, inout float3 normal)
            {
                float weight=smoothstep(.45,.95,p.x)*(1-smoothstep(-_Height*.45,-_Height*.15,p.y));
                float angle=-_Mouth*.25*weight, s=sin(angle), c=cos(angle);
                float2 offset=p.xy-float2(.58,-_Height*.18);
                p.xy=float2(c*offset.x-s*offset.y,s*offset.x+c*offset.y)+float2(.58,-_Height*.18);
                normal.xy=float2(c*normal.x-s*normal.y,s*normal.x+c*normal.y);
            }
            v2f vert(appdata v)
            {
                v2f o;
                o.skin=v.vertex.xyz;
                if (_Part > 1.5 && _Part < 2.5 && _Expression == 1) v.vertex.y = _EyeCenter.y + (v.vertex.y-_EyeCenter.y)*.08;
                if (_Part > 2.5)
                {
                    float z=v.surface.x*_Head.w*.74;
                    float smile=.13+(_Expression==3?.04:0);
                    float y=_Head.z*(-.20-_Mouth*.08+smile*v.surface.x*v.surface.x+lerp(.018,.25,_Mouth)*v.surface.y);
                    float front=sqrt(max(.02,1-pow(y/_Head.z,2)-pow(z/_Head.w,2)));
                    v.vertex.xyz=float3(_Head.x+_Head.y*front+.008,y,z);
                    v.normal=normalize(float3(front/_Head.y,y/(_Head.z*_Head.z),z/(_Head.w*_Head.w)));
                }
                if (_Part < .5 || _Part > 2.5) jaw(v.vertex.xyz,v.normal);
                float rear = saturate((0.45 - v.vertex.x) / 1.5);
                float wave = _Phase - v.vertex.x * 3;
                float flex = sin(wave) * _TailFlex * _Energy + _TurnBend;
                v.vertex.z += rear * rear * flex;
                float slope = -2 * rear / 1.5 * flex - rear * rear * cos(wave) * 3 * _TailFlex * _Energy;
                v.normal.x -= v.normal.z * slope;
                if (_Part > .5 && _Part < 1.5 && _FinRoot.w > .5)
                {
                    float angle=sin(_Phase*1.3+v.flex.y*.45)*v.flex.x*_Flutter;
                    float s=sin(angle),c=cos(angle);float2 offset=v.vertex.yz-_FinRoot.yz;
                    v.vertex.yz=float2(c*offset.x-s*offset.y,s*offset.x+c*offset.y)+_FinRoot.yz;
                    v.normal.yz=float2(c*v.normal.y-s*v.normal.z,s*v.normal.y+c*v.normal.z);
                }
                else v.vertex.z += sin(_Phase * 1.3) * v.flex.x * _Flutter;
                o.local = v.vertex.xyz;
                o.position = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal(normalize(v.normal));
                float3 world = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.view = unity_OrthoParams.w > .5 ? UNITY_MATRIX_V[2].xyz : _WorldSpaceCameraPos - world;
                o.color = v.color; o.surface = v.surface;
                o.front = smoothstep(.40,.85, v.normal.x) * smoothstep(.55,.95,v.vertex.x); return o;
            }
            fixed3 procedural(float3 p)
            {
                float y = p.y / max(_Height, 0.1);
                fixed3 skin = lerp(_Base.rgb, _Belly.rgb, smoothstep(0.1, -0.65, y) * 0.78);
                skin = lerp(skin, _Dark.rgb, smoothstep(0.30, 0.95, y) * 0.55);
                if (_Pattern == 1)
                {
                    float x=p.x+y*.025;
                    float distance=min(min(abs(x-.66),abs(x+.18)),abs(x+.75));
                    float border=1-smoothstep(.115,.145,distance), white=1-smoothstep(.085,.11,distance);
                    skin=lerp(skin,fixed3(.025,.045,.07),border*.92);skin=lerp(skin,fixed3(.95,.96,.87),white);
                }
                if (_Pattern == 2 || _Pattern == 6) skin = lerp(skin,_Dark.rgb,smoothstep(.78,.94,sin(p.x*15+y*.4))*.7);
                if (_Pattern == 3) skin = lerp(skin, fixed3(.02,.09,.27), smoothstep(.1,.2,y) * (1-smoothstep(.6,.83,p.x)) * .75);
                if (_Pattern == 4) { float2 cell=floor(p.xy*11); float noise=frac(sin(dot(cell,float2(12.9898,78.233)))*43758.5453); float spot=(1-smoothstep(.10,.23,length(frac(p.xy*11)-.5)))*step(.5,noise); skin=lerp(skin,_Dark.rgb,spot*.65); }
                if (_Pattern == 5 || _Pattern == 7 || _Pattern == 0)
                {
                    float row=floor(y*13), column=frac(p.x*14+row*.5);
                    float scale=(1-smoothstep(.03,.09,abs(length(float2(column-.5,frac(y*13)-.55))-.40)));
                    skin+=scale*.035;
                }
                if (_Pattern == 7)
                {
                    float stripe=1-smoothstep(.055,.08,abs(y-(.43+.025*sin(p.x*3))));
                    skin=lerp(skin,_Accent.rgb,stripe*.95);
                    float spots=0;
                    for (int j=0;j<3;j++) spots=max(spots,1-smoothstep(.035,.055,length(float2(p.x-(.02-j*.28),y+.17))));
                    skin=lerp(skin,_Belly.rgb,spots*.8);
                }
                if (_Pattern == 9) skin=lerp(skin,_Dark.rgb,(1-smoothstep(.035,.06,abs(y-.06)))*.45);
                // Small recessed gill crease follows the curved skin on both sides.
                float gill = exp(-pow((p.x-.48+y*.13)*38,2)) * (1-smoothstep(.30,.65,abs(y)));
                if (_Pattern == 8 || _Pattern == 10)
                    for (int j=1;j<4;j++) gill+=exp(-pow((p.x-(.48-j*.105)+y*.14)*46,2))*(1-smoothstep(.25,.6,abs(y)))*.7;
                return skin * (1-gill*.14);
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float3 normal = normalize(i.normal), view = normalize(i.view);
                fixed3 rgb = i.color.rgb;
                if (_Part < .5)
                {
                    // Keep projected skin inside the illustration's body. Its outer black contour and
                    // baked dorsal/ventral fins belong to the flat silhouette, not the rounded surface.
                    float2 skin=float2(i.skin.x,i.skin.y*.78);
                    float2 uv=(skin-_SpriteBounds.xy)/_SpriteBounds.zw;
                    fixed4 painted=tex2D(_MainTex,_Frame.xy+saturate(uv)*_Frame.zw);
                    fixed3 cleanSkin=procedural(i.skin);
                    // Remove the baked eye before adding paired globe eyes, including the occluded side.
                    float mask=1-smoothstep(.78,1.12,length((skin-_Eye.xy)/max(_Eye.zw,.02)));
                    painted.rgb=lerp(painted.rgb,cleanSkin,mask);
                    float finMask=1-smoothstep(.7,1.08,length((skin-float2(.20,-_Height*.13))/float2(.52,_Height*.40)));
                    painted.rgb=lerp(painted.rgb,cleanSkin,finMask);
                    float mouthMask=1-smoothstep(.6,1.1,length((skin-float2(1.09,-_Height*.19))/float2(.23,_Height*.24)));
                    painted.rgb=lerp(painted.rgb,lerp(_Base.rgb,_Belly.rgb,saturate(-skin.y/_Height)*.7),mouthMask);
                    // Composite the illustration before blending the front: its transparent outline must
                    // not punch patches of flat base color into the curved, head-on skin.
                    fixed3 paintedSkin=lerp(_Base.rgb,painted.rgb,painted.a);
                    float profile=i.skin.x>=_Head.x?sqrt(max(.000016,1-pow((i.skin.x-_Head.x)/_Head.y,2))):lerp(.10,1,smoothstep(-1.04,_Head.x,i.skin.x));
                    float contour=smoothstep(.35,.68,abs(i.skin.y)/max(.001,_Height*profile));
                    paintedSkin=lerp(paintedSkin,cleanSkin,max(contour,max(i.front,_FrontView)));
                    rgb=lerp(paintedSkin,cleanSkin,_Style);
                }
                else if (_Part < 1.5) rgb *= .96 + .04*cos(i.surface.y*18+i.surface.x*1.5)*smoothstep(.05,.25,i.surface.x);
                else if (_Part < 2.5)
                {
                    if (_Expression == 1 && dot(rgb,1) > 1.5) rgb = _Base.rgb;
                    if (_Expression == 2 && i.local.y > _EyeCenter.y + .025 - (i.local.x-_EyeCenter.x)*.35) rgb = _Dark.rgb;
                    if (_Expression == 3 && i.local.y < _EyeCenter.y - .015) rgb = _Base.rgb;
                }
                else
                {
                    float lip=smoothstep(.82,.99,length(i.surface));
                    rgb=lerp(fixed3(.035,.055,.075),lerp(_Base.rgb,_Dark.rgb,.4),lip);
                    float tongue=(1-smoothstep(.8,1,length((i.surface-float2(0,-.45))/float2(.62,.35))))*smoothstep(.2,.6,_Mouth)*(1-lip);
                    rgb=lerp(rgb,fixed3(.78,.25,.34),tongue);
                    if (dot(i.color.rgb,1)>2.8) rgb=fixed3(.94,.95,.84);
                }
                float diffuse=saturate(dot(normal,normalize(float3(-.3,.65,-.7))));
                float ndv=saturate(dot(normal,view));
                float lighting=lerp(.80,.65,_Style)+diffuse*lerp(.25,.42,_Style);
                float specular=pow(saturate(dot(normal,normalize(view+normalize(float3(-.3,.65,-.7))))),24)*lerp(.035,.08,_Style);
                rgb=PaintedLighting(rgb, i.local.y / max(_Height*2,.1)+.5)*lighting;
                rgb+=specular*fixed3(.65,.85,.9);
                rgb*=lerp(.88,1,smoothstep(.02,.30,ndv));
                float visibility=_Visibility;
                if (_Part > 2.5 && dot(i.color.rgb,1)>2.8) visibility *= smoothstep(.25,.6,_Mouth);
                return fixed4(lerp(rgb,_FogColor.rgb,_Fog),visibility);
            }
            ENDCG
        }
    }
}
