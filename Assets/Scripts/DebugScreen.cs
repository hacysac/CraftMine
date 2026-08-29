using UnityEngine;
using UnityEngine.UI;

public class DebugScreen : MonoBehaviour
{

    World world;
    Text text;

    float frameRate;
    float timer;

    int startX;
    int startZ;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        world = GameObject.Find("World").GetComponent<World>();
        text = GetComponent<Text>();
        startX = Mathf.FloorToInt(world.player.transform.position.x);
        startZ = Mathf.FloorToInt(world.player.transform.position.z);
    }

    // Update is called once per frame
    void Update()
    {
        Vector3 pos = world.player.transform.position;
        int x = Mathf.FloorToInt(pos.x) - startX;
        int y = Mathf.FloorToInt(pos.y);
        int z = Mathf.FloorToInt(pos.z) - startZ;

        text.text = "CraftMine Debug Screen\n" +
                    $"{frameRate}fps\n\n" +
                    $"XYZ: {x} / {y} / {z}\n" +
                    $"Chunks: {world.playerLastChunkCoord.x} / {world.playerLastChunkCoord.z}";

        if (timer > 1f)
        {
            frameRate = (int) (1f/Time.unscaledDeltaTime);
            timer = 0;
        }
        timer += Time.deltaTime;

    }
}
