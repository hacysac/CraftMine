
#if UNITY_EDITOR

using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Regenerates Assets/Scripts/BlockID.cs from the blockTypes list on the
/// World component in the currently open scene.
///
/// Air is always guaranteed to exist as BlockID.Air = 0.
///
/// Open:
///     Tools > Generate BlockID Enum
///
/// Automatic generation:
///     Tools > Generate BlockID On Scene Save
/// </summary>
public static class BlockIDGenerator
{
    const string EnumPath = "Assets/Scripts/BlockID.cs";

    const string ToggleMenuName =
        "Tools/Generate BlockID On Scene Save";

    const string AirName = "Air";

    const string Header =
        "// GENERATED FILE - do not edit by hand.\n" +
        "// Regenerate via the Unity menu: Tools > Generate BlockID Enum\n" +
        "// Members are derived from World.blockTypes blockName entries.\n" +
        "// Spaces are converted to underscores.\n\n" +
        "public enum BlockID\n" +
        "{\n";

    static bool autoGenerate;


    // ============================================================
    // INITIALIZATION
    // ============================================================

    [InitializeOnLoadMethod]
    static void Init()
    {
        autoGenerate =
            EditorPrefs.GetBool(
                ToggleMenuName,
                true
            );

        EditorSceneManager.sceneSaved -= OnSceneSaved;
        EditorSceneManager.sceneSaved += OnSceneSaved;
    }


    // ============================================================
    // AUTO-GENERATION TOGGLE
    // ============================================================

    [MenuItem(ToggleMenuName)]
    static void ToggleAutoGenerate()
    {
        autoGenerate = !autoGenerate;

        EditorPrefs.SetBool(
            ToggleMenuName,
            autoGenerate
        );

        Debug.Log(
            $"BlockIDGenerator: auto-generate on scene save is now " +
            $"{(autoGenerate ? "ON" : "OFF")}."
        );
    }


    [MenuItem(ToggleMenuName, true)]
    static bool ToggleAutoGenerateValidate()
    {
        Menu.SetChecked(
            ToggleMenuName,
            autoGenerate
        );

        return true;
    }


    // ============================================================
    // SCENE SAVED
    // ============================================================

    static void OnSceneSaved(Scene scene)
    {
        if (!autoGenerate)
        {
            return;
        }

        Generate();
    }


    // ============================================================
    // MANUAL GENERATION
    // ============================================================

    [MenuItem("Tools/Generate BlockID Enum")]
    public static void Generate()
    {
        World world =
            Object.FindObjectOfType<World>();

        if (world == null)
        {
            Debug.LogError(
                "BlockIDGenerator: no World component found " +
                "in the open scene."
            );

            return;
        }

        if (world.blockTypes == null)
        {
            Debug.LogError(
                "BlockIDGenerator: World.blockTypes is null."
            );

            return;
        }


        // ========================================================
        // FIND AIR
        // ========================================================

        int airIndex = -1;

        for (int i = 0; i < world.blockTypes.Length; i++)
        {
            BlockType block = world.blockTypes[i];

            if (block == null)
            {
                continue;
            }

            if (
                !string.IsNullOrEmpty(block.blockName)
                &&
                string.Equals(
                    block.blockName.Trim(),
                    AirName,
                    System.StringComparison.OrdinalIgnoreCase
                )
            )
            {
                airIndex = i;
                break;
            }
        }


        // ========================================================
        // AIR MUST EXIST
        // ========================================================

        if (airIndex == -1)
        {
            Debug.LogError(
                "BlockIDGenerator: Air was not found in " +
                "World.blockTypes.\n\n" +
                "The BlockTypeGenerator should create Air automatically. " +
                "Run Tools > Generate BlockTypes From Textures first."
            );

            return;
        }


        // ========================================================
        // AIR MUST BE INDEX 0
        // ========================================================

        if (airIndex != 0)
        {
            Debug.LogError(
                $"BlockIDGenerator: Air is at blockTypes index " +
                $"{airIndex}, but Air must be at index 0.\n\n" +
                "Run Tools > Generate BlockTypes From Textures " +
                "to rebuild the blockTypes array."
            );

            return;
        }


        // ========================================================
        // BUILD ENUM
        // ========================================================

        var sb =
            new StringBuilder(Header);

        int written = 0;


        for (
            int i = 0;
            i < world.blockTypes.Length;
            i++
        )
        {
            BlockType block =
                world.blockTypes[i];

            if (block == null)
            {
                Debug.LogWarning(
                    $"BlockIDGenerator: " +
                    $"blockTypes[{i}] is null, skipped."
                );

                continue;
            }


            string name =
                block.blockName;


            if (string.IsNullOrEmpty(name))
            {
                Debug.LogWarning(
                    $"BlockIDGenerator: " +
                    $"blockTypes[{i}] has no blockName, skipped."
                );

                continue;
            }


            string member =
                Sanitize(name);


            if (!IsValidIdentifier(member))
            {
                Debug.LogError(
                    $"BlockIDGenerator: '{name}' cannot be " +
                    $"turned into a valid enum member, skipped."
                );

                continue;
            }


            sb.AppendLine(
                $"    {member} = {i},"
            );

            written++;
        }


        sb.AppendLine("}");


        // ========================================================
        // WRITE FILE
        // ========================================================

        string content =
            sb.ToString();


        if (
            System.IO.File.Exists(EnumPath)
            &&
            System.IO.File.ReadAllText(EnumPath) == content
        )
        {
            return;
        }


        System.IO.File.WriteAllText(
            EnumPath,
            content
        );

        AssetDatabase.Refresh();


        Debug.Log(
            $"BlockIDGenerator: wrote " +
            $"{written} members to {EnumPath}."
        );
    }


    // ============================================================
    // SANITIZE ENUM MEMBER
    // ============================================================

    static string Sanitize(string name)
    {
        return name
            .Trim()
            .Replace(' ', '_');
    }


    // ============================================================
    // VALIDATE C# IDENTIFIER
    // ============================================================

    static bool IsValidIdentifier(string s)
    {
        if (
            string.IsNullOrEmpty(s)
            ||
            (
                !char.IsLetter(s[0])
                &&
                s[0] != '_'
            )
        )
        {
            return false;
        }


        foreach (char c in s)
        {
            if (
                !char.IsLetterOrDigit(c)
                &&
                c != '_'
            )
            {
                return false;
            }
        }


        return true;
    }
}

#endif