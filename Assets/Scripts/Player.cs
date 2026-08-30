using System.Security.Cryptography;
using UnityEngine;

public class Player : MonoBehaviour
{   
    public Transform camera;
    public World world;
    public Transform breakHighlight;
    public Transform placeHighlight;
    public Toolbar toolbar;

    public bool isGrounded;
    public bool isSprinting;

    public float speed = 3f;
    public float sprintSpeed = 6f;
    public float jumpForce = 5f;
    public float gravity = -9.8f;
    public float bounceTolerance = 0.1f;

    public float playerWidth = 0.4f;
    public float playerHeight = 1.8f;

    public float checkIncrement = 0.1f;
    public float reach = 8f;
    
    private float horizontal;
    private float vertical;
    private float mouseX;
    private float mouseY;
    private Vector3 velocity;
    private float verticalMomentum = 0;
    private bool jumpRequest;
    private float xRotation = 0f;


    private void Start()
    {
        camera = GameObject.Find("Main Camera").transform;
        world = GameObject.Find("World").GetComponent<World>();
        //selectedBlockID = toolbar.slots[0].itemSlot.stack.id;

        Cursor.lockState = CursorLockMode.Locked;
    }

    private void FixedUpdate()
    {

        if (!world.inUI)
        {
            CalculateVelocity();
            if (jumpRequest)
            {
                Jump();
            }
            transform.Translate(velocity, Space.World);
        }
    }

    private void Update()
    {

        if (Input.GetKeyDown(KeyCode.E))
        {
            world.inUI = !world.inUI;
        }

        if (!world.inUI)
        {
            GetInput();
            PlaceCursorBlocks();

            transform.Rotate(Vector3.up * mouseX);
            xRotation -= mouseY;
            xRotation = Mathf.Clamp(xRotation, -90f, 90f);

            camera.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        }
    }

