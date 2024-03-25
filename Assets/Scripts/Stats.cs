using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Stats : MonoBehaviour
{
    public int Id;

    [Header("Imię")]
    public string Name;

    [Header("Rasa")]
    public string Race;

    [Header("Cechy pierwszorzędowe")]
    public int WW;
    public int US;
    public int K;
    public int Odp;
    public int Zr;
    public int Int;
    public int SW;
    public int Ogd;

    [Header("Cechy drugorzędowe")]
    public int A;
    public int S;
    public int Wt;
    public int Sz;
    [HideInInspector] public int TempSz;
    public int Mag;
    public int MaxHealth;
    public int TempHealth;
    public int PP;
    public int PS;

    [Header("Punkty zbroi")]
    public int Armor_head;
    public int Armor_arms;
    public int Armor_torso;
    public int Armor_legs;

    [Header("Zdolności, umiejętności i inicjatywa")]
    public int Initiative;
    public bool InstantReload;
    public bool PrecisionShot;
    public bool StreetFighting;
    public bool StrongBlow;

    [Header("Umiejętności")]
    public int Dodge;

}

