using System.Collections.Generic;
using UnityEngine;

public class WeaponsPool : MonoBehaviour
{
    [SerializeField] private GameObject _weaponPrefab; // Prefab broni
    private Queue<GameObject> _weaponsQueue = new Queue<GameObject>(); // Kolejka dla puli broni

    private List<GameObject> _weapons = new List<GameObject>();
    private int _weaponsAmount = 0;

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
            GameObject weaponObj = Instantiate(_weaponPrefab, this.transform);

            _weapons.Add(weaponObj);
            _weaponsAmount ++;
            weaponObj.name = "Weapon " + _weaponsAmount.ToString();

            weaponObj.SetActive(false);
            _weaponsQueue.Enqueue(weaponObj);
        }
    }

    // Pobieranie broni z puli
    public GameObject GetWeapon()
    {
        if (_weaponsQueue.Count == 0)
        {
            InitializePool(1); // Jeśli pula jest pusta, dodaj nowy element
        }

        GameObject weapon = _weaponsQueue.Dequeue();
        weapon.SetActive(true);
        return weapon;
    }

    // Zwracanie broni do puli
    public void ReturnWeaponToPool(GameObject weapon)
    {
        weapon.SetActive(false);
        _weaponsQueue.Enqueue(weapon);
    }
}
