using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class Energy : MonoBehaviour
{
    public float maxEnergy = 100f;
    public float currentEnergy = 100f;

    public Slider energyBar;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        currentEnergy = 0f;
        UpdateEnergyBar();
    }
	
	void Update()
	{
		UpdateEnergyBar();
		StartCoroutine(GainEnergyPerTime());
	}
	
	IEnumerator GainEnergyPerTime()
	{
		if (currentEnergy <= 100)
		{
			currentEnergy+= 0.1f;
		}
		else if (currentEnergy == 0) currentEnergy =0;
		yield return new WaitForSeconds(1f);
		
	}

    public void GainEnergyOnHit (float amount)
    {
        currentEnergy = Mathf.Clamp(currentEnergy + amount, 0f, maxEnergy);
        Debug.Log("Energi bertambah karena diserang! Energi saat ini: " + currentEnergy);

        UpdateEnergyBar();
    }

    public void DecreaseEnergyOnAttack(float amount)
    {
        currentEnergy = Mathf.Clamp(currentEnergy - amount, 0f, maxEnergy);
        Debug.Log("Energi berkurang karena menyerang! Energi saat ini: " + currentEnergy);

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
