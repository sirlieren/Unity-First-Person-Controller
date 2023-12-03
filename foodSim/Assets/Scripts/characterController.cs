using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class characterController : MonoBehaviour
{
    public bool CanMove { get; private set; } = true;
    private bool isSprinting => canSprint && Input.GetKey(sprintKey);
    private bool shouldJump => Input.GetKey(jumpKey) && charController.isGrounded && !isSliding;
    private bool shouldCrouch => Input.GetKey(crouchKey) && charController.isGrounded && !duringCrouchAnim;

    [Header("Functional Options")]
    [SerializeField] private bool canSprint = true;
    [SerializeField] private bool canJump = true;
    [SerializeField] private bool canCrouch = true;
    [SerializeField] private bool canUseHeadbob = true;
    [SerializeField] private bool willSlideOnSlopes = true;
    [SerializeField] private bool useFootsteps = true;

    [Header("Keys")]
    [SerializeField] private KeyCode sprintKey = KeyCode.LeftShift;
    [SerializeField] private KeyCode jumpKey = KeyCode.Space;
    [SerializeField] private KeyCode crouchKey = KeyCode.LeftControl;

    [Header("Movement Parameters")]
    //x-z Axis
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float sprintSpeed = 7f;
    [SerializeField] private float crouchSpeed = 3f;
    [SerializeField] private float slopeSpeed = 7f;

    //y Axis
    [SerializeField] private float gravity = 30f;
    [SerializeField] private float jumpForce = 10f;

    [Header("Health Parameters")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float timeToRegenStart = 5f; //After taken damage per1 sec later heal starts
    [SerializeField] private float healthValueIncrement = 1; //Health increment per2 value
    [SerializeField] private float healthTimeIncrement = .15f; //Per2 value of seconds health get +per3 
    private float currentHealth;
    private Coroutine regeneratingHealth;
    public static Action<float> OnTakeDamage;
    public static Action<float> OnDamage;
    public static Action<float> OnHeal;

    [Header("Crouch Parameters")]
    [SerializeField] private float crouchHeight = 0.5f;
    [SerializeField] private float standHeight = 2f;
    [SerializeField] private float timeToCrouch = 0.25f;
    [SerializeField] private Vector3 crouchCenter = new Vector3(0, 0.5f, 0);
    [SerializeField] private Vector3 standCenter = new Vector3(0, 0, 0);
    private bool isCrouching = false;
    private bool duringCrouchAnim = false;

    [Header("Look Parameters")]
    [SerializeField, Range(1, 10)] private float lookSpeedX = 3f;
    [SerializeField, Range(1, 10)] private float lookSpeedY = 3f;
    [SerializeField, Range(1, 180)] private float upperLookLimit = 80f;
    [SerializeField, Range(1, 180)] private float lowerLookLimit = 80f;

    [Header("Headbob Parameters")]
    [SerializeField, Range(1, 20)] private float walkBobSpeed = 12.5f;
    [SerializeField] private float walkBobAmount = .05f;
    [SerializeField, Range(1, 20)] private float sprintBobSpeed = 17.5f;
    [SerializeField] private float sprintBobAmount = .1f;
    [SerializeField, Range(1, 20)] private float crouchBobSpeed = 7.5f;
    [SerializeField] private float crouchBobAmount = .025f;
    private float defaultYPos = 0;
    private float timer;

    //Sliding Parameters
    private Vector3 hitPointNormal;

    private bool isSliding
    {
        get
        {
            if (charController.isGrounded && Physics.Raycast(transform.position, Vector3.down, out RaycastHit slopeHit, 2f))
            {
                hitPointNormal = slopeHit.normal;
                return Vector3.Angle(hitPointNormal, Vector3.up) > charController.slopeLimit;
            }
            else
            {
                return false;
            }
        }
    }

    //Sliding Parameters End

    [Header("Footstep Parameters")]
    [SerializeField] private float baseStepSpeed = .5f;
    [SerializeField] private float sprintStepMulipler = .6f;
    [SerializeField] private float crouchStepMulipler = .6f;
    [SerializeField] private AudioSource footStepAS = default;
    [SerializeField] private AudioClip[] woodClips = default;
    [SerializeField] private AudioClip[] grassClips = default;
    [SerializeField] private AudioClip[] stoneClips = default;
    [SerializeField] private AudioClip[] platformClips = default;
    private float footstepTimer = 0;
    private float GetCurrentOffset => isCrouching ? baseStepSpeed * crouchStepMulipler : isSprinting ? baseStepSpeed * sprintStepMulipler : baseStepSpeed;

    //Other Parameters
    private Camera playerCamera;
    private CharacterController charController;



    private Vector3 moveDirection;
    private Vector2 currentInput;

    private float rotationX = 0;

    private void OnEnable()
    {
        OnTakeDamage += ApplyDamage;
    }
    private void OnDisable()
    {
        OnTakeDamage -= ApplyDamage;
    }
    void Awake()
    {
        //Getting Components
        playerCamera = GetComponentInChildren<Camera>();
        charController = GetComponent<CharacterController>();

        //Hiding & Locking Cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
       

        currentHealth = maxHealth;

        defaultYPos = playerCamera.transform.localPosition.y;
    }

    void Update()
    {
        if (CanMove)
        {
            HandleMovementInput();
            HandleMouseLook();

            if (canJump) { HandleJump(); }
            if (canCrouch) { HandleCrouch(); }
            if (canUseHeadbob) { HandleHeadbob(); }
            if (useFootsteps) { HandleFootsteps(); }
      
            ApplyFinalMovements();
        }
    }
    private void HandleMovementInput()
    {
        //Getting Inputs From Player
        currentInput = new Vector2(( (isCrouching ? crouchSpeed : (isSprinting ? sprintSpeed : walkSpeed))) * Input.GetAxis("Vertical"), ((isCrouching ? crouchSpeed : (isSprinting ? sprintSpeed : walkSpeed))) * Input.GetAxis("Horizontal"));

        float moveDirectionY = moveDirection.y;

        //Translating Inputs from Vector2 to Vector3
        moveDirection = (transform.TransformDirection(Vector3.forward) * currentInput.x) + (transform.TransformDirection(Vector3.right) * currentInput.y);
        moveDirection.y = moveDirectionY;
    }

    private void HandleMouseLook()
    {
        //Getting X Input and cooking it
        rotationX -= Input.GetAxis("Mouse Y") * lookSpeedY;
        rotationX = Mathf.Clamp(rotationX, -upperLookLimit, lowerLookLimit);

        //Applying  Rotations
        playerCamera.transform.localRotation = Quaternion.Euler(rotationX, 0, 0);
        transform.rotation *= Quaternion.Euler(0, Input.GetAxis("Mouse X") * lookSpeedX, 0);
    }
    private void HandleJump()
    {
        //Jump
        if (shouldJump)
            moveDirection.y = jumpForce;
    }

    private void HandleCrouch()
    {
        if (shouldCrouch)
            StartCoroutine(CrouchStand());
    }
    private void HandleHeadbob()
    {
        //  disabling headbob while on air
        if (!charController.isGrounded)
            return;
        //Doing Headbob
        if (Mathf.Abs(moveDirection.x) > 0.1f || Mathf.Abs(moveDirection.z) > .1f)
        {
            timer += Time.deltaTime * (isCrouching ? crouchBobSpeed : isSprinting ? sprintBobSpeed : walkBobSpeed);
            playerCamera.transform.localPosition = new Vector3(playerCamera.transform.localPosition.x,
                defaultYPos + Mathf.Sin(timer) * (isCrouching ? crouchBobAmount : isSprinting ? sprintBobAmount : walkBobAmount),
                playerCamera.transform.localPosition.z);
        }

    }
    private void ApplyDamage(float dmg)
    {
        currentHealth -= dmg;
        OnDamage?.Invoke(currentHealth);

        if (currentHealth <= 0)
        {
            KillPlayer();
        }
        else if (regeneratingHealth != null)
        {
            StopCoroutine(RegenerateHealth());
        }
        regeneratingHealth = StartCoroutine(RegenerateHealth());


    }
    private void KillPlayer()
    {
        print("Dead!");

        if (regeneratingHealth != null)
        {
            StopCoroutine(RegenerateHealth());
        }

    }
    private void HandleFootsteps()
    {
        if (!charController.isGrounded)
            return;
        if (currentInput == Vector2.zero)
            return;


        footstepTimer -= Time.deltaTime;

        if (footstepTimer <= 0)
        {
            int layerMaskAS = (-1) - (1 << LayerMask.NameToLayer("Player"));  // Raycasy Everything Except "Player" Layer. 


            if (Physics.Raycast(playerCamera.transform.position, Vector3.down, out RaycastHit hit, 3f, layerMaskAS))
            {

                switch (hit.collider.tag)
                {
                    case "footsteps/Wood":
                        footStepAS.PlayOneShot(woodClips[UnityEngine.Random.Range(0, woodClips.Length - 1)]);
                        break;
                    case "footsteps/Stone":
                        footStepAS.PlayOneShot(stoneClips[UnityEngine.Random.Range(0, stoneClips.Length - 1)]);
                        break;
                    case "footsteps/Grass":
                        footStepAS.PlayOneShot(grassClips[UnityEngine.Random.Range(0, grassClips.Length - 1)]);
                        break;
                    default:
                        footStepAS.PlayOneShot(platformClips[UnityEngine.Random.Range(0, platformClips.Length - 1)]);
                        break;
                }
            }
            footStepAS.pitch = UnityEngine.Random.Range(0.9f, 1.1f);
            footstepTimer = GetCurrentOffset;
        }

    }
    private void ApplyFinalMovements()
    {
        //Applying Gravity
        if (!charController.isGrounded)
        {
            moveDirection.y -= gravity * Time.deltaTime;
        }

        if (willSlideOnSlopes && isSliding)
        {
            moveDirection += new Vector3(hitPointNormal.x, -hitPointNormal.y, hitPointNormal.z) * slopeSpeed;
        }
        //Final stuff for applying movement
        charController.Move(moveDirection * Time.deltaTime);
    }
    private IEnumerator CrouchStand()
    {
        duringCrouchAnim = true;

        //Getting Target Values
        float timeElapsed = 0;
        float targetHeight = isCrouching ? standHeight : crouchHeight;
        float currentHeight = charController.height;
        Vector3 targetCenter = isCrouching ? standCenter : crouchCenter;
        Vector3 currentCenter = charController.center;

        //Equalize the current values to target values with lerp
        while (timeElapsed < timeToCrouch)
        {
            charController.height = Mathf.Lerp(currentHeight, targetHeight, timeElapsed / timeToCrouch);
            charController.center = Vector3.Lerp(currentCenter, targetCenter, timeElapsed / timeToCrouch);
            timeElapsed += Time.deltaTime;
            yield return null;
        }

        charController.height = targetHeight;
        charController.center = targetCenter;
        //changing the current state
        isCrouching = !isCrouching;
        duringCrouchAnim = false;
    }
    private IEnumerator RegenerateHealth()
    {
        yield return new WaitForSeconds(timeToRegenStart);
        WaitForSeconds timeToWait = new WaitForSeconds(healthTimeIncrement);
        while (currentHealth < maxHealth)
        {
            currentHealth += healthValueIncrement;
            if (currentHealth > maxHealth)
                currentHealth = maxHealth;

            OnHeal?.Invoke(currentHealth);
            yield return timeToWait;
        }

        regeneratingHealth = null;

    }

}
