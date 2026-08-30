#if UNITY_EDITOR

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Automatically builds World.blockTypes from the textures in:
///     Assets/Textures/Blocks
///
/// Rules:
///     Air is always created automatically at index 0.
///     Air does not need a texture.
///     Existing blocks keep their manually edited properties.
///     Removing a block texture removes that block from blockTypes.
///     Adding a texture creates a new BlockType.
///     Texture changes update the appropriate face sprites.
///     Icons are loaded from Assets/Textures/Block Icons.
///
/// Automatic generation happens when the scene is saved.
///
/// Manual generation:
///     Tools > Generate BlockTypes From Textures
///
/// Toggle automatic generation:
///     Tools > Generate BlockTypes On Scene Save
/// </summary>
public static class BlockTypeGenerator
{
    const string TextureFolder = "Assets/Textures/Blocks";
    const string IconFolder = "Assets/Textures/Block Icons";

    const string ToggleMenuName =
        "Tools/Generate BlockTypes On Scene Save";

    const string AirName = "Air";


    static readonly string[] FaceSuffixes =
    {
        "_top",
        "_bottom",
        "_side",
        "_front",
        "_back",
        "_left",
        "_right",
        "_front_lit"
    };


    static readonly HashSet<string> TransparentBlocks =
        new HashSet<string>
        {
            "glass",
            "oak_leaves"
        };


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

