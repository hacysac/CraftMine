using UnityEngine;

public static class Noise
{
    // ---------------------------------------------------------------------
    // 3D gradient noise
    //
    // Unity only ships a 2D Mathf.PerlinNoise, which is why Get3DValue fakes a
    // 3D field by averaging six 2D samples. That is expensive and not genuinely
    // 3D, so tunnels use this instead: real 3D noise, one lookup per sample,
    // cheap enough to take finite-difference gradients from.
    //
    // The permutation is Ken Perlin's fixed reference table rather than a seeded
    // shuffle, so the field is byte-identical on every platform. Per-lode
    // variation comes from the offset, not from the table.
    // ---------------------------------------------------------------------

    static readonly int[] perm = BuildPermutation();

    static int[] BuildPermutation()
    {
        int[] source =
        {
            151,160,137,91,90,15,131,13,201,95,96,53,194,233,7,225,140,36,103,30,
            69,142,8,99,37,240,21,10,23,190,6,148,247,120,234,75,0,26,197,62,94,
            252,219,203,117,35,11,32,57,177,33,88,237,149,56,87,174,20,125,136,
            171,168,68,175,74,165,71,134,139,48,27,166,77,146,158,231,83,111,229,
            122,60,211,133,230,220,105,92,41,55,46,245,40,244,102,143,54,65,25,63,
            161,1,216,80,73,209,76,132,187,208,89,18,169,200,196,135,130,116,188,
            159,86,164,100,109,198,173,186,3,64,52,217,226,250,124,123,5,202,38,
            147,118,126,255,82,85,212,207,206,59,227,47,16,58,17,182,189,28,42,
            223,183,170,213,119,248,152,2,44,154,163,70,221,153,101,155,167,43,
            172,9,129,22,39,253,19,98,108,110,79,113,224,232,178,185,112,104,218,
            246,97,228,251,34,242,193,238,210,144,12,191,179,162,241,81,51,145,
            235,249,14,239,107,49,192,214,31,181,199,106,157,184,84,204,176,115,
            121,50,45,127,4,150,254,138,236,205,93,222,114,67,29,24,72,243,141,
            128,195,78,66,215,61,156,180
        };

        // Doubled so lattice lookups can index up to 511 without wrapping logic.
        int[] result = new int[512];

        for (int i = 0; i < 512; i++)
        {
            result[i] = source[i & 255];
        }

        return result;
    }

    static float Fade(float t)
    {
        return t * t * t * (t * (t * 6f - 15f) + 10f);
    }

    static float Grad(int hash, float x, float y, float z)
    {
        int h = hash & 15;

        float u = h < 8 ? x : y;
        float v = h < 4 ? y : (h == 12 || h == 14 ? x : z);

        return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
    }

    // Roughly -1 to 1, zero on the lattice.
    public static float Perlin3D(float x, float y, float z)
    {
        float fx = Mathf.Floor(x);
        float fy = Mathf.Floor(y);
        float fz = Mathf.Floor(z);

        int xi = (int)fx & 255;
        int yi = (int)fy & 255;
        int zi = (int)fz & 255;

        x -= fx;
        y -= fy;
        z -= fz;

        float u = Fade(x);
        float v = Fade(y);
        float w = Fade(z);

        int a = perm[xi] + yi;
        int aa = perm[a] + zi;
        int ab = perm[a + 1] + zi;
        int b = perm[xi + 1] + yi;
        int ba = perm[b] + zi;
        int bb = perm[b + 1] + zi;

        float near = Mathf.Lerp(
            Mathf.Lerp(Grad(perm[aa], x, y, z), Grad(perm[ba], x - 1f, y, z), u),
            Mathf.Lerp(Grad(perm[ab], x, y - 1f, z), Grad(perm[bb], x - 1f, y - 1f, z), u),
            v);

        float far = Mathf.Lerp(
            Mathf.Lerp(Grad(perm[aa + 1], x, y, z - 1f), Grad(perm[ba + 1], x - 1f, y, z - 1f), u),
            Mathf.Lerp(Grad(perm[ab + 1], x, y - 1f, z - 1f), Grad(perm[bb + 1], x - 1f, y - 1f, z - 1f), u),
            v);

        return Mathf.Lerp(near, far, w);
    }


