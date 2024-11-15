using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using TMPro;
using UnityEngine.UI;
using System.IO;
using Unity.VisualScripting;
using static UnityEngine.UI.CanvasScaler;

public class RoundsManager : MonoBehaviour
{   
    // Prywatne statyczne pole przechowujące instancję
    private static RoundsManager instance;

    // Publiczny dostęp do instancji
    public static RoundsManager Instance
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
    public static int RoundNumber;
    [SerializeField] private TMP_Text _roundNumberDisplay;
    [SerializeField] private TMP_Text _nextRoundButtonText;
    public Dictionary <Unit, int> UnitsWithActionsLeft = new Dictionary<Unit, int>();
    [SerializeField] private GameObject _actionsLeftInfo;
    [SerializeField] private TMP_Text _actionsLeftText;
    [SerializeField] private GameObject _useFortunePointsButton;
    private bool _isFortunePointSpent; //informacja o tym, że punkt szczęścia został zużyty, aby nie można było ponownie go użyć do wczytania tego samego autozapisu

    private void Start()
    {
        RoundNumber = 0;
        _roundNumberDisplay.text = "Zaczynamy?";
        _nextRoundButtonText.text = "Start";

        _actionsLeftInfo.SetActive(false);
        _useFortunePointsButton.SetActive(false);
    }

    public void NextRound()
    {
        RoundNumber++;
        _roundNumberDisplay.text = "Runda: " + RoundNumber;
        _nextRoundButtonText.text = "Następna runda";
 
        Debug.Log($"<color=#4dd2ff>--------------------------------------------- RUNDA {RoundNumber} ---------------------------------------------</color>");

        //Resetuje ilość dostępnych akcji dla wszystkich jednostek
        foreach (var key in UnitsWithActionsLeft.Keys.ToList())
        {
            if(key == null) continue;

            //Stosuje zdolności specjalne różnych jednostek (np. regeneracja żywotności trolla)
            key.GetComponent<Stats>().CheckForSpecialRaceAbilities();

            UnitsWithActionsLeft[key] = 2;

            key.CanParry = true;
            if(key.GetComponent<Stats>().Dodge > 0) key.CanDodge = true;
            if(key.GetComponent<Stats>().Mag > 0) key.CanCastSpell = true;
            key.CanAttack = true;
            key.GuardedAttackBonus = 0;

            if (key.StunDuration > 0)
            {
                UnitsWithActionsLeft[key] = 0;
                key.StunDuration--;
            }
            if (key.HelplessDuration > 0)
            {
                UnitsWithActionsLeft[key] = 0;
                key.HelplessDuration--;
            }
            if (key.SpellDuration > 0)
            {
                key.SpellDuration--;

                if (key.SpellDuration == 0)
                {
                    MagicManager.Instance.ResetSpellEffect(key);
                }
            }
            if(key.Trapped)
            {
                UnitsWithActionsLeft[key] = 0;
                CombatManager.Instance.EscapeFromTheSnare(key);
            }

            if (key.TrappedUnitId != 0)
            {
                bool trappedUnitExist = false;

                foreach (var unit in UnitsManager.Instance.AllUnits)
                {
                    if(unit.UnitId == key.TrappedUnitId && unit.Trapped == true)
                    {
                        UnitsWithActionsLeft[key] = 0;
                        trappedUnitExist = true;
                    }
                }

                if (!trappedUnitExist)
                {
                    key.TrappedUnitId = 0;
                }
            }
        }

        //Wykonuje testy grozy i strachu jeśli na polu bitwy są jednostki straszne lub przerażające
        if(GameManager.IsFearIncluded == true)
        {
            UnitsManager.Instance.LookForScaryUnits();
        }

        //Aktualizuje wyświetlane dostępne akcje
        if(Unit.SelectedUnit != null)
        {
            DisplayActionsLeft(Unit.SelectedUnit.GetComponent<Unit>());
        }

        InitiativeQueueManager.Instance.UpdateInitiativeQueue();

        //Odświeża panel jednostki, aby zaktualizowac ewentualną informację o długości trwania stanu (np. ogłuszenia) wybranej jednostki
        if(Unit.SelectedUnit != null)
        {
            UnitsManager.Instance.UpdateUnitPanel(Unit.SelectedUnit);
        }

        //Wybiera jednostkę zgodnie z kolejką inicjatywy, jeśli ten tryb jest włączony
        if (GameManager.IsAutoSelectUnitMode && InitiativeQueueManager.Instance.ActiveUnit != null)
        {
            InitiativeQueueManager.Instance.SelectUnitByQueue();
        }

        //Wykonuje automatyczną akcję za każdą jednostkę
        if(GameManager.IsAutoCombatMode)
        {
            StartCoroutine(AutoCombat());
        }
    }

