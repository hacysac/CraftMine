#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine;

/// Regenerates Assets/Scripts/BlockID.cs from the blockTypes list on the
/// World component in the currently open scene. Open Tools > Generate BlockID Enum.
public static class BlockIDGenerator
{
    const string EnumPath = "Assets/Scripts/BlockID.cs";
    const string ToggleMenuName = "Tools/Generate BlockID On Scene Save";
    const string Header =
        "// GENERATED FILE - do not edit by hand.\n" +
        "// Regenerate via the Unity menu: Tools > Generate BlockID Enum\n" +
        "// Members are derived from World.blockTypes blockName entries (spaces -> underscores).\n" +
        "public enum BlockID\n{\n";

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
    public static void Generate()
    {
        World world = Object.FindObjectOfType<World>();

        if (world == null || world.blockTypes == null || world.blockTypes.Length == 0)
        {
            Debug.LogError("BlockIDGenerator: no World with blockTypes found in the open scene. " +
                           "Open the scene containing the World object and try again.");
            return;
        }

        var sb = new StringBuilder(Header);
        int written = 0;

        for (int i = 0; i < world.blockTypes.Length; i++)
        {
            string name = world.blockTypes[i].blockName;

            if (string.IsNullOrEmpty(name))
            {
                Debug.LogWarning($"BlockIDGenerator: blockTypes[{i}] has no blockName, skipped.");
                continue;
            }

            string member = Sanitize(name);

            if (!IsValidIdentifier(member))
            {
                Debug.LogError($"BlockIDGenerator: '{name}' cannot be turned into a valid enum member, skipped.");
                continue;
            }

            sb.AppendLine($"    {member} = {i},");
            written++;
        }

        sb.AppendLine("}");

        string content = sb.ToString();

        if (System.IO.File.Exists(EnumPath) && System.IO.File.ReadAllText(EnumPath) == content)
        {
            return; // nothing changed, skip rewrite/refresh/log
        }

        System.IO.File.WriteAllText(EnumPath, content);
        AssetDatabase.Refresh();
        Debug.Log($"BlockIDGenerator: wrote {written} members to {EnumPath}.");
    }

    // "Oak Leaves" -> "Oak_Leaves", "Coal Ore" -> "Coal_Ore"
    static string Sanitize(string name)
    {
        return name.Trim().Replace(' ', '_');
    }

    static bool IsValidIdentifier(string s)
    {
        if (string.IsNullOrEmpty(s) || (!char.IsLetter(s[0]) && s[0] != '_'))
            return false;

        foreach (char c in s)
        {
            if (!char.IsLetterOrDigit(c) && c != '_')
                return false;
        }
        return true;
    }
}
#endif
