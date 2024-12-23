using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.TextCore.Text;
using static UnityEngine.GraphicsBuffer;
using System.Linq;

public class Stats : MonoBehaviour
{
    public int Id;
    public int Index;
    public bool IsBig;
    public int Overall; // Łączna wartość bojowa jednostki
    public int Exp; // Punkty doświadczenia

    [Header("Imię")]
    public string Name;

    [Header("Rasa")]
    public string Race;

    [Header("Id początkowej broni")]
    public int PrimaryWeaponId;

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
    public int PO;
    public int PP;
    public int PS;

    [Header("Punkty zbroi")]
    public int Armor_head;
    public int Armor_arms;
    public int Armor_torso;
    public int Armor_legs;

    [Header("Inicjatywa")]
    public int Initiative;

    [Header("Zdolności")]
    public bool Ambidextrous; // Oburęczność
    public bool Disarm; // Rozbrojenie
    public bool Ethereal; // Eteryczny
    public bool FastHands; //Dotyk mocy
    public bool Fearless; // Nieustraszony
    public bool Frightening; // Straszny (test Fear)
    public bool LightningParry; // Błyskawiczny blok
    public bool MagicSense; //Zmysł magii
    public bool MasterGunner; // Artylerzysta
    public bool MightyShot; // Strzał precyzyjny
    public bool MightyMissile; // Morderczy pocisk
    public bool PowerfulBlow; // Potężny cios (parowanie -30)
    public bool RapidReload; // Błyskawiczne przeładowanie
    public bool Sharpshooter; // Strzał przebijający
    public bool StoutHearted; // Odwaga
    public bool StreetFighting; // Bijatyka
    public bool StrikeMightyBlow; // Silny cios
    public bool StrikeToStun; // Ogłuszanie
    public bool Sturdy; // Krzepki
    public bool SureShot; // Strzał przebijający
    public bool Terryfying; // Przerażający (test Terror)
    public bool QuickDraw; // Szybkie wyciągnięcie

    [Header("Umiejętności")]
    public int Channeling; // Splatanie magii
    public int Dodge; // Unik

    [Header("Statystyki")]
    public int HighestDamageDealt; // Największe zadane obrażenia
    public int TotalDamageDealt; // Suma zadanych obrażeń
    public int HighestDamageTaken; // Największe otrzymane obrażenia
    public int TotalDamageTaken; // Suma otrzymanych obrażeń
    public int OpponentsKilled; // Zabici przeciwnicy
    public string StrongestDefeatedOpponent; // Najsilniejszy pokonany przeciwnik
    public int StrongestDefeatedOpponentOverall; // Overall najsilniejszego pokonanego przeciwnika
    public int RoundsPlayed; // Suma rozegranych rund
    public int FortunateEvents; // Ilość "Szczęść"
    public int UnfortunateEvents; // Ilość "Pechów"

    private void Start()
    {
        Overall = CalculateOverall();
    }

    public void RollForBaseStats()
    {
        WW += Random.Range(2, 21);
        US += Random.Range(2, 21);
        K += Random.Range(2, 21);
        Odp += Random.Range(2, 21);
        Zr += Random.Range(2, 21);
        Int += Random.Range(2, 21);
        SW += Random.Range(2, 21);
        Ogd += Random.Range(2, 21);

        int rollMaxHealth = Random.Range(1, 11);
        if (rollMaxHealth <= 6 && rollMaxHealth > 3)
        {
            MaxHealth += 1;
            TempHealth += 1;
        }   
        else if (rollMaxHealth <= 9)
        {
            MaxHealth += 2;
            TempHealth += 2;
        }
        else if (rollMaxHealth == 10)
        {
            MaxHealth += 3;
            TempHealth += 3;
        }

        int rollPP = Random.Range(1, 11);
        if (rollPP <= 4)
            PP = 2;
        else if (rollPP <= 7)
            PP = 3;
        else if (rollPP >= 8)
            PP = 3;

        if (Race == "Elf")
        {
            PP--;
        }
        else if (Race == "Krasnolud")
        {
            if (PP != 3) PP--;
        }
        else if (Race == "Niziołek")
        {
            if (rollPP <= 7 && rollPP > 4) PP--;
        }

        PS = PP;
    }

    public void CheckForSpecialRaceAbilities()
    {
        //Zdolność regeneracji
        if (Race == "Troll")
        {
            int regeneration = Random.Range(0, 11);
            int currentWounds = 0;

            if (TempHealth < MaxHealth)
            {
                currentWounds = MaxHealth - TempHealth;
            }
            else return;

            int woundsToHeal = regeneration < currentWounds ? regeneration : currentWounds;
            TempHealth += woundsToHeal;
            this.GetComponent<Unit>().DisplayUnitHealthPoints();

            Debug.Log($"{Name} zregenerował {woundsToHeal} żywotności.");
        }
    }
    
    public int CalculateOverall()
    {
        // Wyznacza większą wartość między WW i US
        int maxWWorUS = Mathf.Max(WW, US);
        int minWWorUS = Mathf.Min(WW, US);

        // Sumowanie cech pierwszorzędowych z uwzględnieniem mnożenia większej wartości (WW lub US) przez ilość Ataków
        int primaryStatsSum = ((maxWWorUS * A) + minWWorUS);

        // Sumowanie zbroi i wytrzymałości
        int totalArmor = Armor_head + Armor_arms + Armor_torso + Armor_legs + ((Odp / 10) * 4);

        int weaponPower = 0;

        Weapon weapon = InventoryManager.Instance.ChooseWeaponToAttack(this.gameObject);
        if(weapon != null)
        {
            if(weapon.Type.Contains("ranged"))
            {
                weaponPower += weapon.S * 8;
            }
            else if(weapon.Type.Contains("melee"))
            {
                weaponPower += weapon.S + (K / 10) * 8;
            }

            weaponPower = weapon.S;
            if(weapon.Impact == true) weaponPower += (maxWWorUS * A) / 2;  
        }

        // Zliczanie aktywnych zdolności
        int activeAbilitiesCount = GetType()
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Where(field => field.FieldType == typeof(bool) && (bool)field.GetValue(this))
            .Count();

        // Obliczanie Overall
        int overall = (primaryStatsSum / 3) + weaponPower + MaxHealth + Sz + totalArmor * 2 + activeAbilitiesCount + Channeling + (Dodge * Zr / 5) + (SW / 5);

        return overall;
    }

    //Zwraca kopię tej klasy
    public Stats Clone()
    {
        return (Stats)this.MemberwiseClone();
    }
}
