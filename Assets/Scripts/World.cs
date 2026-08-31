using System.Collections.Concurrent;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.U2D;
using System.Threading;
using System.IO;

public class World : MonoBehaviour
{
    private bool _inUI = false;
    [Range(0f, 1f)]
    public float globalLightLevel;

    public Color day;
    public Color night;

    public Transform player;
    public Material material;
    public Material transparentMaterial;
    public SpriteAtlas blockAtlas;
    public BlockType[] blockTypes;
    public BiomeAttributes biome;
    public GameObject debugScreen;
    public GameObject creativeInventoryWindow;
    public GameObject cursorSlot;
    public Settings settings;

    public Vector3 spawnPosition;
    public ChunkCoord playerLastChunkCoord;
    bool applyingModifications = false;

    Chunk[,] chunks = new Chunk[VoxelData.WorldSizeInChunks, VoxelData.WorldSizeInChunks];
    List<ChunkCoord> activeChunks = new List<ChunkCoord>();
    public List<Chunk> chunksToUpdate = new List<Chunk>();
    // Read (CreateChunk) on the main thread and written (ApplyModifications) on
    // the worker thread, so it must be thread-safe.
    ConcurrentQueue<ChunkCoord> chunksToCreate = new ConcurrentQueue<ChunkCoord>();
    ConcurrentQueue<Chunk> chunksToPopulate = new ConcurrentQueue<Chunk>();
    Queue<Queue<VoxelMod>> modifications = new Queue<Queue<VoxelMod>>();
    public Queue<Chunk> chunksToDraw = new Queue<Chunk>();
    public FaceUVs[,] faceUVCache;

    Thread ChunkUpdateThread;
    public object ChunkUpdateThreadLock = new object();

    // Called by Chunk.Init on the main thread; the worker thread does the actual
    // population via ThreadedUpdate.
    public void QueueForPopulation(Chunk chunk)
    {
        chunksToPopulate.Enqueue(chunk);
    }

    void BuildFaceUVCache()
    {
        faceUVCache = new FaceUVs[blockTypes.Length, 6];

        for (int b = 0; b < blockTypes.Length; b++)
        {
            for (int f = 0; f < 6; f++)
            {
                Sprite sprite = blockTypes[b].GetFaceSprite(f);
                if (sprite == null)
                {
                    // Leave as default(FaceUVs), which is already all zeroes.
                    continue;
                }

                Rect rect = sprite.textureRect;
                float texWidth = sprite.texture.width;
                float texHeight = sprite.texture.height;

                float xMin = rect.xMin / texWidth;
                float xMax = rect.xMax / texWidth;
                float yMin = rect.yMin / texHeight;
                float yMax = rect.yMax / texHeight;

                faceUVCache[b, f] = new FaceUVs
                {
                    uv00 = new Vector2(xMin, yMin),
                    uv01 = new Vector2(xMin, yMax),
                    uv10 = new Vector2(xMax, yMin),
                    uv11 = new Vector2(xMax, yMax)
                };
            }
        }
    }

    // BlockID member values are the blockTypes indices (the generator writes them
    // explicitly), so a BlockID converts to an index with a plain cast. This only
    // validates that the two are still in sync; it is not a lookup.
    void ValidateBlockIDs()
    {
        if (blockTypes.Length != System.Enum.GetValues(typeof(BlockID)).Length)
        {
            Debug.LogWarning("blockTypes and BlockID have different lengths. " +
                             "Re-run Tools > Generate BlockID Enum.");
        }

        for (ushort i = 0; i < blockTypes.Length; i++)
        {
            string name = blockTypes[i].blockName;

            if (string.IsNullOrEmpty(name))
            {
                Debug.LogWarning($"blockTypes[{i}] has no blockName set.");
                continue;
            }

            if (!System.Enum.TryParse<BlockID>(name.Replace(' ', '_'), out BlockID id) || id != (BlockID)i)
            {
                Debug.LogWarning($"blockTypes[{i}] blockName '{name}' does not match BlockID.{id} = {(int)id}. " +
                                 "Re-run Tools > Generate BlockID Enum.");
            }
        }
    }

