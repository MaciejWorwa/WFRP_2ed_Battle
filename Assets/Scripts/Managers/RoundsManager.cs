using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using TMPro;
using UnityEngine.UI;
using System.IO;

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
    [SerializeField] private GameObject _useFortunePointsButton;

    private void Start()
    {
        RoundNumber = 0;
        _roundNumberDisplay.text = "Zaczynamy?";
        _nextRoundButtonText.text = "Start";

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
        }

        //Wykonuje testy grozy i strachu jeśli na polu bitwy są jednostki straszne lub przerażające
        UnitsManager.Instance.LookForScaryUnits();

        InitiativeQueueManager.Instance.UpdateInitiativeQueue();

        //Wybiera jednostkę zgodnie z kolejką inicjatywy, jeśli ten tryb jest włączony
        if (GameManager.IsAutoSelectUnitMode && InitiativeQueueManager.Instance.ActiveUnit != null)
        {
            InitiativeQueueManager.Instance.SelectUnitByQueue();
        }
    }

    #region Units actions
    public bool DoHalfAction(Unit unit)
    {
        if (UnitsWithActionsLeft.ContainsKey(unit) && UnitsWithActionsLeft[unit] >= 1)
        {
            UnitsWithActionsLeft[unit]--;

            Debug.Log($"<color=green> {unit.GetComponent<Stats>().Name} wykonał/a akcję pojedynczą. </color>");

            SaveAndLoadManager.Instance.SaveUnits(UnitsManager.Instance.AllUnits, "autosave");
            if(!_useFortunePointsButton.activeSelf)
            {
                _useFortunePointsButton.SetActive(true);
            }

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
            UnitsWithActionsLeft[unit] -= 2;

            Debug.Log($"<color=green> {unit.GetComponent<Stats>().Name} wykonał/a akcję podwójną. </color>");

            SaveAndLoadManager.Instance.SaveUnits(UnitsManager.Instance.AllUnits, "autosave");
            if (!_useFortunePointsButton.activeSelf)
            {
                _useFortunePointsButton.SetActive(true);
            }

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
