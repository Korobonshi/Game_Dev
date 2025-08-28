using UnityEngine;
using UnityEngine.UI;

public class Energy : MonoBehaviour
{
    public float maxEnergy = 100f;
    public float currentEnergy;

    public Slider energyBar;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        currentEnergy = 0f;

        UpdateEnergyBar();
    }


    public void GainEnergyOnHit (float amount)
    {
        currentEnergy = Mathf.Clamp(currentEnergy + amount, 0f, maxEnergy);
        Debug.Log("Energi bertambah karena diserang! Energi saat ini: " + currentEnergy);

        UpdateEnergyBar();

    }

    public void GainEnergyOnAttack(float amount)
    {
        currentEnergy = Mathf.Clamp(currentEnergy + amount, 0f, maxEnergy);
        Debug.Log("Energi bertambah karena menyerang! Energi saat ini: " + currentEnergy);

        UpdateEnergyBar();

    }

    public void UsingUltimate ()
    {
        if (currentEnergy >= maxEnergy){
            currentEnergy = 0f;

            UpdateEnergyBar();
        }else{
            Debug.Log("Energi Belum Cukup");
        }
    }
    // Update is called once per frame
    private void UpdateEnergyBar()
    {
        energyBar.value = currentEnergy / maxEnergy;
    }
}
