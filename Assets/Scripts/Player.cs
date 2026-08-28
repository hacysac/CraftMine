using UnityEngine;
using UnityEngine.UI;

public class Player : MonoBehaviour
{   
    public Transform camera;
    public World world;
    public Toolbar toolbar;
    public Transform breakHighlight;
    public Transform placeHighlight;

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
    public string selectedBlockType;
    
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
        selectedBlockType = toolbar.itemSlots[0].itemName;

        Cursor.lockState = CursorLockMode.Locked;
    }

    private void FixedUpdate()
    {
        CalculateVelocity();
        if (jumpRequest)
        {
            Jump();
        }
        transform.Translate(velocity, Space.World);
    }

    private void Update()
    {
        GetInput();
        placeCursorBlocks();

        transform.Rotate(Vector3.up * mouseX);
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        camera.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
    }

    private void placeCursorBlocks()
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
        // velocity.y = checkDownSpeed(velocity.y);
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
            velocity.y = checkDownSpeed(velocity.y);
        }
        else if(velocity.y > 0)
        {
            velocity.y = checkUpSpeed(velocity.y);
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
            world.getChunkFromVector3(breakHighlight.position).EditVoxel(breakHighlight.position, "Air");
        }
        bool onXZ = Mathf.FloorToInt(transform.position.x) == placeHighlight.position.x && Mathf.FloorToInt(transform.position.z) == placeHighlight.position.z;
        bool onY = Mathf.FloorToInt(transform.position.y) == placeHighlight.position.y || Mathf.FloorToInt(transform.position.y) + 1 == placeHighlight.position.y;
        if (placeHighlight.gameObject.activeSelf && Input.GetMouseButtonDown(1) && !(onXZ && onY))
        {
            world.getChunkFromVector3(placeHighlight.position).EditVoxel(placeHighlight.position, selectedBlockType);
        }
    }

    private float checkDownSpeed(float downSpeed)
    {
        bool corner1Check = world.CheckForVoxel(new Vector3(transform.position.x - playerWidth, transform.position.y + downSpeed, transform.position.z + playerWidth)) && !left && !front;
        bool corner2Check = world.CheckForVoxel(new Vector3(transform.position.x - playerWidth, transform.position.y + downSpeed, transform.position.z - playerWidth)) && !left && !back;
        bool corner3Check = world.CheckForVoxel(new Vector3(transform.position.x + playerWidth, transform.position.y + downSpeed, transform.position.z + playerWidth)) && !right && !front;
        bool corner4Check = world.CheckForVoxel(new Vector3(transform.position.x + playerWidth, transform.position.y + downSpeed, transform.position.z - playerWidth)) && !right && !back;
        
        if (corner1Check || corner2Check || corner3Check || corner4Check)
        {
            isGrounded = true;
            return 0f;
        }
        else
        {
            isGrounded = false;
            return downSpeed;
        }
    }

    
    private float checkUpSpeed(float upSpeed)
    {
        bool corner1Check = world.CheckForVoxel(new Vector3(transform.position.x - playerWidth, transform.position.y + upSpeed + playerHeight + bounceTolerance, transform.position.z + playerWidth)) && !left && !front;
        bool corner2Check = world.CheckForVoxel(new Vector3(transform.position.x - playerWidth, transform.position.y + upSpeed + playerHeight + bounceTolerance, transform.position.z - playerWidth)) && !left && !back;
        bool corner3Check = world.CheckForVoxel(new Vector3(transform.position.x + playerWidth, transform.position.y + upSpeed + playerHeight + bounceTolerance, transform.position.z + playerWidth)) && !front && !right;
        bool corner4Check = world.CheckForVoxel(new Vector3(transform.position.x + playerWidth, transform.position.y + upSpeed + playerHeight + bounceTolerance, transform.position.z - playerWidth)) && !back && !right;
        
        if (corner1Check || corner2Check || corner3Check || corner4Check)
        {
            verticalMomentum = 0;
            return 0f;
        }
        else
        {
            return upSpeed;
        }
    }

    public bool front
    {
        get
        {
            bool corner1Check = world.CheckForVoxel(new Vector3(transform.position.x, transform.position.y, transform.position.z + playerWidth));
            bool corner2Check = world.CheckForVoxel(new Vector3(transform.position.x, transform.position.y + 1f, transform.position.z + playerWidth));
            
            return corner1Check || corner2Check;
        }
    }
    public bool back
    {
        get
        {
            bool corner1Check = world.CheckForVoxel(new Vector3(transform.position.x, transform.position.y, transform.position.z - playerWidth));
            bool corner2Check = world.CheckForVoxel(new Vector3(transform.position.x, transform.position.y + 1f, transform.position.z - playerWidth));
            
            return corner1Check || corner2Check;
        }
    }
    public bool left
    {
        get
        {
            bool corner1Check = world.CheckForVoxel(new Vector3(transform.position.x - playerWidth, transform.position.y, transform.position.z));
            bool corner2Check = world.CheckForVoxel(new Vector3(transform.position.x - playerWidth, transform.position.y + 1f, transform.position.z));
            
            return corner1Check || corner2Check;
        }
    }
    public bool right
    {
        get
        {
            bool corner1Check = world.CheckForVoxel(new Vector3(transform.position.x + playerWidth, transform.position.y, transform.position.z));
            bool corner2Check = world.CheckForVoxel(new Vector3(transform.position.x + playerWidth, transform.position.y + 1f, transform.position.z));
            
            return corner1Check || corner2Check;
        }
    }
}
