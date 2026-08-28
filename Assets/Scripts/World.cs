using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using UnityEngine.U2D;

public class World : MonoBehaviour
    {
        public int seed;
        public Transform player;
        public Vector3 spawnPosition;
        public Material material;
        public Material transparentMaterial;
        public BlockType[] blockTypes;
        public BiomeAttributes biome;
        public ChunkCoord playerLastChunkCoord;
        private bool isCreatingChunks;

        public SpriteAtlas blockAtlas;

        public GameObject debugScreen;

        Chunk[,] chunks = new Chunk[VoxelData.WorldSizeInChunks, VoxelData.WorldSizeInChunks];
        List<ChunkCoord> activeChunks = new List<ChunkCoord>();

        List<ChunkCoord> chunksToCreate = new List<ChunkCoord>();

        private Dictionary<string, ushort> blockNameToID;

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

            spawnPosition = new Vector3(spawnX+0.5f, spawnY+2, spawnZ+0.5f);

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
            if (chunksToCreate.Count > 0 && !isCreatingChunks)
            {
                StartCoroutine("CreateChunks");
            }
            if (Input.GetKeyDown(KeyCode.F3))
            {
                debugScreen.SetActive(!debugScreen.activeSelf);
            }
        }

        void GenerateWorld()
        {
                for (int x = VoxelData.WorldSizeInChunks/2 - VoxelData.ViewDistanceInChunks/2; x < VoxelData.WorldSizeInChunks/2 + VoxelData.ViewDistanceInChunks/2; x++)
                {
                        for (int z = VoxelData.WorldSizeInChunks/2 - VoxelData.ViewDistanceInChunks/2; z < VoxelData.WorldSizeInChunks/2 + VoxelData.ViewDistanceInChunks/2; z++)
                        {
                            ChunkCoord thisChunk = new ChunkCoord(x,z);
                            chunks[x,z] = new Chunk(thisChunk, this, true);
                            activeChunks.Add(thisChunk);
                        }
                }
                player.position = spawnPosition;
        }

        IEnumerator CreateChunks()
        {
            isCreatingChunks = true;

            while(chunksToCreate.Count > 0)
            {
                chunks[chunksToCreate[0].x, chunksToCreate[0].z].Init();
                chunksToCreate.RemoveAt(0);
                yield return null;
            }

            isCreatingChunks = false;
        }

        ChunkCoord GetChunkCoordFromVector3(Vector3 pos)
        {
                int x = Mathf.FloorToInt(pos.x / VoxelData.ChunkWidth);
                int z = Mathf.FloorToInt(pos.z / VoxelData.ChunkWidth);
                return new ChunkCoord(x, z);
        }

        public Chunk getChunkFromVector3(Vector3 pos)
        {
            {
                int x = Mathf.FloorToInt(pos.x / VoxelData.ChunkWidth);
                int z = Mathf.FloorToInt(pos.z / VoxelData.ChunkWidth);
                return chunks[x,z];
        }
        }

        void CheckViewDistance()
        {
            List<ChunkCoord> previouslyActiveChunks = new List<ChunkCoord>(activeChunks);
            ChunkCoord playerChunkCoord = GetChunkCoordFromVector3(player.position);
            playerLastChunkCoord = playerChunkCoord;
            for (int x = playerChunkCoord.x - VoxelData.ViewDistanceInChunks; x < playerChunkCoord.x + VoxelData.ViewDistanceInChunks; x++)
            {
                for (int z = playerChunkCoord.z - VoxelData.ViewDistanceInChunks; z < playerChunkCoord.z + VoxelData.ViewDistanceInChunks; z++)
                {
                    ChunkCoord thisChunk = new ChunkCoord(x, z);
                    if (isChunkInWorld(thisChunk))
                    {

                        if (chunks[x, z] == null)
                        {
                            chunks[x,z] = new Chunk(thisChunk, this, false);
                            chunksToCreate.Add(thisChunk);
                        }
                        else if (!chunks[x, z].isActive)
                        {
                            chunks[x, z].isActive = true;
                        }
                        activeChunks.Add(thisChunk);
                    }
                    for (int i = 0; i < previouslyActiveChunks.Count; i++) {

                        if (previouslyActiveChunks[i].x == x && previouslyActiveChunks[i].z == z)
                            previouslyActiveChunks.RemoveAt(i);

                    }
                }
            }
            foreach (ChunkCoord chunk in previouslyActiveChunks)
            {
                chunks[chunk.x, chunk.z].isActive = false;
                activeChunks.Remove(chunk);
            }
        }

        public bool CheckForVoxel(Vector3 pos)
        {
            ChunkCoord thisChunk = new ChunkCoord(pos);

            if (!isVoxelInWorld(pos))
            {
                return false;
            }
            if (chunks[thisChunk.x, thisChunk.z] != null && chunks[thisChunk.x, thisChunk.z].isVoxelMapPopulated)
            {
                return this.blockTypes[chunks[thisChunk.x, thisChunk.z].GetVoxelFromGlobalVector3(pos)].isSolid;
            }
            return this.blockTypes[GetVoxel(pos)].isSolid;
        }

        public bool CheckIfVoxelTransparent(Vector3 pos)
        {
            ChunkCoord thisChunk = new ChunkCoord(pos);

            if (!isVoxelInWorld(pos))
            {
                return false;
            }
            if (chunks[thisChunk.x, thisChunk.z] != null && chunks[thisChunk.x, thisChunk.z].isVoxelMapPopulated)
            {
                return this.blockTypes[chunks[thisChunk.x, thisChunk.z].GetVoxelFromGlobalVector3(pos)].isTransparent;
            }
            return this.blockTypes[GetVoxel(pos)].isTransparent;
        }

        public ushort GetVoxel(Vector3 pos)
        {
            int yPos = Mathf.FloorToInt(pos.y);
            // Immutable Pass

            // if outside return air
            if (!isVoxelInWorld(pos))
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
                voxelValue = this.GetBlockIndex("Grass");;
            }
            else if (yPos < terrainHeight && yPos > terrainHeight - 4)
            {
                voxelValue = this.GetBlockIndex("Dirt");;
            }
            else if (yPos > terrainHeight)
            {
                return this.GetBlockIndex("Air");;
            }
            else
            {
                voxelValue = this.GetBlockIndex("Stone");;
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

            return voxelValue;
        }

        bool isChunkInWorld(ChunkCoord chunkCoord)
        {
                if (chunkCoord.x < 0 || chunkCoord.x >= VoxelData.WorldSizeInChunks || chunkCoord.z < 0 || chunkCoord.z >= VoxelData.WorldSizeInChunks)
                {
                        return false;
                }
                return true;
        }

        bool isVoxelInWorld(Vector3 pos)
        {
                if (pos.x < 0 || pos.x >= VoxelData.WorldSizeInVoxels || pos.y < 0 || pos.y >= VoxelData.ChunkHeight || pos.z < 0 || pos.z >= VoxelData.WorldSizeInVoxels)
                {
                        return false;
                }
                return true;
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

    public Sprite GetTextureID(int faceIndex)
    {
        switch (faceIndex)
        {
            case 0: // Back Face
                return backFaceTexture;
            case 1: // Front Face
                return frontFaceTexture;
            case 2: // Top Face
                return topFaceTexture;
            case 3: // Bottom Face
                return bottomFaceTexture;
            case 4: // Left Face
                return leftFaceTexture;
            case 5: // Right Face
                return rightFaceTexture;
            default:
                Debug.Log("Error in GetTextureID. Invalid face index.");
                return null;
        }
    }
}