    public bool inUI
    {
        get { return _inUI; }
        set
        {
            _inUI = value;
            if (_inUI)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                creativeInventoryWindow.SetActive(true);
                cursorSlot.SetActive(true);
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                creativeInventoryWindow.SetActive(false);
                cursorSlot.SetActive(false);
            }
        }
    }

    private void Awake()
    {
        // Settings is only assigned in the Inspector; if it's missing, Player.Update
        // would throw a NullReferenceException on world.settings.mouseSensitivity
        // every frame and mouse look would be dead until it's assigned manually.
        if (settings == null)
        {
            settings = new Settings { viewDistance = 5, mouseSensitivity = 1f };
        }
        // A serialized Settings object defaults mouseSensitivity to 0, which makes
        // mouse look completely dead until the value is changed manually.
        if (settings.mouseSensitivity <= 0f)
        {
            settings.mouseSensitivity = 1f;
        }
        ValidateBlockIDs();
        BuildFaceUVCache();
    }

    private void Start()
    {
        //string jsonExport = JsonUtility.ToJson(settings);
        //File.WriteAllText(Application.dataPath + "/settings.cfg", jsonExport);

        string jsonImport = File.ReadAllText(Application.dataPath + "/settings.cfg");
        settings = JsonUtility.FromJson<Settings>(jsonImport);

        Cursor.lockState = CursorLockMode.Locked;
        Random.InitState(settings.seed);

        Shader.SetGlobalFloat("minGlobalLightLevel", VoxelData.minLightLevel);
        Shader.SetGlobalFloat("maxGlobalLightLevel", VoxelData.maxLightLevel);

        int spawnX = VoxelData.WorldSizeInVoxels / 2;
        int spawnZ = VoxelData.WorldSizeInVoxels / 2;

        int spawnY = 0;

        for (int y = VoxelData.ChunkHeight - 1; y >= 0; y--)
        {
            if (GetVoxel(new Vector3(spawnX, y, spawnZ)) != (ushort)BlockID.Air)
            {
                spawnY = y + 1;
                break;
            }
        }

        SetGlobalLightValue();
        spawnPosition = new Vector3(spawnX + 0.5f, spawnY + 2, spawnZ + 0.5f);

        ChunkUpdateThread = new Thread(new ThreadStart(ThreadedUpdate));
        ChunkUpdateThread.Start();

        GenerateWorld();

        if (blockAtlas != null)
        {
            Sprite firstSprite = blockAtlas.GetSprite("grass_top");

            if (firstSprite != null)
            {
                material.SetTexture("_BaseMap", firstSprite.texture);
            }
        }

        playerLastChunkCoord = GetChunkCoordFromVector3(player.position);
    }

    public void SetGlobalLightValue()
    {
        Shader.SetGlobalFloat("GlobalLightLevel", globalLightLevel);
        Camera.main.backgroundColor = Color.Lerp(night, day, globalLightLevel);
    }

    public void Update()
    {

        if (!GetChunkCoordFromVector3(player.position).Equals(playerLastChunkCoord))
        {
            CheckViewDistance();
        }
        if (chunksToCreate.TryDequeue(out ChunkCoord c))
        {
            chunks[c.x, c.z].Init();
        }
        if (Input.GetKeyDown(KeyCode.F3))
        {
            debugScreen.SetActive(!debugScreen.activeSelf);
        }
        // Drain the draw queue with a small per-frame time budget so the startup
        // backlog clears in a few frames instead of seconds.
        float drawBudgetEnd = Time.realtimeSinceStartup + 0.008f;
        while (chunksToDraw.Count > 0 && Time.realtimeSinceStartup < drawBudgetEnd)
        {
            if (!chunksToDraw.Peek().isEditable)
            {
                break;
            }
            Chunk drawChunk = chunksToDraw.Dequeue();
            lock (drawChunk.buildLock)
            {
                drawChunk.CreateMesh();
            }
        }
    }

    void GenerateWorld()
    {
        for (int x = VoxelData.WorldSizeInChunks/2 - settings.viewDistance/2; x < VoxelData.WorldSizeInChunks/2 + settings.viewDistance/2; x++)
        {
            for (int z = VoxelData.WorldSizeInChunks/2 - settings.viewDistance/2; z < VoxelData.WorldSizeInChunks/2 + settings.viewDistance/2; z++)
            {
                ChunkCoord thisChunk = new ChunkCoord(x,z);
                chunks[x, z] = new Chunk(thisChunk, this);
                chunksToCreate.Enqueue(thisChunk);
            }
        }

        player.position = spawnPosition;
        CheckViewDistance();
    }

    void UpdateChunks()
    {
        bool updated = false;
        int index = 0;
        lock (ChunkUpdateThreadLock)
        {
            while (!updated && index < chunksToUpdate.Count)
            {
                if (chunksToUpdate[index].isEditable)
                {
                    lock (chunksToUpdate[index].buildLock)
                    {
                        chunksToUpdate[index].UpdateChunk();
                    }

                    // Registers chunks created outside CheckViewDistance (e.g. tree
                    // spillover in ApplyModifications) so they can be deactivated later.
                    if (!activeChunks.Contains(chunksToUpdate[index].chunkCoord))
                    {
                        activeChunks.Add(chunksToUpdate[index].chunkCoord);
                    }

                    chunksToUpdate.RemoveAt(index);
                    updated = true;
                }
                index++;
            }
        }
    }

    void ThreadedUpdate()
    {
        while (true)
        {
            bool didWork = false;

            // Population is pure data work, so it runs here instead of on the
            // main thread (Init just queues it).
            if (chunksToPopulate.TryDequeue(out Chunk chunkToPopulate))
            {
                chunkToPopulate.PopulateVoxelMap();
                didWork = true;
            }

            if (!applyingModifications)
            {
                ApplyModifications();
                didWork = true;
            }
            if (chunksToUpdate.Count > 0)
            {
                UpdateChunks();
                didWork = true;
            }

            if (!didWork)
            {
                Thread.Sleep(1);
            }
        }
    }

    private void OnDisable()
    {
        ChunkUpdateThread.Abort();
    }

    void ApplyModifications()
    {
        applyingModifications = true;
        while (modifications.Count > 0)
        {
            Queue<VoxelMod> queue;

            lock (modifications)
            {
                if (modifications.Count == 0)
                {
                    break;
                }
                queue = modifications.Dequeue();
            }

            while(queue.Count > 0)
            {
                VoxelMod v = queue.Dequeue();
                // Tree structures can spill past the world edge, where there is no
                // chunk slot to write into.
                if (v.position.y < 0 || v.position.y >= VoxelData.ChunkHeight)
                {
                    continue;
                }
                ChunkCoord c = GetChunkCoordFromVector3(v.position);
                if (!IsChunkInWorld(c))
                {
                    continue;
                }
                if (chunks[c.x,c.z] == null)
                {
                    chunks[c.x, c.z] = new Chunk(c, this);
                    chunksToCreate.Enqueue(c);
                }
                Chunk chunk = chunks[c.x, c.z];

                lock (chunk.modifications)
                {
                    chunk.modifications.Enqueue(v);
                }

                if (chunk.isEditable)
                {
                    lock (ChunkUpdateThreadLock)
                    {
                        if (!chunksToUpdate.Contains(chunk))
                        {
                            chunksToUpdate.Add(chunk);
                        }
                    }
                }
            }
        }
        applyingModifications = false;
    }

    ChunkCoord GetChunkCoordFromVector3(Vector3 pos)
    {
        int x = Mathf.FloorToInt(pos.x / VoxelData.ChunkWidth);
        int z = Mathf.FloorToInt(pos.z / VoxelData.ChunkWidth);
        return new ChunkCoord(x, z);
    }

    public Chunk GetChunkFromVector3(Vector3 pos)
    {
        ChunkCoord coord = GetChunkCoordFromVector3(pos);
        return chunks[coord.x, coord.z];
    }

    void CheckViewDistance()
    {
        List<ChunkCoord> previouslyActiveChunks = new List<ChunkCoord>(activeChunks);
        ChunkCoord playerChunkCoord = GetChunkCoordFromVector3(player.position);
        playerLastChunkCoord = playerChunkCoord;

        activeChunks.Clear();
        for (int x = playerChunkCoord.x - settings.viewDistance; x < playerChunkCoord.x + settings.viewDistance; x++)
        {
            for (int z = playerChunkCoord.z - settings.viewDistance; z < playerChunkCoord.z + settings.viewDistance; z++)
            {
                ChunkCoord thisChunk = new ChunkCoord(x, z);
                if (IsChunkInWorld(thisChunk))
                {

                    if (chunks[x, z] == null)
                    {
                        chunks[x,z] = new Chunk(thisChunk, this);
                        chunksToCreate.Enqueue(thisChunk);
                    }
                    else if (!chunks[x, z].isActive)
                    {
                        chunks[x, z].isActive = true;
                    }
                    activeChunks.Add(thisChunk);
                }
                // Backwards, so RemoveAt cannot skip the element that shifts into i.
                for (int i = previouslyActiveChunks.Count - 1; i >= 0; i--)
                {
                    if (previouslyActiveChunks[i].x == x && previouslyActiveChunks[i].z == z)
                    {
                        previouslyActiveChunks.RemoveAt(i);
                    }
                }
            }
        }

        // Whatever is left is out of view now.
        foreach (ChunkCoord chunk in previouslyActiveChunks)
        {
            chunks[chunk.x, chunk.z].isActive = false;
        }
    }

    public bool CheckForVoxel (Vector3 pos) {

        ChunkCoord thisChunk = new ChunkCoord(pos);

        if (!IsChunkInWorld(thisChunk) || pos.y < 0 || pos.y > VoxelData.ChunkHeight)
            return false;

        if (chunks[thisChunk.x, thisChunk.z] != null && chunks[thisChunk.x, thisChunk.z].isEditable)
            return blockTypes[chunks[thisChunk.x, thisChunk.z].GetVoxelFromGlobalVector3(pos).id].isSolid;

        return blockTypes[GetVoxel(pos)].isSolid;

    }

    public VoxelState GetVoxelState(Vector3 pos)
    {

        ChunkCoord thisChunk = new ChunkCoord(pos);

        if (!IsChunkInWorld(thisChunk) || pos.y < 0 || pos.y > VoxelData.ChunkHeight)
            return null;

        if (chunks[thisChunk.x, thisChunk.z] != null && chunks[thisChunk.x, thisChunk.z].isEditable)
            return chunks[thisChunk.x, thisChunk.z].GetVoxelFromGlobalVector3(pos);

        return new VoxelState(GetVoxel(pos));

    }

    public ushort GetVoxel(Vector3 pos)
    {
        int yPos = Mathf.FloorToInt(pos.y);
        // Immutable Pass

        // if outside return air
        if (!IsVoxelInWorld(pos))
        {
            return (ushort)BlockID.Air;
        }
        // if at ground return bedrock
        if  (yPos == 0)
        {
            return (ushort)BlockID.Bedrock;
        }

        // Basic Terrain Pass
        float noise = Noise.Get2DPerlin(new Vector2(pos.x, pos.z), 0, biome.terrainScale);
        int terrainHeight = Mathf.FloorToInt(noise * biome.terrainHeight) + biome.solidGroundHeight;
        ushort voxelValue = (ushort)BlockID.Air;

        if (yPos == terrainHeight)
        {
            voxelValue = (ushort)BlockID.Grass;
        }
        else if (yPos < terrainHeight && yPos > terrainHeight - 4)
        {
            voxelValue = (ushort)BlockID.Dirt;
        }
        else if (yPos > terrainHeight)
        {
            return (ushort)BlockID.Air;
        }
        else
        {
            voxelValue = (ushort)BlockID.Stone;
        }

        // Second Pass

        if (voxelValue == (ushort)BlockID.Stone)
        {
            // Check for lode generation
            foreach (Lode lode in biome.lodes)
            {
                if (yPos > lode.minHeight && yPos < lode.maxHeight)
                {
                    bool noise2 = Noise.Get3DPerlin(pos, lode.noiseOffset, lode.scale, lode.threshold);
                    if (noise2)
                    {
                        voxelValue = (ushort)lode.block;
                    }
                }
            }
        }

        //Tree Pass
        if(yPos == terrainHeight)
        {
            if (Noise.Get2DPerlin(new Vector2(pos.x, pos.z), 0, biome.treeZoneScale) > biome.treeZoneThreshold)
            {
                if (Noise.Get2DPerlin(new Vector2(pos.x,pos.z), 0, biome.treePlacementScale) > biome.treePlacementThreshold)
                {
                    Queue<VoxelMod> tree = Structure.MakeTree(pos, biome.minTreeHeight, biome.maxTreeHeight,this);
                    lock (modifications)
                    {
                        modifications.Enqueue(tree);
                    }
                }
            }
        }

        return voxelValue;
    }

    bool IsChunkInWorld(ChunkCoord chunkCoord)
    {
        return chunkCoord.x >= 0 && chunkCoord.x < VoxelData.WorldSizeInChunks
            && chunkCoord.z >= 0 && chunkCoord.z < VoxelData.WorldSizeInChunks;
    }

    bool IsVoxelInWorld(Vector3 pos)
    {
        return pos.x >= 0 && pos.x < VoxelData.WorldSizeInVoxels
            && pos.y >= 0 && pos.y < VoxelData.ChunkHeight
            && pos.z >= 0 && pos.z < VoxelData.WorldSizeInVoxels;
    }
}

