using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Spell : MonoBehaviour
{
    public int Id;
    public string Name;
    public string[] Type; // np. offensive, buff, magic-bolt, healing (?) 
    public int CastingNumber; //poziom mocy
    public float Range;
    public int Strength;
    public float AreaSize;
    public int CastingTime;
    public int Duration;

}
