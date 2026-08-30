using UnityEngine;
using System.Collections.Generic;
using System;

public class CreativeInventory : MonoBehaviour
{
    public GameObject slotPrefab;
    World world;

    List<ItemSlot> slots = new List<ItemSlot>();

    private void Start()
    {
        world = GameObject.Find("World").GetComponent<World>();
        BlockID[] blockIDs = (BlockID[])Enum.GetValues(typeof(BlockID));

        for (int i = 1; i < world.blockTypes.Length; i++)
        {
            GameObject newSlot = Instantiate(slotPrefab, transform);

            ItemStack stack = new ItemStack((ushort)blockIDs[i % blockIDs.Length], 64);
            ItemSlot slot = new ItemSlot(newSlot.GetComponent<UIItemSlot>(), stack);

            slot.isCreative = true;
        }
    }

}
