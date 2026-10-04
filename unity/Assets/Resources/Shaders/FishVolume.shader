Shader "DeepFeast/FishVolume"
{
    Properties { _MainTex ("Painted skin", 2D) = "white" {} }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "DisableBatching"="True" }
        CGINCLUDE
        #include "UnityCG.cginc"
        #include "PaintedLighting.cginc"
        struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; fixed4 color : COLOR; float2 surface : TEXCOORD0; float2 flex : TEXCOORD1; };
        sampler2D _MainTex;
        float4 _Frame, _SpriteBounds, _Eye, _Head, _Profile, _FinRoot, _MouthShape;
        fixed4 _Base, _Dark, _Belly, _FogColor, _Accent, _WaterReflection, _GroundReflection;
        float _Phase, _Energy, _TailFlex, _TurnBend, _Flutter, _Mouth, _Height, _Fog, _Visibility, _Style, _Pattern, _Part, _Expression, _FrontView, _Facing, _ReflectionStrength, _SharkKind;
        float _PaintedFins, _PaintedPectoral, _Painterly;
        float4 _PectoralMask;

        float bodyProfile(float x)
        {
            if(x>=_Head.x) return pow(max(.000016,1-pow((x-_Head.x)/_Head.y,2)),_Profile.y);
            return lerp(_Profile.z,1,pow(max(0,sin(saturate((x+1.04)/(_Head.x+1.04))*UNITY_PI*.5)),_Profile.x));
        }
        float mouthCenter(float across)
        {
            return _MouthShape.x-_Mouth*.08+((_Pattern==8?.055:.13)+(_Expression==3&&_Pattern!=8?.04:0))*across*across;
        }
        float mouthRadius() { return lerp(.018,_MouthShape.y,_Mouth); }

        void jaw(inout float3 p, inout float3 normal)
        {
            bool shark=_Pattern==8;
            float weight=smoothstep(shark?.24:.45,shark?.80:.95,p.x)*(1-smoothstep(-_Height*.45+_Profile.w,-_Height*.15+_Profile.w,p.y));
            float angle=-_Mouth*_MouthShape.w*weight, s=sin(angle), c=cos(angle);
            float2 hinge=float2(shark?.40:.58,-_Height*.18+_Profile.w);
            float2 offset=p.xy-hinge;
            p.xy=float2(c*offset.x-s*offset.y,s*offset.x+c*offset.y)+hinge;
            normal.xy=float2(c*normal.x-s*normal.y,s*normal.x+c*normal.y);
        }
        // Every pass deforms the mesh identically, so the ink line stays fixed to the swimming body.
        void Animate(inout appdata v)
        {
            if (_Part > 1.5 && _Part < 2.5 && _Expression == 1) v.vertex.y = v.surface.y + (v.vertex.y-v.surface.y)*.08;
            if (_Part > 2.5)
            {
                float z=v.surface.x*_Head.w*_MouthShape.z;
                float y=_Head.z*(mouthCenter(v.surface.x)+mouthRadius()*v.surface.y);
                float radial=pow(y/_Head.z,2)+pow(z/_Head.w,2);
                float front=sqrt(max(.02,1-pow(radial,1/(2*_Profile.y))));
                float inset=_Mouth*.15*(1-smoothstep(.78,1,length(v.surface)));
                v.vertex.xyz=float3(_Head.x+_Head.y*front+.008-inset,y+_Profile.w,z);
                float slope=2*_Profile.y*front/_Head.y*pow(max(.02,1-front*front),2*_Profile.y-1);
                v.normal=normalize(float3(slope,y/(_Head.z*_Head.z),z/(_Head.w*_Head.w)));
            }
            if (_Part < .5 || _Part > 2.5) jaw(v.vertex.xyz,v.normal);
            // A travelling wave reaches the tail after the shoulder. The fin root receives exactly
            // the same displacement as the body; its thin edge then trails the root's motion.
            float rear = max(0,(0.50 - v.vertex.x) / 1.54);
            float wave = _Phase + (v.vertex.x-.35) * 2.2;
            float flex = sin(wave) * _TailFlex * _Energy + _TurnBend;
            v.vertex.z += rear * rear * flex;
            float slope = -2 * rear / 1.54 * flex + rear * rear * cos(wave) * 2.2 * _TailFlex * _Energy;
            v.normal.x -= v.normal.z * slope;
            if (_Part > .5 && _Part < 1.5 && _FinRoot.w > .5)
            {
                float angle=sin(_Phase+v.flex.y*.3-v.flex.x*.8)*v.flex.x*_Flutter*(_Pattern==8?.25:1);
                float s=sin(angle),c=cos(angle);float2 offset=v.vertex.yz-_FinRoot.yz;
                v.vertex.yz=float2(c*offset.x-s*offset.y,s*offset.x+c*offset.y)+_FinRoot.yz;
                v.normal.yz=float2(c*v.normal.y-s*v.normal.z,s*v.normal.y+c*v.normal.z);
            }
            else if (_Part > .5 && _Part < 1.5)
            {
                float weight=v.flex.x, lag=wave-weight*.9+v.surface.y*.25;
                float stiffness=_Pattern==8?.25:1;
                float flutter=sin(lag)*weight*weight*_Flutter*stiffness;
                v.vertex.z+=flutter;
                float bend=(2*weight*sin(lag)-.9*weight*weight*cos(lag))*_Flutter*stiffness;
                if (v.flex.y>3.5) v.normal.x+=v.normal.z*bend/.65;
                else v.normal.y-=v.normal.z*bend*sign(2.5-v.flex.y)/max(.1,_Height*.5);
            }
        }
        fixed3 OutlineColor() { return lerp(fixed3(.025,.04,.055),_Dark.rgb*.30,.35); }
        // Ink width follows the fish's on-screen size, like a contour drawn at the sprite's scale.
        float InkPixels(float4 clip)
        {
            float2 axisX = mul(UNITY_MATRIX_VP, mul(unity_ObjectToWorld, float4(1,0,0,0))).xy*_ScreenParams.xy*.5;
            float2 axisY = mul(UNITY_MATRIX_VP, mul(unity_ObjectToWorld, float4(0,1,0,0))).xy*_ScreenParams.xy*.5;
            float pixelsPerUnit = max(length(axisX),length(axisY))/max(clip.w,1e-4);
            return clamp(pixelsPerUnit*.045, 1.25, 5*_ScreenParams.y/1440);
        }
        ENDCG

        // Inverted hull: expanded back faces give the solid body the painted art's dark contour.
        // Fins draw a rim line in the main pass instead: a hull around a thin or small fin
        // reads as a solid black bar, and translucent membranes would darken through it.
        // The opaque shark caudal is the exception; its surface runs root to tip, not to a rim.
        Pass
        {
            Name "OUTLINE"
            Cull Front ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex outlineVert
            #pragma fragment outlineFrag
            struct v2fo { float4 position : SV_POSITION; };
            v2fo outlineVert(appdata v)
            {
                v2fo o;
                if (_Part > 1.5 || (_Part > .5 && (_Pattern != 8 || v.flex.y < 3.5))) { o.position = float4(2,2,2,1); return o; }
                Animate(v);
                float4 clip = UnityObjectToClipPos(v.vertex);
                float2 normal = mul((float3x3)UNITY_MATRIX_VP, UnityObjectToWorldNormal(normalize(v.normal))).xy;
                float length2 = dot(normal,normal);
                normal = length2 > 1e-8 ? normal*rsqrt(length2) : 0;
                clip.xy += normal*InkPixels(clip)*2/_ScreenParams.xy*clip.w;
                o.position = clip;
                return o;
            }
            fixed4 outlineFrag(v2fo i) : SV_Target { return fixed4(lerp(OutlineColor(),_FogColor.rgb,_Fog),_Visibility*.95); }
            ENDCG
        }

        Pass
        {
            Cull Back ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            struct v2f { float4 position : SV_POSITION; float3 local : TEXCOORD0; float3 normal : TEXCOORD1; fixed4 color : COLOR; float3 view : TEXCOORD2; float2 surface : TEXCOORD3; float front : TEXCOORD4; float3 skin : TEXCOORD5; float2 fin : TEXCOORD6; };
            v2f vert(appdata v)
            {
                v2f o;
                o.skin=v.vertex.xyz;
                Animate(v);
                o.local = v.vertex.xyz;
                o.position = UnityObjectToClipPos(v.vertex);
                o.fin = float2(v.flex.y, InkPixels(o.position));
                o.normal = UnityObjectToWorldNormal(normalize(v.normal));
                float3 world = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.view = unity_OrthoParams.w > .5 ? UNITY_MATRIX_V[2].xyz : _WorldSpaceCameraPos - world;
                o.color = v.color; o.surface = v.surface;
                o.front = smoothstep(.35,.95, v.normal.x) * smoothstep(.50,1.0,v.vertex.x); return o;
            }
            fixed4 painting(float2 skin)
            {
                float2 uv=(skin-_SpriteBounds.xy)/_SpriteBounds.zw;
                fixed4 painted=tex2D(_MainTex,_Frame.xy+saturate(uv)*_Frame.zw);
                painted.a*=all(uv==saturate(uv));
                return painted;
            }
            // Paints over a baked feature with the surrounding illustration: four samples just
            // outside the ellipse, weighted by distance and ignoring transparent or inked pixels.
            fixed3 inpaint(float2 skin, float4 ellipse, fixed3 fallback)
            {
                float2 q=(skin-ellipse.xy)/ellipse.zw, r=ellipse.zw*1.15;
                float4 sx=float4(ellipse.x-r.x,ellipse.x+r.x,skin.x,skin.x), sy=float4(skin.y,skin.y,ellipse.y+r.y,ellipse.y-r.y);
                float4 distance=float4(q.x+1.15,1.15-q.x,1.15-q.y,q.y+1.15);
                fixed3 sum=fallback*.02; float total=.02;
                [unroll] for (int j=0;j<4;j++)
                {
                    fixed4 c=painting(float2(sx[j],sy[j]));
                    float w=c.a*smoothstep(.10,.24,dot(c.rgb,fixed3(.2126,.7152,.0722)))/max(.05,distance[j]);
                    sum+=c.rgb*w; total+=w;
                }
                return sum/total;
            }
            fixed3 procedural(float3 p)
            {
                float y = p.y / max(_Height, 0.1);
                fixed3 skin = lerp(_Base.rgb, _Belly.rgb, smoothstep(0.1, -0.65, y) * 0.78);
                skin = lerp(skin, _Dark.rgb, smoothstep(0.35, 0.98, y) * 0.40);
                skin += _Base.rgb * exp(-pow((y-.18)*3,2)) * .07;
                if (_Pattern == 8)
                {
                    float belly=1-smoothstep(-.27,-.10,y+.035*sin(p.x*8));
                    skin=lerp(lerp(_Base.rgb,_Dark.rgb,smoothstep(.18,.90,y)*.42),_Belly.rgb,belly*.96);
                    if (_SharkKind==2)
                    {
                        float wave=p.x*20+y*1.8+sin(y*7+p.x*2)*.9;
                        float bars=smoothstep(.72,.94,sin(wave))*smoothstep(-.32,-.16,y);
                        skin=lerp(skin,_Dark.rgb,bars*.65*(1-smoothstep(.85,1.12,p.x))*smoothstep(-1.02,-.78,p.x)*smoothstep(-.22,.10,y));
                    }
                }
                if (_Pattern == 1)
                {
                    float x=p.x+y*.035+y*y*.065;
                    float distance=min(min(abs(x-.66),abs(x+.18)),abs(x+.75));
                    float border=1-smoothstep(.115,.145,distance), white=1-smoothstep(.085,.11,distance);
                    skin=lerp(skin,fixed3(.025,.045,.07),border*.92);skin=lerp(skin,fixed3(.95,.96,.87),white);
                }
                if (_Pattern == 2)
                {
                    float x=p.x+y*.065;
                    float distance=min(min(abs(x-.60),abs(x+.04)),abs(x+.70));
                    skin=lerp(skin,fixed3(.035,.045,.05),(1-smoothstep(.07,.105,distance))*.92);
                }
                if (_Pattern == 6) skin = lerp(skin,_Dark.rgb,smoothstep(.78,.94,sin(p.x*15+y*.4))*.7);
                if (_Pattern == 3) skin = lerp(skin, fixed3(.02,.09,.27), smoothstep(.1,.2,y) * (1-smoothstep(.6,.83,p.x)) * .75);
                if (_Pattern == 4)
                {
                    // Scattered blotches of varied size. Cells span all three axes, so spots stay
                    // round on the face and flanks instead of streaking along the projection.
                    float3 uv=p*float3(7,8,8); float3 cell=floor(uv);
                    float seed=frac(sin(dot(cell,float3(127.1,311.7,74.7)))*43758.5453);
                    float seed2=frac(sin(dot(cell,float3(269.5,183.3,246.1)))*43758.5453);
                    float size=lerp(.16,.30,seed);
                    float distance=length(frac(uv)-float3(.30+seed2*.40,.30+seed*.40,.35+frac(seed*7.3)*.30));
                    float spot=(1-smoothstep(size*.55,size,distance))*step(.30,seed2)*smoothstep(-.55,-.1,y);
                    skin=lerp(skin,_Dark.rgb,spot*.55);
                }
                if (_Style<.5 && (_Pattern == 5 || _Pattern == 7 || _Pattern == 0))
                {
                    float row=floor(y*13), column=frac(p.x*14+row*.5);
                    float scale=(1-smoothstep(.03,.09,abs(length(float2(column-.5,frac(y*13)-.55))-.40)));
                    skin+=scale*.035;
                }
                if (_Pattern == 7)
                {
                    float stripe=1-smoothstep(.06,.085,abs(y-(.40+.075*sin(p.x*2.3))));
                    skin=lerp(skin,_Accent.rgb,stripe*.95);
                    float spots=0;
                    for (int j=0;j<3;j++) spots=max(spots,1-smoothstep(.035,.055,length(float2(p.x-(.02-j*.28),y+.17))));
                    skin=lerp(skin,_Belly.rgb,spots*.8);
                }
                if (_Pattern == 9 || _Pattern == 20) skin=lerp(skin,_Dark.rgb,(1-smoothstep(.035,.06,abs(y-.06)))*.45);
                // Pelagic counter-shading: a dark back meets the silver flank along a crisp, wavy
                // line. It is what tells these silver species apart at play size.
                if (_Pattern==16 || _Pattern==17 || _Pattern==20)
                {
                    float boundary=(_Pattern==16?.06:_Pattern==17?.16:.10)+.035*sin(p.x*5.5+1.3);
                    skin=lerp(skin,_Dark.rgb*(_Pattern==16?.78:.85),smoothstep(boundary-.05,boundary+.09,y)*(_Pattern==17?.72:.88));
                }
                // Species markings wrap the body in rest coordinates, so they remain stable in turns.
                if (_Pattern==11)
                {
                    float amber=exp(-pow((y-.08)*5,2));
                    skin=lerp(skin,fixed3(.69,.61,.29),amber*.27);
                    skin=lerp(skin,_Dark.rgb,exp(-pow((p.x-.78+y*.45)*12,2))*.35);
                }
                if (_Pattern==12 || _Pattern==13)
                {
                    float2 uv=p.xy*float2(10,15);
                    float2 cell=floor(uv); float seed=frac(sin(dot(cell,float2(127.1,311.7)))*43758.5453);
                    float seed2=frac(sin(dot(cell,float2(269.5,183.3)))*43758.5453);
                    float2 offset=float2(.20+seed*.60,.20+seed2*.60);
                    float distance=length((frac(uv)-offset)*float2(1,lerp(.75,1.5,seed2)));
                    float size=lerp(.08,.19,seed);
                    float spot=(1-smoothstep(size*.50,size,distance))*step(.27,seed2);
                    float mottling=.5+.5*sin(p.x*18+sin(y*13))*sin(y*21+p.x*4);
                    skin=lerp(skin,_Dark.rgb,spot*.58+mottling*(_Pattern==13?.28:.18));
                    if (_Pattern==12) skin=lerp(skin,_Dark.rgb,smoothstep(.79,.96,sin(p.x*15+y*.8))*.15);
                    if (_Pattern==13)
                    {
                        float lateral=.08+.20*exp(-pow((p.x-.35)*2.8,2));
                        skin=lerp(skin,_Dark.rgb,exp(-pow((y-lateral)*75,2))*.26);
                    }
                }
                if (_Pattern==14)
                {
                    skin=lerp(skin,_Dark.rgb,smoothstep(.1,.6,y)*.52);
                    float wave=p.x*34+sin(y*10+p.x*3)*1.2;
                    skin=lerp(skin,_Dark.rgb,smoothstep(.40,.75,sin(wave))*smoothstep(.03,.27,y)*.88);
                }
                if (_Pattern==15)
                {
                    skin=lerp(_Base.rgb,_Dark.rgb,smoothstep(-.08,.54,y)*.9);
                    skin=lerp(skin,_Belly.rgb,smoothstep(-.22,-.75,y)*.7);
                    float2 uv=p.xy*float2(13,17); float2 cell=floor(uv);
                    float seed=frac(sin(dot(cell,float2(127.1,311.7)))*43758.5453);
                    float seed2=frac(sin(dot(cell,float2(269.5,183.3)))*43758.5453);
                    float spot=(1-smoothstep(.035,.09+seed*.035,length(frac(uv)-float2(.20+seed*.60,.20+seed2*.60))))*step(.48,seed);
                    skin=lerp(skin,fixed3(.03,.28,.59),spot*.84);
                }
                if (_Pattern==16 || _Pattern==17)
                {
                    float stripe=0;
                    for(int j=0;j<7;j++)
                    {
                        float center=_Pattern==16?-.14-j*.095:.47-j*.15;
                        float marking=1-smoothstep(.022,.036,abs(y-center-.012*sin(p.x*3)));
                        stripe=max(stripe,marking*(_Pattern==16&&j>4?0:1));
                    }
                    skin=lerp(skin,_Dark.rgb*.55,stripe*.85*(1-smoothstep(.60,.95,p.x)));
                }
                if (_Pattern==18)
                {
                    skin=lerp(skin,_Dark.rgb,smoothstep(.04,.65,y)*.76);
                    float marking=1-smoothstep(.045,.10,abs(y-.02));
                    skin=lerp(skin,fixed3(.90,.78,.27),marking*.65);
                }
                if (_Pattern==19)
                {
                    float spot=1-smoothstep(.075,.105,length(float2(p.x+.84,y-.13)));
                    skin=lerp(skin,fixed3(.025,.035,.04),spot*.98);
                }
                // Small recessed gill crease follows the curved skin on both sides.
                float gill = exp(-pow((p.x-.48+y*.13)*38,2)) * (1-smoothstep(.30,.65,abs(y)));
                if (_Pattern == 8)
                {
                    gill=0;
                    [unroll] for (int j=0;j<5;j++) gill+=exp(-pow((p.x-(.54-j*.065)+y*y*.07)*105,2))*(1-smoothstep(.25,.44,abs(y+.02)));
                    skin*=1-gill*.30;
                    float nostril=exp(-pow((p.x-(_Head.x+_Head.y*.94))*38,2)-pow((y+.12)*30,2));
                    skin*=1-nostril*.45;
                }
                else if (_Painterly<.5) skin*=1-gill*.20;
                return skin;
            }
            // Species without an illustration borrow its cues on the procedural skin: a lit band
            // along the flank, an inked gill cover with a raised rim, and soft rows of scales.
            fixed3 painterly(float3 p, fixed3 skin)
            {
                float center=_Profile.w*saturate((p.x+1.04)/(_Head.x+1.04));
                float relative=(p.y-center)/max(_Height*bodyProfile(p.x),.001);
                float sheen=exp(-pow((relative-.45)*5,2))*smoothstep(-1.0,-.6,p.x);
                skin=lerp(skin,skin*1.15+.09,sheen*.6);
                if (_Pattern==8) return skin;
                float radius=_Height*.92;
                float2 hinge=float2(_Eye.x+radius*.35,center-_Height*.10);
                float rim=length(p.xy-hinge)-radius;
                float cover=smoothstep(hinge.x,hinge.x-.15,p.x)*(1-smoothstep(.70,.86,abs(relative)));
                skin*=1-exp(-pow(rim/.016,2))*.42*cover;
                skin=lerp(skin,skin*1.12+.05,exp(-pow((rim+.045)/.03,2))*.5*cover);
                skin*=1-exp(-pow((rim-.05)/.04,2))*.10*cover;
                if (_Pattern!=12 && _Pattern!=13)
                {
                    // Free edges face the tail, as overlapping scales do.
                    float rows=relative*7, row=floor(rows), column=frac(p.x*9+row*.5)-.5;
                    float scale=1-smoothstep(.04,.12,abs(length(float2(column*1.1,frac(rows)-.65))-.42));
                    scale*=smoothstep(.05,-.15,column)*smoothstep(0,.08,rim)*smoothstep(-1.0,-.70,p.x)*(1-smoothstep(.60,.95,abs(relative)));
                    skin*=1-scale*.07;
                }
                return skin;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float3 normal = normalize(i.normal), view = normalize(i.view);
                fixed3 rgb = i.color.rgb;
                float ink = 0;
                if (_Part < .5)
                {
                    // Open a real aperture in the skin so the inset mouth bowl has visible depth.
                    // The test uses rest coordinates, before the shared jaw bend on skin and lip.
                    float across=i.skin.z/max(.001,_Head.w*_MouthShape.z);
                    float vertical=((i.skin.y-_Profile.w)/_Height-mouthCenter(across))/mouthRadius();
                    if (_Mouth>.08 && i.skin.x>_Head.x+_Head.y*.20 && across*across+vertical*vertical<.82*.82) discard;
                    // Keep projected skin inside the illustration's body. Its outer black contour and
                    // baked dorsal/ventral fins belong to the flat silhouette, not the rounded surface.
                    float profile=bodyProfile(i.skin.x);
                    float center=_Profile.w*saturate((i.skin.x+1.04)/(_Head.x+1.04));
                    float2 skin=float2(i.skin.x,i.skin.y*.78);
                    if (_PaintedFins>.5)
                    {
                        // Matched bodies sample the painting where it is; only the outer band is
                        // squeezed so the constant-width painted contour never lands on the curved back.
                        float halfHeight=max(_Height*profile,.001), relative=(i.skin.y-center)/halfHeight;
                        float limit=max(.40,1-.075/halfHeight), knee=min(.60,limit), outer=abs(relative);
                        if (outer>knee) outer=knee+(outer-knee)*(limit-knee)/(1-knee);
                        skin=float2(min(i.skin.x,_Head.x+_Head.y-.07),center+sign(relative)*outer*halfHeight);
                    }
                    fixed4 painted=painting(skin);
                    fixed3 cleanSkin=procedural(i.skin);
                    // Remove the baked eye before adding paired globe eyes, including the occluded side.
                    float4 eye=float4(_Eye.xy,max(_Eye.zw,.02));
                    float mask=1-smoothstep(.78,1.12,length((skin-eye.xy)/eye.zw));
                    if (mask>0) painted.rgb=lerp(painted.rgb,_PaintedFins>.5?inpaint(skin,eye,cleanSkin):cleanSkin,mask);
                    // The traced fan covers the baked pectoral; when it lifts off in a turn, the darkened
                    // painting beneath reads as its shadow on the flank.
                    if (_PectoralMask.z>0) painted.rgb*=1-(1-smoothstep(.55,1.05,length((skin-_PectoralMask.xy)/_PectoralMask.zw)))*.32;
                    else if (_PaintedFins<.5) painted.rgb=lerp(painted.rgb,cleanSkin,1-smoothstep(.7,1.08,length((skin-float2(.20,-_Height*.13))/float2(.52,_Height*.40))));
                    // The sculpted mouth replaces the painted one; matched heads cover both positions.
                    float4 mouth=_PaintedFins>.5?float4(_Head.x+_Head.y-.10,_Profile.w+_Head.z*_MouthShape.x*.5,.20,_Height*.26):float4(1.09,-_Height*.19,.23,_Height*.24);
                    float mouthMask=1-smoothstep(.6,1.1,length((skin-mouth.xy)/mouth.zw));
                    fixed3 snout=lerp(_Base.rgb,_Belly.rgb,saturate(-skin.y/_Height)*.7);
                    if (mouthMask>0) painted.rgb=lerp(painted.rgb,_PaintedFins>.5?inpaint(skin,mouth,snout):snout,mouthMask);
                    // Composite the illustration before blending the front: its transparent outline must
                    // not punch patches of flat base color into the curved, head-on skin.
                    fixed3 paintedSkin=lerp(_Base.rgb,painted.rgb,painted.a);
                    float contour=_PaintedFins>.5?0:max(i.front,smoothstep(.35,.68,abs(i.skin.y-center)/max(.001,_Height*profile)));
                    paintedSkin=lerp(paintedSkin,cleanSkin,max(contour,_FrontView));
                    // Vertex tint colors the puffer's spines; the rest of the body is white.
                    rgb=(_Painterly>.5?painterly(i.skin,cleanSkin):lerp(paintedSkin,cleanSkin,_Style))*i.color.rgb;
                }
                else if (_Part < 1.5 && _PaintedFins > .5 && _FinRoot.w < .5 && _Style < .5)
                {
                    // Painted median fins: the illustration's own outline, rays and ink cut the envelope.
                    fixed4 painted=painting(i.skin.xy);
                    rgb=painted.rgb; i.color.a=painted.a;
                }
                else if (_Part < 1.5)
                {
                    if (_PaintedPectoral > .5 && _Style < .5)
                    {
                        fixed4 painted=painting(i.skin.xy);
                        rgb=lerp(rgb,painted.rgb,painted.a*.85);
                    }
                    float rays=i.fin.x>3.5?14:12;
                    float phase=i.surface.y*UNITY_PI*2*rays+i.surface.x*.25;
                    float ray=pow(saturate(.5+.5*cos(phase)),10), groove=pow(saturate(.5-.5*cos(phase)),12);
                    float strength=(_Pattern==8?0:1)*smoothstep(.12,.4,i.surface.x);
                    rgb*=(.96+ray*(.09+.10*_Painterly)*strength)*(1-groove*.12*_Painterly*strength);
                    // Generated pectorals share the flank's colors; a pale membrane lets the fan
                    // read against the body instead of as an inked loop. Shark fins stay solid.
                    float fan=_Painterly*_FinRoot.w*(_Pattern==8?0:1);
                    rgb=lerp(rgb,rgb*1.20+.07,smoothstep(.15,.9,i.surface.x)*(1-ray*.6)*.65*fan);
                    rgb*=lerp(.83,1,smoothstep(0,.18,i.surface.x));
                    rgb=lerp(rgb,rgb*.88,smoothstep(.95,1,i.surface.x)*.4);
                    // Ink along the free rim, as wide as the body hull but never more than a
                    // quarter of a small fin, which would otherwise turn into a dark blot.
                    float edge=max(fwidth(i.surface.x),1e-5), width=min(i.fin.y*(1-.4*fan),.25/edge);
                    ink=smoothstep(1-edge*(width+.6),1-edge*max(width-.6,.2),i.surface.x)*step(.5,i.surface.x);
                }
                else if (_Part < 2.5)
                {
                    if (_Expression == 1 && dot(rgb,1) > 1.5) rgb = _Base.rgb;
                    // An inked lid slanting down to the snout reads as a frown, like the painted brows.
                    // Eye surface coordinates hold the globe's own center.
                    if (_Expression == 2 && i.local.y > i.surface.y + .012 - (i.local.x-i.surface.x)*.60) rgb = lerp(_Dark.rgb,OutlineColor(),.65);
                    if (_Expression == 3 && i.local.y < i.surface.y - .015) rgb = _Base.rgb;
                }
                else
                {
                    // A warm cavity, as in the painted bite: dark throat, rose palate and a pale tongue.
                    float radius=length(i.surface), lip=smoothstep(.82,.99,radius);
                    fixed3 palate=_Pattern==8?fixed3(.66,.20,.24):fixed3(.82,.25,.31);
                    rgb=lerp(fixed3(.19,.035,.06),palate,smoothstep(.10,.78,radius));
                    rgb=lerp(rgb,lerp(_Base.rgb,_Dark.rgb,.4),lip);
                    float tongue=(1-smoothstep(.75,1,length((i.surface-float2(0,-.42))/float2(.60,.36))))*smoothstep(.10,.40,_Mouth)*(1-lip);
                    rgb=lerp(rgb,_Pattern==8?fixed3(.82,.47,.47):fixed3(.96,.50,.54),tongue);
                    if (dot(i.color.rgb,1)>2.8) rgb=fixed3(.94,.95,.84);
                    else rgb=lerp(lerp(_Base.rgb,_Dark.rgb,.52),rgb,smoothstep(.04,.30,_Mouth));
                }
                float diffuse=saturate(dot(normal,normalize(float3(-.3,.65,-.7))));
                float ndv=saturate(dot(normal,view));
                // Two soft tone steps, closer to the cel shading of the painted art than a smooth ramp.
                float toon=lerp(diffuse,smoothstep(.16,.40,diffuse)*.6+smoothstep(.60,.84,diffuse)*.4,.55);
                float lighting=_Painterly>.5?.70+toon*.38:lerp(.82,.62,_Style)+toon*lerp(.24,.48,_Style);
                lighting+=(1-diffuse)*saturate(-normal.y)*.08*_Style;
                float gloss=saturate(dot(normal,normalize(view+normalize(float3(-.3,.65,-.7)))));
                float specular=_Painterly>.5?pow(gloss,40)*.10:pow(gloss,48)*lerp(.035,.14,_Style)+pow(gloss,10)*.025*_Style;
                if (_Part<.5 && _Style>.5)
                {
                    // Tiny staggered scale arcs wrap around the body rather than lying on a flat side.
                    float row=floor(i.surface.y*44), x=frac(i.surface.x*20+row*.5);
                    float scale=1-smoothstep(.018,.065,abs(length(float2((x-.5)*1.15,frac(i.surface.y*44)-.58))-.43));
                    rgb+=scale*(_Pattern==8?0:.018)*(.4+.6*diffuse);
                    rgb=lerp(rgb,rgb*fixed3(.86,1.035,1.06),pow(1-ndv,3)*.30);
                }
                rgb=PaintedLighting(rgb, i.local.y / max(_Height*2,.1)+.5)*lighting;
                if (_Part<1.5) rgb=lerp(dot(rgb,fixed3(.2126,.7152,.0722)).xxx,rgb,_Painterly>.5?1.16:1.10);
                if (_Part>.5 && _Part<1.5 && (_PaintedFins<.5 || _Style>.5)) rgb+=i.color.rgb*(1-diffuse)*.10;
                rgb+=specular*fixed3(.65,.85,.9);
                if (_Style>.5 && _Part<1.5)
                {
                    float3 reflection=reflect(-view,normal);
                    fixed3 environment=lerp(_GroundReflection.rgb,_WaterReflection.rgb,smoothstep(-.3,.5,reflection.y));
                    float fresnel=.15+.85*pow(1-ndv,4);
                    rgb+=environment*_ReflectionStrength*fresnel;
                }
                rgb*=_Painterly>.5?lerp(.76,1,smoothstep(.02,.45,ndv)):lerp(.88,1,smoothstep(.02,.30,ndv));
                // Water light catches the upper contour and separates the back from dark scenery.
                if (_Part<.5) rgb+=_WaterReflection.rgb*pow(1-ndv,3)*saturate(normal.y*.8+.35)*.24;
                rgb=lerp(rgb,OutlineColor(),ink*.9);
                float visibility=_Visibility;
                if (_Part>.5 && _Part<1.5) visibility*=lerp(i.color.a,1,max(ink*.9,_Painterly*.55));
                if (_Part > 2.5 && dot(i.color.rgb,1)>2.8) visibility *= smoothstep(.25,.6,_Mouth);
                return fixed4(lerp(rgb,_FogColor.rgb,_Fog),visibility);
            }
            ENDCG
        }
    }
}