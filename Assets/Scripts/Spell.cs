using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Spell : MonoBehaviour
{
    public int Id;
    public string Name;
    public int PoziomMocy; //przet³umaczyæ
    public float Range;
    public string[] Type; // np. offensive, buff, magic-bolt, healing (?) 
    public int Strength;
    public float AreaSize;
    public int CastDuration;
    public int Duration;

}
