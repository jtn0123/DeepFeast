// Shared soft upper light and restrained saturation for fish, vegetation and painted scenery.
fixed4 _SceneLight;
fixed3 PaintedLighting(fixed3 rgb, float height)
{
    fixed luminance = dot(rgb, fixed3(0.2126, 0.7152, 0.0722));
    rgb = lerp(luminance.xxx, rgb, 0.95);
    float volume = lerp(0.91, 1.035, smoothstep(0.05, 0.95, saturate(height)));
    return rgb * volume * lerp(fixed3(1, 1, 1), _SceneLight.rgb, 0.45);
}
