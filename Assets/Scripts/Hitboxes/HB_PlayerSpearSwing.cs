﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HB_PlayerSpearSwing : HitboxBase
{
    [SerializeField] private PlayerController _PC;
    [SerializeField] private EntityController _EC;

    private int playerStats;

    public override void OnHit(EntityController en)
    {
        if (_PC.currentHeat > 0f && _PC.criticalHeat)
        {
            float consumedHeat = (_PC.currentHeat > _PC.heatConsumption) ? _PC.heatConsumption : _PC.currentHeat;
            _PC.currentHeat -= consumedHeat;
            if (_PC.currentHeat <= 0f) _PC.criticalHeat = false;
            _PC.SetHeatSlider(_PC.currentHeat);
        }
        // Apply knockback.
        en.ApplyVelocity(_PC.lastSwingDirection, 15f);
        Debug.Log("Hit : " + en.entityName);
    }
}
