// Some stupid rigidbody based movement by Dani

using System;
using UnityEngine;
using UnityEngine.UI;

public class PlayerMovement : MonoBehaviour {

    //Assingables
    public ParticleSystem splatterParticles;
    public Gradient particleColorGradient;
    public Transform playerCam;
    public Transform orientation;

    public GameObject DeathCanvas;
    public Text UIText;
    public Animator animator;
    
    //Other
    private Rigidbody rb;
    private CapsuleCollider col;

    //Rotation and look
    private float xRotation;
    private float sensitivity = 10f;
    private float sensMultiplier = 1f;
    
    //Movement
    public float moveSpeed = 3000;
    public float maxSpeed = 15;
    public float walkSpeed = 5;
    public bool grounded;
    public LayerMask whatIsGround;
    
    public float counterMovement = 0.175f;
    private float threshold = 0.01f;
    public float maxSlopeAngle = 70f;

    //Crouch & Slide
    private Vector3 crouchScale = new Vector3(1, 0.5f, 1);
    private Vector3 playerScale;
    public float slideForce = 500;
    public float slideCounterMovement = 0.4f;

    //Jumping
    private bool readyToJump = true;
    private float jumpCooldown = 0.25f;
    public float jumpForce = 500f;

    //Input
    PlayerActions playerActions;
    Vector2 lookInput;
    Vector2 movementInput;
    float x, y;
    bool jumping, walking, crouching, cancelCrouching = false;
    
    //Sliding
    private Vector3 normalVector = Vector3.up;
    private Vector3 wallNormalVector;

    //Wallrunning
    public LayerMask whatIsWall;
    public float wallrunForce, maxWallrunTime, maxWallrunSpeed;
    bool isWallRight, isWallLeft;
    bool isWallrunning;
    public float maxWallrunCameraTilt, wallRunCameraTilt;
    float timer;

    //Death & Restart
    private bool restart = false;
    private bool isDead = false;

    private void WallrunInput()
    {
        if (x > 0 && isWallRight) StartWallrun();
        if (x < 0 && isWallLeft) StartWallrun();
    }

    private void StartWallrun()
    {
        rb.useGravity = false;
        isWallrunning = true;

        //Check for maxSpeed
        if (rb.velocity.magnitude <= maxWallrunSpeed)
        {
            rb.AddForce(orientation.forward * wallrunForce * Time.deltaTime);

            //Make Character Stick to the wall
            if (isWallRight)
                rb.AddForce(orientation.right * wallrunForce / 5 * Time.deltaTime);
            else
                rb.AddForce(-orientation.right * wallrunForce / 5 * Time.deltaTime);
        }
    }

    private void StopWallrun()
    {
        rb.useGravity = true;
        isWallrunning = false;
        timer = 0.0f;
    }

    private void CheckForWall()
    {
        isWallRight = Physics.Raycast(transform.position, orientation.right, 1f, whatIsWall);
        //-orientation.right = orientation.left
        isWallLeft = Physics.Raycast(transform.position, -orientation.right, 1f, whatIsWall);

        //leave wallrun
        if (!isWallLeft && !isWallRight) StopWallrun();
    }

    private void OnEnable()
    {
        playerActions.Enable();
    }

    private void OnDisable()
    {
        playerActions.Disable();
    }

    void Awake() {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<CapsuleCollider>();

        playerActions = new PlayerActions();
        playerActions.PlayerControls.Movement.performed += context => movementInput = context.ReadValue<Vector2>();
        playerActions.PlayerControls.Mouse.performed += lookContext => lookInput = lookContext.ReadValue<Vector2>();
        playerActions.PlayerControls.Jump.performed += jumpContext => jumping = true;
        playerActions.PlayerControls.Sprint.performed += sprintContext => walking = true;
        playerActions.PlayerControls.Sprint.canceled += sprintContext => walking = false;
        playerActions.PlayerControls.Crouch.performed += crouchContext => crouching = true;
        playerActions.PlayerControls.Crouch.canceled += crouchContext => cancelCrouching = true;
        playerActions.PlayerControls.Restart.performed += restartContext => restart = true;
    }
    
