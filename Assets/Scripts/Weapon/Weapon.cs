using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;


public class Weapon : WeaponController
{
	public LayerMask enemyLayer;
	

	public float giveDamage(float damage){
		return damage;
	}
	
	// disini buat kasih fungsi yang lain (Melee or Range)
	
}