    private void PlaceCursorBlocks()
    {
        float step = checkIncrement;
        Vector3 lastPos = new Vector3();

        while(step < reach)
        {
            Vector3 pos = camera.position + (camera.forward*step);
            if (world.CheckForVoxel(pos))
            {
                breakHighlight.position = new Vector3(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y), Mathf.FloorToInt(pos.z));
                placeHighlight.position = lastPos;

                breakHighlight.gameObject.SetActive(true);
                placeHighlight.gameObject.SetActive(true);
                return;
            }
            lastPos = new Vector3(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y), Mathf.FloorToInt(pos.z));
            step+= checkIncrement;
        }
        breakHighlight.gameObject.SetActive(false);
        placeHighlight.gameObject.SetActive(false);
    }

    private void CalculateVelocity()
    {
        // velocity.y += gravity * Time.deltaTime;
        // velocity.y = CheckDownSpeed(velocity.y);
        if (verticalMomentum > gravity)
        {
            verticalMomentum += Time.fixedDeltaTime * gravity;
        }
        velocity = ((transform.forward*vertical) + (transform.right*horizontal)) * (isSprinting ? sprintSpeed : speed) * Time.fixedDeltaTime;
        velocity.y += verticalMomentum * Time.fixedDeltaTime;

        if ((velocity.z > 0 && front) || (velocity.z < 0 && back))
        {
            velocity.z = 0;
        }
        if ((velocity.x > 0 && right) || (velocity.x < 0 && left))
        {
            velocity.x = 0;
        }
        if (velocity.y < 0)
        {
            velocity.y = CheckDownSpeed(velocity.y);
        }
        else if(velocity.y > 0)
        {
            velocity.y = CheckUpSpeed(velocity.y);
        }

    }

    void Jump()
    {
        verticalMomentum = jumpForce;
        isGrounded = false;
        jumpRequest = false;
    }

    private void GetInput()
    {
        horizontal = Input.GetAxis("Horizontal");
        vertical = Input.GetAxis("Vertical");
        mouseX = Input.GetAxis("Mouse X");
        mouseY = Input.GetAxis("Mouse Y");

        if (Input.GetButtonDown("Sprint"))
        {
            isSprinting = true;
        }
        if (Input.GetButtonUp("Sprint"))
        {
            isSprinting = false;
        }
        if (isGrounded && Input.GetButtonDown("Jump"))
        {
            jumpRequest = true;
        }

        if (breakHighlight.gameObject.activeSelf && Input.GetMouseButtonDown(0))
        {
            Chunk breakChunk = world.GetChunkFromVector3(breakHighlight.position);
            if (breakChunk != null)
            {
                breakChunk.EditVoxel(breakHighlight.position, (ushort)BlockID.Air);
            }
        }
        if (placeHighlight.gameObject.activeSelf && Input.GetMouseButtonDown(1))
        {
            // Don't let the player place a block inside their own body.
            bool onXZ = Mathf.FloorToInt(transform.position.x) == placeHighlight.position.x && Mathf.FloorToInt(transform.position.z) == placeHighlight.position.z;
            bool onY = Mathf.FloorToInt(transform.position.y) == placeHighlight.position.y || Mathf.FloorToInt(transform.position.y) + 1 == placeHighlight.position.y;

            Chunk placeChunk = world.GetChunkFromVector3(placeHighlight.position);
            if (!(onXZ && onY) && placeChunk != null && toolbar.slots[toolbar.slotIndex].HasItem)
            {
                placeChunk.EditVoxel(placeHighlight.position, toolbar.slots[toolbar.slotIndex].itemSlot.stack.id);
                toolbar.slots[toolbar.slotIndex].itemSlot.Take(1);
            }
        }
    }

    // True if the player overlaps a solid voxel at the given world height.
    //
    // The corner probes are suppressed when we are already pressed against a wall on
    // that side, otherwise the wall itself reads as ground and the player can climb it.
    // But if two opposing sides are blocked (l && r, or f && b) every corner term is
    // suppressed at once, which would leave no vertical check at all - so the centre
    // column is always probed unguarded. A wall can never occupy the player's own
    // centre column, so it cannot cause false grounding.
    private bool BlockedAtHeight(float y)
    {
        Vector3 p = transform.position;

        if (world.CheckForVoxel(new Vector3(p.x, y, p.z)))
        {
            return true;
        }

        bool l = left, r = right, f = front, b = back;

        return (!l && !f && world.CheckForVoxel(new Vector3(p.x - playerWidth, y, p.z + playerWidth)))
            || (!l && !b && world.CheckForVoxel(new Vector3(p.x - playerWidth, y, p.z - playerWidth)))
            || (!r && !f && world.CheckForVoxel(new Vector3(p.x + playerWidth, y, p.z + playerWidth)))
            || (!r && !b && world.CheckForVoxel(new Vector3(p.x + playerWidth, y, p.z - playerWidth)));
    }

    private float CheckDownSpeed(float downSpeed)
    {
        isGrounded = BlockedAtHeight(transform.position.y + downSpeed);
        return isGrounded ? 0f : downSpeed;
    }


    private float CheckUpSpeed(float upSpeed)
    {
        if (!BlockedAtHeight(transform.position.y + upSpeed + playerHeight + bounceTolerance))
        {
            return upSpeed;
        }

        verticalMomentum = 0;
        return 0f;
    }

    // True if a solid voxel sits against the player at either body height in the given direction.
    private bool BlockedTowards(Vector3 offset)
    {
        Vector3 p = transform.position + offset;
        return world.CheckForVoxel(p) || world.CheckForVoxel(p + Vector3.up);
    }

    public bool front => BlockedTowards(new Vector3(0f, 0f,  playerWidth));
    public bool back  => BlockedTowards(new Vector3(0f, 0f, -playerWidth));
    public bool left  => BlockedTowards(new Vector3(-playerWidth, 0f, 0f));
    public bool right => BlockedTowards(new Vector3( playerWidth, 0f, 0f));
}
