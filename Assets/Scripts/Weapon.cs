using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Weapon : MonoBehaviour
{
    public int Id;

    [Header("Nazwa")]
    public string Name;

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


    public Dictionary<int, int> WeaponsInInventory = new Dictionary<int, int>(); // słownik zawierający wszystkie posiadane przez postać bronie wraz z ich ReloadLeft
}
