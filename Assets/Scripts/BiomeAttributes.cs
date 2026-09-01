using UnityEngine;

[CreateAssetMenu(fileName = "BiomeAttributes", menuName = "MinecraftTutorial/BiomeAttributes")]
public class BiomeAttributes : ScriptableObject
{
    public string biomeName;
    public int terrainHeight;
    public float terrainScale;
    public Lode[] lodes;

    public BlockID surfaceBlock;
    public BlockID subSurfaceBlock;

    // Assigned by World.InitNoiseOffsets from the world seed. Not set here, because
    // ScriptableObject.Awake runs when Unity loads the asset - before the seed has
    // been read from settings.cfg.
    [System.NonSerialized]
    public float offset;

    [Header("Noise Offsets")]
    public float scale;

    [Header("Major Flora")]
    public FloraType majorFloraIndex;
    public float majorFloraZoneScale = 1.3f;
    [Range(0.1f,1f)]
    public float majorFloraZoneThreshold = 0.6f;
    public float majorFloraPlacementScale = 15f;
    public float majorFloraPlacementThreshold = 0.8f;
    public bool placeMajorFlora = true;
    public int maxHeight = 12;
    public int minHeight = 5;
}
