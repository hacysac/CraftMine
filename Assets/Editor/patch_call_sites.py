import io


def patch(path, edits):
    src = io.open(path, encoding="utf-8").read()
    for old, new in edits:
        if src.count(old) != 1:
            raise SystemExit(
                f"{path}: expected 1 match, found {src.count(old)} for:\n{old[:80]}"
            )
        src = src.replace(old, new)
    io.open(path, "w", encoding="utf-8", newline="").write(src)
    print(f"{path} patched OK")


patch(
    r"Assets/Scripts/ItemStack.cs",
    [
        ("    public string id;", "    public BlockID id;"),
        (
            "    public ItemStack (string id, int amount)",
            "    public ItemStack (BlockID id, int amount)",
        ),
    ],
)

patch(
    r"Assets/Scripts/Toolbar.cs",
    [
        (
            """        int index = 1;
        

        foreach (UIItemSlot s in slots)
        {
            string itemName = world.blockNameToID.Keys
                .Skip(index)                        // Skip the current key itself
                .FirstOrDefault();              // Get the next key (or null if it's the last one)
            ItemStack stack = new ItemStack(itemName, UnityEngine.Random.Range(2, 65));
            ItemSlot slot = new ItemSlot(s, stack);
            index++;
        }""",
            """        // Every block except Air, straight from the auto-generated enum.
        BlockID[] blockIDs = (BlockID[])Enum.GetValues(typeof(BlockID));

        for (int i = 0; i < slots.Length; i++)
        {
            // Offset by 1 to skip Air at index 0.
            BlockID blockID = blockIDs[(i + 1) % blockIDs.Length];
            ItemStack stack = new ItemStack(blockID, UnityEngine.Random.Range(2, 65));
            ItemSlot slot = new ItemSlot(slots[i], stack);
        }""",
        ),
    ],
)

patch(
    r"Assets/Scripts/Player.cs",
    [
        (
            "    public string selectedBlockType;",
            "    public BlockID selectedBlockType;",
        ),
        (
            "EditVoxel(placeHighlight.position, selectedBlockType);",
            "EditVoxel(placeHighlight.position, selectedBlockType);",
        ),
        (
            'EditVoxel(breakHighlight.position, "Air");',
            "EditVoxel(breakHighlight.position, BlockID.Air);",
        ),
    ],
)

patch(
    r"Assets/Scripts/Chunk.cs",
    [
        (
            "    public void EditVoxel (Vector3 pos, string newBlock)",
            "    public void EditVoxel (Vector3 pos, BlockID newBlock)",
        ),
        ('world.GetBlockIndex("Bedrock")', "world.GetBlockIndex(BlockID.Bedrock)"),
        ("world.GetBlockIndex(newBlock);", "world.GetBlockIndex(newBlock);"),
    ],
)

patch(
    r"Assets/Scripts/UIItemSlot.cs",
    [
        (
            "world.blockTypes[world.GetBlockIndex(itemSlot.stack.id)].icon;",
            "world.blockTypes[world.GetBlockIndex(itemSlot.stack.id)].icon;",
        ),
    ],
)

patch(
    r"Assets/Scripts/World.cs",
    [
        (
            "    public VoxelMod(Vector3 position, string id, World world)",
            "    public VoxelMod(Vector3 position, BlockID id, World world)",
        ),
    ],
)
