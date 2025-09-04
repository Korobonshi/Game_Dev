using UnityEngine;

public class Coins : MonoBehaviour
{

    private void OnTriggerEnter2D(Collider2D other){
        if(other.gameObject.CompareTag("Player")){
            Debug.Log("Coin Di ambil Player");
			Debug.Log(other);
            Destroy(gameObject);
        }
    }
}
