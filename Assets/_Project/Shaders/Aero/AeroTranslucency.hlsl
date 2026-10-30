#ifndef AERO_TRANSLUCENCY_INCLUDED
#define AERO_TRANSLUCENCY_INCLUDED
// Thin-surface back transmission (leaves, acrylic, lampshades): light behind the
// surface shines through toward the viewer. Thickness 0 = paper thin, 1 = solid.

half AeroTransmission(half3 normalWS, half3 view, half3 lightDir, half distortion, half power, half thickness)
{
    half3 back = -(lightDir + normalWS * distortion);
    half forward = pow(saturate(dot(view, normalize(back))), max(power, 1));
    // Also accept diffuse light through the far side of thin geometry.
    half diffuseThrough = saturate(-dot(normalWS, lightDir)) * .25;
    return (forward + diffuseThrough) * (1 - saturate(thickness));
}

half3 AeroTranslucentLight(half3 normalWS, half3 view, half3 lightDir, half3 lightColor, half attenuation,
    half3 transmissionColor, half distortion, half power, half thickness, half scale)
{
    return transmissionColor * lightColor * attenuation * scale *
        AeroTransmission(normalWS, view, lightDir, distortion, power, thickness);
}
#endif
