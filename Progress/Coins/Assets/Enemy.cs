using UnityEngine;

public class Enemy : MonoBehaviour
{
    public int health = 100;
    public GameObject Coins;
    public int numberCoin = 1;

    public void TakeDameg(int damage){
        
        health -= damage;

        if (health <= 0){
            Die();
        }
    }

    public void Die(){
        Debug.Log ("Musuh Mati");

        DropCoins();

        Destroy(gameObject);
    }

    private void DropCoins(){
        for (int i=0; i<=numberCoin; i++){
            Instantiate(Coins, transform.position, Quaternion.identity);

        }

    }
}
