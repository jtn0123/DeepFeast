// Glow, colour grading and water ripples over the finished scene. Bloom follows the usual
// chain: a soft-threshold prefilter at half size, box downsamples to a small mip, then box
// upsamples back up, each added to the level above (into a fresh target rather than blending,
// so no pass relies on a target's earlier contents); the composite adds it to the scene,
// ripples the lookup and grades.
Shader "DeepFeast/PostFx"
{
    Properties { _MainTex ("Texture", 2D) = "black" {} }

    CGINCLUDE
    #include "UnityCG.cginc"
    sampler2D _MainTex, _BloomTex, _BaseTex;
    float4 _MainTex_TexelSize, _BloomTex_TexelSize;
    // x threshold, y threshold - knee, z 2 * knee, w 0.25 / knee
    float4 _Threshold;
    float _Intensity, _Aspect;
    float4 _BloomTint, _Gain, _Lift;
    // x saturation, y contrast
    float4 _Grade;
    // _BloomTint: rgb tint, w how much of the glow's own colour it keeps
    // xy centre (uv), z ring radius (screen heights), w strength
    float4 _Ripples[6];

    half3 Box(sampler2D tex, float4 texel, float2 uv, float delta)
    {
        float4 o = texel.xyxy * float2(-delta, delta).xxyy;
        half3 s = tex2D(tex, uv + o.xy).rgb + tex2D(tex, uv + o.zy).rgb + tex2D(tex, uv + o.xw).rgb + tex2D(tex, uv + o.zw).rgb;
        return s * 0.25h;
    }

    half3 Prefilter(half3 c)
    {
        half br = max(c.r, max(c.g, c.b));
        half soft = clamp(br - _Threshold.y, 0, _Threshold.z);
        soft = soft * soft * _Threshold.w;
        half contrib = max(soft, br - _Threshold.x) / max(br, 0.0001h);
        return c * contrib;
    }

    half4 FragPrefilter(v2f_img i) : SV_Target { return half4(Prefilter(Box(_MainTex, _MainTex_TexelSize, i.uv, 1)), 1); }
    half4 FragDown(v2f_img i) : SV_Target { return half4(Box(_MainTex, _MainTex_TexelSize, i.uv, 1), 1); }
    half4 FragUp(v2f_img i) : SV_Target { return half4(tex2D(_BaseTex, i.uv).rgb + Box(_MainTex, _MainTex_TexelSize, i.uv, 0.5), 1); }

    half4 FragComposite(v2f_img i) : SV_Target
    {
        // Each ripple is a travelling ring that bends the lookup outward and then inward, with a
        // faint bright crest where the water lenses light.
        float2 uv = i.uv;
        float crest = 0;
        for (int k = 0; k < 6; k++)
        {
            float4 r = _Ripples[k];
            float2 d = (i.uv - r.xy) * float2(_Aspect, 1);
            float dist = length(d);
            float x = (dist - r.z) / 0.02;
            float e = exp(-x * x) * r.w;
            uv += d / max(dist, 0.0001) * x * e * 0.03 * float2(1 / _Aspect, 1);
            crest += exp(-(x + 0.6) * (x + 0.6) * 2) * r.w;
        }
        const half3 lumaWeights = half3(0.2126h, 0.7152h, 0.0722h);
        half3 c = tex2D(_MainTex, uv).rgb + crest * 0.08h;
        // Scattered light loses some of its colour, so glows read as light rather than paint.
        half3 bloom = Box(_BloomTex, _BloomTex_TexelSize, uv, 0.5);
        bloom = lerp(dot(bloom, lumaWeights).xxx, bloom, _BloomTint.w);
        c += bloom * _Intensity * _BloomTint.rgb;
        c = c * _Gain.rgb + _Lift.rgb;
        half luma = dot(c, lumaWeights);
        // Saturation eases off in the highlights so bright water stays water, not neon.
        c = lerp(luma.xxx, c, lerp(_Grade.x, 1, smoothstep(0.55h, 0.95h, luma)));
        c = (c - 0.5h) * _Grade.y + 0.5h;
        return half4(max(c, 0), 1);
    }
    ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragPrefilter
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragDown
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragUp
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragComposite
            ENDCG }
    }
}
