using UnityEngine;

public class EnemyAI : Enemy
{
    public Transform player;          // Target
    public float attackRange = 0.5f;  // Jarak serang
    public float attackCooldown = 1f; // Delay antar serangan
	
	private Rigidbody2D rb;
    //private float nextAttackTime = 0.3f;

	[SerializeField] private GameObject foundPlayer;
	
	void Start()
	{
		rb = GetComponent<Rigidbody2D>();
		if (player == null)
        {
            foundPlayer = GameObject.FindGameObjectWithTag("Player");
            if (foundPlayer != null)
                player = foundPlayer.transform;
			else Debug.LogWarning("Player tidak ditemukan");
        }
	}
	
	void Update()
    {
        Move();
    }
	
	public override void Move()
    {
		player = foundPlayer.transform;
        float distance = Vector2.Distance(transform.position, player.position);
		
		// Kejar player
		Vector2 direction = (player.position - transform.position).normalized;
		rb.linearVelocity = direction * moveSpeed;

		// Hadap ke arah player
		if (direction.x > 0) transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
		else if (direction.x < 0) transform.localScale = new Vector3(-0.5f, 0.5f, 0.5f);
    }

    public override void Attack()
    {
        Debug.Log($"{gameObject.name} attacking!");
      
    }


}
