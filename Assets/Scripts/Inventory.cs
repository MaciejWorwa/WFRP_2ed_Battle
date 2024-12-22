using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Inventory : MonoBehaviour
{
    public List<Weapon> AllWeapons = new List<Weapon>(); //Wszystkie posiadane przez postać przedmioty
    public Weapon[] EquippedWeapons = new Weapon[2]; //Przedmioty trzymane w rękach (liczba 2 odpowiada za dwie ręce. Gdy mamy broń dwuręczną to w obu rękach będzie kopia tej samej broni)
    public int CopperCoins;
    public int SilverCoins;
    public int GoldCoins;
}
