using UnityEngine;
using System.Collections.Generic;

public class Chunk
{

    GameObject chunkObject;
    public ChunkCoord chunkCoord;

    public MeshRenderer meshRenderer;
    public MeshFilter meshFilter;

    int vertexIndex = 0;
    List<Vector3> vertices = new List<Vector3>();
    List<int> triangles = new List<int>();
    List<int> transparentTriangles = new List<int>();
    Material[] materials = new Material[2];
    List<Vector2> uvs = new List<Vector2>();

    public ushort [,,] voxelMap = new ushort[VoxelData.ChunkWidth, VoxelData.ChunkHeight, VoxelData.ChunkWidth];

    World world;

    private bool _isActive;
    public bool isVoxelMapPopulated = false;

    public Chunk(ChunkCoord chunkCoord, World world, bool generateOnLoad)
    {
        this.chunkCoord = chunkCoord;
        this.world = world;
        isActive = true;

        if (generateOnLoad)
        {
            Init();
        }
    }

    public void Init()
    {
        chunkObject = new GameObject("Chunk");

        meshFilter = chunkObject.AddComponent<MeshFilter>();
        meshRenderer = chunkObject.AddComponent<MeshRenderer>();

        materials[0] = world.material;
        materials[1] = world.transparentMaterial;
        meshRenderer.materials = materials;

        chunkObject.transform.SetParent(world.transform);
        chunkObject.transform.position = new Vector3(chunkCoord.x * VoxelData.ChunkWidth, 0, chunkCoord.z * VoxelData.ChunkWidth);
        chunkObject.name = "Chunk " + chunkCoord.x + ", " + chunkCoord.z;

        PopulateVoxelMap();
        PopulateChunk();
    }

    void PopulateVoxelMap()
    {
        for (int y = 0; y < VoxelData.ChunkHeight; y++)
        {
            for (int x = 0; x < VoxelData.ChunkWidth; x++)
            {
                for (int z = 0; z < VoxelData.ChunkWidth; z++)
                {
                    voxelMap[x, y, z] = world.GetVoxel(new Vector3(x, y, z) + position);
                }
            }
        }
        isVoxelMapPopulated = true;
    }

    void AddVoxelDataToChunk(Vector3 position)
    {
        for (int j = 0; j < 6; j++)
        {
            ushort blockID = voxelMap[(int)position.x, (int)position.y, (int)position.z];
            bool isTransparent = world.blockTypes[blockID].isTransparent;

            if(checkVoxel(position + VoxelData.faceChecks[j]))
            {
                vertices.Add(VoxelData.voxelVerts[VoxelData.voxelTris[j, 0]] + position);
                vertices.Add(VoxelData.voxelVerts[VoxelData.voxelTris[j, 1]] + position);
                vertices.Add(VoxelData.voxelVerts[VoxelData.voxelTris[j, 2]] + position);
                vertices.Add(VoxelData.voxelVerts[VoxelData.voxelTris[j, 3]] + position);
                
                AddTexture(world.blockTypes[blockID].GetTextureID(j));

                if (!isTransparent)
                {
                    triangles.Add(vertexIndex);
                    triangles.Add(vertexIndex + 1);
                    triangles.Add(vertexIndex + 2);
                    triangles.Add(vertexIndex + 2);
                    triangles.Add(vertexIndex + 1);
                    triangles.Add(vertexIndex + 3);
                }
                else
                {
                    transparentTriangles.Add(vertexIndex);
                    transparentTriangles.Add(vertexIndex + 1);
                    transparentTriangles.Add(vertexIndex + 2);
                    transparentTriangles.Add(vertexIndex + 2);
                    transparentTriangles.Add(vertexIndex + 1);
                    transparentTriangles.Add(vertexIndex + 3);
                }
                vertexIndex += 4;
                    
            }            
        }
    }

    void PopulateChunk()
    {
        ClearMeshData();
        for (int y = 0; y < VoxelData.ChunkHeight; y++)
        {
            for (int x = 0; x < VoxelData.ChunkWidth; x++)
            {
                for (int z = 0; z < VoxelData.ChunkWidth; z++)
                {
                    if (world.blockTypes[voxelMap[x, y, z]].isSolid)
                    {
                        AddVoxelDataToChunk(new Vector3(x, y, z));
                    }
                }
            }
        }
        CreateMesh();
    }

    void ClearMeshData()
    {
        vertexIndex = 0;
        vertices.Clear();
        triangles.Clear();
        transparentTriangles.Clear();
        uvs.Clear();
    }

