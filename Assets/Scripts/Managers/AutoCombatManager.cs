using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

public class AutoCombatManager : MonoBehaviour
{
    // Prywatne statyczne pole przechowuj�ce instancj�
    private static AutoCombatManager instance;

    // Publiczny dost�p do instancji
    public static AutoCombatManager Instance
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
            // Je�li instancja ju� istnieje, a pr�bujemy utworzy� kolejn�, niszczymy nadmiarow�
            Destroy(gameObject);
        }
    }

    public void Act(Unit unit)
    {
        Weapon weapon = InventoryManager.Instance.ChooseWeaponToAttack(unit.gameObject);

        bool closestOpponentInDistanceRangeNeeded = !weapon.Type.Contains("melee");

        GameObject closestOpponent = GetClosestOpponent(unit.gameObject, closestOpponentInDistanceRangeNeeded);

        //Je�eli jednostka walczy broni� dystansow� ale nie ma �adnego przeciwnika do kt�rego mo�e strzela� to obiera za cel przeciwnika w zwarciu. B�dzie si� to wi�za�o z pr�b� dobycia broni typu "melee"
        if(closestOpponent == null && closestOpponentInDistanceRangeNeeded)
        {
            closestOpponentInDistanceRangeNeeded = false;
            closestOpponent = GetClosestOpponent(unit.gameObject, closestOpponentInDistanceRangeNeeded);
        }
        if (closestOpponent == null || RoundsManager.Instance.UnitsWithActionsLeft[unit] == 0) return;

        float distance = Vector3.Distance(closestOpponent.transform.position, unit.transform.position);

        // Je�li rywal jest w zasi�gu ataku to wykonuje atak
        if (unit.CanAttack == true && (distance <= weapon.AttackRange || distance <= weapon.AttackRange * 2 && weapon.Type.Contains("ranged") && !weapon.Type.Contains("short-range-only")))
        {
            Debug.Log($"{unit.GetComponent<Stats>().Name} atakuje przeciwnika {closestOpponent}");

            // Je�eli posta� ma wielokrotny atak to wykonuje go
            if (RoundsManager.Instance.UnitsWithActionsLeft[unit] == 2 && unit.GetComponent<Stats>().A > 1 && (weapon.Type.Contains("melee") || (weapon.ReloadTime == 1 && unit.GetComponent<Stats>().RapidReload == true && distance > 1.5f)))
            {
                CombatManager.Instance.ChangeAttackType("SwiftAttack");

                for(int i = 1; i <= unit.GetComponent<Stats>().A; i++)
                {
                    CombatManager.Instance.Attack(unit, closestOpponent.GetComponent<Unit>(), false);
                }

                return;
            }

            if (distance > 1.5f) //atak dystansowy
            {
                // Je�li bro� nie wymaga naladowania to wykonuje atak, w przeciwnym razie wykonuje �adowanie
                if (weapon.ReloadLeft == 0)
                {
                    if (RoundsManager.Instance.UnitsWithActionsLeft[unit] == 2 && unit.GetComponent<Stats>().RapidReload)
                    {
                        CombatManager.Instance.SetAim();
                    }

                    CombatManager.Instance.Attack(unit, closestOpponent.GetComponent<Unit>(), false);

                    if(RoundsManager.Instance.UnitsWithActionsLeft[unit] != 0 || unit.GetComponent<Stats>().RapidReload)
                    {
                        CombatManager.Instance.Reload();
                    }
                }
                else if (weapon.ReloadLeft == 1)
                {
                    CombatManager.Instance.Reload();

                    if (RoundsManager.Instance.UnitsWithActionsLeft[unit] != 0)
                    {
                        CombatManager.Instance.Attack(unit, closestOpponent.GetComponent<Unit>(), false);
                    }
                    return;
                }
                else if (weapon.ReloadLeft > 1)
                {
                    CombatManager.Instance.Reload();

                    if (RoundsManager.Instance.UnitsWithActionsLeft[unit] != 0 || unit.GetComponent<Stats>().RapidReload)
                    {
                        CombatManager.Instance.Reload();
                    }
                    return;
                }
            }
            else //atak w zwarciu
            {
                //Dobycie broni, je�li obecna bro� uniemo�liwia walk� w zwarciu. W tej chwili jest dobywana pierwsza bro� na li�cie ekwipunku. Nie jest to optymalne. Kod powinien sprawdza�, czy nowo dobywana bro� nadaje si� do walki w zwarciu
                if (!weapon.Type.Contains("melee"))
                {
                    // Sprawdzenie, czy jednostka posiada wi�cej ni� jedn� bro�
                    if (InventoryManager.Instance.InventoryScrollViewContent.GetComponent<CustomDropdown>().Buttons.Count > 1)
                    {
                        int selectedIndex = 1;

                        for(int i = 0; i < InventoryManager.Instance.InventoryScrollViewContent.GetComponent<CustomDropdown>().Buttons.Count; i++)
                        {
                            if (weapon.Type.Contains("melee")) break;

                            InventoryManager.Instance.GrabWeapon();
                            weapon = InventoryManager.Instance.ChooseWeaponToAttack(unit.gameObject);

                            InventoryManager.Instance.InventoryScrollViewContent.GetComponent<CustomDropdown>().SetSelectedIndex(selectedIndex);
                            InventoryManager.Instance.InventoryScrollViewContent.GetComponent<CustomDropdown>().SelectedButton =InventoryManager.Instance.InventoryScrollViewContent.GetComponent<CustomDropdown>().Buttons[selectedIndex - 1];

                            selectedIndex++;
                        }

                        RoundsManager.Instance.DoHalfAction(unit.GetComponent<Unit>());
                        Debug.Log($"{unit.GetComponent<Stats>().Name} zmienia bro� na {weapon.Name}");
                    }
                    else if (RoundsManager.Instance.UnitsWithActionsLeft[unit] == 2)
                    {
                        CombatManager.Instance.DefensiveStance();
                        return;
                    }
                    else
                    {
                        InventoryManager.Instance.InventoryScrollViewContent.GetComponent<CustomDropdown>().SetSelectedIndex(1);
                        InventoryManager.Instance.InventoryScrollViewContent.GetComponent<CustomDropdown>().SelectedButton = InventoryManager.Instance.InventoryScrollViewContent.GetComponent<CustomDropdown>().Buttons[0];
                        InventoryManager.Instance.RemoveWeaponFromInventory();
                    }

                }

                if (RoundsManager.Instance.UnitsWithActionsLeft[unit] == 2)
                {
                    CombatManager.Instance.SetAim();
                }

                if (RoundsManager.Instance.UnitsWithActionsLeft[unit] != 0)
                {
                    CombatManager.Instance.Attack(unit, closestOpponent.GetComponent<Unit>(), false);
                }
            }
        }
        else
        {
            //Ustawia aktualn� szybko�� postaci na wysok� warto��, �eby ruch nie by� ograniczony dystansem
            MovementManager.Instance.UpdateMovementRange(20);

            // Szuka wolnej pozycji obok celu, do kt�rej droga postaci jest najkr�tsza
            GameObject targetTile = CombatManager.Instance.GetTileAdjacentToTarget(unit.gameObject, closestOpponent);
            Vector3 targetTilePosition = Vector3.zero;

            if (targetTile != null)
            {
                targetTilePosition = new Vector3(targetTile.transform.position.x, targetTile.transform.position.y, 0);
            }
            else
            {
                Debug.Log($"{unit.GetComponent<Stats>().Name} nie jest w stanie podej�� do {closestOpponent.GetComponent<Stats>().Name}");
                MovementManager.Instance.UpdateMovementRange(1);
                return;
            }

            //�cie�ka ruchu atakuj�cego
            List<Vector3> path = MovementManager.Instance.FindPath(unit.transform.position, targetTilePosition, unit.GetComponent<Stats>().TempSz); //liczba 100 zosta�a u�yta aby nie ogranicza� mo�liwo�ci ruchu w stron� pola, kt�re znajduje si� zbyt daleko

            if (unit.CanAttack == true && path.Count <= unit.GetComponent<Stats>().Sz * 2 && path.Count >= 3 && RoundsManager.Instance.UnitsWithActionsLeft[unit] == 2) // Je�li rywal jest w zasi�gu szar�y to wykonuje szar��
            {
                Debug.Log($"{unit.GetComponent<Stats>().Name} szar�uje na przeciwnika {closestOpponent}");

                MovementManager.Instance.UpdateMovementRange(2);
                CombatManager.Instance.ChangeAttackType("Charge");

                CombatManager.Instance.Attack(unit, closestOpponent.GetComponent<Unit>(), false);
            }
            else if (path.Count < 3 && RoundsManager.Instance.UnitsWithActionsLeft[unit] == 2) //Wykonuje ruch w kierunku przeciwnika, a nast�pnie atak
            {
                Debug.Log("path.Count" + path.Count);

                // Uruchomia korutyn� odpowiedzialn� za ruch i atak
                StartCoroutine(MoveAndAttack(unit, targetTile, closestOpponent.GetComponent<Unit>()));
            }
            else //Wykonuje ruch w kierunku przeciwnika
            {
                if(RoundsManager.Instance.UnitsWithActionsLeft[unit] == 2) //wykonuje bieg
                {
                    MovementManager.Instance.UpdateMovementRange(3);

                    Debug.Log($"{unit.GetComponent<Stats>().Name} biegnie do przeciwnika {closestOpponent}");
                }
                else
                {
                    Debug.Log($"{unit.GetComponent<Stats>().Name} idzie do przeciwnika {closestOpponent}");
                }
                
                MovementManager.Instance.MoveSelectedUnit(targetTile, unit.gameObject);
            }

            // Synchronizuje collidery
            Physics2D.SyncTransforms();
        }
    }

    IEnumerator MoveAndAttack(Unit unit, GameObject targetTile, Unit closestOpponent)
    {
        Debug.Log($"{unit.GetComponent<Stats>().Name} idzie do przeciwnika {closestOpponent.GetComponent<Stats>().Name} i atakuje go");

        //Przywraca standardow� szybko��
        MovementManager.Instance.UpdateMovementRange(1);

        // Ruch
        MovementManager.Instance.MoveSelectedUnit(targetTile, unit.gameObject);

        // Czeka a� ruch si� zako�czy
        yield return new WaitUntil(() => MovementManager.Instance.IsMoving == false);

        // Atak
        CombatManager.Instance.Attack(unit, closestOpponent, false);
    }


    public GameObject GetClosestOpponent(GameObject attacker, bool closestOpponentInDistanceRangeNeeded)
    {
        GameObject closestOpponent = null;
        float minDistance = Mathf.Infinity;

        foreach (Unit unit in UnitsManager.Instance.AllUnits)
        {
            if (unit.gameObject == attacker || unit.CompareTag(attacker.tag) == true) continue;

            float distance = Vector3.Distance(unit.transform.position, attacker.transform.position);

            if (closestOpponentInDistanceRangeNeeded == true && distance < 1.5f) continue;

            if (distance < minDistance)
            {
                closestOpponent = unit.gameObject;
                minDistance = distance;
            }
        }
        Debug.Log($"Najbli�szy przeciwnik do {attacker} to {closestOpponent}");

        return closestOpponent;
    }

    /* POPRAWKA POWY�SZEGO KODU PRZEZ CHAT GPT, �EBY BY� BARDZIEJ CZYTELNY I MODULARNY. JEST TO DO SPRAWDZENIA, BO NIE MIA�EM CZASU

    public void Act(Unit unit)
    {
        Weapon weapon = InventoryManager.Instance.ChooseWeaponToAttack(unit.gameObject);
        GameObject closestOpponent = GetClosestOpponent(unit.gameObject, !weapon.Type.Contains("melee"));

        if (closestOpponent == null || RoundsManager.Instance.UnitsWithActionsLeft[unit] == 0) return;

        float distance = Vector3.Distance(closestOpponent.transform.position, unit.transform.position);
        HandleAttack(unit, closestOpponent.GetComponent<Unit>(), weapon, distance);
    }

    private void HandleAttack(Unit unit, Unit opponent, Weapon weapon, float distance)
    {
        if (weapon.Type.Contains("melee"))
        {
            HandleMeleeAttack(unit, opponent, weapon, distance);
        }
        else
        {
            HandleRangedAttack(unit, opponent, weapon, distance);
        }
    }

    private void HandleMeleeAttack(Unit unit, Unit opponent, Weapon weapon, float distance)
    {
        if (ShouldAttack(unit, weapon, distance))
        {
            if (CanMakeMultipleAttacks(unit, weapon))
            {
                PerformMultipleAttacks(unit, opponent, weapon);
            }
            else
            {
                CombatManager.Instance.Attack(unit, opponent, false);
            }
        }
        else
        {
            AttemptToMoveCloser(unit, opponent);
        }
    }

    private void HandleRangedAttack(Unit unit, Unit opponent, Weapon weapon, float distance)
    {
        if (weapon.ReloadLeft == 0)
        {
            if (ShouldAttack(unit, weapon, distance))
            {
                CombatManager.Instance.Attack(unit, opponent, false);
                if (RoundsManager.Instance.UnitsWithActionsLeft[unit] != 0 || unit.GetComponent<Stats>().RapidReload)
                {
                    CombatManager.Instance.Reload();
                }
            }
            else
            {
                AttemptToMoveCloser(unit, opponent);
            }
        }
        else
        {
            CombatManager.Instance.Reload();
        }
    }

    private bool ShouldAttack(Unit unit, Weapon weapon, float distance)
    {
        return unit.CanAttack && (distance <= weapon.AttackRange ||
                                  (weapon.Type.Contains("ranged") && distance <= weapon.AttackRange * 2 && !weapon.Type.Contains("short-range-only")));
    }

    private bool CanMakeMultipleAttacks(Unit unit, Weapon weapon)
    {
        return RoundsManager.Instance.UnitsWithActionsLeft[unit] == 2 && unit.GetComponent<Stats>().A > 1 &&
               (weapon.Type.Contains("melee") || (weapon.ReloadTime == 1 && unit.GetComponent<Stats>().RapidReload && !IsClose(distance)));
    }

    private void PerformMultipleAttacks(Unit unit, Unit opponent, Weapon weapon)
    {
        CombatManager.Instance.ChangeAttackType("SwiftAttack");
        for (int i = 1; i <= unit.GetComponent<Stats>().A; i++)
        {
            CombatManager.Instance.Attack(unit, opponent, false);
        }
    }

    private bool IsClose(float distance)
    {
        return distance <= 1.5f;
    }

    private void AttemptToMoveCloser(Unit unit, Unit opponent)
    {
        GameObject targetTile = CombatManager.Instance.GetTileAdjacentToTarget(unit.gameObject, opponent);
        if (targetTile != null)
        {
            MovementManager.Instance.MoveSelectedUnit(targetTile, unit.gameObject);
        }
        else
        {
            Debug.Log($"{unit.GetComponent<Stats>().Name} cannot approach {opponent.GetComponent<Stats>().Name}");
        }
    }

    public GameObject GetClosestOpponent(GameObject attacker, bool needDistanceWeapon)
    {
        GameObject closestOpponent = null;
        float minDistance = Mathf.Infinity;

        foreach (Unit unit in UnitsManager.Instance.AllUnits)
        {
            if (unit.gameObject == attacker || unit.CompareTag(attacker.tag)) continue;

            float distance = Vector3.Distance(unit.transform.position, attacker.transform.position);
            if (needDistanceWeapon && distance < 1.5f) continue;

            if (distance < minDistance)
            {
                closestOpponent = unit.gameObject;
                minDistance = distance;
            }
        }

        return closestOpponent;
    }
    */
}
