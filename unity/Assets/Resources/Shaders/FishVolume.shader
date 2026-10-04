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
        float _PaintedFins, _PaintedPectoral, _Painterly, _Glow, _Roll, _Snout;
        // x: where the painted-outline fit ends; y: 1 when fitted. See FishVolume.FitToPainting.
        float4 _Fit;
        float _FitCenter[32], _FitScale[32];
        float4 _PectoralMask;
        fixed4 _GlowColor;

        // Painted bodies: the centre-line shift and half-height scale that follow the painting's outline.
        float2 bodyFit(float x)
        {
            float t=saturate((x+1.04)/(_Fit.x+1.04))*31;
            int i=min((int)t,30);
            float2 fit=lerp(float2(_FitCenter[i],_FitScale[i]),float2(_FitCenter[i+1],_FitScale[i+1]),t-i);
            return _Fit.y>.5 && x<_Fit.x ? fit : float2(0,1);
        }
        float bodyProfile(float x)
        {
            float profile=x>=_Head.x ? pow(max(.000016,1-pow((x-_Head.x)/_Head.y,2)),_Profile.y)
                : lerp(_Profile.z,1,pow(max(0,sin(saturate((x+1.04)/(_Head.x+1.04))*UNITY_PI*.5)),_Profile.x));
            return profile*bodyFit(x).y;
        }
        // The body's centre line: a slight rise to the shoulder, then a shark's raised snout.
        float centerY(float x)
        {
            float snout=saturate((x-_Head.x)/_Head.y);
            return _Profile.w*saturate((x+1.04)/(_Head.x+1.04))+_Snout*_Height*snout*snout+bodyFit(x).x;
        }
        float mouthCenter(float across)
        {
            return _MouthShape.x-_Mouth*.08+((_Pattern==8?.055:.13)+(_Expression==3&&_Pattern!=8?.04:0))*across*across;
        }
        float mouthRadius() { return lerp(.018,_MouthShape.y,_Mouth); }

        // Rigid appendages flag their bone: x follows the lower jaw, y stays with the skull.
        void jaw(inout float3 p, inout float3 normal, float2 rigid)
        {
            bool shark=_Pattern==8;
            float center=centerY(p.x);
            float weight=smoothstep(shark?.24:.45,shark?.80:.95,p.x)*(1-smoothstep(-_Height*.45+center,-_Height*.15+center,p.y));
            if (_Part<.5 && rigid.x>.5) weight=1;
            if (_Part<.5 && rigid.y>.5) weight=0;
            float angle=-_Mouth*_MouthShape.w*weight, s=sin(angle), c=cos(angle);
            float2 hinge=float2(shark?.40:.58,0);
            hinge.y=-_Height*.18+centerY(hinge.x);
            float2 offset=p.xy-hinge;
            p.xy=float2(c*offset.x-s*offset.y,s*offset.x+c*offset.y)+hinge;
            normal.xy=float2(c*normal.x-s*normal.y,s*normal.x+c*normal.y);
        }
        // Every pass deforms the mesh identically, so the ink line stays fixed to the swimming body.
        void Animate(inout appdata v)
        {
            if (_Part > 1.5 && _Part < 2.5 && _Expression == 1) v.vertex.y = v.surface.y + (v.vertex.y-v.surface.y)*.08;
            if (_Pattern==31 && _Part<.5)
            {
                // The manta's wings beat: outboard of the body they rise and fall, the tips lagging.
                float reach=max(0,abs(v.vertex.z)-.35), beat=_Phase-reach*1.6;
                v.vertex.y+=sin(beat)*reach*reach*.32;
                v.normal.z-=v.normal.y*(2*reach*sin(beat)+1.6*reach*reach*cos(beat))*.32*sign(v.vertex.z);
            }
            if (_Part > 2.5)
            {
                float z=v.surface.x*_Head.w*_MouthShape.z;
                float y=_Head.z*(mouthCenter(v.surface.x)+mouthRadius()*v.surface.y);
                float radial=pow(y/_Head.z,2)+pow(z/_Head.w,2);
                float front=sqrt(max(.02,1-pow(radial,1/(2*_Profile.y))));
                float inset=_Mouth*.15*(1-smoothstep(.78,1,length(v.surface)));
                float x=_Head.x+_Head.y*front+.008-inset;
                v.vertex.xyz=float3(x,y+centerY(x),z);
                float slope=2*_Profile.y*front/_Head.y*pow(max(.02,1-front*front),2*_Profile.y-1);
                v.normal=normalize(float3(slope,y/(_Head.z*_Head.z),z/(_Head.w*_Head.w)));
            }
            if (_Part < .5 || _Part > 2.5) jaw(v.vertex.xyz,v.normal,v.flex);
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
            if (_Roll!=0)
            {
                // Bank about the body axis (the manta shows its back to the side camera).
                float s=sin(_Roll),c=cos(_Roll);
                v.vertex.yz=float2(c*v.vertex.y-s*v.vertex.z,s*v.vertex.y+c*v.vertex.z);
                v.normal.yz=float2(c*v.normal.y-s*v.normal.z,s*v.normal.y+c*v.normal.z);
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
                // Fangs and the anglerfish's lure are too fine for the hull; it would ink them solid.
                if (_Part > 1.5 || (_Part > .5 && (_Pattern != 8 || v.flex.y < 3.5)) || (_Part < .5 && v.color.a < .45)) { o.position = float4(2,2,2,1); return o; }
                // A painted tail carries its own contour; the body's flat rear end runs into it unlined.
                float ink = _Part < .5 && _PaintedFins > .5 ? smoothstep(-1.035,-.98,v.vertex.x) : 1;
                Animate(v);
                float4 clip = UnityObjectToClipPos(v.vertex);
                float2 normal = mul((float3x3)UNITY_MATRIX_VP, UnityObjectToWorldNormal(normalize(v.normal))).xy;
                float length2 = dot(normal,normal);
                normal = length2 > 1e-8 ? normal*rsqrt(length2) : 0;
                clip.xy += normal*InkPixels(clip)*ink*2/_ScreenParams.xy*clip.w;
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
            // Paints over a baked feature with the surrounding illustration: a ring of samples just
            // outside the ellipse, blended by inverse squared distance and ignoring transparent or
            // inked pixels, so the fill shades smoothly instead of streaking along rows and columns.
            fixed3 inpaint(float2 skin, float4 ellipse, fixed3 fallback)
            {
                float2 q=(skin-ellipse.xy)/ellipse.zw;
                fixed3 sum=fallback*.02; float total=.02;
                [unroll] for (int j=0;j<12;j++)
                {
                    float angle=j*UNITY_PI/6;
                    float2 ring=float2(cos(angle),sin(angle))*1.2, offset=q-ring;
                    fixed4 c=painting(ellipse.xy+ring*ellipse.zw);
                    float w=c.a*smoothstep(.10,.24,dot(c.rgb,fixed3(.2126,.7152,.0722)))/max(.01,dot(offset,offset));
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
                    // Shark markings follow the centre line, so the white runs up under the raised snout.
                    y=(p.y-centerY(p.x))/max(_Height,.1);
                    float belly=1-smoothstep(-.27,-.10,y+.035*sin(p.x*8));
                    skin=lerp(lerp(_Base.rgb,_Dark.rgb,smoothstep(.18,.90,y)*.42),_Belly.rgb,belly*.96);
                    if (_SharkKind==4)
                    {
                        // Whale shark: pale spots between thin pale stripes, a checkerboard over the back.
                        float upper=smoothstep(-.32,-.14,y);
                        float spot=1-smoothstep(.15,.24,length(frac(float2(p.x*14+sin(y*5)*.3,y*9))-.5));
                        float columns=smoothstep(.45,.475,abs(frac(p.x*3.5+y*.2)-.5)), rows=smoothstep(.44,.47,abs(frac(y*3+.5)-.5));
                        float stripes=max(columns,rows)*(1-smoothstep(.30,.45,p.x))*smoothstep(-.95,-.75,p.x);
                        skin=lerp(skin,_Belly.rgb*.92,max(spot*.85,stripes*.7)*upper);
                    }
                    // Great hammerhead: a bronze cast over the grey back.
                    if (_SharkKind==5) skin=lerp(skin,skin*fixed3(1.10,1.0,.84),smoothstep(-.25,.35,y)*.7);
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
                if (_Pattern==12)
                {
                    float2 uv=p.xy*float2(10,15);
                    float2 cell=floor(uv); float seed=frac(sin(dot(cell,float2(127.1,311.7)))*43758.5453);
                    float seed2=frac(sin(dot(cell,float2(269.5,183.3)))*43758.5453);
                    float2 offset=float2(.20+seed*.60,.20+seed2*.60);
                    float distance=length((frac(uv)-offset)*float2(1,lerp(.75,1.5,seed2)));
                    float size=lerp(.08,.19,seed);
                    float spot=(1-smoothstep(size*.50,size,distance))*step(.27,seed2);
                    float mottling=.5+.5*sin(p.x*18+sin(y*13))*sin(y*21+p.x*4);
                    skin=lerp(skin,_Dark.rgb,spot*.58+mottling*.18);
                    skin=lerp(skin,_Dark.rgb,smoothstep(.79,.96,sin(p.x*15+y*.8))*.15);
                }
                if (_Pattern==13)
                {
                    // Halibut: the eyed side is one olive brown from edge to edge, with no pale belly,
                    // clouded by soft darker and paler blotches.
                    skin=lerp(_Base.rgb,_Dark.rgb,.10+.14*smoothstep(-.8,.8,y));
                    float2 uv=p.xy*float2(5.5,7)+sin(p.yx*float2(11,9)+float2(.8,2.1))*.35, cell=floor(uv);
                    float seed=frac(sin(dot(cell,float2(127.1,311.7)))*43758.5453), seed2=frac(sin(dot(cell,float2(269.5,183.3)))*43758.5453);
                    float blotch=1-smoothstep(.05,.42,length(frac(uv)-float2(.3+seed*.4,.3+seed2*.4)));
                    skin=lerp(skin,seed>.55?_Dark.rgb:_Base.rgb*1.16+.03,blotch*.26);
                    // The lateral line arches high over the pectoral fin.
                    float lateral=.02+.24*exp(-pow((p.x-.46)*3.0,2));
                    skin=lerp(skin,_Dark.rgb*.8,exp(-pow((y-lateral)*70,2))*.38*(1-smoothstep(.60,.68,p.x)));
                    // The big mouth's cleft runs from the snout tip back to below the lower eye.
                    float t=(_Head.x+_Head.y-.02-p.x)/.36;
                    float cleft=-.06-.13*t+.05*t*t;
                    skin=lerp(skin,_Dark.rgb*.45,exp(-pow((y-cleft)*34,2))*smoothstep(-.05,.05,t)*(1-smoothstep(.90,1.04,t))*.85);
                    skin=lerp(skin,_Base.rgb*1.15+.04,exp(-pow((y-cleft+.07)*24,2))*smoothstep(0,.1,t)*(1-smoothstep(.80,1.0,t))*.30);
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
                if (_Pattern==16)
                {
                    float stripe=0;
                    for(int j=0;j<5;j++) stripe=max(stripe,1-smoothstep(.022,.036,abs(y+.14+j*.095-.012*sin(p.x*3))));
                    skin=lerp(skin,_Dark.rgb*.55,stripe*.85*(1-smoothstep(.60,.95,p.x)));
                }
                if (_Pattern==17)
                {
                    // Striped bass: seven bold, dark stripes run from the gill cover to the tail over
                    // silver sides, closing up as the body tapers; the white belly below stays clear.
                    float center=centerY(p.x), relative=(p.y-center)/max(_Height*lerp(1,bodyProfile(p.x),.6),.001);
                    float stripe=0;
                    [unroll] for(int j=0;j<7;j++) stripe=max(stripe,1-smoothstep(.038,.058,abs(relative-(.58-j*.15)-.012*sin(p.x*3+j))));
                    float radius=_Height*.92, hinge=_Eye.x+radius*.35, rim=length(p.xy-float2(hinge,center-_Height*.10))-radius;
                    stripe*=smoothstep(.030,.042,rim)*step(p.x,hinge)*smoothstep(-1.0,-.80,p.x);
                    skin=lerp(skin,_Dark.rgb*.50,stripe*.90);
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
                if (_Pattern==21)
                {
                    // Sardine: a blue-green back over silver, with a row of dark spots along the boundary.
                    float boundary=.14+.03*sin(p.x*5);
                    skin=lerp(skin,_Dark.rgb*.9,smoothstep(boundary-.06,boundary+.08,y)*.9);
                    float spots=(1-smoothstep(.16,.24,length(float2(frac(p.x*7.5)-.5,(y-.06)*2))))*smoothstep(-.55,-.35,p.x)*(1-smoothstep(.55,.70,p.x));
                    skin=lerp(skin,_Dark.rgb*.45,spots*.85);
                }
                // Garibaldi: one vivid orange from back to belly, without the usual dark back.
                if (_Pattern==22) skin=lerp(_Base.rgb,_Belly.rgb,smoothstep(.2,-.8,y)*.35)+_Base.rgb*exp(-pow((y-.18)*3,2))*.07;
                if (_Pattern==23)
                {
                    // Male sheephead: black head and tail around a red middle, and a white chin.
                    float head=smoothstep(.50,.60,p.x+y*.10-y*y*.12), tail=1-smoothstep(-.60,-.50,p.x-y*.05+y*y*.06);
                    skin=lerp(_Base.rgb,_Belly.rgb,smoothstep(-.3,-.9,y)*.25);
                    skin=lerp(skin,_Dark.rgb,max(head,tail)*.92);
                    skin=lerp(skin,_Belly.rgb,(1-smoothstep(.8,1.05,length(float2((p.x-1.35)/.40,(y+.50)/.45))))*.95);
                }
                if (_Pattern==26)
                {
                    // Lingcod: dark olive blotches and copper flecks above a pale belly.
                    // Warped cells of varied size break the blotches out of a regular grid.
                    float2 uv=p.xy*float2(6,9)+sin(p.yx*float2(23,17)+float2(1.7,.4))*.38, cell=floor(uv);
                    float seed=frac(sin(dot(cell,float2(127.1,311.7)))*43758.5453), seed2=frac(sin(dot(cell,float2(269.5,183.3)))*43758.5453);
                    float size=lerp(.26,.46,seed2);
                    float blotch=(1-smoothstep(size*.55,size,length((frac(uv)-float2(.25+seed*.5,.25+seed2*.5))*float2(1,lerp(.7,1.4,seed)))))*step(.22,seed);
                    skin=lerp(skin,_Dark.rgb,blotch*.62*smoothstep(-.7,-.2,y));
                    float2 fine=p.xy*float2(16,22), fineCell=floor(fine);
                    float seed3=frac(sin(dot(fineCell,float2(127.1,311.7)))*43758.5453), seed4=frac(sin(dot(fineCell,float2(269.5,183.3)))*43758.5453);
                    float fleck=(1-smoothstep(.12,.22,length(frac(fine)-float2(.3+seed3*.4,.3+seed4*.4))))*step(.55,seed3);
                    skin=lerp(skin,fixed3(.80,.55,.30),fleck*.55*smoothstep(-.5,0,y));
                }
                if (_Pattern==24)
                {
                    // Lanternfish: a dark back over bronze-silver flanks.
                    skin=lerp(skin,_Dark.rgb,smoothstep(0,.45,y)*.85);
                    skin=lerp(skin,skin*fixed3(1.06,1,.9)+.04,exp(-pow((y+.15)*4,2))*.5);
                }
                if (_Pattern==25)
                {
                    // Hatchetfish: mirror-silver flanks under a narrow dark back.
                    float center=centerY(p.x), relative=(p.y-center)/max(_Height*bodyProfile(p.x),.001);
                    skin=lerp(_Belly.rgb,_Base.rgb,smoothstep(-.6,.3,relative)*.6);
                    skin=lerp(skin,_Dark.rgb,smoothstep(.55,.80,relative)*.9);
                    skin+=.05*smoothstep(.5,1,sin(p.x*26+relative*2));
                }
                if (_Pattern==27)
                {
                    // Humpback anglerfish: velvety black-brown skin with faint pale blotches.
                    skin=lerp(_Base.rgb,_Dark.rgb,smoothstep(-.6,.6,y)*.6);
                    float2 uv=p.xy*7+sin(p.yx*float2(13,11))*.4, cell=floor(uv);
                    float seed=frac(sin(dot(cell,float2(127.1,311.7)))*43758.5453);
                    skin=lerp(skin,_Belly.rgb,(1-smoothstep(.15,.35,length(frac(uv)-.5)))*step(.45,seed)*.35);
                }
                if (_Pattern==28)
                {
                    // Viperfish: a near-black back above flanks with a metallic blue-green sheen.
                    skin=lerp(skin,_Dark.rgb,smoothstep(-.1,.6,y)*.8);
                    skin+=fixed3(.04,.12,.14)*exp(-pow((y+.1)*3,2))*(.6+.4*sin(p.x*18+y*4));
                }
                if (_Pattern==29)
                {
                    // Swordfish: a dark purple-brown back and a bronze sheen along the flank.
                    skin=lerp(skin,_Dark.rgb,smoothstep(0,.6,y)*.75);
                    skin=lerp(skin,fixed3(.62,.50,.40),exp(-pow((y-.02)*5,2))*.35);
                }
                if (_Pattern==30)
                {
                    // Ocean sunfish: silver-grey with soft pale mottling under a darker back.
                    float2 uv=p.xy*float2(5,6)+sin(p.yx*float2(9,7)+float2(.6,1.9))*.45, cell=floor(uv);
                    float seed=frac(sin(dot(cell,float2(127.1,311.7)))*43758.5453);
                    skin=lerp(skin,_Belly.rgb,(1-smoothstep(.18,.42,length(frac(uv)-.5)))*step(.35,seed)*.30);
                }
                if (_Pattern==31)
                {
                    // Giant manta: a black back with white shoulder patches, a white belly with dark
                    // spots and dusky wing margins. Rest coordinates: z runs across the wings.
                    float span=abs(p.z), behind=.22-.505*(span-.42)-p.x;
                    // Triangles behind the leading edge: broad beside the head, pointed towards the tips.
                    float reach=.44*(1-saturate((span-.36)/.62));
                    float shoulder=smoothstep(.03,.08,behind)*(1-smoothstep(reach-.06,reach,behind))*smoothstep(.28,.38,span);
                    fixed3 back=lerp(_Dark.rgb,_Belly.rgb*.9,shoulder*.92);
                    float2 uv=p.xz*6, cell=floor(uv);
                    float seed=frac(sin(dot(cell,float2(127.1,311.7)))*43758.5453);
                    float spot=(1-smoothstep(.10,.20,length(frac(uv)-.5)))*step(.72,seed)*(1-smoothstep(.45,.70,span));
                    fixed3 belly=lerp(_Belly.rgb,_Dark.rgb,max(spot*.85,smoothstep(.85,1.35,span)*.55));
                    skin=lerp(belly,back,smoothstep(-.12,.12,y));
                }
                // Small recessed gill crease follows the curved skin on both sides.
                float gill = exp(-pow((p.x-.48+y*.13)*38,2)) * (1-smoothstep(.30,.65,abs(y)));
                if (_Pattern == 8)
                {
                    gill=0;
                    [unroll] for (int j=0;j<5;j++) gill+=exp(-pow((p.x-(.60-j*.06)+y*y*.07)*105,2))*(1-smoothstep(.25,.44,abs(y+.02)));
                    skin*=1-gill*.30;
                    float nostril=exp(-pow((p.x-(_Head.x+_Head.y*.94))*38,2)-pow((y+.12)*30,2));
                    skin*=1-nostril*.45;
                }
                else if (_Painterly<.5) skin*=1-gill*.20;
                return skin;
            }
            // Photophores: rows of light organs along the belly, emissive in the dark.
            float photophores(float3 p)
            {
                float center=centerY(p.x), relative=(p.y-center)/max(_Height*bodyProfile(p.x),.001);
                float glow=0;
                if (_Pattern==24)
                {
                    glow=max(1-smoothstep(.10,.20,length(float2(frac(p.x*9)-.5,(relative+.50)*2.6))),
                             1-smoothstep(.10,.20,length(float2(frac(p.x*9+.5)-.5,(relative+.80)*2.6))));
                    glow*=smoothstep(-.95,-.75,p.x)*(1-smoothstep(.75,.95,p.x));
                }
                if (_Pattern==28)
                {
                    glow=max(1-smoothstep(.10,.20,length(float2(frac(p.x*12)-.5,(relative+.62)*3))),
                             1-smoothstep(.10,.20,length(float2(frac(p.x*12+.5)-.5,(relative+.88)*3))));
                    glow*=smoothstep(-.95,-.70,p.x)*(1-smoothstep(.85,1.0,p.x));
                }
                if (_Pattern==25)
                    glow=(1-smoothstep(.10,.22,length(float2(frac(p.x*11)-.5,(relative+.86)*3))))*smoothstep(-.9,-.6,p.x)*(1-smoothstep(.75,.9,p.x));
                return glow;
            }
            // Species without an illustration borrow its cues on the procedural skin: a lit band
            // along the flank, an inked gill cover with a raised rim, and soft rows of scales.
            fixed3 painterly(float3 p, fixed3 skin)
            {
                float center=centerY(p.x);
                float relative=(p.y-center)/max(_Height*bodyProfile(p.x),.001);
                float sheen=exp(-pow((relative-.45)*5,2))*smoothstep(-1.0,-.6,p.x);
                skin=lerp(skin,skin*1.15+.09,sheen*.6);
                // Sharks, the sunfish and the manta have no bony gill cover or rows of scales.
                if (_Pattern==8 || _Pattern==30 || _Pattern==31) return skin;
                float radius=_Height*.92;
                float2 hinge=float2(_Eye.x+radius*.35,center-_Height*.10);
                float rim=length(p.xy-hinge)-radius;
                float cover=smoothstep(hinge.x,hinge.x-.15,p.x)*(1-smoothstep(.70,.86,abs(relative)));
                skin*=1-exp(-pow(rim/.016,2))*.42*cover;
                skin=lerp(skin,skin*1.12+.05,exp(-pow((rim+.045)/.03,2))*.5*cover);
                skin*=1-exp(-pow((rim-.05)/.04,2))*.10*cover;
                if (_Pattern!=12 && _Pattern!=13 && _Pattern!=26)
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
                    float vertical=((i.skin.y-centerY(i.skin.x))/_Height-mouthCenter(across))/mouthRadius();
                    if (_Mouth>.08 && i.color.a>.9 && i.skin.x>_Head.x+_Head.y*.20 && across*across+vertical*vertical<.82*.82) discard;
                    // Keep projected skin inside the illustration's body. Its outer black contour and
                    // baked dorsal/ventral fins belong to the flat silhouette, not the rounded surface.
                    float profile=bodyProfile(i.skin.x);
                    float center=centerY(i.skin.x);
                    float2 skin=float2(i.skin.x,i.skin.y*.78);
                    if (_PaintedFins>.5)
                    {
                        // Matched bodies sample the painting where it is; only the outer band is
                        // squeezed so the constant-width painted contour never lands on the curved back.
                        // It keeps some slope on a thin snout, where one row would otherwise smear upward.
                        float halfHeight=max(_Height*profile,.001), relative=(i.skin.y-center)/halfHeight;
                        float limit=max(.40,1-.075/halfHeight), knee=min(.60,limit*.75), outer=abs(relative);
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
                    // Appendages (fangs, bill, lure) keep their own color.
                    if (i.color.a<.9) rgb=i.color.rgb;
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
                    // A pectoral the painting lacks is drawn in its manner, with dark rays like the painted fins.
                    float drawn=_PaintedFins*_FinRoot.w*(1-_PaintedPectoral)*(1-_Style);
                    rgb*=(.96+ray*(.09+.10*_Painterly)*strength*(1-drawn))*(1-groove*.12*_Painterly*strength)*(1-ray*.30*drawn*strength);
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
                if (_Glow>0 && _Part<.5)
                {
                    // Lures shine in their color with a white-hot center; photophores dot the skin.
                    if (i.color.a<.2) rgb=lerp(_GlowColor.rgb*1.1,1,pow(ndv,3)*.55);
                    else
                    {
                        float glow=photophores(i.skin)*_Glow;
                        rgb=lerp(rgb,_GlowColor.rgb*1.3,glow)+_GlowColor.rgb*glow*.35;
                    }
                }
                float visibility=_Visibility;
                if (_Part>.5 && _Part<1.5) visibility*=lerp(i.color.a,1,max(ink*.9,_Painterly*.55));
                if (_Part > 2.5 && dot(i.color.rgb,1)>2.8) visibility *= smoothstep(.25,.6,_Mouth);
                return fixed4(lerp(rgb,_FogColor.rgb,_Fog),visibility);
            }
            ENDCG
        }
    }
}