    void Start() {
        playerScale =  transform.localScale;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    
    private void FixedUpdate() {
        Movement();
    }

    private void Update() {
        MyInput();
        Look();
        isGrounded();
        CheckForWall();
        WallrunInput();
        Animate();

        if((jumping && isDead) || restart)
        {
            isDead = false;
            restart = false;
            UnityEngine.SceneManagement.SceneManager.LoadScene(0);
        }
    }

    /// <summary>
    /// Find user input. Should put this in its own class but im lazy
    /// </summary>
    private void MyInput() {
        x = movementInput.x;
        y = movementInput.y;

        //Crouching
        if (crouching) StartCrouch();
        if (cancelCrouching) StopCrouch();

        crouching = false;
        cancelCrouching = false;
    }

    private void Animate() {
        if(movementInput.magnitude > 0.2) {
            animator.SetBool("isRunning", true);
            EmitAtLocation();
        }
        else {
            animator.SetBool("isRunning", false);
        }
    }

    private void StartCrouch() {
        transform.localScale = crouchScale;
        transform.position = new Vector3(transform.position.x, transform.position.y + 0.5f, transform.position.z);
        if (rb.velocity.magnitude > 0.5f) {
            if (grounded) {
                rb.AddForce(orientation.transform.forward * slideForce);
            }
        }
    }

    private void StopCrouch() {
        transform.localScale = playerScale;
        transform.position = new Vector3(transform.position.x, transform.position.y - 0.5f, transform.position.z);
    }

    private void Movement() {
        //Extra gravity
        rb.AddForce(Vector3.down * Time.deltaTime * 10);
        
        //Find actual velocity relative to where player is looking
        Vector2 mag = FindVelRelativeToLook();
        float xMag = mag.x, yMag = mag.y;

        //Counteract sliding and sloppy movement
        CounterMovement(x, y, mag);
        
        //If holding jump && ready to jump, then jump
        if (jumping) Jump();
        jumping = false;
        float maxSpeed;

        //Set max speed
        if (walking)
            maxSpeed = this.walkSpeed;
        else
            maxSpeed = this.maxSpeed;

        //If sliding down a ramp, add force down so player stays grounded and also builds speed
        if (transform.localScale == crouchScale && grounded && readyToJump) {
            rb.AddForce(Vector3.down * Time.deltaTime * 3000);
            return;
        }
        
        //If speed is larger than maxspeed, cancel out the input so you don't go over max speed
        if (x > 0 && xMag > maxSpeed) x = 0;
        if (x < 0 && xMag < -maxSpeed) x = 0;
        if (y > 0 && yMag > maxSpeed) y = 0;
        if (y < 0 && yMag < -maxSpeed) y = 0;

        //Some multipliers
        float multiplier = 1f, multiplierV = 1f;
        
        // Movement in air
        if (!grounded) {
            multiplier = 0.5f;
            multiplierV = 0.5f;
        }
        
        // Movement while sliding
        if (grounded && transform.localScale == crouchScale) multiplierV = 0f;

        if (isWallrunning) {
            timer += Time.deltaTime;
            int secondsWallrunning = Convert.ToInt32(timer % 60);
            if (secondsWallrunning > maxWallrunTime) {
                StopWallrun();
            }
        }

        //Apply forces to move player
        rb.AddForce(orientation.transform.forward * y * moveSpeed * Time.deltaTime * multiplier * multiplierV);
        rb.AddForce(orientation.transform.right * x * moveSpeed * Time.deltaTime * multiplier);
    }

    private void Jump() {
        if (grounded && readyToJump) {
            readyToJump = false;

            //Add jump forces
            rb.AddForce(Vector3.up * jumpForce * 1.5f);
            rb.AddForce(normalVector * jumpForce * 0.5f);
            
            //If jumping while falling, reset y velocity.
            Vector3 vel = rb.velocity;
            if (rb.velocity.y < 0.5f)
                rb.velocity = new Vector3(vel.x, 0, vel.z);
            else if (rb.velocity.y > 0) 
                rb.velocity = new Vector3(vel.x, vel.y / 2, vel.z);
            
            Invoke(nameof(ResetJump), jumpCooldown);
            if(movementInput.magnitude > 0.2) {
                animator.SetTrigger("triggerRunningJump");
            } else {
                animator.SetTrigger("triggerJump");
            }
        }

        //Walljump
        if (isWallrunning)
        {
            readyToJump = false;

            //sidewards wallhop
            if (isWallRight) rb.AddForce(-orientation.right * jumpForce * 0.8f);
            if (isWallLeft) rb.AddForce(orientation.right * jumpForce * 0.8f);

            rb.AddForce(Vector3.up * jumpForce * 1.5f);
            rb.AddForce(normalVector * jumpForce * 0.5f);

            //Always add Force forward
            rb.AddForce(orientation.forward * jumpForce * 0.5f);

            Invoke(nameof(ResetJump), jumpCooldown);
        }
    }
    
    private void ResetJump() {
        readyToJump = true;
    }
    
    private float desiredX;
    private void Look() {
        float mouseX = lookInput.x * sensitivity * Time.fixedDeltaTime * sensMultiplier;
        float mouseY = lookInput.y * sensitivity * Time.fixedDeltaTime * sensMultiplier;

        //Find current look rotation
        Vector3 rot = orientation.transform.localRotation.eulerAngles;
        desiredX = rot.y + mouseX;
        
        //Rotate, and also make sure we dont over- or under-rotate.
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -80f, 75f);
        mouseY = Mathf.Clamp(mouseY,-35,60);
        
        //Perform the rotations
        transform.LookAt(playerCam);
        playerCam.transform.localRotation = Quaternion.Euler(xRotation, 0,wallRunCameraTilt);
        orientation.transform.localRotation = Quaternion.Euler(0,desiredX,0);

        //While Wallrunning
        //Tilts camera in .5 seconds
        if (Math.Abs(wallRunCameraTilt) < maxWallrunCameraTilt && isWallrunning && isWallRight)
            wallRunCameraTilt += maxWallrunCameraTilt * 2 * Time.deltaTime;
        if (Math.Abs(wallRunCameraTilt) < maxWallrunCameraTilt && isWallrunning && isWallLeft)
            wallRunCameraTilt -= maxWallrunCameraTilt * 2 * Time.deltaTime;
    
        //Tilts camera back again
        if (wallRunCameraTilt > 0 && !isWallRight && !isWallLeft)
            wallRunCameraTilt -= maxWallrunCameraTilt * 2 * Time.deltaTime;
        if (wallRunCameraTilt < 0 && !isWallRight && !isWallLeft)
            wallRunCameraTilt += maxWallrunCameraTilt * 2 * Time.deltaTime;
    }

