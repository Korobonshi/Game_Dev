using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class Attack : WeaponController
{
	private ParticleSystem punchEffect;
	private Transform punchPoint;
	private Animator animator;
	public Energy energy;
	
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
		animator = GetComponent<Animator>();
		punchEffect = transform.GetChild(0).GetComponent<ParticleSystem>();
        player.OnAttack += Serang;
    }

    // Update is called once per frame
    void Serang()
	{
		if(animator.GetBool("isPunch"))
		{}
		else{
			energy.DecreaseEnergyOnAttack(20f);
			Debug.Log("Menyerang");
			animator.SetTrigger("Punch");
		}
	}
	
	public void ParticleEffect()
	{
		punchEffect.Play();
	}
}
