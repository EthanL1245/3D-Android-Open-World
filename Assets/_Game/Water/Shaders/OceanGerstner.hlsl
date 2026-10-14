#ifndef OPENWORLD_OCEAN_GERSTNER_INCLUDED
#define OPENWORLD_OCEAN_GERSTNER_INCLUDED

// Shared by the ocean mesh and surface-following effects. The vertical waves
// retain their existing phases/amplitudes; this adds Gerstner's horizontal orbit.
// Each wave contributes at most 0.05 to the horizontal derivative, keeping
// the surface single-valued and its two-step inverse stable even offshore.
void OceanHorizontalWave(float2 p, float amplitude, float wavelength,
    float2 direction, float phase, float islandPhase, float blend,
    float2 blendGradient, out float2 offset, out float4 jacobian,
    out float height, out float2 heightGradient)
{
    float2 d = dot(direction, direction) > 0.0001 ? normalize(direction) : float2(1, 0);
    float k = 6.28318530718 / max(0.1, wavelength);
    float orbit = min(abs(amplitude) * _GerstnerStrength, 0.05 / k);
    float roughOrbit = min(abs(amplitude * _IslandWaveAmplitude) * _GerstnerStrength, 0.05 / k);
    float spatial = dot(p, d) * k;
    float s, c, rs, rc;
    sincos(spatial + phase, s, c);
    sincos(spatial + islandPhase, rs, rc);
    float calmHeight = amplitude * s;
    float roughHeight = amplitude * _IslandWaveAmplitude * rs;
    height = lerp(calmHeight, roughHeight, blend);
    heightGradient = k * lerp(amplitude * c, amplitude * _IslandWaveAmplitude * rc, blend) * d
                     + (roughHeight - calmHeight) * blendGradient;
    float calm = orbit * c;
    float rough = roughOrbit * rc;
    offset = d * lerp(calm, rough, blend);
    float2 gradient = -k * lerp(orbit * s, roughOrbit * rs, blend) * d
                      + (rough - calm) * blendGradient;
    // xx, xz, zx, zz: derivative of horizontal displacement.
    jacobian = float4(d.x * gradient.x, d.x * gradient.y,
                     d.y * gradient.x, d.y * gradient.y);
}

void OceanGerstnerSample(float2 p, out float2 offset, out float4 jacobian,
    out float height, out float2 heightGradient)
{
    float blend = 0;
    float2 gradient = 0;
    if (_IslandWaveEnabled > 0.5)
    {
        float2 delta = p - _IslandWaveCenter.xy;
        float radius = length(delta);
        float width = max(0.01, _IslandWaveCenter.w - _IslandWaveCenter.z);
        float t = saturate((radius - _IslandWaveCenter.z) / width);
        blend = 1.0 - t * t * (3.0 - 2.0 * t);
        gradient = (-6.0 * t * (1.0 - t) / width) * delta / max(radius, 0.001);
    }
    float2 o1, o2, o3;
    float4 j1, j2, j3;
    float h1, h2, h3;
    float2 g1, g2, g3;
    OceanHorizontalWave(p, _WaveAmplitude1, _WaveLength1, _WaveDirection1.xy,
        _WavePhases.x, _IslandWavePhases.x, blend, gradient, o1, j1, h1, g1);
    OceanHorizontalWave(p, _WaveAmplitude2, _WaveLength2, _WaveDirection2.xy,
        _WavePhases.y, _IslandWavePhases.y, blend, gradient, o2, j2, h2, g2);
    OceanHorizontalWave(p, _WaveAmplitude3, _WaveLength3, _WaveDirection3.xy,
        _WavePhases.z, _IslandWavePhases.z, blend, gradient, o3, j3, h3, g3);
    offset = o1 + o2 + o3;
    jacobian = j1 + j2 + j3;
    height = h1 + h2 + h3;
    heightGradient = g1 + g2 + g3;
}

void OceanHorizontalSample(float2 p, out float2 offset, out float4 jacobian)
{
    float unusedHeight;
    float2 unusedGradient;
    OceanGerstnerSample(p, offset, jacobian, unusedHeight, unusedGradient);
}

float2 OceanUndisplacedPosition(float2 worldXZ)
{
    float2 p = worldXZ;
    // Exactly the same two fixed-point steps as OceanWater.GetSurfaceHeight.
    [unroll] for (int i = 0; i < 2; i++)
    {
        float2 offset;
        float4 unused;
        OceanHorizontalSample(p, offset, unused);
        p = worldXZ - offset;
    }
    return p;
}
#endif
