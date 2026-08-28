using UnityEngine;
using UnityEngine.UI;

public class Toolbar : MonoBehaviour
{
    World world;
    public Player player;
    public RectTransform highlight;
    public ItemSlot[] itemSlots;

    int slotIndex = 0;

    private void Start()
    {
        world = GameObject.Find("World").GetComponent<World>();
        foreach (ItemSlot slot in itemSlots)
        {   

            slot.icon.sprite = world.blockTypes[world.GetBlockIndex(slot.itemName)].icon;
            slot.icon.enabled = true;
        }
    }

    private void Update()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0)
        {
            if (scroll < 0)
            {
                slotIndex++;
                if (slotIndex > itemSlots.Length-1)
                {
                    slotIndex = 0;
                }
            }
            else
            {
                slotIndex--;
                if (slotIndex < 0){
                    slotIndex = itemSlots.Length - 1;
                }
            }
            highlight.position = itemSlots[slotIndex].icon.transform.position;
            player.selectedBlockType = itemSlots[slotIndex].itemName;
        }
    }
}

[System.Serializable]
public class ItemSlot
{
    public string itemName;
    public Image icon;
}