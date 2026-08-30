using UnityEngine;

public class ItemStack
{
    public ushort id;
    public int amount;

    public ItemStack (ushort id, int amount)
    {
        this.id = id;
        this.amount = amount;
    }
}
