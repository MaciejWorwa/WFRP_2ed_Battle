using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Inventory : MonoBehaviour
{
    public List<Weapon> allWeapons = new List<Weapon>(); //Wszystkie posiadane przez postać przedmioty
    public Weapon[] heldWeapons = new Weapon[2]; //Przedmioty trzymane w rękach (liczba 2 odpowiada za dwie ręce. Gdy mamy broń dwuręczną to w obu rękach będzie kopia tej samej broni)
}