[System.Serializable]
public class BlockType
{
    public string blockName;
    public bool isSolid;
    public bool renderNeighborFaces;
    public float transparency;
    public int maxStackSize;
    public Sprite icon;

    [Header("Texture Values")]
    public Sprite backFaceTexture;
    public Sprite frontFaceTexture;
    public Sprite topFaceTexture;
    public Sprite bottomFaceTexture;
    public Sprite leftFaceTexture;
    public Sprite rightFaceTexture;

    // Face indices match VoxelData.faceChecks.
    public Sprite GetFaceSprite(int faceIndex) => faceIndex switch
    {
        0 => backFaceTexture,
        1 => frontFaceTexture,
        2 => topFaceTexture,
        3 => bottomFaceTexture,
        4 => leftFaceTexture,
        5 => rightFaceTexture,
        _ => null,
    };
}

public class VoxelMod
{
    public Vector3 position;
    public ushort id;

    public VoxelMod(Vector3 position, ushort id, World world)
    {
        this.position = position;
        this.id = id;
    }
}

[System.Serializable]
public struct FaceUVs
{
    public Vector2 uv00, uv01, uv10, uv11; // min-min, min-max, max-min, max-max
}

[System.Serializable]
public class Settings
{
    [Header("Game Data")]
    public string version;

    [Header("Performance")]
    public int viewDistance;
    
    [Header("Controls")]
    [Range(0.5f, 10f)]
    public float mouseSensitivity;

    [Header("World Gen")]
    public int seed;
}