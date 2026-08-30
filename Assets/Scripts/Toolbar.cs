using System;
using UnityEngine;
using System.Linq;
public class Toolbar : MonoBehaviour
{
    public UIItemSlot[] slots;
    public RectTransform highlight;
    public int slotIndex = 0;

    World world;
    Player player;

    private void Start()
    {
        world = GameObject.Find("World").GetComponent<World>();
        player = GameObject.Find("Player").GetComponent<Player>();
        // Every block except Air, straight from the auto-generated enum.
        BlockID[] blockIDs = (BlockID[])Enum.GetValues(typeof(BlockID));

        for (int i = 0; i < slots.Length; i++)
        {
            // Offset by 1 to skip Air at index 0.
            BlockID blockID = blockIDs[(i+1) % blockIDs.Length];
            ItemStack stack = new ItemStack((ushort)blockID, UnityEngine.Random.Range(2, 65));
            ItemSlot slot = new ItemSlot(slots[i], stack);
        }
    }
    
    public void Update()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");

        if (scroll != 0)
        {
            if (scroll > 0)
            {
                slotIndex--;
            }
            else
            {
                slotIndex++;
            }
        }

        if (slotIndex >= slots.Length)
        {
            slotIndex = 0;
        }
        if (slotIndex < 0)
        {
            slotIndex = slots.Length - 1;
        }

        highlight.position = slots[slotIndex].slotIcon.transform.position;
    }
}
