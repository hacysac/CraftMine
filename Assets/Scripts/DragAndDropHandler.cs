using UnityEngine;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class DragAndDropHandler : MonoBehaviour
{
    [SerializeField] private UIItemSlot cursorSlot = null;
    private ItemSlot cursorItemSlot;
    [SerializeField] private GraphicRaycaster raycaster = null;
    private PointerEventData pointerEventData;
    [SerializeField] private EventSystem eventSystem = null;

    World world;

    private void Start()
    {
        world = GameObject.Find("World").GetComponent<World>();

        cursorItemSlot = new ItemSlot(cursorSlot);
    }

    private void Update()
    {
        if (!world.inUI)
        {
            return;
        }

        cursorSlot.transform.position = Input.mousePosition;

        if (Input.GetMouseButtonDown(0))
        {
            if (CheckForSlot() != null)
            {
                HandleSlotClick(CheckForSlot());
            }
        }
    }

    private void HandleSlotClick(UIItemSlot clickedSlot)
    {
        if (clickedSlot == null)
        {
            return;
        }

        if (!cursorSlot.HasItem && !clickedSlot.HasItem)
        {
            return;
        }

        else if (clickedSlot.itemSlot.isCreative)
        {
            cursorSlot.itemSlot.EmptySlot();
            cursorSlot.itemSlot.PutAll(clickedSlot.itemSlot.stack);
        }

        else if (!cursorSlot.HasItem)
        {
            cursorSlot.itemSlot.PutAll(clickedSlot.itemSlot.TakeAll());
            return;
        }
        else if (!clickedSlot.HasItem)
        {
            clickedSlot.itemSlot.PutAll(cursorSlot.itemSlot.TakeAll());
            return;
        }

        ItemStack cursorStack = cursorSlot.itemSlot.stack;
        ItemStack clickedStack = clickedSlot.itemSlot.stack;


        if (cursorStack.id != clickedStack.id)
        {
            ItemStack oldCursor = cursorSlot.itemSlot.TakeAll();
            ItemStack oldSlot = clickedSlot.itemSlot.TakeAll();

            clickedSlot.itemSlot.PutAll(oldCursor);
            cursorSlot.itemSlot.PutAll(oldSlot);
            return;
        }
        cursorSlot.itemSlot.Take(clickedSlot.itemSlot.Put(cursorStack.amount));
        return;
    }
    
    private UIItemSlot CheckForSlot()
    {
        pointerEventData = new PointerEventData(eventSystem);
        pointerEventData.position = Input.mousePosition;

        List<RaycastResult> results = new List<RaycastResult>();
        raycaster.Raycast(pointerEventData, results);

        foreach (RaycastResult result in results)
        {
            if (result.gameObject.tag == "UIItemSlot")
            {
                return result.gameObject.GetComponent<UIItemSlot>();
            }
        }

        return null;
    }

}
