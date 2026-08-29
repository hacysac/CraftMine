import io

p = r"Assets/Editor/BlockIDGenerator.cs"
src = io.open(p, encoding="utf-8").read()

old = """public static class BlockIDGenerator
{
    const string EnumPath = "Assets/Scripts/BlockID.cs";
    const string Header =
        "// GENERATED FILE - do not edit by hand.\\n" +
        "// Regenerate via the Unity menu: Tools > Generate BlockID Enum\\n" +
        "// Members are derived from World.blockTypes blockName entries (spaces -> underscores).\\n" +
        "public enum BlockID\\n{\\n";

    [MenuItem("Tools/Generate BlockID Enum")]
    public static void Generate()"""

new = """public static class BlockIDGenerator
{
    const string EnumPath = "Assets/Scripts/BlockID.cs";
    const string ToggleMenuName = "Tools/Generate BlockID On Scene Save";
    const string Header =
        "// GENERATED FILE - do not edit by hand.\\n" +
        "// Regenerate via the Unity menu: Tools > Generate BlockID Enum\\n" +
        "// Members are derived from World.blockTypes blockName entries (spaces -> underscores).\\n" +
        "public enum BlockID\\n{\\n";

    static bool autoGenerate;

    [InitializeOnLoadMethod]
    static void Init()
    {
        autoGenerate = EditorPrefs.GetBool(ToggleMenuName, true);
        EditorSceneManager.sceneSaved -= OnSceneSaved;
        EditorSceneManager.sceneSaved += OnSceneSaved;
    }

    [MenuItem(ToggleMenuName)]
    static void ToggleAutoGenerate()
    {
        autoGenerate = !autoGenerate;
        EditorPrefs.SetBool(ToggleMenuName, autoGenerate);
        Debug.Log($"BlockIDGenerator: auto-generate on scene save is now {(autoGenerate ? "ON" : "OFF")}.");
    }

    [MenuItem(ToggleMenuName, true)]
    static bool ToggleAutoGenerateValidate()
    {
        Menu.SetChecked(ToggleMenuName, autoGenerate);
        return true;
    }

    static void OnSceneSaved(Scene scene)
    {
        if (autoGenerate)
        {
            Generate();
        }
    }

    [MenuItem("Tools/Generate BlockID Enum")]
    public static void Generate()"""

if src.count(old) != 1:
    raise SystemExit(f"expected 1 match, found {src.count(old)}")

src = src.replace(old, new)

# The saved-scene callback needs the Scene type.
if (
    "using UnityEngine.SceneManagement;" not in src
    and "using UnityEditor.SceneManagement;" in src
):
    # Scene lives in UnityEngine.SceneManagement
    src = src.replace(
        "using UnityEditor.SceneManagement;",
        "using UnityEditor.SceneManagement;\nusing UnityEngine.SceneManagement;",
        1,
    )

# Also skip the log spam when auto-generating: only log when content actually changes.
old_write = """        System.IO.File.WriteAllText(EnumPath, sb.ToString());
        AssetDatabase.Refresh();
        Debug.Log($"BlockIDGenerator: wrote {written} members to {EnumPath}.");"""
new_write = """        string content = sb.ToString();

        if (System.IO.File.Exists(EnumPath) && System.IO.File.ReadAllText(EnumPath) == content)
        {
            return; // nothing changed, skip rewrite/refresh/log
        }

        System.IO.File.WriteAllText(EnumPath, content);
        AssetDatabase.Refresh();
        Debug.Log($"BlockIDGenerator: wrote {written} members to {EnumPath}.");"""
if src.count(old_write) != 1:
    raise SystemExit("write block not found")
src = src.replace(old_write, new_write)

io.open(p, "w", encoding="utf-8", newline="").write(src)
print("BlockIDGenerator.cs patched OK")
