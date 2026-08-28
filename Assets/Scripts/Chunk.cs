using UnityEngine;
using System.Collections.Generic;
using System.Threading;

public class Chunk
{

    GameObject chunkObject;
    World world;
    public ChunkCoord chunkCoord;
    public MeshRenderer meshRenderer;
    public MeshFilter meshFilter;
    public Vector3 position;

    int vertexIndex = 0;

    List<Vector3> vertices = new List<Vector3>();
    List<int> triangles = new List<int>();
    List<int> transparentTriangles = new List<int>();
    Material[] materials = new Material[2];
    List<Vector2> uvs = new List<Vector2>();

    public ushort [,,] voxelMap = new ushort[VoxelData.ChunkWidth, VoxelData.ChunkHeight, VoxelData.ChunkWidth];
    public Queue<VoxelMod> modifications = new Queue<VoxelMod>();

    private bool _isActive;
    private bool isThreadLocked;
    private bool isVoxelMapPopulated = false;

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

        position = chunkObject.transform.position;   // now correctly reflects the chunk's actual world position
        chunkObject.name += " " + (position.x/VoxelData.ChunkWidth) + "," + (position.z/VoxelData.ChunkWidth); 

        Thread thread = new Thread(new ThreadStart(PopulateVoxelMap));
        thread.Start();
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
        _updateChunk();
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
                
                AddTexture(blockID, j);

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

    public void UpdateChunk()
    {
       Thread thread = new Thread(new ThreadStart(_updateChunk));
       thread.Start();
    }

    private void _updateChunk()
    {
        isThreadLocked = true;

        lock (modifications)
        {
            while (modifications.Count > 0)
            {
                VoxelMod v = modifications.Dequeue();
                Vector3 pos = v.position -= position;
                voxelMap[(int)pos.x, (int)pos.y, (int)pos.z] = v.id;
            }
        }

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
        lock (world.chunksToDraw)
        {
            world.chunksToDraw.Enqueue(this);
        }

        isThreadLocked = false;
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

    public bool isEditable
    {
        get
        {
            if(!isVoxelMapPopulated || isThreadLocked)
            {
                return false;
            }
            return true;
        }
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

        _updateChunk();
    }

    void UpdateSurroundingVoxels(int x, int y, int z)
    {
        Vector3 thisVoxel = new Vector3(x,y,z);
        for (int j = 0; j < 6; j++)
        {
            Vector3 currentVoxel = thisVoxel + VoxelData.faceChecks[j];
            if (!isVoxelInChunk((int) currentVoxel.x, (int) currentVoxel.y, (int)currentVoxel.z))
            {
                world.getChunkFromVector3(currentVoxel+position).UpdateChunk();
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

    public void CreateMesh()
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

    void AddTexture(ushort blockID, int faceIndex)
    {
        FaceUVs uv = world.faceUVCache[blockID, faceIndex];

        uvs.Add(uv.uv00);
        uvs.Add(uv.uv01);
        uvs.Add(uv.uv10);
        uvs.Add(uv.uv11);
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
