using UnityEngine;
using System.Collections.Generic;
using System.Threading;

public class Chunk
{

    public GameObject chunkObject;
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
    List<Color> colors = new List<Color>();
    List<Vector3> normals = new List<Vector3>();

    public VoxelState [,,] voxelMap = new VoxelState[VoxelData.ChunkWidth, VoxelData.ChunkHeight, VoxelData.ChunkWidth];
    public Queue<VoxelMod> modifications = new Queue<VoxelMod>();
    public object buildLock = new object();

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

        meshRenderer.materials = new Material[]
        {
            world.material,
            world.transparentMaterial
        };

        chunkObject.transform.SetParent(world.transform);

        chunkObject.transform.position =
            new Vector3(
                chunkCoord.x * VoxelData.ChunkWidth,
                0,
                chunkCoord.z * VoxelData.ChunkWidth
            );

        position = chunkObject.transform.position;

        chunkObject.name += " " +
            (position.x / VoxelData.ChunkWidth) + "," +
            (position.z / VoxelData.ChunkWidth);

        isActive = true;

        world.QueueForPopulation(this);

        if (world.settings.doChunkAnimation)
        {
            chunkObject.AddComponent<ChunkAnimation>();
        }
    }

    public void PopulateVoxelMap()
    {
        for (int y = 0; y < VoxelData.ChunkHeight; y++)
        {
            for (int x = 0; x < VoxelData.ChunkWidth; x++)
            {
                for (int z = 0; z < VoxelData.ChunkWidth; z++)
                {
                    voxelMap[x, y, z] = new VoxelState(world.GetVoxel(new Vector3(x, y, z) + position));
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

        int x = Mathf.FloorToInt(voxelPos.x);
        int y = Mathf.FloorToInt(voxelPos.y);
        int z = Mathf.FloorToInt(voxelPos.z);

        ushort blockID = voxelMap[x,y,z].id;
        List<int> faceTriangles = triangles;
        if (world.blockTypes[blockID].renderNeighborFaces)
        {
            faceTriangles = transparentTriangles;
        }

        for (int j = 0; j < 6; j++)
        {

            VoxelState neighbor = CheckVoxel(voxelPos + VoxelData.faceChecks[j]);

            if (neighbor == null || !world.blockTypes[neighbor.id].renderNeighborFaces)
            {
                continue;
            }

            for (int v = 0; v < 4; v++)
            {
                vertices.Add(VoxelData.voxelVerts[VoxelData.voxelTris[j, v]] + voxelPos);
                normals.Add(VoxelData.faceChecks[j]);
            }

            AddTexture(blockID, j);

            float lightLevel = neighbor.globalLightPercent;



            colors.Add(new Color(0, 0, 0, lightLevel));
            colors.Add(new Color(0, 0, 0, lightLevel));
            colors.Add(new Color(0, 0, 0, lightLevel));
            colors.Add(new Color(0, 0, 0, lightLevel));

            faceTriangles.Add(vertexIndex);
            faceTriangles.Add(vertexIndex + 1);
            faceTriangles.Add(vertexIndex + 2);
            faceTriangles.Add(vertexIndex + 2);
            faceTriangles.Add(vertexIndex + 1);
            faceTriangles.Add(vertexIndex + 3);

            vertexIndex += 4;
        }
    }

    public void UpdateChunk(bool queueForDraw = true)
    {

        lock (modifications)
        {
            while (modifications.Count > 0)
            {
                VoxelMod v = modifications.Dequeue();
                Vector3 pos = v.position - position;
                voxelMap[(int)pos.x, (int)pos.y, (int)pos.z].id = v.id;
            }
        }

        ClearMeshData();

        CalculateLight();

        for (int y = 0; y < VoxelData.ChunkHeight; y++)
        {
            for (int x = 0; x < VoxelData.ChunkWidth; x++)
            {
                for (int z = 0; z < VoxelData.ChunkWidth; z++)
                {
                    if (world.blockTypes[(int)voxelMap[x, y, z].id].isSolid)
                    {
                        AddVoxelDataToChunk(new Vector3(x, y, z));
                    }
                }
            }
        }
        if (queueForDraw)
        {
            world.chunksToDraw.Enqueue(this);
        }
    }

    void CalculateLight()
    {

        Queue<Vector3Int> litVoxels = new Queue<Vector3Int>();

        for (int x = 0; x < VoxelData.ChunkWidth; x++) {
            for (int z = 0; z < VoxelData.ChunkWidth; z++) {

                float lightRay = 1f;

                for (int y = VoxelData.ChunkHeight - 1; y >= 0; y--) {

                    VoxelState thisVoxel = voxelMap[x, y, z];

                    if (thisVoxel.id > 0 && world.blockTypes[thisVoxel.id].transparency < lightRay)
                    {
                        lightRay = world.blockTypes[thisVoxel.id].transparency;
                    }

                    thisVoxel.globalLightPercent = lightRay;

                    voxelMap[x, y, z] = thisVoxel;

                    if (lightRay > VoxelData.lightFalloff)
                    {
                        litVoxels.Enqueue(new Vector3Int(x, y, z));
                    }

                }
            }
        }
        while (litVoxels.Count > 0)
        {
            Vector3Int v = litVoxels.Dequeue();
            for (int p = 0; p <6; p++)
            {
                Vector3 currentVoxel = v + VoxelData.faceChecks[p];
                Vector3Int neighbor = new Vector3Int((int)currentVoxel.x, (int)currentVoxel.y, (int)currentVoxel.z);
                if (IsVoxelInChunk(neighbor.x, neighbor.y, neighbor.z))
                {
                    if (voxelMap[neighbor.x, neighbor.y, neighbor.z].globalLightPercent < voxelMap[v.x,v.y,v.z].globalLightPercent - VoxelData.lightFalloff)
                    {
                        voxelMap[neighbor.x, neighbor.y, neighbor.z].globalLightPercent = voxelMap[v.x, v.y, v.z].globalLightPercent - VoxelData.lightFalloff;

                        if (voxelMap[neighbor.x, neighbor.y, neighbor.z].globalLightPercent > VoxelData.lightFalloff && !world.blockTypes[voxelMap[neighbor.x, neighbor.y, neighbor.z].id].isSolid)
                        {
                            litVoxels.Enqueue(neighbor);
                        }
                    }
                }
            }
        }
    }

    void ClearMeshData()
    {
        vertexIndex = 0;
        vertices.Clear();
        triangles.Clear();
        transparentTriangles.Clear();
        uvs.Clear();
        colors.Clear();
        normals.Clear();
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

    public void EditVoxel (Vector3 pos, ushort newBlock)
    {
        int xCheck = Mathf.FloorToInt(pos.x - position.x);
        int yCheck = Mathf.FloorToInt(pos.y);
        int zCheck = Mathf.FloorToInt(pos.z - position.z);

        if (!IsVoxelInChunk(xCheck, yCheck, zCheck) || voxelMap[xCheck, yCheck, zCheck].id == (ushort)BlockID.Bedrock)
        {
            return;
        }

        voxelMap[xCheck, yCheck, zCheck].id = newBlock;

        lock (world.ChunkUpdateThreadLock)
        {
            // Rebuild this chunk immediately so the edit is visible this frame
            // instead of waiting behind the startup mesh queue.
            world.chunksToUpdate.Remove(this);
            lock (buildLock)
            {
                UpdateChunk(false);
                CreateMesh();
            }
            UpdateSurroundingVoxels(xCheck, yCheck, zCheck);
        }
    }

    void UpdateSurroundingVoxels(int x, int y, int z)
    {
        Vector3 thisVoxel = new Vector3(x, y, z);

        for (int j = 0; j < 6; j++)
        {
            Vector3 currentVoxel = thisVoxel + VoxelData.faceChecks[j];

            if (!IsVoxelInChunk(
                (int)currentVoxel.x,
                (int)currentVoxel.y,
                (int)currentVoxel.z))
            {
                Vector3 globalVoxel = currentVoxel + position;
                ChunkCoord coord = world.GetChunkCoordFromVector3(globalVoxel);

                // Don't try to access a chunk outside the world.
                if (!world.IsChunkInWorld(coord))
                    continue;

                Chunk neighborChunk = world.GetChunkFromVector3(globalVoxel);

                // Caller holds ChunkUpdateThreadLock, so the list is safe to inspect.
                // Without the Contains check an edge block queues the same neighbor
                // once per touching face, and each duplicate is a full 32k-voxel
                // rebuild that delays the mesh the player is waiting to see.
                if (neighborChunk != null && !world.chunksToUpdate.Contains(neighborChunk))
                {
                    world.chunksToUpdate.Insert(0, neighborChunk);
                }
            }
        }
    }

    public VoxelState GetVoxelFromGlobalVector3(Vector3 pos)
    {
        int xCheck = Mathf.FloorToInt(pos.x - position.x);
        int yCheck = Mathf.FloorToInt(pos.y);
        int zCheck = Mathf.FloorToInt(pos.z - position.z);

        if (!IsVoxelInChunk(xCheck, yCheck, zCheck))
        {
            return new VoxelState((ushort)BlockID.Air);
        }

        return voxelMap[xCheck, yCheck, zCheck];
    }

    VoxelState CheckVoxel(Vector3 pos)
    {
        int x = Mathf.FloorToInt(pos.x);
        int y = Mathf.FloorToInt(pos.y);
        int z = Mathf.FloorToInt(pos.z);

        Vector3 globalPos = pos + position;

        // Outside the world is air.
        if (!world.IsVoxelInWorld(globalPos))
        {
            return new VoxelState((ushort)BlockID.Air);
        }

        // Neighbor is in this chunk.
        if (IsVoxelInChunk(x, y, z))
        {
            return voxelMap[x, y, z];
        }

        // Neighbor is in another chunk.
        return world.GetVoxelState(globalPos);
    }

    public void CreateMesh()
    {
        Mesh mesh = new Mesh();
        mesh.vertices = vertices.ToArray();
        mesh.subMeshCount = 2;
        mesh.SetTriangles(triangles.ToArray(), 0);
        mesh.SetTriangles(transparentTriangles.ToArray(), 1);
        //mesh.triangles = triangles.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.colors = colors.ToArray();
        mesh.normals = normals.ToArray();

        //mesh.RecalculateNormals();

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

public class VoxelState
{
    public ushort id;
    public float globalLightPercent;

    public VoxelState()
    {
        id = (ushort)BlockID.Air;
        globalLightPercent = 0f;
    }

    public VoxelState(ushort id)
    {
        this.id = id;
        globalLightPercent = 0f;
    }
}