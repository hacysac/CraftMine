import io
p = r"Assets/Editor/patch_collapse.py"
s = io.open(p, encoding="utf-8").read()

old = """        ("        BuildBlockIDLookup();", "        ValidateBlockIDs();"),
        ("this.GetBlockIndex(BlockID.Air)", "(ushort)BlockID.Air"),
        ("this.GetBlockIndex(BlockID.Bedrock)", "(ushort)BlockID.Bedrock"),
        ("this.GetBlockIndex(BlockID.Grass)", "(ushort)BlockID.Grass"),
        ("this.GetBlockIndex(BlockID.Dirt)", "(ushort)BlockID.Dirt"),
        ("this.GetBlockIndex(BlockID.Stone)", "(ushort)BlockID.Stone"),
        ("this.GetBlockIndex(lode.block)", "(ushort)lode.block"),
        ("        this.id = world.GetBlockIndex(id);", "        this.id = (ushort)id;"),
    ],
)
"""
new = """        ("        BuildBlockIDLookup();", "        ValidateBlockIDs();"),
    ],
)

def patch_all(path, pairs):
    src = io.open(path, encoding="utf-8").read()
    for old, new in pairs:
        src = src.replace(old, new)
    io.open(path, "w", encoding="utf-8", newline="").write(src)
    print(f"{path} patched (all occurrences) OK")

patch_all(r"Assets/Scripts/World.cs", [
    ("this.GetBlockIndex(BlockID.Air)", "(ushort)BlockID.Air"),
    ("this.GetBlockIndex(BlockID.Bedrock)", "(ushort)BlockID.Bedrock"),
    ("this.GetBlockIndex(BlockID.Grass)", "(ushort)BlockID.Grass"),
    ("this.GetBlockIndex(BlockID.Dirt)", "(ushort)BlockID.Dirt"),
    ("this.GetBlockIndex(BlockID.Stone)", "(ushort)BlockID.Stone"),
    ("this.GetBlockIndex(lode.block)", "(ushort)lode.block"),
    ("        this.id = world.GetBlockIndex(id);", "        this.id = (ushort)id;"),
])
"""
assert s.count(old) == 1, s.count(old)
io.open(p, "w", encoding="utf-8", newline="").write(s.replace(old, new))
print("patch_collapse.py fixed")
