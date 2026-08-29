using System.Security.AccessControl;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.U2D;
using System.Threading;

public class World : MonoBehaviour
{
    public int seed;

    public Transform player;
    public Material material;
    public Material transparentMaterial;
    public SpriteAtlas blockAtlas;
    public BlockType[] blockTypes;
    public BiomeAttributes biome;
    public GameObject debugScreen;

    public Vector3 spawnPosition;
    public ChunkCoord playerLastChunkCoord;
    bool applyingModifications = false;

    Chunk[,] chunks = new Chunk[VoxelData.WorldSizeInChunks, VoxelData.WorldSizeInChunks];
    List<ChunkCoord> activeChunks = new List<ChunkCoord>();
    public List<Chunk> chunksToUpdate = new List<Chunk>();
    List<ChunkCoord> chunksToCreate = new List<ChunkCoord>();
    Queue<Queue<VoxelMod>> modifications = new Queue<Queue<VoxelMod>>();
    Dictionary<string, ushort> blockNameToID;
    public Queue<Chunk> chunksToDraw = new Queue<Chunk>();
    public FaceUVs[,] faceUVCache;

    Thread ChunkUpdateThread;
    public object ChunkUpdateThreadLock = new object();

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

    void BuildBlockNameLookup()
    {
        blockNameToID = new Dictionary<string, ushort>();

        for (ushort i = 0; i < blockTypes.Length; i++)
        {
            string name = blockTypes[i].blockName;

            if (string.IsNullOrEmpty(name))
            {
                Debug.LogWarning($"blockTypes[{i}] has no blockName set.");
                continue;
            }

            if (!blockNameToID.TryAdd(name, i))
                Debug.LogWarning($"Duplicate block name '{name}' at index {i} (already used by index {blockNameToID[name]}).");
        }
    }

    public ushort GetBlockIndex(string blockName)
    {
        if (blockNameToID.TryGetValue(blockName, out ushort index))
            return index;

        Debug.LogError($"Block name '{blockName}' not found in blockTypes.");
        return 0; // falls back to Air
    }

    private void Awake()
    {
        BuildBlockNameLookup();
        BuildFaceUVCache();
    }

