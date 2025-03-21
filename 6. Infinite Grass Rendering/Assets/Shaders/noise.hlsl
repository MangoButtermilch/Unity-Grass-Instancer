#ifndef NOISE
#define NOISE 

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Hashes.hlsl"

// generated by shadergraph
inline float Unity_SimpleNoise_RandomValue_float(float2 uv)
{
    return frac(sin(dot(uv, float2(12.9898, 78.233))) * 43758.5453);
}

// generated by shadergraph
inline float Unity_SimpleNnoise_Interpolate_float(float a, float b, float t)
{
    return (1.0 - t) * a + (t * b);
}

// generated by shadergraph
inline float Unity_SimpleNoise_ValueNoise_float(float2 uv)
{
    float2 i = floor(uv);
    float2 f = frac(uv);
    f = f * f * (3.0 - 2.0 * f);

    uv = abs(frac(uv) - 0.5);
    float2 c0 = i + float2(0.0, 0.0);
    float2 c1 = i + float2(1.0, 0.0);
    float2 c2 = i + float2(0.0, 1.0);
    float2 c3 = i + float2(1.0, 1.0);
    float r0 = Unity_SimpleNoise_RandomValue_float(c0);
    float r1 = Unity_SimpleNoise_RandomValue_float(c1);
    float r2 = Unity_SimpleNoise_RandomValue_float(c2);
    float r3 = Unity_SimpleNoise_RandomValue_float(c3);

    float bottomOfGrid = Unity_SimpleNnoise_Interpolate_float(r0, r1, f.x);
    float topOfGrid = Unity_SimpleNnoise_Interpolate_float(r2, r3, f.x);
    float t = Unity_SimpleNnoise_Interpolate_float(bottomOfGrid, topOfGrid, f.y);
    return t;
}

// generated by shadergraph
void Unity_SimpleNoise_float(float2 UV, float Scale, out float Out)
{
    float t = 0.0;

    float freq = pow(2.0, float(0));
    float amp = pow(0.5, float(3 - 0));
    t += Unity_SimpleNoise_ValueNoise_float(float2(UV.x * Scale / freq, UV.y * Scale / freq)) * amp;

    freq = pow(2.0, float(1));
    amp = pow(0.5, float(3 - 1));
    t += Unity_SimpleNoise_ValueNoise_float(float2(UV.x * Scale / freq, UV.y * Scale / freq)) * amp;

    freq = pow(2.0, float(2));
    amp = pow(0.5, float(3 - 2));
    t += Unity_SimpleNoise_ValueNoise_float(float2(UV.x * Scale / freq, UV.y * Scale / freq)) * amp;

    Out = t;
}

// generated by shadergraph
float2 Unity_GradientNoise_Deterministic_Dir_float(float2 p)
{
    float x; Hash_Tchou_2_1_float(p, x);
    return normalize(float2(x - floor(x + 0.5), abs(x) - 0.5));
}

// generated by shadergraph
void Unity_GradientNoise_Deterministic_float (float2 UV, float3 Scale, out float Out)
{
    float2 p = UV * Scale.xy;
    float2 ip = floor(p);
    float2 fp = frac(p);
    float d00 = dot(Unity_GradientNoise_Deterministic_Dir_float(ip), fp);
    float d01 = dot(Unity_GradientNoise_Deterministic_Dir_float(ip + float2(0, 1)), fp - float2(0, 1));
    float d10 = dot(Unity_GradientNoise_Deterministic_Dir_float(ip + float2(1, 0)), fp - float2(1, 0));
    float d11 = dot(Unity_GradientNoise_Deterministic_Dir_float(ip + float2(1, 1)), fp - float2(1, 1));
    fp = fp * fp * fp * (fp * (fp * 6 - 15) + 10);
    Out = lerp(lerp(d00, d01, fp.y), lerp(d10, d11, fp.y), fp.x) + 0.5;
}


//https://gist.github.com/keijiro/24f9d505fac238c9a2982c0d6911d8e3
uint SimpleHash(uint s)
{
    s ^= 2747636419u;
    s *= 2654435769u;
    s ^= s >> 16;
    s *= 2654435769u;
    s ^= s >> 16;
    s *= 2654435769u;
    return s;
}

//https://gist.github.com/keijiro/24f9d505fac238c9a2982c0d6911d8e3
// returns random number between 0 and 1
float Random01(uint seed)
{
    return float(SimpleHash(seed)) / 4294967295.0; // 2^32-1
}

// returns random number between -1 and 1
float Random11(uint seed)
{
    return (Random01(seed) - .5) * 2.;
}
 
#endif