    private void CounterMovement(float x, float y, Vector2 mag) {
        if (!grounded || jumping) return;

        //Slow down sliding
        if (transform.localScale == crouchScale) {
            rb.AddForce(moveSpeed * Time.deltaTime * -rb.velocity.normalized * slideCounterMovement);
            return;
        }

        //Counter movement
        if (Math.Abs(mag.x) > threshold && Math.Abs(x) < 0.05f || (mag.x < -threshold && x > 0) || (mag.x > threshold && x < 0)) {
            rb.AddForce(moveSpeed * orientation.transform.right * Time.deltaTime * -mag.x * counterMovement);
        }
        if (Math.Abs(mag.y) > threshold && Math.Abs(y) < 0.05f || (mag.y < -threshold && y > 0) || (mag.y > threshold && y < 0)) {
            rb.AddForce(moveSpeed * orientation.transform.forward * Time.deltaTime * -mag.y * counterMovement);
        }
        
        //Limit diagonal running. This will also cause a full stop if sliding fast and un-crouching, so not optimal.
        if (Mathf.Sqrt((Mathf.Pow(rb.velocity.x, 2) + Mathf.Pow(rb.velocity.z, 2))) > maxSpeed) {
            float fallspeed = rb.velocity.y;
            Vector3 n = rb.velocity.normalized * maxSpeed;
            rb.velocity = new Vector3(n.x, fallspeed, n.z);
        }
    }

    /// <summary>
    /// Find the velocity relative to where the player is looking
    /// Useful for vectors calculations regarding movement and limiting movement
    /// </summary>
    /// <returns></returns>
    public Vector2 FindVelRelativeToLook() {
        float lookAngle = orientation.transform.eulerAngles.y;
        float moveAngle = Mathf.Atan2(rb.velocity.x, rb.velocity.z) * Mathf.Rad2Deg;

        float u = Mathf.DeltaAngle(lookAngle, moveAngle);
        float v = 90 - u;

        float magnitue = rb.velocity.magnitude;
        float yMag = magnitue * Mathf.Cos(u * Mathf.Deg2Rad);
        float xMag = magnitue * Mathf.Cos(v * Mathf.Deg2Rad);
        
        return new Vector2(xMag, yMag);
    }

    private void isGrounded()
    {
        float sphereRadius = 0.5f;
        float castDistance = 1f;
        // Cast a sphere fownwards to detect if there is something underneath the player with layer gorund
        RaycastHit hit;
        grounded = Physics.SphereCast(this.transform.position + col.center, sphereRadius, Vector3.down, out hit, castDistance, whatIsGround);
    }

    // Player Collision
    private void OnTriggerEnter(Collider other)
    {
        DeathCanvas.SetActive(true);
        isDead = true;
        if (other.name == "Win")
        {
            UIText.text = "You won!";
        }
    }

    void EmitAtLocation()
    {
        splatterParticles.transform.position = transform.position;
        splatterParticles.transform.rotation = Quaternion.LookRotation(Vector3.up);
        ParticleSystem.MainModule psMain = splatterParticles.main;
        psMain.startColor = particleColorGradient.Evaluate(UnityEngine.Random.Range(0f, 1f));
        splatterParticles.Emit(1);
    }
}
