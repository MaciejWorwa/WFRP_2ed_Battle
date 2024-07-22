using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using static UnityEngine.UI.CanvasScaler;

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
            ExecuteAttack(unit, closestOpponent, weapon, distance);      
        }
        else
        {
            AttemptToChangeDistanceAndAttack(unit, closestOpponent);
        }
    }

    private void ExecuteAttack(Unit unit, GameObject closestOpponent, Weapon weapon, float distance)
    {
        // Je�eli posta� ma wielokrotny atak to wykonuje go
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
            // Je�li bro� nie wymaga naladowania to wykonuje atak, w przeciwnym razie wykonuje �adowanie
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
            //Dobycie broni, je�li obecna bro� uniemo�liwia walk� w zwarciu.
            if (!weapon.Type.Contains("melee"))
            {
                // Sprawdzenie, czy jednostka posiada wi�cej ni� jedn� bro�
                if (InventoryManager.Instance.InventoryScrollViewContent.GetComponent<CustomDropdown>().Buttons.Count > 1)
                {
                    int selectedIndex = 1;

                    //  Zmienia bronie dop�ki nie znajdzie takiej, kt�r� mo�e walczy� w zwarciu
                    for (int i = 0; i < InventoryManager.Instance.InventoryScrollViewContent.GetComponent<CustomDropdown>().Buttons.Count; i++)
                    {
                        if (weapon.Type.Contains("melee")) break;

                        InventoryManager.Instance.InventoryScrollViewContent.GetComponent<CustomDropdown>().SetSelectedIndex(selectedIndex);
                        InventoryManager.Instance.InventoryScrollViewContent.GetComponent<CustomDropdown>().SelectedButton = InventoryManager.Instance.InventoryScrollViewContent.GetComponent<CustomDropdown>().Buttons[selectedIndex - 1];

                        InventoryManager.Instance.GrabWeapon();
                        weapon = InventoryManager.Instance.ChooseWeaponToAttack(unit.gameObject);

                        selectedIndex++;
                    }

                    // Zu�ywa akcj� na dobycie broni dopiero po dobyciu odpowiedniej z nich w wyniku powy�szej p�tli
                    if(!unit.GetComponent<Stats>().QuickDraw)
                    {
                        RoundsManager.Instance.DoHalfAction(unit.GetComponent<Unit>());
                    }

                    Debug.Log($"{unit.GetComponent<Stats>().Name} zmienia bro� na {weapon.Name}.");
                }
                else // Upuszcza bro�, �eby walczy� przy u�yciu pi�ci
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
            Debug.Log($"{unit.GetComponent<Stats>().Name} nie jest w stanie podej�� do {closestOpponent.GetComponent<Stats>().Name}.");
            MovementManager.Instance.UpdateMovementRange(1);
            return;
        }

        //�cie�ka ruchu atakuj�cego
        List<Vector3> path = MovementManager.Instance.FindPath(unit.transform.position, targetTilePosition, unit.GetComponent<Stats>().TempSz); //liczba 100 zosta�a u�yta aby nie ogranicza� mo�liwo�ci ruchu w stron� pola, kt�re znajduje si� zbyt daleko

        if (unit.CanAttack == true && path.Count <= unit.GetComponent<Stats>().Sz * 2 && path.Count >= 3 && RoundsManager.Instance.UnitsWithActionsLeft[unit] == 2) // Je�li rywal jest w zasi�gu szar�y to wykonuje szar��
        {
            Debug.Log($"{unit.GetComponent<Stats>().Name} szar�uje na {closestOpponent.GetComponent<Stats>().Name}.");

            MovementManager.Instance.UpdateMovementRange(2);
            CombatManager.Instance.ChangeAttackType("Charge");

            CombatManager.Instance.Attack(unit, closestOpponent.GetComponent<Unit>(), false);
        }
        else if (path.Count < 3 && RoundsManager.Instance.UnitsWithActionsLeft[unit] == 2) //Wykonuje ruch w kierunku przeciwnika, a nast�pnie atak
        {
            // Uruchomia korutyn� odpowiedzialn� za ruch i atak
            StartCoroutine(MoveAndAttack(unit, targetTile, closestOpponent.GetComponent<Unit>()));
        }
        else //Wykonuje ruch w kierunku przeciwnika
        {
            if (RoundsManager.Instance.UnitsWithActionsLeft[unit] == 2) //Wykonuje bieg
            {
                MovementManager.Instance.UpdateMovementRange(3);

                Debug.Log($"{unit.GetComponent<Stats>().Name} biegnie w stron� {closestOpponent.GetComponent<Stats>().Name}.");
            }
            else
            {
                Debug.Log($"{unit.GetComponent<Stats>().Name} idzie w stron� {closestOpponent.GetComponent<Stats>().Name}.");
            }

            MovementManager.Instance.MoveSelectedUnit(targetTile, unit.gameObject);
        }

        // Synchronizuje collidery
        Physics2D.SyncTransforms();
    }

    IEnumerator MoveAndAttack(Unit unit, GameObject targetTile, Unit closestOpponent)
    {
        Debug.Log($"{unit.GetComponent<Stats>().Name} podchodzi do {closestOpponent.GetComponent<Stats>().Name} i atakuje.");

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

        return closestOpponent;
    }
}
