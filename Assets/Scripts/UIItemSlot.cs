using System.Reflection.PortableExecutable;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.UI;

public class UIItemSlot : MonoBehaviour
{
    public bool isLinked = false;
    public ItemSlot itemSlot;
    public Image slotImage;
    public Image slotIcon;
    public Text slotAmount;

    World world;

    private void Awake()
    {
        world = GameObject.Find("World").GetComponent<World>();
    }

    public bool HasItem
    {
        get
        {
            if (itemSlot == null)
            {
                return false;
            }
            return itemSlot.HasItem;
        }
    }

    public void Link(ItemSlot itemSlot)
    {
        this.itemSlot = itemSlot;
        isLinked = true;
        itemSlot.LinkUISlot(this);
        UpdateSlot();
    }

    public void Unlink()
    {
        itemSlot.UnlinkUISlot();
        itemSlot = null;
        UpdateSlot();
    }
    public void UpdateSlot()
    {
        if (itemSlot != null && itemSlot.HasItem)
        {
            slotIcon.sprite = world.blockTypes[(int)itemSlot.stack.id].icon;
            slotAmount.text = itemSlot.stack.amount.ToString();
            slotIcon.enabled = true;
            slotAmount.enabled = true;
        }
        else
        {
            Clear();
        }
    }

    public void Clear()
    {
        slotIcon.sprite = null;
        slotAmount.text = "";
        slotIcon.enabled = false;
        slotAmount.enabled = false;
    }

    private void OnDestroy()
    {
        if (isLinked)
        {
            itemSlot.UnlinkUISlot();
        }
    }
}

public class ItemSlot
{
    public ItemStack stack = null;
    private UIItemSlot uiItemSlot = null;

    public bool isCreative;

    World world;

    public ItemSlot(UIItemSlot uiItemSlot)
    {
        world = GameObject.Find("World").GetComponent<World>();
        this.uiItemSlot = uiItemSlot;
        stack = null;
        uiItemSlot.Link(this);
    }

    public ItemSlot(UIItemSlot uiItemSlot, ItemStack stack)
    {
        world = GameObject.Find("World").GetComponent<World>();
        this.uiItemSlot = uiItemSlot;
        this.stack = stack;
        uiItemSlot.Link(this);
    }

    public void LinkUISlot(UIItemSlot uiItemSlot)
    {
        this.uiItemSlot = uiItemSlot;
    }

    public void UnlinkUISlot()
    {
        this.uiItemSlot = null;
    }

    public void EmptySlot()
    {
        stack = null;
        if (uiItemSlot != null)
        {
            uiItemSlot.UpdateSlot();
        }
    }

    public void PutAll(ItemStack stack)
    {
        this.stack = stack;
        uiItemSlot.UpdateSlot();
    }

    public int Put(int amount)
    {
        if (stack.amount + amount < world.blockTypes[(int)stack.id].maxStackSize)
        {
            stack.amount += amount;
            uiItemSlot.UpdateSlot();
            return amount;
        }
        int takeout = world.blockTypes[(int)stack.id].maxStackSize - stack.amount;
        stack.amount = world.blockTypes[(int) stack.id].maxStackSize;
        uiItemSlot.UpdateSlot();
        return takeout;
    }

    public ItemStack TakeAll()
    {
        ItemStack handOver = new ItemStack(stack.id, stack.amount);
        EmptySlot();
        return handOver;
    }

    public int Take(int amount)
    {
        if (amount > stack.amount)
        {
            int result = stack.amount;
            EmptySlot();
            return amount;
        }
        else if (amount < stack.amount)
        {
            stack.amount -= amount;
            uiItemSlot.UpdateSlot();
            return amount;
        }
        EmptySlot();
        return amount;
    }
    
    public bool HasItem
    {
        get
        {
            if (stack != null)
            {
                return true;
            }
            return false;
        }
    }
}