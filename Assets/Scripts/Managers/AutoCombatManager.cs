using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using static UnityEngine.UI.CanvasScaler;

public class AutoCombatManager : MonoBehaviour
{
    // Prywatne statyczne pole przechowuj¹ce instancjê
    private static AutoCombatManager instance;

    // Publiczny dostêp do instancji
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
            // Jeœli instancja ju¿ istnieje, a próbujemy utworzyæ kolejn¹, niszczymy nadmiarow¹
            Destroy(gameObject);
        }
    }

    public void Act(Unit unit)
    {
        Weapon weapon = InventoryManager.Instance.ChooseWeaponToAttack(unit.gameObject);

        bool closestOpponentInDistanceRangeNeeded = !weapon.Type.Contains("melee");

        GameObject closestOpponent = GetClosestOpponent(unit.gameObject, closestOpponentInDistanceRangeNeeded);

        //Je¿eli jednostka walczy broni¹ dystansow¹ ale nie ma ¿adnego przeciwnika do którego mo¿e strzelaæ to obiera za cel przeciwnika w zwarciu. Bêdzie siê to wi¹za³o z prób¹ dobycia broni typu "melee"
        if(closestOpponent == null && closestOpponentInDistanceRangeNeeded)
        {
            closestOpponentInDistanceRangeNeeded = false;
            closestOpponent = GetClosestOpponent(unit.gameObject, closestOpponentInDistanceRangeNeeded);
        }
        if (closestOpponent == null || RoundsManager.Instance.UnitsWithActionsLeft[unit] == 0) return;

        float distance = Vector3.Distance(closestOpponent.transform.position, unit.transform.position);

        // Jeœli rywal jest w zasiêgu ataku to wykonuje atak
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
        // Je¿eli postaæ ma wielokrotny atak to wykonuje go
        if (RoundsManager.Instance.UnitsWithActionsLeft[unit] == 2 && unit.GetComponent<Stats>().A > 1 && (weapon.Type.Contains("melee") || (weapon.ReloadTime == 1 && unit.GetComponent<Stats>().RapidReload == true && distance > 1.5f)))
        {
            CombatManager.Instance.ChangeAttackType("SwiftAttack");
            for (int i = 1; i <= unit.GetComponent<Stats>().A; i++)
            {
                CombatManager.Instance.Attack(unit, closestOpponent.GetComponent<Unit>(), false);
            }
            return;
        }

        if (distance > 1.5f) //atak dystansowy
        {
            // Jeœli broñ nie wymaga naladowania to wykonuje atak, w przeciwnym razie wykonuje ³adowanie
            if (weapon.ReloadLeft == 0)
            {
                if (RoundsManager.Instance.UnitsWithActionsLeft[unit] == 2 && unit.GetComponent<Stats>().RapidReload)
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
            //Dobycie broni, jeœli obecna broñ uniemo¿liwia walkê w zwarciu.
            if (!weapon.Type.Contains("melee"))
            {
                // Sprawdzenie, czy jednostka posiada wiêcej ni¿ jedn¹ broñ
                if (InventoryManager.Instance.InventoryScrollViewContent.GetComponent<CustomDropdown>().Buttons.Count > 1)
                {
                    int selectedIndex = 1;

                    //  Zmienia bronie dopóki nie znajdzie takiej, któr¹ mo¿e walczyæ w zwarciu
                    for (int i = 0; i < InventoryManager.Instance.InventoryScrollViewContent.GetComponent<CustomDropdown>().Buttons.Count; i++)
                    {
                        if (weapon.Type.Contains("melee")) break;

                        InventoryManager.Instance.InventoryScrollViewContent.GetComponent<CustomDropdown>().SetSelectedIndex(selectedIndex);
                        InventoryManager.Instance.InventoryScrollViewContent.GetComponent<CustomDropdown>().SelectedButton = InventoryManager.Instance.InventoryScrollViewContent.GetComponent<CustomDropdown>().Buttons[selectedIndex - 1];

                        InventoryManager.Instance.GrabWeapon();
                        weapon = InventoryManager.Instance.ChooseWeaponToAttack(unit.gameObject);

                        selectedIndex++;
                    }

                    // Zu¿ywa akcjê na dobycie broni dopiero po dobyciu odpowiedniej z nich w wyniku powy¿szej pêtli
                    if(!unit.GetComponent<Stats>().QuickDraw)
                    {
                        RoundsManager.Instance.DoHalfAction(unit.GetComponent<Unit>());
                    }

                    Debug.Log($"{unit.GetComponent<Stats>().Name} zmienia broñ na {weapon.Name}.");
                }
                else // Upuszcza broñ, ¿eby walczyæ przy u¿yciu piêœci
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
        //Ustawia aktualn¹ szybkoœæ postaci na wysok¹ wartoœæ, ¿eby ruch nie by³ ograniczony dystansem
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
            Debug.Log($"{unit.GetComponent<Stats>().Name} nie jest w stanie podejœæ do {closestOpponent.GetComponent<Stats>().Name}.");
            MovementManager.Instance.UpdateMovementRange(1);
            return;
        }

        //Œcie¿ka ruchu atakuj¹cego
        List<Vector3> path = MovementManager.Instance.FindPath(unit.transform.position, targetTilePosition, unit.GetComponent<Stats>().TempSz); //liczba 100 zosta³a u¿yta aby nie ograniczaæ mo¿liwoœci ruchu w stronê pola, które znajduje siê zbyt daleko

        if (unit.CanAttack == true && path.Count <= unit.GetComponent<Stats>().Sz * 2 && path.Count >= 3 && RoundsManager.Instance.UnitsWithActionsLeft[unit] == 2) // Jeœli rywal jest w zasiêgu szar¿y to wykonuje szar¿ê
        {
            Debug.Log($"{unit.GetComponent<Stats>().Name} szar¿uje na {closestOpponent.GetComponent<Stats>().Name}.");

            MovementManager.Instance.UpdateMovementRange(2);
            CombatManager.Instance.ChangeAttackType("Charge");

            CombatManager.Instance.Attack(unit, closestOpponent.GetComponent<Unit>(), false);
        }
        else if (path.Count < 3 && RoundsManager.Instance.UnitsWithActionsLeft[unit] == 2) //Wykonuje ruch w kierunku przeciwnika, a nastêpnie atak
        {
            // Uruchomia korutynê odpowiedzialn¹ za ruch i atak
            StartCoroutine(MoveAndAttack(unit, targetTile, closestOpponent.GetComponent<Unit>()));
        }
        else //Wykonuje ruch w kierunku przeciwnika
        {
            if (RoundsManager.Instance.UnitsWithActionsLeft[unit] == 2) //Wykonuje bieg
            {
                MovementManager.Instance.UpdateMovementRange(3);

                Debug.Log($"{unit.GetComponent<Stats>().Name} biegnie w stronê {closestOpponent.GetComponent<Stats>().Name}.");
            }
            else
            {
                Debug.Log($"{unit.GetComponent<Stats>().Name} idzie w stronê {closestOpponent.GetComponent<Stats>().Name}.");
            }

            MovementManager.Instance.MoveSelectedUnit(targetTile, unit.gameObject);
        }

        // Synchronizuje collidery
        Physics2D.SyncTransforms();
    }

    IEnumerator MoveAndAttack(Unit unit, GameObject targetTile, Unit closestOpponent)
    {
        Debug.Log($"{unit.GetComponent<Stats>().Name} podchodzi do {closestOpponent.GetComponent<Stats>().Name} i atakuje.");

        //Przywraca standardow¹ szybkoœæ
        MovementManager.Instance.UpdateMovementRange(1);

        // Ruch
        MovementManager.Instance.MoveSelectedUnit(targetTile, unit.gameObject);

        // Czeka a¿ ruch siê zakoñczy
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

        return closestOpponent;
    }
}