    private void Start()
    {
        Random.InitState(seed);

        int spawnX = VoxelData.WorldSizeInVoxels / 2;
        int spawnZ = VoxelData.WorldSizeInVoxels / 2;

        int spawnY = 0;

        for (int y = VoxelData.ChunkHeight - 1; y >= 0; y--)
        {
            if (GetVoxel(new Vector3(spawnX, y, spawnZ)) != 0)
            {
                spawnY = y + 1;
                break;
            }
        }

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

    public void Update()
    {
        if (!GetChunkCoordFromVector3(player.position).Equals(playerLastChunkCoord))
        {
            CheckViewDistance();
        }
        if (chunksToCreate.Count > 0)
        {
            CreateChunk();
        }
        if (Input.GetKeyDown(KeyCode.F3))
        {
            debugScreen.SetActive(!debugScreen.activeSelf);
        }
        if (chunksToDraw.Count > 0)
        {
            if (chunksToDraw.Peek().isEditable)
            {
                chunksToDraw.Dequeue().CreateMesh();
            }
        }
    }

    void GenerateWorld()
    {
        for (int x = VoxelData.WorldSizeInChunks/2 - VoxelData.ViewDistanceInChunks/2; x < VoxelData.WorldSizeInChunks/2 + VoxelData.ViewDistanceInChunks/2; x++)
        {
            for (int z = VoxelData.WorldSizeInChunks/2 - VoxelData.ViewDistanceInChunks/2; z < VoxelData.WorldSizeInChunks/2 + VoxelData.ViewDistanceInChunks/2; z++)
            {
                ChunkCoord thisChunk = new ChunkCoord(x,z);
                chunks[x, z] = new Chunk(thisChunk, this);
                chunksToCreate.Add(thisChunk);
            }
        }

        player.position = spawnPosition;
        CheckViewDistance();
    }

    void CreateChunk()
    {
        ChunkCoord c = chunksToCreate[0];
        chunksToCreate.RemoveAt(0);
        chunks[c.x,c.z].Init();
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
                    chunksToUpdate[index].UpdateChunk();

                    // Registers chunks created outside CheckViewDistance (e.g. tree
                    // spillover in ApplyModifications) so they can be deactivated later.
                    activeChunks.Add(chunksToUpdate[index].chunkCoord);

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
            if (!applyingModifications)
            {
                ApplyModifications();
            }
            if (chunksToUpdate.Count > 0)
            {
                UpdateChunks();
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
                ChunkCoord c = GetChunkCoordFromVector3(v.position);
                if (chunks[c.x,c.z] == null)
                {
                    chunks[c.x, c.z] = new Chunk(c, this);
                    chunksToCreate.Add(c);
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
        for (int x = playerChunkCoord.x - VoxelData.ViewDistanceInChunks; x < playerChunkCoord.x + VoxelData.ViewDistanceInChunks; x++)
        {
            for (int z = playerChunkCoord.z - VoxelData.ViewDistanceInChunks; z < playerChunkCoord.z + VoxelData.ViewDistanceInChunks; z++)
            {
                ChunkCoord thisChunk = new ChunkCoord(x, z);
                if (IsChunkInWorld(thisChunk))
                {

                    if (chunks[x, z] == null)
                    {
                        chunks[x,z] = new Chunk(thisChunk, this);
                        chunksToCreate.Add(thisChunk);
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

    // Resolves the block at a world position, preferring the live chunk data when it is
    // ready and falling back to procedural generation otherwise. Null when out of world.
    BlockType BlockTypeAt(Vector3 pos)
    {
        if (!IsVoxelInWorld(pos))
        {
            return null;
        }

        ChunkCoord coord = new ChunkCoord(pos);
        Chunk chunk = chunks[coord.x, coord.z];

        if (chunk != null && chunk.isEditable)
        {
            return blockTypes[chunk.GetVoxelFromGlobalVector3(pos)];
        }

        return blockTypes[GetVoxel(pos)];
    }

    public bool CheckForVoxel(Vector3 pos)
    {
        BlockType block = BlockTypeAt(pos);

        if (block == null)
        {
            return false;
        }

        return block.isSolid;
    }

    public bool CheckIfVoxelTransparent(Vector3 pos)
    {
        BlockType block = BlockTypeAt(pos);

        if (block == null)
        {
            return false;
        }

        return block.isTransparent;
    }

    public ushort GetVoxel(Vector3 pos)
    {
        int yPos = Mathf.FloorToInt(pos.y);
        // Immutable Pass

        // if outside return air
        if (!IsVoxelInWorld(pos))
        {
            return this.GetBlockIndex("Air");
        }
        // if at ground return bedrock
        if  (yPos == 0)
        {
            return this.GetBlockIndex("Bedrock");
        }

        // Basic Terrain Pass
        float noise = Noise.Get2DPerlin(new Vector2(pos.x, pos.z), 0, biome.terrainScale);
        int terrainHeight = Mathf.FloorToInt(noise * biome.terrainHeight) + biome.solidGroundHeight;
        ushort voxelValue = 0;

        if (yPos == terrainHeight)
        {
            voxelValue = this.GetBlockIndex("Grass");
        }
        else if (yPos < terrainHeight && yPos > terrainHeight - 4)
        {
            voxelValue = this.GetBlockIndex("Dirt");
        }
        else if (yPos > terrainHeight)
        {
            return this.GetBlockIndex("Air");
        }
        else
        {
            voxelValue = this.GetBlockIndex("Stone");
        }

        // Second Pass

        if (voxelValue == this.GetBlockIndex("Stone"))
        {
            // Check for lode generation
            foreach (Lode lode in biome.lodes)
            {
                if (yPos > lode.minHeight && yPos < lode.maxHeight)
                {
                    bool noise2 = Noise.Get3DPerlin(pos, lode.noiseOffset, lode.scale, lode.threshold);
                    if (noise2)
                    {
                        voxelValue = this.GetBlockIndex(lode.blockName);
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
    public bool isTransparent;
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

    public VoxelMod(Vector3 position, string id, World world)
    {
        this.position = position;
        this.id = world.GetBlockIndex(id);
    }
}

[System.Serializable]
public struct FaceUVs
{
    public Vector2 uv00, uv01, uv10, uv11; // min-min, min-max, max-min, max-max
}