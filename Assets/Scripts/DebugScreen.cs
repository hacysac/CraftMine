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
        string debugText = "CraftMine Debug Screen\n";
        debugText += frameRate + "fps\n\n";
        debugText += "XYZ: " + (Mathf.FloorToInt(world.player.transform.position.x)- startX) + " / " + Mathf.FloorToInt(world.player.transform.position.y) + " / " + (Mathf.FloorToInt(world.player.transform.position.z) - startZ) + "\n";
        debugText += "Chunks: "  + world.playerLastChunkCoord.x + " / " + world.playerLastChunkCoord.z;

        text.text = debugText;

        if (timer > 1f)
        {
            frameRate = (int) (1f/Time.unscaledDeltaTime);
            timer = 0;
        }
        timer += Time.deltaTime;

    }
}
