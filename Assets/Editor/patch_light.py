import io

def patch(path, edits):
    src = io.open(path, encoding="utf-8").read()
    for old, new, count in edits:
        found = src.count(old)
        if found != count:
            raise SystemExit(f"{path}: expected {count} match(es), found {found} for:\n{old[:120]}")
        src = src.replace(old, new)
    io.open(path, "w", encoding="utf-8", newline="").write(src)
    print(f"{path} patched OK")

patch(r"Assets/Scripts/Chunk.cs", [
    # BUG 1: draw faces against neighbors that DO render their faces (air, leaves, glass).
    ("""            VoxelState neighbor = CheckVoxel(voxelPos + VoxelData.faceChecks[j]);
            
            if (neighbor == null || world.blockTypes[neighbor.id].renderNeighborFaces)
            {
                continue;
            }""",
     """            VoxelState neighbor = CheckVoxel(voxelPos + VoxelData.faceChecks[j]);

            if (neighbor != null && !world.blockTypes[neighbor.id].renderNeighborFaces)
            {
                continue;
            }""", 1),
    # BUG 2: population moves off the main thread. Init just queues the chunk.
    ("""        chunkObject.name += " " + (position.x/VoxelData.ChunkWidth) + "," + (position.z/VoxelData.ChunkWidth);

        PopulateVoxelMap();
    }

    void PopulateVoxelMap()""",
     """        chunkObject.name += " " + (position.x/VoxelData.ChunkWidth) + "," + (position.z/VoxelData.ChunkWidth);

        // Population is pure data work, so it runs on the worker thread instead of
        // spiking the main thread every time a new chunk comes into view.
        world.QueueForPopulation(this);
    }

    public void PopulateVoxelMap()""", 1),
])

patch(r"Assets/Scripts/World.cs", [
    ("""    public Queue<Chunk> chunksToDraw = new Queue<Chunk>();""",
     """    public Queue<Chunk> chunksToDraw = new Queue<Chunk>();
    System.Collections.Concurrent.ConcurrentQueue<Chunk> chunksToPopulate = new System.Collections.Concurrent.ConcurrentQueue<Chunk>();""", 1),
    ("""        ChunkUpdateThread = new Thread(new ThreadStart(ThreadedUpdate));
        ChunkUpdateThread.Start();""",
     """        ChunkUpdateThread = new Thread(new ThreadStart(ThreadedUpdate));
        ChunkUpdateThread.Start();
        ChunkUpdateThread.Priority = System.Threading.ThreadPriority.BelowNormal;""", 1),
    ("""            if (!applyingModifications)
            {
                ApplyModifications();
                didWork = true;
            }""",
     """            while (chunksToPopulate.TryDequeue(out Chunk toPopulate))
            {
                toPopulate.PopulateVoxelMap();
                didWork = true;
            }

            if (!applyingModifications)
            {
                ApplyModifications();
                didWork = true;
            }""", 1),
    ("""    public Chunk GetChunkFromVector3(Vector3 pos)""",
     """    public void QueueForPopulation(Chunk chunk)
    {
        chunksToPopulate.Enqueue(chunk);
    }

    public Chunk GetChunkFromVector3(Vector3 pos)""", 1),
])
