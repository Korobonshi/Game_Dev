using UnityEngine;
using UnityEngine.UI;

public abstract class Enemy : MonoBehaviour
{
	public float healthPoint;
	public float moveSpeed;
	public float maxHealth = 100;
	
	public abstract void Move();
	public abstract void Attack();
	
	private RectTransform barHp;
	
	
	public virtual void takeDamage(float amount)
	{
		Debug.Log("AI being Attacked");
		healthPoint -= amount;
		barHpMove();
		
		if (healthPoint <= 0)
		{
			Die();
		}
	}
	
	public virtual void barHpMove()
	{
		barHp = transform.GetChild(0).GetChild(0).GetComponent<RectTransform>();
		float hpPercent = Mathf.Clamp01(healthPoint / maxHealth);

        // Ambil ukuran awal (width) bar HP
        float fullWidth = 3f; // ganti dengan lebar asli barHp (misalnya 100 pixel)
        
        // Ubah width
        barHp.sizeDelta = new Vector2(fullWidth * hpPercent, barHp.sizeDelta.y);

        // Geser posisinya ke kiri (pivot harus di tengah atau kanan)
        float offsetX = (fullWidth - barHp.sizeDelta.x) / 2f;
        barHp.anchoredPosition = new Vector2(-offsetX, barHp.anchoredPosition.y);
	}
	
	public virtual void Die()
	{
		Destroy(gameObject);
	}
}