    IEnumerator AutoCombat()
    {
        _nextRoundButtonText.transform.parent.gameObject.SetActive(false);
        _useFortunePointsButton.SetActive(false);

        // Posortowanie wszystkich jednostek wg inicjatywy
        List<Unit> AllUnitsSorted = UnitsManager.Instance.AllUnits
            .OrderByDescending(unit => unit.GetComponent<Stats>().Initiative)
            .ToList();

        foreach (Unit unit in AllUnitsSorted)
        {
            InitiativeQueueManager.Instance.SelectUnitByQueue();

            yield return new WaitForSeconds(0.1f);

            AutoCombatManager.Instance.Act(Unit.SelectedUnit.GetComponent<Unit>());

            //Sortuje jeszcze raz, bo któraś jednostka mogła zginąć
            AllUnitsSorted = UnitsManager.Instance.AllUnits
                .OrderByDescending(unit => unit.GetComponent<Stats>().Initiative)
                .ToList();

            // Czeka, aż postać skończy ruch, zanim wybierze kolejną postać
            yield return new WaitUntil(() => MovementManager.Instance.IsMoving == false);
            yield return new WaitForSeconds(0.5f);
        }

        _nextRoundButtonText.transform.parent.gameObject.SetActive(true);
        _useFortunePointsButton.SetActive(true);
    }

    #region Units actions
    public bool DoHalfAction(Unit unit)
    {
        if (UnitsWithActionsLeft.ContainsKey(unit) && UnitsWithActionsLeft[unit] >= 1)
        {
            // Automatyczny zapis, aby możliwe było użycie punktów szczęścia. Jeżeli jednostka ich nie posiada to zapis nie jest wykonywany
            if(unit.Stats.PS > 0 && !GameManager.IsAutoCombatMode)
            {
                SaveAndLoadManager.Instance.SaveUnits(UnitsManager.Instance.AllUnits, "autosave");
                _isFortunePointSpent = false;
            }

            UnitsWithActionsLeft[unit]--;
            DisplayActionsLeft(unit);

            Debug.Log($"<color=green>{unit.GetComponent<Stats>().Name} wykonał/a akcję pojedynczą. </color>");

            //Zresetowanie szarży lub biegu, jeśli były aktywne (po zużyciu jednej akcji szarża i bieg nie mogą być możliwe)
            MovementManager.Instance.UpdateMovementRange(1);

            //Aktualizuje aktywną postać na kolejce inicjatywy, gdy obecna postać wykona wszystkie akcje w tej rundzie
            if(UnitsWithActionsLeft[unit] == 0)
            {
                InitiativeQueueManager.Instance.SelectUnitByQueue();
            }
            return true;
        }
        else
        {
            Debug.Log("Ta jednostka nie może w tej rundzie wykonać więcej akcji.");
            return false;
        }
    }

