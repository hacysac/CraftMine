using UnityEngine;

public static class Noise
{

    public static float Get2DPerlin(Vector2 position, float offset, float scale)
    {
        return Mathf.PerlinNoise((position.x + 0.1f) / VoxelData.ChunkWidth * scale + offset, (position.y + 0.1f) / VoxelData.ChunkWidth * scale + offset);
    }

    public static bool Get3DPerlin(Vector3 position, float offset, float scale, float threshold)
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

        return (ab + bc + ac + ba + cb + ca) / 6f > threshold;
    }

}
