using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

public class AutoCombatManager : MonoBehaviour
{
    // Prywatne statyczne pole przechowujące instancję
    private static AutoCombatManager instance;

    // Publiczny dostęp do instancji
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
            // Jeśli instancja już istnieje, a próbujemy utworzyć kolejną, niszczymy nadmiarową
            Destroy(gameObject);
        }
    }

    public void Act(Unit unit)
    {
        Weapon weapon = InventoryManager.Instance.ChooseWeaponToAttack(unit.gameObject);

        bool closestOpponentInDistanceRangeNeeded = !weapon.Type.Contains("melee");

        GameObject closestOpponent = GetClosestOpponent(unit.gameObject, closestOpponentInDistanceRangeNeeded);

        //Jeżeli jednostka walczy bronią dystansową ale nie ma żadnego przeciwnika do którego może strzelać to obiera za cel przeciwnika w zwarciu. Będzie się to wiązało z próbą dobycia broni typu "melee"
        if(closestOpponent == null && closestOpponentInDistanceRangeNeeded)
        {
            closestOpponentInDistanceRangeNeeded = false;
            closestOpponent = GetClosestOpponent(unit.gameObject, closestOpponentInDistanceRangeNeeded);
        }
        if (closestOpponent == null || RoundsManager.Instance.UnitsWithActionsLeft[unit] == 0) return;

        float distance = Vector3.Distance(closestOpponent.transform.position, unit.transform.position);

        // Jeśli rywal jest w zasięgu ataku to wykonuje atak
        if (unit.CanAttack == true && (distance <= weapon.AttackRange || distance <= weapon.AttackRange * 2 && weapon.Type.Contains("ranged") && !weapon.Type.Contains("short-range-only")))
        {
            Debug.Log($"{unit.GetComponent<Stats>().Name} atakuje przeciwnika {closestOpponent}");

            // Jeżeli postać ma wielokrotny atak to wykonuje go
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
                // Jeśli broń nie wymaga naladowania to wykonuje atak, w przeciwnym razie wykonuje ładowanie
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
                //Dobycie broni, jeśli obecna broń uniemożliwia walkę w zwarciu. W tej chwili jest dobywana pierwsza broń na liście ekwipunku. Nie jest to optymalne. Kod powinien sprawdzać, czy nowo dobywana broń nadaje się do walki w zwarciu
                if (!weapon.Type.Contains("melee"))
                {
                    // Sprawdzenie, czy jednostka posiada więcej niż jedną broń
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
                        Debug.Log($"{unit.GetComponent<Stats>().Name} zmienia broń na {weapon.Name}");
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
            //Ustawia aktualną szybkość postaci na wysoką wartość, żeby ruch nie był ograniczony dystansem
            MovementManager.Instance.UpdateMovementRange(20);

            // Szuka wolnej pozycji obok celu, do której droga postaci jest najkrótsza
            GameObject targetTile = CombatManager.Instance.GetTileAdjacentToTarget(unit.gameObject, closestOpponent);
            Vector3 targetTilePosition = Vector3.zero;

            if (targetTile != null)
            {
                targetTilePosition = new Vector3(targetTile.transform.position.x, targetTile.transform.position.y, 0);
            }
            else
            {
                Debug.Log($"{unit.GetComponent<Stats>().Name} nie jest w stanie podejść do {closestOpponent.GetComponent<Stats>().Name}");
                MovementManager.Instance.UpdateMovementRange(1);
                return;
            }

            //Ścieżka ruchu atakującego
            List<Vector3> path = MovementManager.Instance.FindPath(unit.transform.position, targetTilePosition, unit.GetComponent<Stats>().TempSz); //liczba 100 została użyta aby nie ograniczać możliwości ruchu w stronę pola, które znajduje się zbyt daleko

            if (unit.CanAttack == true && path.Count <= unit.GetComponent<Stats>().Sz * 2 && path.Count >= 3 && RoundsManager.Instance.UnitsWithActionsLeft[unit] == 2) // Jeśli rywal jest w zasięgu szarży to wykonuje szarżę
            {
                Debug.Log($"{unit.GetComponent<Stats>().Name} szarżuje na przeciwnika {closestOpponent}");

                MovementManager.Instance.UpdateMovementRange(2);
                CombatManager.Instance.ChangeAttackType("Charge");

                CombatManager.Instance.Attack(unit, closestOpponent.GetComponent<Unit>(), false);
            }
            else if (path.Count < 3 && RoundsManager.Instance.UnitsWithActionsLeft[unit] == 2) //Wykonuje ruch w kierunku przeciwnika, a następnie atak
            {
                Debug.Log("path.Count" + path.Count);

                // Uruchomia korutynę odpowiedzialną za ruch i atak
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

        //Przywraca standardową szybkość
        MovementManager.Instance.UpdateMovementRange(1);

        // Ruch
        MovementManager.Instance.MoveSelectedUnit(targetTile, unit.gameObject);

        // Czeka aż ruch się zakończy
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
        Debug.Log($"Najbliższy przeciwnik do {attacker} to {closestOpponent}");

        return closestOpponent;
    }

    /* POPRAWKA POWYŻSZEGO KODU PRZEZ CHAT GPT, ŻEBY BYŁ BARDZIEJ CZYTELNY I MODULARNY. JEST TO DO SPRAWDZENIA, BO NIE MIAŁEM CZASU

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