    public bool DoFullAction(Unit unit)
    {
        if (UnitsWithActionsLeft.ContainsKey(unit) && UnitsWithActionsLeft[unit] == 2)
        {
            // Automatyczny zapis, aby możliwe było użycie punktów szczęścia. Jeżeli jednostka ich nie posiada to zapis nie jest wykonywany. W przypadku szarży gra jest zapisywana przed wykonaniem ruchu (w klasie CombatManager), a nie w momencie zużywania akcji
            if (unit.Stats.PS > 0 && !CombatManager.Instance.AttackTypes["Charge"] == true && !GameManager.IsAutoCombatMode)
            {
                SaveAndLoadManager.Instance.SaveUnits(UnitsManager.Instance.AllUnits, "autosave");
                _isFortunePointSpent = false;
            }

            UnitsWithActionsLeft[unit] -= 2;
            DisplayActionsLeft(unit);

            Debug.Log($"<color=green>{unit.GetComponent<Stats>().Name} wykonał/a akcję podwójną. </color>");

            //Aktualizuje aktywną postać na kolejce inicjatywy, bo obecna postać wykonała wszystkie akcje w tej rundzie. Wyjątkiem jest atak wielokrotny
            if (!CombatManager.Instance.AttackTypes["SwiftAttack"])
            {
                InitiativeQueueManager.Instance.SelectUnitByQueue();
            }    

            return true;
        }
        else
        {
            Debug.Log("Ta jednostka nie może w tej rundzie wykonać akcji podwójnej.");
            return false;
        }     
    }

    public void DisplayActionsLeft(Unit unit)
    {
        if (!UnitsWithActionsLeft.ContainsKey(unit)) return;

        if(Unit.SelectedUnit == null)
        {
            _actionsLeftInfo.SetActive(false);
            _useFortunePointsButton.SetActive(false);
        }
        else
        {
            _actionsLeftInfo.SetActive(true);
            _actionsLeftText.text = UnitsWithActionsLeft[unit].ToString();

            if (_isFortunePointSpent != true && UnitsWithActionsLeft[unit] != 2 && !GameManager.IsAutoCombatMode)
            {
                _useFortunePointsButton.SetActive(true);
            }
        }
    }

    public void ChangeActionsLeft(int value)
    {
        if (Unit.SelectedUnit == null) return;

        Unit unit = Unit.SelectedUnit.GetComponent<Unit>();
 
        if (!UnitsWithActionsLeft.ContainsKey(unit)) return;

        UnitsWithActionsLeft[unit] += value;

        //Limitem dolnym jest 0, a górnym 2
        if (UnitsWithActionsLeft[unit] < 0) UnitsWithActionsLeft[unit] = 0;
        else if (UnitsWithActionsLeft[unit] > 2) UnitsWithActionsLeft[unit] = 2;

        DisplayActionsLeft(unit);
    }

    public void UseFortunePoint()
    {
        if (Unit.SelectedUnit == null) return;

        Unit unit = Unit.SelectedUnit.GetComponent<Unit>();
        Stats stats = Unit.SelectedUnit.GetComponent<Stats>();

        if (UnitsWithActionsLeft[unit] == 2)
        {
            stats = Unit.LastSelectedUnit.GetComponent<Stats>();
        }

        if (stats.PS == 0)
        {
            Debug.Log("Ta jednostka nie posiada Punktów Szczęścia. Przerzut jest niemożliwy.");
            return;
        }
        stats.PS--;
        _isFortunePointSpent = true;

        SaveAndLoadManager.Instance.SaveFortunePoints("autosave", stats, stats.PS);
        SaveAndLoadManager.Instance.LoadAllUnits("autosave");

        _useFortunePointsButton.SetActive(false);
    }
    #endregion

    public void LoadRoundsManagerData(RoundsManagerData data)
    {
        RoundNumber = data.RoundNumber;
        _roundNumberDisplay.text = "Runda: " + RoundNumber;
        _nextRoundButtonText.text = "Następna runda";

        UnitsWithActionsLeft.Clear(); // Czyści słownik przed uzupełnieniem nowymi danymi

        foreach (var entry in data.Entries)
        {
            GameObject unitObject = GameObject.Find(entry.UnitName);

            if (unitObject != null)
            {
                Unit matchedUnit = unitObject.GetComponent<Unit>();
                UnitsWithActionsLeft[matchedUnit] = entry.ActionsLeft;
            }
        }
    }
}