    // Deterministic stand-in for Random.Range when picking a noise offset: the same
    // key and seed always yield the same offset, on every run and platform.
    // string.GetHashCode is unsuitable here because .NET randomizes it per process,
    // which would reintroduce the run-to-run drift this exists to remove.
    public static float SeedOffset(string key, int seed)
    {
        unchecked
        {
            uint hash = 2166136261u;
            hash = (hash ^ (uint)seed) * 16777619u;

            foreach (char c in key)
            {
                hash = (hash ^ c) * 16777619u;
            }

            // Deliberately kept small. Get2DPerlin adds the offset *after* scaling,
            // so a large offset eats the float32 mantissa: at 90000 the per-block
            // step of ~0.005 is smaller than the ulp of ~0.011, and adjacent blocks
            // quantise onto the same noise coordinate. 0-1024 in thousandths leaves
            // plenty of separation between fields with none of that loss.
            return (hash % 1024000u) / 1000f;
        }
    }

    public static float Get2DPerlin(Vector2 position, float offset, float scale)
    {
        return Mathf.PerlinNoise((position.x + 0.1f) / VoxelData.ChunkWidth * scale + offset, (position.y + 0.1f) / VoxelData.ChunkWidth * scale + offset);
    }

    public static bool Get3DPerlin(Vector3 position, float offset, float scale, float threshold)
    {
        return Get3DValue(position, offset, scale) > threshold;
    }

    // Carves noodle caves: long winding tunnels of near-constant bore, like the
    // early Minecraft carvers. Two independent 3D fields each define a surface at
    // their zero level, and those two surfaces cross along a curve - the tunnel
    // axis. Distance to each surface is measured in blocks, so radius is a real
    // radius and the bore stays put instead of ballooning wherever the surfaces
    // happen to meet at a shallow angle.
    //
    // scale sets how far the tunnels wander between turns, radius sets the bore in
    // blocks, and the two are now independent. verticalSquash above 1 raises the
    // vertical frequency, tipping tunnels toward horizontal runs over vertical
    // shafts.
    public static bool GetTunnel(Vector3 position, float offset, float scale, float radius, float verticalSquash)
    {
        position.y *= verticalSquash;

        float da = DistanceToZeroSet(position, offset, scale);
        float db = DistanceToZeroSet(position, offset + 713.3f, scale);

        // Inside the tube when the point is within radius of the crossing curve.
        return da * da + db * db < radius * radius;
    }

    // |f| / |grad f| is the standard first-order estimate of the distance from a
    // point to a field's zero set. Dividing the noise-space gradient by scale puts
    // the answer in blocks. Gradients come from forward differences, which costs
    // three extra samples per field - affordable only because Perlin3D is a single
    // lookup, unlike the six-sample average in Get3DValue.
    const float GradientStep = 0.05f;

    static float DistanceToZeroSet(Vector3 position, float offset, float scale)
    {
        float x = (position.x + offset) * scale;
        float y = (position.y + offset) * scale;
        float z = (position.z + offset) * scale;

        float f = Perlin3D(x, y, z);

        float gx = (Perlin3D(x + GradientStep, y, z) - f) / GradientStep;
        float gy = (Perlin3D(x, y + GradientStep, z) - f) / GradientStep;
        float gz = (Perlin3D(x, y, z + GradientStep) - f) / GradientStep;

        float gradient = Mathf.Sqrt(gx * gx + gy * gy + gz * gz) * scale;

        // A vanishing gradient means a flat spot with no nearby zero crossing.
        return gradient > 1e-6f ? f / gradient : 1e6f;
    }

    public static float Get3DValue(Vector3 position, float offset, float scale)
    {

        float x = (position.x + 0.1f + offset) * scale;
        float y = (position.y + 0.1f + offset) * scale;
        float z = (position.z + 0.1f + offset) * scale;

        float ab = Mathf.PerlinNoise(x , y);
        float bc = Mathf.PerlinNoise(y , z);
        float ac = Mathf.PerlinNoise(x , z);
        float ba = Mathf.PerlinNoise(y , x);
        float cb = Mathf.PerlinNoise(z , y);
        float ca = Mathf.PerlinNoise(z , x);

        float noise = (ab + bc + ac + ba + cb + ca) / 6f;

        // Averaging six Perlin samples collapses the distribution toward 0.5
        // (mean 0.5, std ~0.066), so the raw value effectively never leaves
        // ~0.3-0.75. Left as-is, any threshold above ~0.7 is unreachable and the
        // lode simply never places, no matter how the scale is tuned. Expand
        // roughly +/-2.5 std back out to 0-1 so threshold means what it looks
        // like it means: 0.5 fills about half the volume, 0.9 is rare.
        return Mathf.Clamp01((noise - 0.5f) / 0.33f + 0.5f);
    }

}
