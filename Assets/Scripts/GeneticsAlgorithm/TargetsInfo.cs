using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class TargetsInfo
{
    public Unit Closest = null;
    public float ClosestDistance = float.MaxValue;

    public Unit Furthest = null;
    public float FurthestDistance = 0f;

    public Unit MostInjured = null;
    public float MostInjuredHP = float.MaxValue;

    public Unit LeastInjured = null;
    public float LeastInjuredHP = 0f;

    public Unit Weakest = null;   // Najniższy Overall
    public int WeakestOverall = int.MaxValue;

    public Unit Strongest = null; // Najwyższy Overall
    public int StrongestOverall = 0;

    public Unit WithMostAllies = null;
    public int WithMostAlliesScore = int.MinValue;

    // Odległość do KAŻDEGO innego Unit
    public Dictionary<Unit, float> Distances = new Dictionary<Unit, float>();
}