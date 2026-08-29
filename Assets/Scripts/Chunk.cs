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
    List<Vector2> uvs = new List<Vector2>();

    public BlockID [,,] voxelMap = new BlockID[VoxelData.ChunkWidth, VoxelData.ChunkHeight, VoxelData.ChunkWidth];
    public Queue<VoxelMod> modifications = new Queue<VoxelMod>();

    private bool _isActive;
    private bool isVoxelMapPopulated = false;

    public Chunk(ChunkCoord chunkCoord, World world)
    {
        this.chunkCoord = chunkCoord;
        this.world = world;
    }

    public void Init()
    {
        chunkObject = new GameObject("Chunk");

        meshFilter = chunkObject.AddComponent<MeshFilter>();
        meshRenderer = chunkObject.AddComponent<MeshRenderer>();

        meshRenderer.materials = new Material[] { world.material, world.transparentMaterial };

        chunkObject.transform.SetParent(world.transform);
        chunkObject.transform.position = new Vector3(chunkCoord.x * VoxelData.ChunkWidth, 0, chunkCoord.z * VoxelData.ChunkWidth);

        position = chunkObject.transform.position;   // now correctly reflects the chunk's actual world position
        chunkObject.name += " " + (position.x/VoxelData.ChunkWidth) + "," + (position.z/VoxelData.ChunkWidth);

        PopulateVoxelMap();
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
        lock (world.ChunkUpdateThreadLock)
        {
            world.chunksToUpdate.Add(this);
        }
    }

    // voxelPos is chunk-local (0..ChunkWidth-1), not a world position.
    void AddVoxelDataToChunk(Vector3 voxelPos)
    {
        BlockID blockID = voxelMap[(int)voxelPos.x, (int)voxelPos.y, (int)voxelPos.z];
        List<int> faceTriangles = world.blockTypes[(int)blockID].isTransparent ? transparentTriangles : triangles;

        for (int j = 0; j < 6; j++)
        {
            if (!CheckVoxel(voxelPos + VoxelData.faceChecks[j]))
            {
                continue;
            }

            for (int v = 0; v < 4; v++)
            {
                vertices.Add(VoxelData.voxelVerts[VoxelData.voxelTris[j, v]] + voxelPos);
            }

            AddTexture(blockID, j);

            faceTriangles.Add(vertexIndex);
            faceTriangles.Add(vertexIndex + 1);
            faceTriangles.Add(vertexIndex + 2);
            faceTriangles.Add(vertexIndex + 2);
            faceTriangles.Add(vertexIndex + 1);
            faceTriangles.Add(vertexIndex + 3);

            vertexIndex += 4;
        }
    }

    public void UpdateChunk()
    {

        lock (modifications)
        {
            while (modifications.Count > 0)
            {
                VoxelMod v = modifications.Dequeue();
                Vector3 pos = v.position - position;
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
                    if (world.blockTypes[(int)voxelMap[x, y, z]].isSolid)
                    {
                        AddVoxelDataToChunk(new Vector3(x, y, z));
                    }
                }
            }
        }
        world.chunksToDraw.Enqueue(this);
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
            if(!isVoxelMapPopulated)
            {
                return false;
            }
            return true;
        }
    }

    bool IsVoxelInChunk(int x, int y, int z)
    {
        if (x < 0 || x >= VoxelData.ChunkWidth || y < 0 || y >= VoxelData.ChunkHeight || z < 0 || z >= VoxelData.ChunkWidth)
        {
            return false;
        }
        return true;
    }

    public void EditVoxel (Vector3 pos, BlockID newBlock)
    {
        int xCheck = Mathf.FloorToInt(pos.x - position.x);
        int yCheck = Mathf.FloorToInt(pos.y);
        int zCheck = Mathf.FloorToInt(pos.z - position.z);

        if (!IsVoxelInChunk(xCheck, yCheck, zCheck) || voxelMap[xCheck, yCheck, zCheck] == BlockID.Bedrock)
        {
            return;
        }

        voxelMap[xCheck, yCheck, zCheck] = newBlock;

        lock (world.ChunkUpdateThreadLock)
        {
            world.chunksToUpdate.Insert(0,this);
            UpdateSurroundingVoxels(xCheck, yCheck, zCheck);
        }
    }

    void UpdateSurroundingVoxels(int x, int y, int z)
    {
        Vector3 thisVoxel = new Vector3(x,y,z);
        for (int j = 0; j < 6; j++)
        {
            Vector3 currentVoxel = thisVoxel + VoxelData.faceChecks[j];
            if (!IsVoxelInChunk((int) currentVoxel.x, (int) currentVoxel.y, (int)currentVoxel.z))
            {
                world.chunksToUpdate.Insert(0,world.GetChunkFromVector3(currentVoxel + position));
            }
        }
    }
    
    public BlockID GetVoxelFromGlobalVector3(Vector3 pos)
    {
        int xCheck = Mathf.FloorToInt(pos.x - position.x);
        int yCheck = Mathf.FloorToInt(pos.y);
        int zCheck = Mathf.FloorToInt(pos.z - position.z);

        if (!IsVoxelInChunk(xCheck, yCheck, zCheck))
        {
            return BlockID.Air;
        }

        return voxelMap[xCheck, yCheck, zCheck];
    }

    bool CheckVoxel(Vector3 pos)
    {
        int x = Mathf.FloorToInt(pos.x);
        int y = Mathf.FloorToInt(pos.y);
        int z = Mathf.FloorToInt(pos.z);

        if (!IsVoxelInChunk(x, y, z))
        {
            return world.CheckIfVoxelTransparent(pos + position);
        }

        return world.blockTypes[(int)voxelMap[x, y, z]].isTransparent;
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

    void AddTexture(BlockID blockID, int faceIndex)
    {
        FaceUVs uv = world.faceUVCache[(int)blockID, faceIndex];

        uvs.Add(uv.uv00);
        uvs.Add(uv.uv01);
        uvs.Add(uv.uv10);
        uvs.Add(uv.uv11);
    }

}

public class ChunkCoord : System.IEquatable<ChunkCoord>
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
        return other != null && x == other.x && z == other.z;
    }

    // Needed so List.Contains/Remove compare by value. Without these overrides
    // EqualityComparer<ChunkCoord>.Default falls back to reference equality, because
    // the typed Equals above overloads rather than overrides object.Equals.
    public override bool Equals(object obj)
    {
        return Equals(obj as ChunkCoord);
    }

    public override int GetHashCode()
    {
        return (x * 397) ^ z;
    }
}
