using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class Player : MonoBehaviour
{
	InputManager _inputManager;
	
	private Rigidbody2D rb;
	private bool isGrounded = true;
	private bool isDashing = false;
	
	private Vector3 lastMoveDir;
	private Animator animator;
	
	[SerializeField]float moveSpeed = 5f;
	[SerializeField]float jumpForce = 10f;
	[SerializeField]private bool doubleJumpState = false;
	
	[SerializeField]private float dashDuration = 0.2f;
	[SerializeField]private float dashCooldown = 3f;
	
	[SerializeField]float dashForce = 15f;
	[SerializeField]float lastDir;
	[SerializeField]float lastTapTime;
	[SerializeField]float doubleTapTime = 0.3f;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
		_inputManager = GetComponent<InputManager>();
		animator = GetComponent<Animator>();
    }
	
	void Start()
	{
		_inputManager.OnDashing += Dash;
		_inputManager.OnJumping += Jump;
	}

    // Update is called once per frame
    void Update()
    {
		if (!isDashing) 
		{
			handleMovement();
		}
    }
		
	void handleMovement()
	{
		// ini buat jalan
		Vector2 inputVector = _inputManager.GetMovementVector();
		rb.linearVelocity = new Vector2(inputVector.x * moveSpeed, rb.linearVelocity.y);
		if (inputVector.x > 0.01f) transform.localScale = new Vector3(0.1f,0.1f,0.1f);
		else if (inputVector.x < -0f) transform.localScale = new Vector3 (-0.1f,0.1f,0.1f);
		
		if (inputVector.x != 0f)
		{
			 animator.SetBool("isRunning", true);
		}
		else animator.SetBool("isRunning",false);
	}
	
	
	
	private void Dash()
	{
		// ini buat dash
		float dir = Mathf.Sign(_inputManager.playerInput.actions["Move"].ReadValue<Vector2>().x);
		if (dir != 0)
		{
			if (dir == lastDir && Time.time - lastTapTime <= doubleTapTime)
				StartCoroutine(DashCoroutine(dir));
			lastDir = dir;
			lastTapTime = Time.time;
		}
	}
	
		
	private IEnumerator DashCoroutine(float direction)
    {
        isDashing = true;

        Vector3 currentDir = new Vector3(direction, 0f, 0f);
			
		// vfx and sfx taro sini
		
        rb.linearVelocity = new Vector2((currentDir.x)*dashForce, 0f); // Apply dash force on rb
        yield return new WaitForSeconds(dashDuration);

        rb.linearVelocity = Vector2.zero; // Stop dashing movement, should be a momentum here

        isDashing = false;
        yield return new WaitForSeconds(dashCooldown);
    }
	
	
	private void Jump()
	{
		// ini buat jump
		// Lompatan pertama (di tanah)
		if (isGrounded)
		{
			rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
			isGrounded = false;
			doubleJumpState = true; // reset double jump
		}
		// Lompatan kedua (di udara)
		else if (doubleJumpState)
		{
			rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f); // reset kecepatan vertikal biar lompat stabil
			rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
			doubleJumpState = false;
		}
	}
	
	void OnCollisionEnter2D(Collision2D obj)
	{
		isGrounded = true;
        doubleJumpState = true; // bisa double jump lagi
	}
	
	

}
