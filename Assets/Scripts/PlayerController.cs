﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Interactions;
using UnityEngine.Events;

/* PlayerController | Kevin Chau
 * 
 * 
 */

public class PlayerController : MonoBehaviour
{
    public UnityEvent e_Land;

    private enum MotionState { GROUNDED, AIR, CLUTCH };
    private Rigidbody2D rb;
    private PlayerControl playerControls;
    private CircleCollider2D mainCollider;
    private BoxCollider2D headCollider;

    private const int COYOTE_TIME = 10;
    private const float CROUCH_SPEED_MULT = 0.5f;
    private const float BASE_GRAVITY = 5f;

    [SerializeField] private MotionState state;
    [SerializeField] private float runSpeed;
    [SerializeField] private float jumpVelocity;
    [SerializeField] private float dashTime;
    [SerializeField] private float currentDashTime;
    [SerializeField] private float dashSpeed;
    [SerializeField] private float jumpTime;
    [SerializeField] private float jumpTimeCounter;
    [SerializeField] private Vector2 playerInfluence = Vector2.zero;
    [SerializeField] private int subAirTime = 0;
    [SerializeField] private int totalAirTime = 0;
    [SerializeField] private int maxJumps;
    [SerializeField] private int jumps;
    [SerializeField] private int maxDashes;
    [SerializeField] private int dashes;
    [SerializeField] private bool crouching = false;
    [SerializeField] private bool facingRight = true;
    [SerializeField] private bool isJumping = false;
    [SerializeField] private bool isDashing = false;
    [SerializeField] private Vector2 currentVelocity = Vector2.zero;

    private void Awake()
    {
        playerControls = new PlayerControl();

        playerControls.Player.Jump.started += ctx =>
        {
            if (ctx.started)
                InitiateJump();
        };

        playerControls.Player.Jump.canceled += ctx =>
        {
            if (ctx.canceled)
                ReleasedJump();
        };

        playerControls.Player.Dash.started += ctx =>
        {
            if (ctx.started)
                InitiateDash();
        };
    }

    // Start is called before the first frame update
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        mainCollider = GetComponent<CircleCollider2D>();
        headCollider = GetComponent<BoxCollider2D>();
        jumps = maxJumps;
    }

    // Update is called once per frame
    void Update()
    {
        playerInfluence = playerControls.Player.Move.ReadValue<Vector2>();


        // Raycast up and down to check for floors.
        RaycastHit2D floorCheck = Physics2D.CircleCast((Vector2)transform.position + mainCollider.offset, 0.49f, Vector2.down, 0.05f, LayerMask.GetMask("Terrain"));
        RaycastHit2D ceilingCheck = Physics2D.CircleCast((Vector2)transform.position + headCollider.offset, 0.49f, Vector2.up, 0.05f, LayerMask.GetMask("Terrain"));

        // If there is ground below us.
        if (floorCheck.collider != null)
        {
            // If we just hit the ground coming out from another state, reset jumps and air statistics.
            if (state != MotionState.GROUNDED)
            {
                state = MotionState.GROUNDED;
                jumps = maxJumps;
                jumpTimeCounter = jumpTime;
                subAirTime = 0;
            }
        }
        // If we don't detect any ground below us, go ahead and fall off.
        else
        {
            // The first frame we leave coyote time, subtract a jump.
            if (subAirTime == COYOTE_TIME && !isJumping)
            {
                jumps--;
            }
            state = MotionState.AIR;
            subAirTime++;
        }

        if (ceilingCheck.collider != null)
        {
            HitCeiling();
        }

        if (isJumping)
        {
            if (jumpTimeCounter > 0)
            {
                rb.velocity = Vector2.up * jumpVelocity;
                jumpTimeCounter -= Time.deltaTime;
            }
            else
            {
                isJumping = false;
                HitCeiling();
            }
        }

        // Jump if we are on the ground, or if within COYOTE_TIME frames.


        // Holding down to CROUCH.
        crouching = playerInfluence.y < 0 ? true : false;
    }

    private void FixedUpdate()
    {
        Vector2 finalVelocity = new Vector2(
            CompoundXVelocities() * Time.deltaTime,
           // isJumping ? jumpVelocity : 0f
           // ((subAirTime * -0.918f) + (yInfluence * jumpVelocity)) * Time.deltaTime
           rb.velocity.y
        );
        currentVelocity = finalVelocity;
        // Vector2.SmoothDamp(rb.velocity, finalVelocity, ref currentVelocity, 0.05f);
        // rb.MovePosition((Vector2)transform.position + finalVelocity);
        rb.velocity = finalVelocity;
    }

    private void InitiateJump()
    {
        if (state == MotionState.GROUNDED || subAirTime < COYOTE_TIME || jumps > 0)
        {
            isJumping = true;
            jumpTimeCounter = jumpTime;
            rb.velocity = Vector2.up * jumpVelocity;
            jumps--;
            Debug.Log("Jumped! Jumps left: " + jumps);
        }
    }

    private void ReleasedJump()
    {
        if (isJumping)
        {
            isJumping = false;
            HitCeiling();
        }
    }

    /*
     * Gravity suspension dash:
     * Cancels all jumps and momentum, and then propels the player in the appointed direction.
     */
    private void InitiateDash()
    {
        if (isDashing) return;

        // Set some parameters immediately.
        isJumping = false;
        isDashing = true;
        rb.gravityScale = 0f;

        // Save the direction the player is holding input on when the dash initiates.
        Vector2 dir = new Vector2(playerInfluence.x, playerInfluence.y);

        // If we do not have any direction inputted for our dash, default to dashing forward.
        if (dir == Vector2.zero)
        {
            dir = Vector2.right * (facingRight ? 1f : -1f);
        }
        StartCoroutine(DashCoroutine(dir));
    }

    /*
     * Moves the player in the direction provided, with magnitude = dashSpeed.
     * NOTE: |dir| = 1, if x and y are non-zero, they will be ~0.71 (sine/cosine at 45 degree notches).
     */
    private IEnumerator DashCoroutine(Vector2 dir)
    {
        currentDashTime = dashTime;
        while(currentDashTime > 0) {
            rb.AddForce(dir * dashSpeed);
            currentDashTime -= Time.deltaTime;
            yield return 0;
        }
        EndDash();
    }

    /*
     * 
     */
    private void EndDash()
    {
        isDashing = false;
        rb.gravityScale = BASE_GRAVITY;
    }

    private float CompoundXVelocities()
    {
        float total = (playerInfluence.x * runSpeed);
        if (state == MotionState.GROUNDED)
        {
            total *= crouching ? CROUCH_SPEED_MULT : 1f;
        }
        return total;
    }

    private void HitCeiling()
    {
        rb.velocity = new Vector2(rb.velocity.x, jumpVelocity/4f);

    }

    private void OnEnable()
    {
        playerControls.Enable();
    }

    private void OnDisable()
    {
        playerControls.Disable();
    }
}