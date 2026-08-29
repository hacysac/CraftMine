using UnityEngine;

public class ItemStack
{
    public BlockID id;
    public int amount;

    public ItemStack (BlockID id, int amount)
    {
        this.id = id;
        this.amount = amount;
    }
}
