using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class Weapon : MonoBehaviour
{
    public int Id;

    [Header("Nazwa")]
    public string Name;

    [Header("Typ")]
    public string[] Type;
    public bool TwoHanded;

    [Header("Jakość")]
    public string Quality;

    [Header("Siła")]
    public int S;

    [Header("Zasięg")]
    public float AttackRange;

    [Header("Czas przeładowania")]
    public int ReloadTime;
    public int ReloadLeft;

    [Header("Cechy")]
    public bool Defensive; // parujący
    public bool Fast; // szybki
    public bool Impact; // druzgoczący
    public bool Slow; // powolny
    public bool Tiring; // ciężki
    public bool ArmourPiercing; // przebijający zbroje
    public Dictionary<int, int> WeaponsWithReloadLeft = new Dictionary<int, int>(); // słownik zawierający wszystkie posiadane przez postać bronie wraz z ich ReloadLeft

    public void ResetWeapon()
    {
        Id = 0;
        Name = "Pięści";
        Type[0] = "melee";
        TwoHanded = false;
        Quality = "Zwykła";
        S = -4;
        AttackRange = 1.5f;
        ReloadTime = 0;
        ReloadLeft = 0;
        Defensive = false;
        Fast = false;
        Impact = false;
        Slow = false;
        Tiring = false;
        ArmourPiercing = false;
    }
}
