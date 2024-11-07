using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using static UnityEngine.UI.CanvasScaler;

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

    public Tile TargetTile;

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
            ExecuteAttack(unit, closestOpponent, weapon, distance);      
        }
        else
        {
            AttemptToChangeDistanceAndAttack(unit, closestOpponent);
        }
    }

    private void ExecuteAttack(Unit unit, GameObject closestOpponent, Weapon weapon, float distance)
    {
        // Jeżeli postać ma wielokrotny atak to wykonuje go
        if (RoundsManager.Instance.UnitsWithActionsLeft[unit] == 2 && unit.GetComponent<Stats>().A > 1 && (weapon.Type.Contains("melee") || (weapon.ReloadTime == 1 && unit.GetComponent<Stats>().RapidReload == true && distance > 1.5f)))
        {
            CombatManager.Instance.ChangeAttackType("SwiftAttack");
            for (int i = 1; i <= unit.GetComponent<Stats>().A; i++)
            {
                //Zapobiega kolejnym atakom, jeśli przeciwnik już nie żyje
                if (closestOpponent.GetComponent<Stats>().TempHealth < 0) break;

                CombatManager.Instance.Attack(unit, closestOpponent.GetComponent<Unit>(), false);
            }
            return;
        }

        if (distance > 1.5f) //atak dystansowy
        {
            // Jeśli broń nie wymaga naladowania to wykonuje atak, w przeciwnym razie wykonuje ładowanie
            if (weapon.ReloadLeft == 0)
            {
                if (RoundsManager.Instance.UnitsWithActionsLeft[unit] == 2 && weapon.ReloadTime == 1 && unit.GetComponent<Stats>().RapidReload)
                {
                    CombatManager.Instance.SetAim();
                }

                CombatManager.Instance.Attack(unit, closestOpponent.GetComponent<Unit>(), false);

                if (RoundsManager.Instance.UnitsWithActionsLeft[unit] != 0 || unit.GetComponent<Stats>().RapidReload)
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
            //Dobycie broni, jeśli obecna broń uniemożliwia walkę w zwarciu.
            if (!weapon.Type.Contains("melee"))
            {
                // Sprawdzenie, czy jednostka posiada więcej niż jedną broń
                if (InventoryManager.Instance.InventoryScrollViewContent.GetComponent<CustomDropdown>().Buttons.Count > 1)
                {
                    int selectedIndex = 1;

                    //  Zmienia bronie dopóki nie znajdzie takiej, którą może walczyć w zwarciu
                    for (int i = 0; i < InventoryManager.Instance.InventoryScrollViewContent.GetComponent<CustomDropdown>().Buttons.Count; i++)
                    {
                        if (weapon.Type.Contains("melee")) break;

                        InventoryManager.Instance.InventoryScrollViewContent.GetComponent<CustomDropdown>().SetSelectedIndex(selectedIndex);
                        InventoryManager.Instance.InventoryScrollViewContent.GetComponent<CustomDropdown>().SelectedButton = InventoryManager.Instance.InventoryScrollViewContent.GetComponent<CustomDropdown>().Buttons[selectedIndex - 1];

                        InventoryManager.Instance.GrabWeapon();
                        weapon = InventoryManager.Instance.ChooseWeaponToAttack(unit.gameObject);

                        selectedIndex++;
                    }

                    // Zużywa akcję na dobycie broni dopiero po dobyciu odpowiedniej z nich w wyniku powyższej pętli
                    if(!unit.GetComponent<Stats>().QuickDraw)
                    {
                        RoundsManager.Instance.DoHalfAction(unit.GetComponent<Unit>());
                    }

                    Debug.Log($"{unit.GetComponent<Stats>().Name} zmienia broń na {weapon.Name}.");
                }
                else // Upuszcza broń, żeby walczyć przy użyciu pięści
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

    private void AttemptToChangeDistanceAndAttack(Unit unit, GameObject closestOpponent)
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
            Debug.Log($"{unit.GetComponent<Stats>().Name} nie jest w stanie podejść do {closestOpponent.GetComponent<Stats>().Name}.");
            WaitForMovementOrAttackOpportunity(unit);

            return;
        }

        //Ścieżka ruchu atakującego
        List<Vector3> path = MovementManager.Instance.FindPath(unit.transform.position, targetTilePosition, unit.GetComponent<Stats>().TempSz); 
        
        if (unit.CanAttack == true && path.Count <= unit.GetComponent<Stats>().Sz * 2 && path.Count >= 3 && RoundsManager.Instance.UnitsWithActionsLeft[unit] == 2) // Jeśli rywal jest w zasięgu szarży to wykonuje szarżę
        {
            Debug.Log($"{unit.GetComponent<Stats>().Name} szarżuje na {closestOpponent.GetComponent<Stats>().Name}.");

            MovementManager.Instance.UpdateMovementRange(2);
            CombatManager.Instance.ChangeAttackType("Charge");

            CombatManager.Instance.Attack(unit, closestOpponent.GetComponent<Unit>(), false);
        }
        else if (path.Count < 3 && path.Count > 0 && RoundsManager.Instance.UnitsWithActionsLeft[unit] == 2) //Wykonuje ruch w kierunku przeciwnika, a następnie atak
        {
            // Uruchomia korutynę odpowiedzialną za ruch i atak
            StartCoroutine(MoveAndAttack(unit, targetTile, closestOpponent.GetComponent<Unit>()));
        }
        else if (path.Count > 0) //Wykonuje ruch w kierunku przeciwnika
        {
            if (RoundsManager.Instance.UnitsWithActionsLeft[unit] == 2) //Wykonuje bieg
            {
                MovementManager.Instance.UpdateMovementRange(3);
                unit.IsRunning = true;

                Debug.Log($"{unit.GetComponent<Stats>().Name} biegnie w stronę {closestOpponent.GetComponent<Stats>().Name}.");
            }
            else
            {
                unit.IsRunning = false;
                Debug.Log($"{unit.GetComponent<Stats>().Name} idzie w stronę {closestOpponent.GetComponent<Stats>().Name}.");
            }

            MovementManager.Instance.MoveSelectedUnit(targetTile, unit.gameObject);
        }
        else //Gdy nie jest w stanie podejść do najbliższego przeciwnika, a stoi on poza zasięgiem jego ataku
        {
            WaitForMovementOrAttackOpportunity(unit);
        }

        // Synchronizuje collidery
        Physics2D.SyncTransforms();
    }

    IEnumerator MoveAndAttack(Unit unit, GameObject targetTile, Unit closestOpponent)
    {
        Debug.Log($"{unit.GetComponent<Stats>().Name} podchodzi do {closestOpponent.GetComponent<Stats>().Name} i atakuje.");

        //Przywraca standardową szybkość
        MovementManager.Instance.UpdateMovementRange(1);

        // Ruch
        MovementManager.Instance.MoveSelectedUnit(targetTile, unit.gameObject);

        // Czeka aż ruch się zakończy
        yield return new WaitUntil(() => MovementManager.Instance.IsMoving == false);

        // Atak
        CombatManager.Instance.Attack(unit, closestOpponent, false);
    }

    public void WaitForMovementOrAttackOpportunity(Unit unit)
    {
        //Resetuje szybkość jednostki
        MovementManager.Instance.UpdateMovementRange(1);

        if (RoundsManager.Instance.UnitsWithActionsLeft[unit] == 2) 
        {
            //Przyjmuje pozycję obronną
            CombatManager.Instance.DefensiveStance();
        }
        else if (RoundsManager.Instance.UnitsWithActionsLeft[unit] == 1) 
        {
            // Przycelowuje
            CombatManager.Instance.SetAim();
        }
    }

    public void CheckForTargetTileOccupancy(GameObject unit)
    {
        //Zaznacza jako zajęte faktyczne pole, na którym jednostka zakończy ruch, a nie pole do którego próbowała dojść
        if(TargetTile != null)
        {
            Vector3 unitPos = new Vector3(unit.transform.position.x, unit.transform.position.y, 1);
            if(TargetTile.transform.position != unitPos)
            {
                TargetTile.IsOccupied = false;

                // Ignoruje warstwę "Unit" podczas wykrywania kolizji, skupiając się tylko na warstiwe 0 (default)
                Collider2D collider = Physics2D.OverlapPoint(unitPos, 0);
                if(collider != null && collider.GetComponent<Tile>() != null)
                {
                    collider.GetComponent<Tile>().IsOccupied = true;
                }
            }
        }
        TargetTile = null;
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

        return closestOpponent;
    }
}
