using UnityEngine;

public class Coins : MonoBehaviour
{
    private void coinCollider(Collider2D other){
        if(other.gameObject.CompareTag("Player")){
            Debug.Log("Coin Di ambil Player");
            Destroy(gameObject);
        }
    }
}
