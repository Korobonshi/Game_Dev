using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using UnityEngine.EventSystems;
using System;

public class WeaponController : MonoBehaviour
{
	public InputManager player;
	
    void Awake()
    {
		player = GetComponentInParent<InputManager>();
    }
	
	// ini bisa dipakai fungsi combo
}
