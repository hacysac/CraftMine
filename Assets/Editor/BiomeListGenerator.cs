
#if UNITY_EDITOR

using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Repopulates the biomes list on the World component in the currently open
/// scene from every BiomeAttributes asset under Assets/Data/Biomes.
///
/// Assets are listed in alphabetical order. Order carries no gameplay meaning -
/// GetVoxel picks a biome by strongest noise weight, not by index - but a stable
/// order keeps the scene file from churning between runs.
///
/// Open:
///     Tools > Populate World Biomes
///
/// Automatic generation:
///     Tools > Populate World Biomes On Scene Save
/// </summary>
public static class BiomeListGenerator
{
    const string BiomesFolder = "Assets/Data/Biomes";

    const string FieldName = "biomes";

    const string ToggleMenuName =
        "Tools/Populate World Biomes On Scene Save";

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

        // sceneSaving, not sceneSaved: this edits the scene itself, so the change
        // has to land before Unity writes the file. Running afterwards would leave
        // the scene dirty again the instant it finished saving, and the new biome
        // would not actually be in the saved scene.
        EditorSceneManager.sceneSaving -= OnSceneSaving;
        EditorSceneManager.sceneSaving += OnSceneSaving;
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
            $"BiomeListGenerator: auto-populate on scene save is now " +
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
    // SCENE SAVING
    // ============================================================

    static void OnSceneSaving(Scene scene, string path)
    {
        if (!autoGenerate)
        {
            return;
        }

        Populate(scene, false);
    }


    // ============================================================
    // MANUAL GENERATION
    // ============================================================

    [MenuItem("Tools/Populate World Biomes")]
    public static void Generate()
    {
        Populate(
            SceneManager.GetActiveScene(),
            true
        );
    }


    // ============================================================
    // POPULATE
    // ============================================================

    static void Populate(Scene scene, bool manual)
    {
        World world =
            Object.FindObjectOfType<World>();

        if (world == null)
        {
            if (manual)
            {
                Debug.LogError(
                    "BiomeListGenerator: no World component found " +
                    "in the open scene."
                );
            }

            return;
        }


        // Saving a scene that does not hold the World leaves it alone.
        if (world.gameObject.scene != scene)
        {
            return;
        }


        if (!AssetDatabase.IsValidFolder(BiomesFolder))
        {
            Debug.LogError(
                $"BiomeListGenerator: folder {BiomesFolder} does not exist."
            );

            return;
        }


        // ========================================================
        // COLLECT BIOMES
        // ========================================================

        string[] guids =
            AssetDatabase.FindAssets(
                "t:BiomeAttributes",
                new[] { BiomesFolder }
            );

        var found =
            new List<BiomeAttributes>();

        foreach (string guid in guids)
        {
            string assetPath =
                AssetDatabase.GUIDToAssetPath(guid);

            BiomeAttributes biome =
                AssetDatabase.LoadAssetAtPath<BiomeAttributes>(assetPath);

            if (biome == null)
            {
                continue;
            }

            found.Add(biome);
        }


        if (found.Count == 0)
        {
            Debug.LogWarning(
                $"BiomeListGenerator: no BiomeAttributes assets found " +
                $"in {BiomesFolder}. World.biomes left unchanged, because " +
                $"an empty list would divide by zero in GetVoxel."
            );

            return;
        }


        found.Sort(
            (a, b) => string.CompareOrdinal(a.name, b.name)
        );


        // ========================================================
        // SKIP IF UNCHANGED
        // ========================================================

        if (Matches(world.biomes, found))
        {
            if (manual)
            {
                Debug.Log(
                    $"BiomeListGenerator: World.biomes already lists all " +
                    $"{found.Count} biomes, nothing to do."
                );
            }

            return;
        }


        // ========================================================
        // APPLY
        // ========================================================

        var serializedWorld =
            new SerializedObject(world);

        SerializedProperty property =
            serializedWorld.FindProperty(FieldName);

        if (property == null)
        {
            Debug.LogError(
                $"BiomeListGenerator: World has no serialized field " +
                $"named '{FieldName}'."
            );

            return;
        }

        property.arraySize = found.Count;

        for (int i = 0; i < found.Count; i++)
        {
            property
                .GetArrayElementAtIndex(i)
                .objectReferenceValue = found[i];
        }

        serializedWorld.ApplyModifiedProperties();


        Debug.Log(
            $"BiomeListGenerator: wrote {found.Count} biomes to " +
            $"World.biomes ({string.Join(", ", found.ConvertAll(b => b.name))})."
        );
    }


    // ============================================================
    // COMPARE
    // ============================================================

    static bool Matches(
        BiomeAttributes[] current,
        List<BiomeAttributes> found
    )
    {
        if (
            current == null
            ||
            current.Length != found.Count
        )
        {
            return false;
        }


        for (int i = 0; i < current.Length; i++)
        {
            if (current[i] != found[i])
            {
                return false;
            }
        }


        return true;
    }
}

#endif
