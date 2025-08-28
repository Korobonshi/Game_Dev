using UnityEngine;
using UnityEngine.UI;

public class InputPlayer : MonoBehaviour
{
    private Energy EnergySystem;
    
    void Start()
    {
        EnergySystem = GetComponent<Energy>();    
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R)){
            EnergySystem.GainEnergyOnAttack(5f);
        }

        if (Input.GetKeyDown(KeyCode.Mouse0)){
            EnergySystem.UsingUltimate();
        }

        if (Input.GetKeyDown(KeyCode.B)){
            EnergySystem.GainEnergyOnHit(20f);
        }

    }
}
