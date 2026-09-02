using UnityEngine;
using System.Collections.Generic;

public class Clouds : MonoBehaviour
{
    public int cloudHeight = 100;
    [SerializeField] private Texture2D cloudPattern = null;
    [SerializeField] private Material cloudMaterial = null;
    [SerializeField] private World world = null;
    bool[,] cloudData;

    int cloudTexWidth;
    int cloudTileSize;
    Vector3Int offset;

    Dictionary<Vector2Int, GameObject> clouds = new Dictionary<Vector2Int, GameObject>();

    private void Start()
    {
        cloudTexWidth = cloudPattern.width;
        cloudTileSize = VoxelData.ChunkWidth; // Set an appropriate value
        offset = new Vector3Int(-cloudTexWidth / 2, 0, -cloudTexWidth / 2);
        transform.position = new Vector3(VoxelData.WorldCenter, cloudHeight, VoxelData.WorldCenter);

        LoadCloudData();
        CreateClouds();
    }

    private void LoadCloudData()
    {
        cloudData = new bool[cloudTexWidth, cloudTexWidth];
        Color[] cloudTex = cloudPattern.GetPixels();

        for (int x = 0; x < cloudTexWidth; x++)
        {
            for (int z = 0; z < cloudTexWidth; z++)
            {
                Color pixelColor = cloudTex[x + z * cloudTexWidth];
                cloudData[x, z] = pixelColor.a > 0;
            }
        }
    }

    private void CreateClouds()
    {
        for (int x = 0; x < cloudTexWidth; x += cloudTileSize)
        {
            for (int z = 0; z < cloudTexWidth; z += cloudTileSize)
            {
                Vector3 position = new Vector3(x, cloudHeight, z);
                clouds.Add(CloudTilePosFromV3(position), CreateCloudTile(CreateCloudMesh(x, z), position));
            }
        }
    }

    public void UpdateClouds()
    {
        for (int x = 0; x < cloudTexWidth; x += cloudTileSize)
        {
            for (int z = 0; z < cloudTexWidth; z += cloudTileSize)
            {
                Vector3 position = world.player.position + new Vector3(x, 0, z) + offset;
                position = new Vector3(RoundToCloud(position.x), cloudHeight, RoundToCloud(position.z));
                Vector2Int cloudTilePos = CloudTilePosFromV3(position);

                clouds[cloudTilePos].transform.position = position;
            }
        }
    }

    private int RoundToCloud(float value)
    {
        return Mathf.FloorToInt(value / cloudTileSize) * cloudTileSize;
    }

    private Mesh CreateCloudMesh(int x, int z)
    {

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector3> normals = new List<Vector3>();

        int vertCount = 0;

        for (int i = 0; i < cloudTileSize; i++)
        {
            for (int j = 0; j < cloudTileSize; j++)
            {
                // Wrap so a tile that overruns the texture edge samples from the
                // opposite side instead of running off the array.
                int xVal = (x + i) % cloudTexWidth;
                int zVal = (z + j) % cloudTexWidth;

                if (cloudData[xVal, zVal])
                {
                    // Local to the tile - the tile GameObject carries the world offset.
                    Vector3 cloudPos = new Vector3(i, 0, j);
                    // Top face
                    vertices.Add(cloudPos + new Vector3(0, 0, 0));
                    vertices.Add(cloudPos + new Vector3(0, 0, 1));
                    vertices.Add(cloudPos + new Vector3(1, 0, 1));
                    vertices.Add(cloudPos + new Vector3(1, 0, 0));

                    for (int m = 0; m < 4; m++)
                    {
                        normals.Add(Vector3.down);
                    }

                    triangles.Add(vertCount + 1);
                    triangles.Add(vertCount);
                    triangles.Add(vertCount + 2);
                    triangles.Add(vertCount + 2);
                    triangles.Add(vertCount);
                    triangles.Add(vertCount + 3);

                    vertCount += 4;
                }
            }
        }

        Mesh mesh = new Mesh();
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.normals = normals.ToArray();
        return mesh;
    }

    private GameObject CreateCloudTile(Mesh mesh, Vector3 position)
    {
        GameObject cloudTile = new GameObject();
        cloudTile.transform.position = position;
        cloudTile.transform.parent = transform;
        cloudTile.name = "CloudTile_" + position.x + ", " + position.z;
        MeshFilter meshFilter = cloudTile.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = cloudTile.AddComponent<MeshRenderer>();

        meshRenderer.material = cloudMaterial;
        meshFilter.mesh = mesh;

        return cloudTile;
    }

    private Vector2Int CloudTilePosFromV3(Vector3 pos)
    {
        return new Vector2Int(CloudTileCoordFromFloat(pos.x), CloudTileCoordFromFloat(pos.z));
    }
    
    private int CloudTileCoordFromFloat(float coord)
    {
        float a = coord / (float)cloudTexWidth;
        a -= Mathf.FloorToInt(a); // Wrap to [0, 1)
        int b = Mathf.FloorToInt(a * (float)cloudTexWidth);
        return b;
    }
}