        EditorSceneManager.sceneSaving -= OnSceneSaving;
        EditorSceneManager.sceneSaving += OnSceneSaving;
    }


    // ============================================================
    // MANUAL GENERATION
    // ============================================================

    [MenuItem("Tools/Generate BlockTypes From Textures")]
    public static void GenerateManual()
    {
        Scene scene =
            EditorSceneManager.GetActiveScene();

        Generate(scene);

        EditorSceneManager.SaveOpenScenes();
    }


    // ============================================================
    // AUTO GENERATION TOGGLE
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
            $"BlockTypeGenerator: auto-generate on scene save is now " +
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

    static void OnSceneSaving(
        Scene scene,
        string path)
    {
        if (!autoGenerate)
        {
            return;
        }

        Generate(scene);
    }


    // ============================================================
    // MAIN GENERATOR
    // ============================================================

    static void Generate(Scene scene)
    {
        World world =
            Object.FindObjectOfType<World>();

        if (world == null)
        {
            Debug.LogWarning(
                "BlockTypeGenerator: no World component found " +
                "in the open scene."
            );

            return;
        }


        if (world.blockTypes == null)
        {
            world.blockTypes =
                new BlockType[0];
        }


        Debug.Log(
            $"BlockTypeGenerator: scanning {TextureFolder}..."
        );


        // ========================================================
        // FIND ALL BLOCK TEXTURES
        // ========================================================

        var textures =
            new Dictionary<
                string,
                Dictionary<string, Sprite>
            >();


        string[] guids =
            AssetDatabase.FindAssets(
                "t:Texture2D",
                new[] { TextureFolder }
            );


        foreach (string guid in guids)
        {
            string texturePath =
                AssetDatabase.GUIDToAssetPath(guid);


            string file =
                System.IO.Path.GetFileNameWithoutExtension(
                    texturePath
                );


            string baseName =
                file;

            string faceKey =
                "";


            // ----------------------------------------------------
            // Determine whether this is a face-specific texture.
            // ----------------------------------------------------

            foreach (string suffix in FaceSuffixes)
            {
                if (file.EndsWith(suffix))
                {
                    baseName =
                        file.Substring(
                            0,
                            file.Length - suffix.Length
                        );

                    faceKey =
                        suffix.Substring(1);

                    break;
                }
            }


            baseName =
                Normalize(baseName);


            // ----------------------------------------------------
            // Load sprite.
            // ----------------------------------------------------

            Sprite sprite =
                AssetDatabase.LoadAssetAtPath<Sprite>(
                    texturePath
                );


            if (sprite == null)
            {
                Debug.LogWarning(
                    $"BlockTypeGenerator: '{texturePath}' " +
                    "is not imported as a Sprite. Skipped."
                );

                continue;
            }


            // ----------------------------------------------------
            // Create texture group.
            // ----------------------------------------------------

            if (!textures.TryGetValue(
                    baseName,
                    out Dictionary<string, Sprite> faces))
            {
                faces =
                    new Dictionary<string, Sprite>();

                textures.Add(
                    baseName,
                    faces
                );
            }


            faces[faceKey] =
                sprite;
        }


        // ========================================================
        // BUILD NEW BLOCK LIST
        // ========================================================

        var newBlockTypes =
            new List<BlockType>();


        // ========================================================
        // AIR
        // ========================================================
        //
        // Air is special.
        //
        // It is NOT controlled by a texture.
        //
        // It ALWAYS exists.
        //
        // It ALWAYS occupies index 0.
        // ========================================================

        BlockType air =
            FindExistingBlock(
                world.blockTypes,
                "air"
            );


        if (air == null)
        {
            air =
                new BlockType
                {
                    blockName = AirName,
                    isSolid = false,
                    isTransparent = true,
                    maxStackSize = 64,
                    icon = null,

                    backFaceTexture = null,
                    frontFaceTexture = null,
                    topFaceTexture = null,
                    bottomFaceTexture = null,
                    leftFaceTexture = null,
                    rightFaceTexture = null
                };


            Debug.Log(
                "BlockTypeGenerator: created Air."
            );
        }


        // Air is ALWAYS first.
        newBlockTypes.Add(air);


        // ========================================================
        // ADD BLOCKS FROM TEXTURES
        // ========================================================

        foreach (
            KeyValuePair<
                string,
                Dictionary<string, Sprite>
            > pair in textures)
        {
            string normalizedName =
                pair.Key;


            // Air is handled above.
            if (normalizedName == "air")
            {
                continue;
            }


            BlockType existing =
                FindExistingBlock(
                    world.blockTypes,
                    normalizedName
                );


            // ----------------------------------------------------
            // Prefix matching.
            //
            // Example:
            //
            // Existing block:
            //     Coal Ore
            //
            // Texture:
            //     coal
            //
            // ----------------------------------------------------

            if (existing == null)
            {
                existing =
                    FindPrefixMatch(
                        world.blockTypes,
                        normalizedName
                    );
            }


            // ----------------------------------------------------
            // Create new block if necessary.
            // ----------------------------------------------------

            if (existing == null)
            {
                existing =
                    new BlockType
                    {
                        blockName =
                            TitleCase(normalizedName),

                        isSolid = true,

                        isTransparent =
                            TransparentBlocks.Contains(
                                normalizedName
                            ),

                        maxStackSize = 64
                    };


                Debug.Log(
                    $"BlockTypeGenerator: created " +
                    $"'{existing.blockName}'."
                );
            }


            // ----------------------------------------------------
            // Update textures.
            //
            // IMPORTANT:
            // This does NOT modify:
            //
            //     isSolid
            //     isTransparent
            //     maxStackSize
            //
            // on an existing block.
            // ----------------------------------------------------

            ApplyFaces(
                existing,
                pair.Value
            );


            ApplyIcon(
                existing,
                normalizedName
            );


            newBlockTypes.Add(existing);
        }


        // ========================================================
        // DETECT CHANGES
        // ========================================================

        bool changed =
            BlockTypesChanged(
                world.blockTypes,
                newBlockTypes
            );


        if (!changed)
        {
            Debug.Log(
                $"BlockTypeGenerator: no changes. " +
                $"blockTypes contains {newBlockTypes.Count} entries."
            );

            return;
        }


        // ========================================================
        // WRITE ARRAY
        // ========================================================

        world.blockTypes =
            newBlockTypes.ToArray();


        // ========================================================
        // MARK WORLD DIRTY
        // ========================================================

        EditorUtility.SetDirty(world);

        EditorSceneManager.MarkSceneDirty(
            scene
        );


        Debug.Log(
            $"BlockTypeGenerator: blockTypes updated. " +
            $"Total blocks: {world.blockTypes.Length}. " +
            $"Element 0: {world.blockTypes[0].blockName}"
        );
    }


    // ============================================================
    // FIND EXISTING BLOCK
    // ============================================================

    static BlockType FindExistingBlock(
        BlockType[] blocks,
        string normalizedName)
    {
        if (blocks == null)
        {
            return null;
        }


        foreach (BlockType block in blocks)
        {
            if (
                block == null
                ||
                string.IsNullOrEmpty(block.blockName)
            )
            {
                continue;
            }


            if (
                Normalize(block.blockName)
                ==
                normalizedName
            )
            {
                return block;
            }
        }


        return null;
    }


    // ============================================================
    // PREFIX MATCH
    // ============================================================

    static BlockType FindPrefixMatch(
        BlockType[] blocks,
        string normalizedTextureName)
    {
        if (blocks == null)
        {
            return null;
        }


        foreach (BlockType block in blocks)
        {
            if (
                block == null
                ||
                string.IsNullOrEmpty(block.blockName)
            )
            {
                continue;
            }


            string normalizedBlockName =
                Normalize(block.blockName);


            if (
                normalizedBlockName.StartsWith(
                    normalizedTextureName + "_"
                )
                ||
                normalizedTextureName.StartsWith(
                    normalizedBlockName + "_"
                )
            )
            {
                return block;
            }
        }


        return null;
    }


    // ============================================================
    // CHECK WHETHER LIST CHANGED
    // ============================================================

    static bool BlockTypesChanged(
        BlockType[] oldBlocks,
        List<BlockType> newBlocks)
    {
        if (oldBlocks == null)
        {
            return true;
        }


        if (
            oldBlocks.Length
            !=
            newBlocks.Count
        )
        {
            return true;
        }


        for (int i = 0; i < newBlocks.Count; i++)
        {
            BlockType oldBlock =
                oldBlocks[i];

            BlockType newBlock =
                newBlocks[i];


            if (oldBlock == null || newBlock == null)
            {
                return oldBlock != newBlock;
            }


            if (
                Normalize(oldBlock.blockName)
                !=
                Normalize(newBlock.blockName)
            )
            {
                return true;
            }
        }


        return false;
    }


    // ============================================================
    // APPLY FACE TEXTURES
    // ============================================================

    static void ApplyFaces(
        BlockType block,
        Dictionary<string, Sprite> faces)
    {
        if (block == null || faces == null)
        {
            return;
        }


        faces.TryGetValue(
            "",
            out Sprite plain
        );


        if (
            plain == null
            &&
            faces.Count == 1
        )
        {
            plain =
                faces.Values.First();
        }


        faces.TryGetValue(
            "top",
            out Sprite top
        );

        faces.TryGetValue(
            "bottom",
            out Sprite bottom
        );

        faces.TryGetValue(
            "side",
            out Sprite side
        );

        faces.TryGetValue(
            "front",
            out Sprite front
        );

        faces.TryGetValue(
            "back",
            out Sprite back
        );

        faces.TryGetValue(
            "left",
            out Sprite left
        );

        faces.TryGetValue(
            "right",
            out Sprite right
        );

        faces.TryGetValue(
            "front_lit",
            out Sprite frontLit
        );


        // --------------------------------------------------------
        // Determine fallback sprites.
        // --------------------------------------------------------

        Sprite sideSprite =
            side
            ?? front
            ?? back
            ?? left
            ?? right
            ?? frontLit
            ?? plain;


        Sprite frontSprite =
            front
            ?? frontLit
            ?? side
            ?? plain;


        Sprite backSprite =
            back
            ?? side
            ?? front
            ?? plain;


        Sprite leftSprite =
            left
            ?? side
            ?? front
            ?? plain;


        Sprite rightSprite =
            right
            ?? side
            ?? front
            ?? plain;


        // --------------------------------------------------------
        // IMPORTANT:
        //
        // Only replace a face when a texture was actually found.
        //
        // This prevents an incomplete texture set from erasing
        // manually configured textures.
        // --------------------------------------------------------

        if (top != null)
        {
            block.topFaceTexture =
                top;
        }
        else if (plain != null)
        {
            block.topFaceTexture =
                plain;
        }


        if (bottom != null)
        {
            block.bottomFaceTexture =
                bottom;
        }
        else if (top != null)
        {
            block.bottomFaceTexture =
                top;
        }
        else if (plain != null)
        {
            block.bottomFaceTexture =
                plain;
        }


        if (leftSprite != null)
        {
            block.leftFaceTexture =
                leftSprite;
        }


        if (rightSprite != null)
        {
            block.rightFaceTexture =
                rightSprite;
        }


        if (backSprite != null)
        {
            block.backFaceTexture =
                backSprite;
        }


        if (frontSprite != null)
        {
            block.frontFaceTexture =
                frontSprite;
        }


        // --------------------------------------------------------
        // Warn about incomplete texture sets.
        // --------------------------------------------------------

        if (
            sideSprite == null
            ||
            (
                top == null
                &&
                plain == null
            )
        )
        {
            Debug.LogWarning(
                $"BlockTypeGenerator: incomplete texture set " +
                $"for '{block.blockName}'. " +
                $"Found: {string.Join(", ", faces.Keys)}"
            );
        }
    }


    // ============================================================
    // APPLY ICON
    // ============================================================

    static void ApplyIcon(
        BlockType block,
        string normalizedName)
    {
        if (block == null)
        {
            return;
        }


        string iconPath =
            $"{IconFolder}/{normalizedName}_icon.png";


        Sprite icon =
            AssetDatabase.LoadAssetAtPath<Sprite>(
                iconPath
            );


        if (icon != null)
        {
            block.icon =
                icon;
        }
    }


    // ============================================================
    // NORMALIZE
    // ============================================================

    static string Normalize(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return "";
        }


        return name
            .Trim()
            .ToLower()
            .Replace(' ', '_');
    }


    // ============================================================
    // TITLE CASE
    // ============================================================

    static string TitleCase(string name)
    {
        return string.Join(
            " ",
            name
                .Split('_')
                .Select(
                    word =>
                        word.Length == 0
                            ? word
                            : char.ToUpper(word[0])
                              + word.Substring(1)
                )
        );
    }
}

#endif