    public bool isActive
    {
        get { return _isActive; }
        set 
        { 
            _isActive = value; 
            if(chunkObject != null)
            {
                chunkObject.SetActive(value);
            }
        }
    }

    public Vector3 position
    {
        get { return chunkObject.transform.position; }
    }

    bool isVoxelInChunk(int x, int y, int z)
    {
        if (x < 0 || x >= VoxelData.ChunkWidth || y < 0 || y >= VoxelData.ChunkHeight || z < 0 || z >= VoxelData.ChunkWidth)
        {
            return false;
        }
        return true;
    }

    public void EditVoxel (Vector3 pos, string newBlock)
    {
        int xCheck = Mathf.FloorToInt(pos.x - position.x);
        int yCheck = Mathf.FloorToInt(pos.y);
        int zCheck = Mathf.FloorToInt(pos.z - position.z);

        if (!isVoxelInChunk(xCheck, yCheck, zCheck) || voxelMap[xCheck, yCheck, zCheck] == world.GetBlockIndex("Bedrock"))
        {
            return;
        }

        voxelMap[xCheck, yCheck, zCheck] = world.GetBlockIndex(newBlock);

        UpdateSurroundingVoxels(xCheck, yCheck, zCheck);

        PopulateChunk();
    }

    void UpdateSurroundingVoxels(int x, int y, int z)
    {
        Vector3 thisVoxel = new Vector3(x,y,z);
        for (int j = 0; j < 6; j++)
        {
            Vector3 currentVoxel = thisVoxel + VoxelData.faceChecks[j];
            if (!isVoxelInChunk((int) currentVoxel.x, (int) currentVoxel.y, (int)currentVoxel.z))
            {
                world.getChunkFromVector3(currentVoxel+position).PopulateChunk();
            }
        }
    }
    
    public ushort GetVoxelFromGlobalVector3(Vector3 pos)
    {
        int xCheck = Mathf.FloorToInt(pos.x - position.x);
        int yCheck = Mathf.FloorToInt(pos.y);
        int zCheck = Mathf.FloorToInt(pos.z - position.z);

        if (!isVoxelInChunk(xCheck, yCheck, zCheck))
        {
            return 0;
        }

        return voxelMap[xCheck, yCheck, zCheck];
    }

    bool checkVoxel(Vector3 pos)
    {
        int x = Mathf.FloorToInt(pos.x);
        int y = Mathf.FloorToInt(pos.y);
        int z = Mathf.FloorToInt(pos.z);

        if (!isVoxelInChunk(x, y, z))
        {
            return world.CheckIfVoxelTransparent(pos + position);
        }

        return world.blockTypes[voxelMap[x, y, z]].isTransparent;
    }

    void CreateMesh()
    {
        Mesh mesh = new Mesh();
        mesh.vertices = vertices.ToArray();
        mesh.subMeshCount = 2;
        mesh.SetTriangles(triangles.ToArray(), 0);
        mesh.SetTriangles(transparentTriangles.ToArray(), 1);
        mesh.uv = uvs.ToArray();
        mesh.RecalculateNormals();

        meshFilter.mesh = mesh;
    }

    void AddTexture(Sprite sprite)
    {
            if (sprite == null)
            {
                // Textureless face
                uvs.Add(Vector2.zero);
                uvs.Add(Vector2.zero);
                uvs.Add(Vector2.zero);
                uvs.Add(Vector2.zero);
                return;
            }

            Rect rect = sprite.textureRect;

            float texWidth = sprite.texture.width;
            float texHeight = sprite.texture.height;

            float xMin = rect.xMin / texWidth;
            float xMax = rect.xMax / texWidth;

            float yMin = rect.yMin / texHeight;
            float yMax = rect.yMax / texHeight;

            uvs.Add(new Vector2(xMin, yMin));
            uvs.Add(new Vector2(xMin, yMax));
            uvs.Add(new Vector2(xMax, yMin));
            uvs.Add(new Vector2(xMax, yMax));
            }

}

public class ChunkCoord
{
    public int x;
    public int z;

    public ChunkCoord()
    {
        this.x = 0;
        this.z = 0;
    }
    public ChunkCoord(Vector3 pos)
    {
        int xCheck = Mathf.FloorToInt(pos.x);
        int zCheck = Mathf.FloorToInt(pos.z);

        this.x = xCheck/VoxelData.ChunkWidth;
        this.z = zCheck/VoxelData.ChunkWidth;
    }

    public ChunkCoord(int x, int z)
    {
        this.x = x;
        this.z = z;
    }

    public bool Equals(ChunkCoord other)
    {
        return x == other.x && z == other.z;
    }
}
