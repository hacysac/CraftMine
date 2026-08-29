import io

def patch(path, edits):
    src = io.open(path, encoding="utf-8").read()
    for old, new in edits:
        if src.count(old) != 1:
            raise SystemExit(f"{path}: expected 1 match, found {src.count(old)} for:\n{old[:100]}")
        src = src.replace(old, new)
    io.open(path, "w", encoding="utf-8", newline="").write(src)
    print(f"{path} patched OK")

# --- World.cs: delete lookup layer, replace all GetBlockIndex calls with casts ---
patch(r"Assets/Scripts/World.cs", [
    ("    Dictionary<BlockID, ushort> blockIDToIndex;\n", ""),
    ("""    // Auto-populates the BlockID -> index map by matching each enum member's
    // name (underscores read back as spaces) against the blockNames in blockTypes.
    void BuildBlockIDLookup()
    {
        blockIDToIndex = new Dictionary<BlockID, ushort>();

        for (ushort i = 0; i < blockTypes.Length; i++)
        {
            string name = blockTypes[i].blockName;

            if (string.IsNullOrEmpty(name))
            {
                Debug.LogWarning($"blockTypes[{i}] has no blockName set.");
                continue;
            }

            if (System.Enum.TryParse<BlockID>(name.Replace(' ', '_'), out BlockID id))
            {
                if (blockIDToIndex.TryAdd(id, i))
                    continue;
                Debug.LogWarning($"Duplicate BlockID.{id} at index {i} (already used by index {blockIDToIndex[id]}).");
            }
            else
            {
                Debug.LogWarning($"blockTypes[{i}] blockName '{name}' has no matching BlockID member. " +
                                 $"Re-run Tools > Generate BlockID Enum.");
            }
        }
    }

    public ushort GetBlockIndex(BlockID id)
    {
        if (blockIDToIndex.TryGetValue(id, out ushort index))
            return index;

        Debug.LogError($"BlockID.{id} has no matching entry in blockTypes.");
        return 0; // falls back to Air
    }
""",
     """    // BlockID member values are the blockTypes indices (the generator writes them
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

            if (!System.Enum.TryParse<BlockID>(name.Replace(' ', '_'), out BlockID id) || (ushort)id != i)
            {
                Debug.LogWarning($"blockTypes[{i}] blockName '{name}' does not match BlockID.{id} = {(int)id}. " +
                                 "Re-run Tools > Generate BlockID Enum.");
            }
        }
    }
"""),
    ("        BuildBlockIDLookup();", "        ValidateBlockIDs();"),
    ('this.GetBlockIndex(BlockID.Air)', '(ushort)BlockID.Air'),
    ('this.GetBlockIndex(BlockID.Bedrock)', '(ushort)BlockID.Bedrock'),
    ('this.GetBlockIndex(BlockID.Grass)', '(ushort)BlockID.Grass'),
    ('this.GetBlockIndex(BlockID.Dirt)', '(ushort)BlockID.Dirt'),
    ('this.GetBlockIndex(BlockID.Stone)', '(ushort)BlockID.Stone'),
    ('this.GetBlockIndex(lode.block)', '(ushort)lode.block'),
    ("        this.id = world.GetBlockIndex(id);", "        this.id = (ushort)id;"),
])

# --- Chunk.cs ---
patch(r"Assets/Scripts/Chunk.cs", [
    ('world.GetBlockIndex(BlockID.Bedrock)', '(ushort)BlockID.Bedrock'),
    ('voxelMap[xCheck, yCheck, zCheck] = world.GetBlockIndex(newBlock);',
     'voxelMap[xCheck, yCheck, zCheck] = (ushort)newBlock;'),
])

# --- UIItemSlot.cs ---
patch(r"Assets/Scripts/UIItemSlot.cs", [
    ('world.blockTypes[world.GetBlockIndex(itemSlot.stack.id)].icon;',
     'world.blockTypes[(ushort)itemSlot.stack.id].icon;'),
])

