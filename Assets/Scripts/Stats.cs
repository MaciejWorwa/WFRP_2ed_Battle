using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Stats: MonoBehaviour
{
    public int Id = 2;
    public string Name;

    [SerializeField] private int _Sz = 4;
    public int Sz
    {
        get { return _Sz; }
        set { _Sz = value; }
    }
    [SerializeField] private int _TempSz = 4;
    public int TempSz
    {
        get { return _TempSz; }
        set { _TempSz = value; }
    }
    [SerializeField] private int _TempHealth;
    public int TempHealth
    {
        get { return _TempHealth; }
        set { _TempHealth = value; }
    }
    [SerializeField] private int _MaxHealth = 10;
    public int MaxHealth
    {
        get { return _MaxHealth; }
        set { _MaxHealth = value; }
    }
}



