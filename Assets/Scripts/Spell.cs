using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Spell : MonoBehaviour
{
    public int Id;
    public string Name;
    public string Lore;
    public string[] Type; // np. offensive, buff, magic-level-related, magic-missile, healing (?) 
    public int CastingNumber; //poziom mocy
    public float Range; // zasiêg
    public int Strength; // si³a zaklêcia
    public int AreaSize; // obszar dzia³ania
    public int CastingTime; // czas rzucania zaklêcia
    public int CastingTimeLeft;
    public int Duration; // czas trwania zaklêcia

    public bool SaveTestRequiring; // okreœla, czy zaklêcie powoduje koniecznoœæ wykonania testu obronnego
    public string[] Attribute; // okreœla cechê, jaka jest testowana podczas próby oparcia siê zaklêciu lub cechê na któr¹ wp³ywa zaklêcie (np. podnosi j¹ lub obni¿a). Czasami jest to wiêcej cech, np. Pancerz Etery wp³ywa na ka¿d¹ z lokalizacji

    public bool ArmourIgnoring; // ignoruj¹cy zbrojê
    public bool WtIgnoring; // ignoruj¹cy wytrzyma³oœæ
    public bool Stunning;  // og³uszaj¹cy
    public bool Paralyzing; // wprowadzaj¹cy w stan bezbronnoœci
}
