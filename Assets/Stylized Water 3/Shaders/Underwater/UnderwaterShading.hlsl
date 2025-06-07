//Stylized Water 2
//Staggart Creations (http://staggart.xyz)
//Copyright protected under Unity Asset Store EULA

#ifndef UNDERWATER_SHADING_INCLUDED
#define UNDERWATER_SHADING_INCLUDED

#include "UnderwaterFog.hlsl"
#include "UnderwaterLighting.hlsl"

#define REFLECTION_ROUGHNESS 0.0

#define WATER_RI 1.333
#define AIR_RI 1.000293 
#define SCHLICK_EXPONENT 5.0
#define REFLECTION_ALPHA 1

//Fresnel factor between two refractive media. https://en.wikipedia.org/wiki/Schlick%27s_approximation
float FresnelReflection(float angle)
{
	//R = (n1 - n2) / (n1 + n2)^2
	//R(ϴ) = R + (1 - R)(1 - cosϴ)^5
	
	float r = (AIR_RI - WATER_RI) / (AIR_RI + WATER_RI);
	r = r * r;
	
	return saturate(r + (AIR_RI - r) * pow(max(0.0, AIR_RI - angle), SCHLICK_EXPONENT));
}

//Schlick's BRDF fresnel
float ReflectionViewFresnel(float3 worldNormal, float3 viewDir, float exponent)
{
	float cosTheta = saturate(dot(worldNormal, viewDir));
	return pow(max(0.0, 1.0 - cosTheta), exponent);
}

float3 UnderwaterReflectionVector(float3 normalWS, float3 worldTangentNormal, float3 viewDir, half smoothness)
{
	//Blend between the vertex/wave normal and tangent normal map
	float3 normal = lerp(worldTangentNormal, normalWS, smoothness);

	float3 reflectionVector = reflect(-viewDir, normal);

	//Mirror on the horizontal plane. Since normals are never actually oriented downwards
	//reflectionVector.y = -reflectionVector.y;

	return reflectionVector;
}

//Snell's Law
float UnderwaterReflectionFactor(float3 normalWS, float3 worldTangentNormal, float3 viewDir, half smoothness, half offset)
{
	float3 normal = lerp(worldTangentNormal, normalWS, smoothness);

	//Incident angle
	const float viewAngle = max(0.0, dot(normal, -viewDir));

	//If given a spherical normal, behaves as a lensing effect.
	const float refractionAngle = WATER_RI * sin(acos(viewAngle + offset)) / AIR_RI;
	const float reflectionAngle = acos(clamp(refractionAngle, -1.0, 1.0)) ;
	
	bool isTotalInternalReflection = viewAngle > reflectionAngle;
	
	const float reflectionFresnel = 1.0 - FresnelReflection(reflectionAngle) * REFLECTION_ALPHA;
	const float viewFresnel = ReflectionViewFresnel(normalWS, viewDir, SCHLICK_EXPONENT);

	return (reflectionFresnel * viewFresnel);
	return isTotalInternalReflection ? 1.0 : (reflectionFresnel * viewFresnel);
}

float3 SampleUnderwaterReflectionProbe(float3 reflectionVector, float smoothness, float3 positionWS, float2 screenPos)
{
	#if !defined(SHADERGRAPH_PREVIEW)

	#if UNITY_VERSION >= 202220
	float3 reflections = saturate(GlossyEnvironmentReflection(reflectionVector, positionWS, smoothness, 1.0, screenPos.xy)).rgb;
	#elif UNITY_VERSION >= 202120
	float3 reflections = saturate(GlossyEnvironmentReflection(reflectionVector, positionWS, smoothness, 1.0)).rgb;
	#else
	float3 reflections = saturate(GlossyEnvironmentReflection(reflectionVector, smoothness, 1.0)).rgb;
	#endif

	return reflections;
	#else
	return 0;
	#endif
}

void MixUnderwaterReflections(inout float3 color, in float3 sceneColor, float skyMask, float3 positionWS, float3 normalWS, float3 worldTangentNormal, float3 viewDir, float2 screenPos, int vFace, float density, half distortion, half refractionOffset)
{
	const float3 reflectionVector = UnderwaterReflectionVector(normalWS, worldTangentNormal, viewDir, distortion);
	float reflectionCoefficient = UnderwaterReflectionFactor(normalWS, worldTangentNormal, viewDir, distortion, refractionOffset);

	//Fade out by fog density. Ensuring the reflections fade out as the water surface gets further away
	reflectionCoefficient *= density;

	//Just use the opaque texture, more reliable
	#ifndef _ENVIRONMENTREFLECTIONS_OFF
		//float3 incomingReflections = reflections;

		//Fallback to opaque texture for pixels in front of opaque geometry
		//sceneColor = lerp(sceneColor, incomingReflections, skyMask);
	#endif
	
	//Faux-reflection of underwater volume
	color = lerp(color, sceneColor.rgb, reflectionCoefficient * (1-vFace));
}

//Main function called at the end of ForwardPass.hlsl
float4 ShadeUnderwaterSurface(in float3 albedo, float3 emission, float3 specular, float3 reflections, float3 sceneColor, float skyMask, float shadowMask, float3 positionWS, float3 normalWS, float3 worldTangentNormal, float3 viewDir, float2 screenPos, float4 shallowColor, float4 deepColor, int vFace, half reflectionSmoothness, half refractionOffset)
{
	float3 color = albedo.rgb;

	#ifndef SHADERGRAPH_PREVIEW
	const float distanceDensity = ComputeDistanceXYZ(positionWS);
	const float heightDensity = ComputeUnderwaterFogHeight(positionWS);
	float density = ComputeDensity(distanceDensity, heightDensity);
	
	//Not using distanceDensity here, so only the deep color is returned, which better represents the volume's color
	float4 volumeColor = GetUnderwaterFogColor(shallowColor, deepColor, 1.0, heightDensity);
	
	//Fade out into fog
	shadowMask = lerp(shadowMask, 1.0, density);
	
	color = lerp(color, volumeColor.rgb, density);
	
	//Apply direct- and indirect lighting to the albedo water+fog color
	ApplyUnderwaterLighting(color, shadowMask, normalWS, viewDir);

	//Re-apply translucency
	color.rgb += emission.rgb * (1-heightDensity);
	//Specular reflection (unknown why point lights don't carry over)
	color.rgb += specular.rgb * (1-density);

	//const float3 reflectionVector = UnderwaterReflectionVector(normalWS, worldTangentNormal, viewDir, reflectionSmoothness);
	float reflectionCoefficient = UnderwaterReflectionFactor(normalWS, worldTangentNormal, viewDir, reflectionSmoothness, refractionOffset);
	reflectionCoefficient = step(0.5, reflectionCoefficient);
	//return reflectionCoefficient;

	//Appears more natural if the reflections fade out into the fog
	reflections.rgb *= (1-density);
	
	color.rgb += reflections.rgb;

	
	//return sceneColor.rgb;
	#if _REFRACTION
	//As the camera does deep, light from above the water fades
	sceneColor.rgb *= 1-heightDensity;

	//Snell's window
	color = lerp(color, sceneColor.rgb, reflectionCoefficient);
	#endif
	
	//MixUnderwaterReflections(color.rgb, sceneColor.rgb, skyMask, positionWS, normalWS, worldTangentNormal, viewDir, screenPos, vFace, 1, reflectionSmoothness, refractionOffset);
	#endif

	float alpha = saturate(1-(reflectionCoefficient * 2.0));

	//alpha = heightDensity;
	
	return float4(color, alpha);
}
#endif