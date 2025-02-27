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
    public Button NextRoundButton;
    public Dictionary <Unit, int> UnitsWithActionsLeft = new Dictionary<Unit, int>();
    [SerializeField] private Slider _actionsLeftSlider;
    [SerializeField] private GameObject _useFortunePointsButton;
    private bool _isFortunePointSpent; //informacja o tym, że punkt szczęścia został zużyty, aby nie można było ponownie go użyć do wczytania tego samego autozapisu

    private void Start()
    {
        RoundNumber = 0;
        _roundNumberDisplay.text = "Zaczynamy?";

        NextRoundButton.transform.GetChild(0).GetComponent<TMP_Text>().text = "Start";

        _useFortunePointsButton.SetActive(false);
    }

    public void NextRound()
    {
        RoundNumber++;
        _roundNumberDisplay.text = "Runda: " + RoundNumber;
        NextRoundButton.transform.GetChild(0).GetComponent<TMP_Text>().text = "Następna runda";
 
        Debug.Log($"<color=#4dd2ff>--------------------------------------------- RUNDA {RoundNumber} ---------------------------------------------</color>");

        //Resetuje ilość dostępnych akcji dla wszystkich jednostek
        foreach (var key in UnitsWithActionsLeft.Keys.ToList())
        {
            if(key == null) continue;

            //Stosuje zdolności specjalne różnych jednostek (np. regeneracja żywotności trolla)
            key.GetComponent<Stats>().CheckForSpecialRaceAbilities();

            UnitsWithActionsLeft[key] = 2;

            key.IsTurnFinished = false;
            key.CanParry = true;
            if(key.GetComponent<Stats>().Dodge > 0) key.CanDodge = true;
            if(key.GetComponent<Stats>().Mag > 0) key.CanCastSpell = true;
            key.CanAttack = true;
            key.GuardedAttackBonus = 0;

            if (key.StunDuration > 0)
            {
                UnitsWithActionsLeft[key] = 0;
                key.StunDuration--;

                if(key.StunDuration == 0) UnitsWithActionsLeft[key] = 2;
            }
            if (key.HelplessDuration > 0)
            {
                UnitsWithActionsLeft[key] = 0;
                key.HelplessDuration--;

                if(key.HelplessDuration == 0) UnitsWithActionsLeft[key] = 2;
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
                //CombatManager.Instance.EscapeFromTheSnare(key); ---- to zostało przeniesione do InitiativeQueueManager, gdy konkretna jednostka jest wybierana, zamiast robić to na początku rundy
            }
            if(key.Grappled)
            {
                UnitsWithActionsLeft[key] = 0;
                //CombatManager.Instance.EscapeFromTheGrappling(key); ---- to zostało przeniesione do InitiativeQueueManager, gdy konkretna jednostka jest wybierana, zamiast robić to na początku rundy
            }
            if (key.IsScared)
            {
                UnitsWithActionsLeft[key] = 0;
            }

            if (key.TrappedUnitId != 0)
            {
                bool trappedUnitExist = false;

                foreach (var unit in UnitsManager.Instance.AllUnits)
                {
                    if(unit.UnitId == key.TrappedUnitId && unit.Trapped == true)
                    {
                        //UnitsWithActionsLeft[key] = 0;
                        trappedUnitExist = true;
                    }
                }

                if (!trappedUnitExist)
                {
                    key.TrappedUnitId = 0;
                }
            }

            if (key.GrappledUnitId != 0)
            {
                bool grappledUnitExist = false;

                foreach (var unit in UnitsManager.Instance.AllUnits)
                {
                    if (unit.UnitId == key.GrappledUnitId && unit.Grappled == true)
                    {
                        //UnitsWithActionsLeft[key] = 0;
                        grappledUnitExist = true;
                    }
                }

                if (!grappledUnitExist)
                {
                    key.GrappledUnitId = 0;
                }
            }

            //Aktualizuje osiągnięcia
            key.GetComponent<Stats>().RoundsPlayed ++;
        }

        //Wykonuje testy grozy i strachu jeśli na polu bitwy są jednostki straszne lub przerażające
        if(GameManager.IsFearIncluded == true)
        {
            UnitsManager.Instance.LookForScaryUnits();
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
        NextRoundButton.gameObject.SetActive(false);
        _useFortunePointsButton.SetActive(false);

        for(int i=0; i < UnitsManager.Instance.AllUnits.Count; i++)
        {
            if (UnitsManager.Instance.AllUnits[i] == null) continue;
            
            InitiativeQueueManager.Instance.SelectUnitByQueue();
            yield return new WaitForSeconds(0.1f);
            
            Unit unit = null;
            if (Unit.SelectedUnit != null)
            {
                unit = Unit.SelectedUnit.GetComponent<Unit>();
            }
            else continue;

            if(!UnitsWithActionsLeft.ContainsKey(unit)) continue;

            // Jeśli jednostka to PlayerUnit i gramy w trybie ukrywania statystyk wrogów
            if (unit.CompareTag("PlayerUnit") && GameManager.IsStatsHidingMode)
            {
                // Czeka aż jednostka zakończy swoją turę (UnitsWithActionsLeft[unit] == 0 lub unit.IsTurnFinished)
                yield return new WaitUntil(() => (!UnitsWithActionsLeft.ContainsKey(unit) || UnitsWithActionsLeft[unit] == 0 && CombatManager.Instance.AvailableAttacks == 0) || unit.IsTurnFinished);
                yield return new WaitForSeconds(0.5f);
            }
            else // Jednostki wrogów lub wszystkie jednostki, jeśli nie ukrywamy ich statystyk
            {
                //TYMCZASOWE - test algorytmów gentycznych
                if(ReinforcementLearningManager.Instance.IsLearning)
                //if (ReinforcementLearningManager.Instance.IsRaceTrained(unit.GetComponent<Stats>().Race))
                {
                    if(unit.CompareTag("PlayerUnit"))
                    {
                        AutoCombatManager.Instance.Act(unit);
                    }
                    else
                    {
                        int iterationCount = 0;

                        while (UnitsWithActionsLeft.ContainsKey(unit) && UnitsWithActionsLeft[unit] > 0 && iterationCount < 5)
                        {
                            ReinforcementLearningManager.Instance.SimulateUnit(unit);
                            iterationCount++;
                        }
                        if(iterationCount >= 5 && UnitsWithActionsLeft.ContainsKey(unit) && UnitsWithActionsLeft[unit] > 0)
                        {
                            FinishTurn();
                        }
                    }
                }
                else
                {
                    //ReinforcementLearningManager.Instance.SimulateUnit(unit);
                    AutoCombatManager.Instance.Act(unit);
                }

                // Czeka, aż jednostka zakończy ruch, zanim wybierze kolejną jednostkę
                yield return new WaitUntil(() => MovementManager.Instance.IsMoving == false);
                yield return new WaitForSeconds(0.5f);
            }      
        }

        NextRoundButton.gameObject.SetActive(true);
        _useFortunePointsButton.SetActive(true);

        //DO SZKOLENIA AI
        if(ReinforcementLearningManager.Instance.IsLearning)
        {
            if(ReinforcementLearningManager.Instance.BothTeamsExist() == false || RoundNumber > 50)
            {
                // Iteruj po wszystkich jednostkach, które jeszcze żyją i są częścią drużyny Enemy
                foreach (Unit unit in UnitsManager.Instance.AllUnits)
                {
                    if(unit != null && unit.CompareTag("EnemyUnit") && unit.GetComponent<Stats>().TempHealth > 0)
                    {
                        ReinforcementLearningManager.Instance.AddTeamWinRewardForUnit(unit);
                    }
                }

                ReinforcementLearningManager.Instance.UpdateTeamWins();

                SaveAndLoadManager.Instance.SetLoadingType(true);
                SaveAndLoadManager.Instance.LoadGame("AIlearning");
            }

            yield return new WaitUntil(() => SaveAndLoadManager.Instance.IsLoading == false);

            GridManager.Instance.CheckTileOccupancy();
            NextRound();
        }
    }

    #region Units actions
    public bool DoHalfAction(Unit unit)
    {
        //Zapobiega zużywaniu akcji przed rozpoczęciem bitwy
        if(RoundNumber == 0) return true;

        if (UnitsWithActionsLeft.ContainsKey(unit) && UnitsWithActionsLeft[unit] >= 1)
        {
            // Automatyczny zapis, aby możliwe było użycie punktów szczęścia. Jeżeli jednostka ich nie posiada to zapis nie jest wykonywany
            if(unit.Stats.PS > 0 && !GameManager.IsAutoCombatMode)
            {
                SaveAndLoadManager.Instance.SaveUnits(UnitsManager.Instance.AllUnits, "autosave");
                _isFortunePointSpent = false;
            }

            UnitsWithActionsLeft[unit]--;
            DisplayActionsLeft();

            Debug.Log($"<color=green>{unit.GetComponent<Stats>().Name} wykonał/a akcję pojedynczą. </color>");

            //Zresetowanie szarży lub biegu, jeśli były aktywne (po zużyciu jednej akcji szarża i bieg nie mogą być możliwe)
            MovementManager.Instance.UpdateMovementRange(1);

            //Aktualizuje aktywną postać na kolejce inicjatywy, gdy obecna postać wykona wszystkie akcje w tej rundzie. 
            if(UnitsWithActionsLeft[unit] == 0)
            {
                //W przypadku ręcznego zadawania obrażeń, czekamy na wpisanie wartości obrażeń przed zmianą jednostki (jednostka jest wtedy zmieniana w funkcji ExecuteAttack w CombatManager)
                if (!CombatManager.Instance.IsManualPlayerAttack)
                {
                    InitiativeQueueManager.Instance.SelectUnitByQueue();
                }
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
        //Zapobiega zużywaniu akcji przed rozpoczęciem bitwy
        if(RoundNumber == 0) return true;

        if (UnitsWithActionsLeft.ContainsKey(unit) && UnitsWithActionsLeft[unit] == 2)
        {
            // Automatyczny zapis, aby możliwe było użycie punktów szczęścia. Jeżeli jednostka ich nie posiada to zapis nie jest wykonywany. W przypadku szarży gra jest zapisywana przed wykonaniem ruchu (w klasie CombatManager), a nie w momencie zużywania akcji
            if (unit.Stats.PS > 0 && !CombatManager.Instance.AttackTypes["Charge"] == true && !GameManager.IsAutoCombatMode)
            {
                SaveAndLoadManager.Instance.SaveUnits(UnitsManager.Instance.AllUnits, "autosave");
                _isFortunePointSpent = false;
            }

            UnitsWithActionsLeft[unit] -= 2;
            DisplayActionsLeft();

            Debug.Log($"<color=green>{unit.GetComponent<Stats>().Name} wykonał/a akcję podwójną. </color>");

            //Aktualizuje aktywną postać na kolejce inicjatywy, bo obecna postać wykonała wszystkie akcje w tej rundzie. Wyjątkiem jest atak wielokrotny
            if (!CombatManager.Instance.AttackTypes["SwiftAttack"])
            {
                //W przypadku ręcznego zadawania obrażeń, czekamy na wpisanie wartości obrażeń przed zmianą jednostki (jednostka jest wtedy zmieniana w funkcji ExecuteAttack w CombatManager)
                if (!CombatManager.Instance.IsManualPlayerAttack)
                {
                    InitiativeQueueManager.Instance.SelectUnitByQueue();
                }
            }    

            return true;
        }
        else
        {
            Debug.Log("Ta jednostka nie może w tej rundzie wykonać akcji podwójnej.");
            return false;
        }     
    }

    public void DisplayActionsLeft()
    {
        if (Unit.SelectedUnit != null && !UnitsWithActionsLeft.ContainsKey(Unit.SelectedUnit.GetComponent<Unit>())) return;

        if(Unit.SelectedUnit == null)
        {
            _useFortunePointsButton.SetActive(false);
        }
        else
        {
            Unit unit = Unit.SelectedUnit.GetComponent<Unit>();

            _actionsLeftSlider.value = UnitsWithActionsLeft[unit];

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

        DisplayActionsLeft();
    }

    public void UseFortunePoint()
    {
        if (Unit.SelectedUnit == null) return;

        Unit unit = Unit.SelectedUnit.GetComponent<Unit>();
        Stats stats = Unit.SelectedUnit.GetComponent<Stats>();

        if (UnitsWithActionsLeft[unit] == 2)
        {
            if (Unit.LastSelectedUnit == null) return;
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
        SaveAndLoadManager.Instance.LoadGame("autosave");

        _useFortunePointsButton.SetActive(false);
    }

    //Zakończenie tury danej jednostki mimo tego, że ma jeszcze dostępne akcje
    public void FinishTurn()
    {
        if (Unit.SelectedUnit == null) return;

        Unit unit = Unit.SelectedUnit.GetComponent<Unit>();
        unit.IsTurnFinished = true;

        InitiativeQueueManager.Instance.SelectUnitByQueue();
    }
    #endregion

    public void LoadRoundsManagerData(RoundsManagerData data)
    {
        RoundNumber = data.RoundNumber;
        if(RoundNumber > 0)
        {
            _roundNumberDisplay.text = "Runda: " + RoundNumber;
            NextRoundButton.transform.GetChild(0).GetComponent<TMP_Text>().text = "Następna runda";
        }
        else
        {
            _roundNumberDisplay.text = "Zaczynamy?";
            NextRoundButton.transform.GetChild(0).GetComponent<TMP_Text>().text = "Start";
        }

        // UnitsWithActionsLeft.Clear(); // Czyści słownik przed uzupełnieniem nowymi danymi

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
