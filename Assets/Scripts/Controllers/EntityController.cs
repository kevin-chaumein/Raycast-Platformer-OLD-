﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public delegate Vector2 VelocityCompoundMethod();
public delegate void StateChangeAction();
public enum FactionList { NEUTRAL, PLAYER, ENEMIES};
public enum EntityMotionState { GROUNDED, AIR, CLUTCH };

public class EntityController : MonoBehaviour
{
    private Rigidbody2D rb;
    public CircleCollider2D mainCollider;
    public BoxCollider2D headCollider;
    private VelocityCompoundMethod vOverride, vAdd, vMult;
    private StateChangeAction eOnLanding;

    [Header("Entity Descriptors")]
    public string entityName;
    public int health, maxHealth;
    public FactionList faction;

    [Header("Runtime Statistics")]
    public EntityMotionState state;
    public Vector2 currentVelocity = Vector2.zero;
    public Vector2 externalVelocity = Vector2.zero;
    public bool facingRight = true;

    [Header("Physics Parameters")]
    public Vector2 normalVector;
    public bool gravityOn;
    public float gravityCoeff;
    public float maxGravitySpeed;

    [Header("Runtime Trackers")]
    public float subAirTime = 0;
    public float totalAirTime = 0;
    public Vector2 directionalInfluence = Vector2.zero;

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        mainCollider = GetComponent<CircleCollider2D>();
        headCollider = GetComponent<BoxCollider2D>();
        if (rb == null)
        {
            Debug.LogError("Missing Rigidbody2D for " + gameObject);
        }
        else
        {
            Physics2D.IgnoreLayerCollision(8, 8, true);
        }
    }

    public bool FacingRight() => facingRight;

    public void SetVelocity(Vector2 ve) => currentVelocity = ve;

    public void SetVelocityFunctions(VelocityCompoundMethod over, VelocityCompoundMethod add, VelocityCompoundMethod mult)
    {
        vOverride = over;
        vAdd = add;
        vMult = mult;
    }

    public void SetEventFunctions(StateChangeAction landing)
    {
        eOnLanding = landing;
    }

    public void Update()
    {
        // Raycast up and down to check for floors.
        RaycastHit2D floorCheck = Physics2D.CircleCast((Vector2)transform.position + mainCollider.offset, mainCollider.radius, normalVector * -1f, 0.05f, LayerInfo.TERRAIN);
        //RaycastHit2D ceilingCheck = Physics2D.CircleCast((Vector2)transform.position + headCollider.offset, 0.49f, Vector2.up, 0.05f, terrainLayer);

        // If there is ground below us.
        if (floorCheck.collider != null)
        {
            // If we just hit the ground coming out from another state, reset jumps and air statistics.
            if (state != EntityMotionState.GROUNDED) OnLanding();
        }
        // If we don't detect any ground below us, go ahead and fall off.
        else
        {
            // TODO: One-time "we left the ground" check.
            // The first frame we leave coyote time, subtract a jump.
            state = EntityMotionState.AIR;
            subAirTime += Time.deltaTime;

            //WhileInAir();
        }
        if (directionalInfluence.x > 0) facingRight = true;
        if (directionalInfluence.x < 0) facingRight = false;
    }

    public void FixedUpdate()
    {
        currentVelocity = CompoundVelocities(vOverride, vAdd, vMult);
        rb.velocity = currentVelocity;
        DecayExternalVelocity(0.1f);
    }

    private Vector2 CompoundVelocities(VelocityCompoundMethod over, VelocityCompoundMethod add, VelocityCompoundMethod mult)
    {
        Vector2 final = (over == null) ? Vector2.zero : over();
        if (final != Vector2.zero) return final;

        if (add != null) final += add();
        if (gravityOn) final += CalculateGravityVector(this);
        final += externalVelocity;
        if (mult != null) final.Scale(mult());

        return final;
    }

    /*
     * Can also be used externally to find the gravity vector of that entity.
     */
    public Vector2 CalculateGravityVector(EntityController en)
    {
        Vector2 dir = en.normalVector.normalized * -1f;
        float gravSpeed = (en.subAirTime * en.gravityCoeff);
        bool uncapped = en.maxGravitySpeed < 0 ? true : false;
        return dir * (uncapped ? gravSpeed : (gravSpeed > en.maxGravitySpeed ? en.maxGravitySpeed : gravSpeed));
    }

    private void OnLanding()
    {
        state = EntityMotionState.GROUNDED;
        eOnLanding?.Invoke();
        ResetAirTime();
    }

    public void TallyAirTime()
    {
        totalAirTime += subAirTime;
        subAirTime = 0;
    }

    public void ResetAirTime()
    {
        totalAirTime = 0f;
        subAirTime = 0f;
    }

    /*
     * Returns the forward vector of the character based on the facingRight boolean.
     * Extrapolated since it is used in multiple places.
     */
    public Vector2 GetForwardVector()
    {
        return Vector2.right * (facingRight ? 1f : -1f);
    }

    public void ApplyVelocity(Vector2 force)
    {
        externalVelocity = force;
    }

    public void ApplyVelocity(Vector2 dir, float mag)
    {
        externalVelocity = dir * mag;
    }

    /*
     * Smoothly 
     */
    private void DecayExternalVelocity(float rate)
    {
        externalVelocity = Vector2.Lerp(externalVelocity, Vector2.zero, rate);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // First check if the Trigger entered is a hitbox.
        HitboxFramework en;
        en = collision.gameObject.GetComponent<HitboxFramework>();
        if (en == null) return;
        if (en.blacklist.Contains(faction)) return;
        en.OnHit(this);
    }
}
