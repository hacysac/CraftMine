using UnityEngine;

public enum LodeShape
{
    // Rounded pockets. Good for ore and dirt deposits.
    Blob,
    // Winding tunnels. Good for caves.
    Tunnel
}

[CreateAssetMenu(fileName = "LodeAttributes", menuName = "MinecraftTutorial/LodeAttributes")]
public class Lode : ScriptableObject
{
    public BlockID block;
    public int minHeight;
    public int maxHeight;
    public float scale;

    // Assigned by World.InitNoiseOffsets from the world seed. Not set here, because
    // ScriptableObject.Awake runs when Unity loads the asset - before the seed has
    // been read from settings.cfg.
    [System.NonSerialized]
    public float noiseOffset;

    public LodeShape shape;

    [Header("Blob")]
    [Range(0f, 1f)]
    public float threshold;

    [Header("Tunnel")]
    // Bore of the tunnel, in blocks. Independent of scale, which now only controls
    // how far a tunnel wanders between turns.
    [Range(0.5f, 6f)]
    public float tunnelRadius = 1.6f;
    [Range(1f, 4f)]
    public float verticalSquash = 1f;

    public BlockID[] replaceables;

    public bool Contains(Vector3 pos) => shape switch
    {
        LodeShape.Tunnel => Noise.GetTunnel(pos, noiseOffset, scale, tunnelRadius, verticalSquash),
        _ => Noise.Get3DPerlin(pos, noiseOffset, scale, threshold),
    };
}