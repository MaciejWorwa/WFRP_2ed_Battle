using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AttackManager : MonoBehaviour
{
    // Prywatne statyczne pole przechowujące instancję
    private static AttackManager instance;

    // Publiczny dostęp do instancji
    public static AttackManager Instance
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

    public void Attack(Unit attacker, Unit target) 
    {
        Stats attackerStats = attacker.GetComponent<Stats>();
        Stats targetStats = target.GetComponent<Stats>();

        Debug.Log(attacker);
        Debug.Log(target);

        // liczy dystans pomiedzy walczacymi
        if (attacker != null && target != null)
        {
            _attackDistance = Vector3.Distance(attacker.transform.position, target.transform.position);
            Debug.Log("dystans: " + _attackDistance);
            Debug.Log("zasięg: " + attackerStats.AttackRange);
        }

        //Wykonuje atak, jeśli cel jest w zasięgu
        if (_attackDistance <= attackerStats.AttackRange || _attackDistance <= attackerStats.AttackRange * 2 && attackerStats.AttackRange > 1.5f )
        {
            int rollResult = Random.Range(1, 101);
            bool isSuccessful = false;

            //Sprawdza, czy atak jest atakiem dystansowym
            if (_attackDistance > 1.5f)
            {
                _attackModifier -= _attackDistance > attackerStats.AttackRange ? 20 : 0;

                isSuccessful = rollResult <= (attackerStats.US + _attackModifier - _defenseModifier);

                if (_attackModifier != 0 || _defenseModifier != 0)
                {
                    Debug.Log($"{attackerStats.Name} Rzut na US: {rollResult} Modyfikator: {_attackModifier - _defenseModifier}");
                }
                else
                {
                    Debug.Log($"{attackerStats.Name} Rzut na US: {rollResult}");
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

            // Zresetowanie bonusu do trafienia
            _attackModifier = 0;

            if (isSuccessful)
            {
                //int armor = CheckAttackLocalization(targetStats);
                int damage;
                int damageRollResult;

                damageRollResult = UnityEngine.Random.Range(1, 11);

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

                damage = damageRollResult; // tymczasowo               
                Debug.Log($"{attackerStats.Name} wyrzucił {damageRollResult} i zadał {damage} obrażeń.");

                targetStats.TempHealth -= damage;
                target.GetComponent<Unit>().DisplayUnitHealthPoints();
                Debug.Log($"Punkty życia {targetStats.Name}: {targetStats.TempHealth}/{targetStats.MaxHealth}");

                if (targetStats.TempHealth < 0)
                {
                    UnitsManager.Instance.DestroyUnit(target.gameObject);
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
}
