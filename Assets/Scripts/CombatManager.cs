using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CombatManager : MonoBehaviour
{
    // Prywatne statyczne pole przechowujące instancję
    private static CombatManager instance;

    // Publiczny dostęp do instancji
    public static CombatManager Instance
    {
        get { return instance; }
    }

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            //DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            // Jeśli instancja już istnieje, a próbujemy utworzyć kolejną, niszczymy nadmiarową
            Destroy(gameObject);
        }
    }

    private int _attackModifier;
    private int _defenseModifier;
    private float _attackDistance;

    #region Attack function
    public void Attack(Unit attacker, Unit target) 
    {
        Stats attackerStats = attacker.GetComponent<Stats>();
        Stats targetStats = target.GetComponent<Stats>();

        Weapon attackerWeapon = attacker.GetComponent<Weapon>();
        Weapon targetWeapon = target.GetComponent<Weapon>();

        //Liczy dystans pomiedzy walczącymi
        _attackDistance = CalculateDistance(attacker.gameObject, target.gameObject);

        //Wykonuje atak, jeśli cel jest w zasięgu
        if (_attackDistance <= attackerWeapon.AttackRange || _attackDistance <= attackerWeapon.AttackRange * 2 && attackerWeapon.AttackRange > 1.5f )
        {
            //Sprawdza, czy broń jest naładowana (w przypadku ataku dystansowego)
            if (_attackDistance > 1.5f && attackerWeapon.ReloadLeft != 0)
            {
                Debug.Log($"Broń wymaga przeładowania.");
                return;
            }

            //Rzut na trafienie
            int rollResult = Random.Range(1, 101);

            //Sprawdza, czy atak jest atakiem dystansowym, czy atakiem w zwarciu i ustala jego skuteczność
            bool isSuccessful = CheckAttackEffectiveness(rollResult, attackerStats, attackerWeapon);

            //Zresetowanie bonusu do trafienia
            _attackModifier = 0;

            if (isSuccessful)
            {
                int damageRollResult = DamageRoll(attackerStats, attackerWeapon);
                int damage = CalculateDamage(damageRollResult, attackerStats, attackerWeapon);
                int armor = CalculateArmor(targetStats, attackerWeapon);
               
                Debug.Log($"{attackerStats.Name} wyrzucił {damageRollResult} i zadał {damage} obrażeń.");

                //Zadanie obrażeń
                if (damage > (targetStats.Wt + armor))
                    {
                        targetStats.TempHealth -= damage - (targetStats.Wt + armor);

                        Debug.Log(targetStats.Name + " znegował " + (targetStats.Wt + armor) + " obrażeń.");
                        
                        //Zaktualizowanie punktów żywotności
                        target.GetComponent<Unit>().DisplayUnitHealthPoints();
                        Debug.Log($"Punkty żywotności {targetStats.Name}: {targetStats.TempHealth}/{targetStats.MaxHealth}");
                    }
                else
                {
                    Debug.Log($"Atak {attackerStats.Name} nie przebił się przez pancerz.");
                }

                //Śmierć
                if (targetStats.TempHealth < 0 && GameManager.Instance.IsAutoKillMode)
                {
                    UnitsManager.Instance.DestroyUnit(target.gameObject);

                    //Aktualizuje podświetlenie pól w zasięgu ruchu atakującego (inaczej pozostanie puste pole w miejscu usuniętego przeciwnika)
                    GridManager.Instance.HighlightTilesInMovementRange(attackerStats);
                }
            }
            else
            {
                Debug.Log($"Atak {attackerStats.Name} chybił.");
            }
        }
        else
        {
            Debug.Log("Cel ataku stoi poza zasięgiem.");
        }
    }
    #endregion

    #region Calculating distance
    private float CalculateDistance(GameObject attacker, GameObject target)
    {
        if (attacker != null && target != null)
        {
            _attackDistance = Vector3.Distance(attacker.transform.position, target.transform.position);

            Debug.Log("Dystans: " + _attackDistance);

            return _attackDistance;
        }
        else
        {
            Debug.LogError("Nie udało się ustalić odległości pomiędzy walczącymi.");
            return 0;
        }
    }
    #endregion

    #region Check attack effectiveness
    private bool CheckAttackEffectiveness(int rollResult, Stats attackerStats, Weapon attackerWeapon)
    {
        bool isSuccessful = false;

        //Sprawdza, czy atak jest atakiem dystansowym
        if (_attackDistance > 1.5f)
        {
            _attackModifier -= _attackDistance > attackerWeapon.AttackRange ? 20 : 0;

            isSuccessful = rollResult <= (attackerStats.US + _attackModifier - _defenseModifier);

            if (_attackModifier != 0 || _defenseModifier != 0)
            {
                Debug.Log($"{attackerStats.Name} Rzut na US: {rollResult} Modyfikator: {_attackModifier - _defenseModifier}");
            }
            else
            {
                Debug.Log($"{attackerStats.Name} Rzut na US: {rollResult}");
            }

            //Sprawia, że po ataku należy przeładować broń
            attackerWeapon.ReloadLeft = attackerWeapon.ReloadTime;
            attackerWeapon.WeaponsInInventory[attackerWeapon.Id] = attackerWeapon.ReloadLeft;
            Debug.Log("attackerWeapon.WeaponsInInventory[attackerWeapon.Id] " + attackerWeapon.WeaponsInInventory[attackerWeapon.Id]);

            //Uwzględnia zdolność Błyskawicznego Przeładowania
            if (attackerStats.InstantReload == true)
            {
                attackerWeapon.ReloadLeft--;   
            }
        }

        //Sprawdza czy atak jest atakiem w zwarciu
        if (_attackDistance <= 1.5f)
        {
            isSuccessful = rollResult <= (attackerStats.WW + _attackModifier - _defenseModifier);

            if (_attackModifier > 0 || _defenseModifier > 0)
            {
                Debug.Log($"{attackerStats.Name} Rzut na WW: {rollResult} Modyfikator: {_attackModifier - _defenseModifier}");
            }
            else
            {
                Debug.Log($"{attackerStats.Name} Rzut na WW: {rollResult}");
            }
        }

        return isSuccessful;
    }
    #endregion

    #region Calculating damage
    private int DamageRoll(Stats attackerStats, Weapon attackerWeapon)
    {
        //Rzut na obrażenia
        int damageRollResult;

        //Uwzględnienie broni druzgoczącej
        if (attackerWeapon.Impact == true)
        {
            int roll1 = Random.Range(1, 11);
            int roll2 = Random.Range(1, 11);
            damageRollResult = roll1 >= roll2 ? roll1 : roll2;
            Debug.Log($"Atak druzgoczącą bronią. Rzut na obrażenia nr 1: {roll1} Rzut nr 2: {roll2}");
        }
        else
        {
            damageRollResult = Random.Range(1, 11);
            Debug.Log($"Rzut na obrażenia: {damageRollResult}");
        }

        // Mechanika Furii Ulryka
        if (damageRollResult == 10)
        {
            int confirmRoll = Random.Range(1, 101); //rzut na potwierdzenie Furii
            int additionalDamage = 0; //obrażenia, które dodajemy do wyniku rzutu

            if (_attackDistance <= 1.5f)
            {
                if (attackerStats.WW >= confirmRoll)
                {
                    additionalDamage = Random.Range(1, 11);
                    damageRollResult += additionalDamage;
                    Debug.Log($"Rzut na potwierdzenie {confirmRoll}. FURIA ULRYKA!");
                }
                else
                {
                    Debug.Log($"Rzut na potwierdzenie {confirmRoll}. Nie udało się potwierdzić Furii Ulryka.");
                }
            }
            else if (_attackDistance > 1.5f)
            {
                if (attackerStats.US >= confirmRoll)
                {
                    additionalDamage = Random.Range(1, 11);
                    damageRollResult += additionalDamage;
                    Debug.Log($"Rzut na potwierdzenie {confirmRoll}. FURIA ULRYKA!");
                }
                else
                {
                    Debug.Log($"Rzut na potwierdzenie {confirmRoll}. Nie udało się potwierdzić Furii Ulryka.");
                }
            }

            while (additionalDamage == 10)
            {
                additionalDamage = Random.Range(1, 11);
                damageRollResult += additionalDamage;
                Debug.Log($"KOLEJNA FURIA ULRYKA!");
            }
        }

        return damageRollResult;
    }

    int CalculateDamage(int damageRollResult, Stats attackerStats, Weapon attackerWeapon)
    {
        int damage;

        if (_attackDistance <= 1.5f) //Oblicza łączne obrażenia dla ataku w zwarciu
        {
            damage = attackerStats.StrongBlow ? damageRollResult + attackerStats.S + attackerWeapon.S + 1 : damageRollResult + attackerStats.S + attackerWeapon.S;
        }
        else //Oblicza łączne obrażenia dla ataku dystansowego
        {
            damage = attackerStats.PrecisionShot ? damageRollResult + attackerWeapon.S + 1 : damageRollResult + attackerWeapon.S;             
        }

        return damage;
    }
    #endregion

    #region Check for attack localization and return armor value
    private int CalculateArmor(Stats targetStats, Weapon attackerWeapon)
    {
        int attackLocalization = Random.Range(1, 101);
        int armor = 0;

        switch (attackLocalization)
        {
            case int n when (n >= 1 && n <= 15):
                Debug.Log("Trafienie w głowę");
                armor = targetStats.Armor_head;
                break;
            case int n when (n >= 16 && n <= 35):
                Debug.Log("Trafienie w prawą rękę");
                armor = targetStats.Armor_arms;
                break;
            case int n when (n >= 36 && n <= 55):
                Debug.Log("Trafienie w lewą rękę");
                armor = targetStats.Armor_arms;
                break;
            case int n when (n >= 56 && n <= 80):
                Debug.Log("Trafienie w korpus");
                armor = targetStats.Armor_torso;
                break;
            case int n when (n >= 81 && n <= 90):
                Debug.Log("Trafienie w prawą nogę");
                armor = targetStats.Armor_legs;
                break;
            case int n when (n >= 91 && n <= 100):
                Debug.Log("Trafienie w lewą nogę");
                armor = targetStats.Armor_legs;
                break;
        }

        //Uwzględnienie broni przebijających zbroję
        if (attackerWeapon.ArmourPiercing == true) armor --;

        return armor;
    }
    #endregion

    #region Reloading
    public void Reload()
    {
        if(Unit.SelectedUnit == null) return;

        Weapon weapon = Unit.SelectedUnit.GetComponent<Weapon>();

        if(weapon.ReloadLeft > 0)
        {
            weapon.ReloadLeft --;
        }
        
        if(weapon.ReloadLeft == 0)
        {
            Debug.Log($"Broń {Unit.SelectedUnit.GetComponent<Stats>().Name} załadowana.");
        }
        else
        {
            Debug.Log($"Ładowanie broni {Unit.SelectedUnit.GetComponent<Stats>().Name}. Pozostała/y {weapon.ReloadLeft} akcja/e do końca.");
        }      
    }
    #endregion
}
