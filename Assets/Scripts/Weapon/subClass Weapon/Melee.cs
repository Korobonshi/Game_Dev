using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;


public class Melee : Weapon
{
	private float attackAngle = -90f;
	private float attackSpeed = 0.15f;
	private float returnSpeed = 0.25f;
	
	public float damage = 20f;
	
	public Transform swordTip;                 // Ujung pedang
    public Transform swordBase;                // Pangkal pedang
	private Vector2 lastTipPos;
	
	private Quaternion originalRotation;
    private Quaternion targetRotation;
	private bool isAttacking = true;
	
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
		originalRotation = transform.localRotation;
        targetRotation = originalRotation;
        player.OnAttack += Attack;
    }

	void Attack()
	{
		StartCoroutine(attackRoutine());
		DetectHit();
        lastTipPos = swordTip.position;
	}
	
	IEnumerator attackRoutine()
	{	
		isAttacking = false;
		if (!isAttacking)
			{
			Quaternion attackRotation = Quaternion.Euler(0f,0f,attackAngle);
			
			float t = 0;
			while (t < 1f)
			{

				t += Time.deltaTime / attackSpeed;
				transform.localRotation = Quaternion.Lerp(originalRotation, attackRotation, t);
				yield return null;
			}

			// Lerping kembali ke posisi awal
			t = 0f;
			while (t < 1f)
			{
				t += Time.deltaTime / returnSpeed;
				transform.localRotation = Quaternion.Lerp(attackRotation, originalRotation, t);
				yield return null;
			}
		}
		isAttacking = true;
	}
	
	void DetectHit()
    {
        Vector2 currentTipPos = swordTip.position;
		Vector2 direction = lastTipPos - currentTipPos;
		
		float distance = 1;
	
		// Atau alternatif:
		Debug.DrawRay(transform.position, -(direction.normalized * distance), Color.red, 1f);
		
		RaycastHit2D hit = Physics2D.Raycast(lastTipPos, -(direction.normalized), distance, enemyLayer);
		
		if (hit.collider != null)
		{
			EnemyAI enemy = hit.collider.GetComponent<EnemyAI>();
			enemy.takeDamage(damage);
		}
    }
}
