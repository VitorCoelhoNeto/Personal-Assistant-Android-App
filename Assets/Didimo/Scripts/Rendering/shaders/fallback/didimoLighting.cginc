
// Upgrade NOTE: excluded shader from DX11, OpenGL ES 2.0 because it uses unsized arrays
#pragma exclude_renderers d3d11 gles
#define UNITY_SAMPLE_FULL_SH_PER_PIXEL 1

#include "UnityCG.cginc"
#include "UnityStandardUtils.cginc"

/// exposed uniforms
half directDiffInt;
half indirectDiffInt;

half directSpecInt;
half indirectSpecInt;

half selfShadowDiff;
half selfShadowSpec;
half shadowSpread;
float shadowBias;

// /// for testing
// samplerCUBE irradCube; 
// samplerCUBE radCube; 

float remapTo01(float v, float fMin, float fMax)
{
  return clamp((v - fMin) / (fMax - fMin), 0, 1);
}

///
/// indirect
///
half3 evalIndDiffuse(half3 wN, float3 wP)
{
  wN *= half3(-1, 1, 1); /// flip it to match Maya
  half3 indirectDiff = ShadeSHPerPixel(wN, 1, wP);

  return indirectDiff.rgb * indirectDiffInt; 
}

half3 evalIndSpec(half3 wN, half3 wV, half roughness)
{
  half3 reflDir = normalize(-reflect(wV, wN));
  reflDir *= half3(-1, 1, 1); /// flip it to match Maya
  half dotDown = 1 - max(0, dot(wN, half3(0, -1, 0)));

  half4 radSample = UNITY_SAMPLE_TEXCUBE_LOD(unity_SpecCube0, reflDir, lerp(0, 9, roughness));
  half3 indirectSpec = DecodeHDR(radSample, unity_SpecCube0_HDR);

  return indirectSpec * indirectSpecInt * dotDown;
}


/// common brdfs
half evalLambert(half3 tlD, half3 tN)
{
  return max(0, dot(tlD, tN));
}

half evalPhong(half3 tlD, half3 tN, half3 tV, fixed roughness)
{
  half3 reflVec = normalize(reflect(-tlD, tN));
  float dotRV = max(0, dot(reflVec, tV));
  return pow(dotRV, (1 - roughness) * 100);
}

half evalBlinn(half3 tlD, half3 tN, half3 tV, fixed roughness)
{
  half3 halfVec = normalize(tlD + tV);
  float dotNH = max(0, dot(tN, halfVec));
  half specTerm = pow(dotNH, (1 - roughness) * 100);
  return specTerm;
}

half evalAniso(half3 tlD, half3 tN, half3 tV, fixed roughness, fixed angle, fixed minTerm)
{
  fixed sX = angle;
  fixed sY = 1.0 - angle;

  half3 halfVec = normalize(tlD + tV);

  half3 upVec = half3(0, 1, 0);

  half3 B  = normalize(cross(upVec, tN));
  half3 N  = normalize(tN) * sX ;
  half3 T  = cross(N, B) / sY; 

  half dotTH = dot(T, halfVec);
  half dotBH = dot(B, halfVec);
  half dotNH = max(0, dot(N, halfVec));

  half specTerm = dotNH * dotNH / (dotTH * dotTH + dotBH * dotBH);
  
  roughness = max(0.0001, 1 - roughness);

  specTerm = pow(specTerm, roughness);

  specTerm = max((specTerm - minTerm) * dot(tlD, tN), 0);

  return specTerm; 
}


float evalFresnel(float amount, float spread, half3 tN, half3 tV)
{
  // half _frenSpread = lerp(10.0, 1.0, spread);
  // return lerp(1.0, min(1.0, pow(1.0 - abs(dot(tN, tV)), _frenSpread)), amount);
  return lerp(1.0, min(1.0, pow(1.0 - abs(dot(tN, tV)), spread)), amount);
}

