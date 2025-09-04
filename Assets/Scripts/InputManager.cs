using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class InputManager : MonoBehaviour
{
	public event Action OnAttack; // event serang
	public event Action OnDashing;
	public event Action OnJumping;
	
	public PlayerInput playerInput;

	private Vector2 movementInput;


    void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
		playerInput.actions["Attack"].performed += ctx => HandleAttack();
		playerInput.actions["Jump"].performed += ctx => Jumping();
		playerInput.actions["Move"].started += ctx => Dashing(); // pake move dashnya
    }
	
	void HandleAttack()
	{
		OnAttack ?.Invoke(); // sama kayak if OnInteraction != null --> function
	}
	
	void Jumping()
	{
		OnJumping ?.Invoke();
	}
	
	void Dashing()
	{
		OnDashing ?.Invoke();
	}
	
	public Vector2 GetMovementVector()
	{
		Vector2 inputVector = playerInput.actions["Move"].ReadValue<Vector2>();
		inputVector = inputVector.normalized;
		
		return inputVector;
	}
	
	
	
	

}
