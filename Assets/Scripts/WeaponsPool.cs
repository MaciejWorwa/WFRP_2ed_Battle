using System.Collections.Generic;
using UnityEngine;

public class WeaponsPool : MonoBehaviour
{
    public GameObject weaponPrefab; // Prefab broni
    private Queue<GameObject> weaponsQueue = new Queue<GameObject>(); // Kolejka dla puli broni

    public static WeaponsPool Instance; // Singleton dla łatwego dostępu

    private void Awake()
    {
        Instance = this;
        InitializePool();
    }

    // Inicjalizacja puli z określoną liczbą broni
    private void InitializePool(int initialCount = 2)
    {
        for (int i = 0; i < initialCount; i++)
        {
            GameObject weaponObj = Instantiate(weaponPrefab, this.transform);
            weaponObj.SetActive(false);
            weaponsQueue.Enqueue(weaponObj);
        }
    }

    // Pobieranie broni z puli
    public GameObject GetWeapon()
    {
        if (weaponsQueue.Count == 0)
        {
            InitializePool(1); // Jeśli pula jest pusta, dodaj nowy element
        }

        GameObject weapon = weaponsQueue.Dequeue();
        weapon.SetActive(true);
        return weapon;
    }

    // Zwracanie broni do puli
    public void ReturnWeaponToPool(GameObject weapon)
    {
        weapon.SetActive(false);
        weaponsQueue.Enqueue(weapon);
    }

    public void ResetPool()
    {
        weaponsQueue.Clear();

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;
            Destroy(child);
        }

        InitializePool();
    }
}
