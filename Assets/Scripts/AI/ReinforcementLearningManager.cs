using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.IO;
using System;
using UnitStates;
using TMPro;

namespace UnitStates
{
    public enum AIState
    {
        IsInMelee,
        IsHeavilyWounded,
        HasRangedWeapon,
        IsBeyondAttackRange,
        IsInChargeRange,
        CanParry,
        TargetBehindObstacle,
        CanDoFullAction,
        WeaponIsLoaded,
        
        // Dodaj tu kolejne stany
        COUNT // Tyle mamy stanów (9 => 2^9 = 512 kombinacji w wersji bool)
    }
}

public enum TargetType
{
    None = 0,    // dla akcji bez celu (DefensiveStance, Reload, itp.)
    Closest,
    Furthest,
    MostInjured,
    LeastInjured,
    Weakest,
    Strongest,
    MostAlliesNearby
}

public enum AttackType
{
    Move = 0,  
    Run,   
    Null,         // Zwykły atak
    Charge,
    Feint,
    Swift,
    Guarded,
    AllOut,

    DefensiveStance, 
    Aim,             
    Reload,          
    FinishTurn,      
    MoveAway,
    RunAway,
    Retreat,
    ChangeWeaponToRanged,
    ChangeWeaponToMelee
}

// Definiujemy parę (target, attack)
public class ActionDefinition
{
    public TargetType targetType;
    public AttackType attackType;

    public ActionDefinition(TargetType t, AttackType a)
    {
        targetType = t;
        attackType = a;
    }
}

public class ReinforcementLearningManager : MonoBehaviour
{
    public static ReinforcementLearningManager Instance { get; private set; }

    [Header("Q-learning parameters")]
    [Tooltip("Współczynnik uczenia (learning rate)")]
    public float Alpha = 0.1f;

    [Tooltip("Współczynnik dyskontowania (discount factor)")]
    public float Gamma = 0.9f;

    [Tooltip("Szansa na losową akcję (eksploracja)")]
    public float Epsilon = 0.2f;

    [Header("Logging Parameters")]
    [Tooltip("Number of actions per epoch before calculating average reward.")]
    public int ActionsPerEpoch = 1000;

    [Header("Graphing")]
    public SimpleGraph simpleGraph;

    // Logowanie nagród
    private List<float> epochRewards = new List<float>();
    private float currentEpochReward = 0f;
    private int actionsThisEpoch = 0;

    private const int HISTORY_LIMIT = 10;
    private Dictionary<Unit, Queue<(int state, int action, string race)>> unitActionHistory = new Dictionary<Unit, Queue<(int, int, string)>>();
    private const float TEAM_WIN_REWARD = 100f;

    [SerializeField] private int _playerWins;
    [SerializeField] private int _enemyWins;
    [SerializeField] private TMP_Text _teamWinsDisplay;

    // Liczba dostępnych akcji
    private const int ACTION_COUNT = 65;

    // Liczba kombinacji stanów (2^(AIState.COUNT))
    private int totalStateCombinations;

    // klucz: nazwa rasy (np. "Human", "Orc", "Elf"), wartość: tablica Q
    private Dictionary<string, float[,]> QTables = new Dictionary<string, float[,]>();

    // Jak często dana akcja została użyta
    private Dictionary<string, int[,]> ActionUsageCount = new Dictionary<string, int[,]>();

    public bool IsLearning;

    // Lista lub zbiór wytrenowanych ras
    private HashSet<string> _trainedRaces = new HashSet<string>();

    public bool HasHitTarget; // oznacza, że jednostka trafiła przeciwnika. Potrzebne do dawania za to nagrody

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        LoadQTables();

        // Obliczamy 2^(int)AIState.COUNT
        totalStateCombinations = 1 << ((int)AIState.COUNT);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R) && IsLearning)
        {
            SaveQTables();
        }
    }

    // Metoda do sprawdzania, czy rasa jest wytrenowana
    public bool IsRaceTrained(string race)
    {
        return _trainedRaces.Contains(race);
    }

    public void ToggleLogs()
    {
        Debug.unityLogger.logEnabled = !Debug.unityLogger.logEnabled;
    }

    // ======================================================================
    //             REJESTRACJA / POBIERANIE TABLICY Q DLA RASY
    // ======================================================================

    // Inicjalizuje (jeśli trzeba) Q-tabelę dla danej rasy.
    public void RegisterRace(string raceName)
    {
        if (string.IsNullOrEmpty(raceName)) return;

        // 1) Zarejestruj w QTables
        if (!QTables.ContainsKey(raceName))
        {
            float[,] table = new float[totalStateCombinations, ACTION_COUNT];
            QTables[raceName] = table;
            Debug.Log($"[RegisterRace] Dodano QTables dla '{raceName}'");
        }

        // 2) Zarejestruj w ActionUsageCount
        if (!ActionUsageCount.ContainsKey(raceName))
        {
            int[,] usage = new int[totalStateCombinations, ACTION_COUNT];
            ActionUsageCount[raceName] = usage;
            Debug.Log($"[RegisterRace] Dodano ActionUsageCount dla '{raceName}'");
        }
    }

    // Zwraca tablicę Q dla podanej rasy. Jeśli brak, tworzy nową.
    private float[,] GetQTable(string raceName)
    {
        RegisterRace(raceName);
        
        return QTables[raceName];
    }

    // ======================================================================
    //                     PODSTAWOWE METODY Q-LEARNING
    // ======================================================================

    public int EncodeState(bool[] states)
    {
        int stateIndex = 0;
        for (int i = 0; i < states.Length; i++)
        {
            if (states[i])
            {
                stateIndex |= (1 << i);
            }
        }
        return stateIndex;
    }

    private string DescribeState(int stateIndex)
    {
        int numberOfStates = (int)AIState.COUNT; 
        
        // Będziemy zbierać części tekstu w stylu "IsInMelee=True"
        List<string> parts = new List<string>();

        for (int i = 0; i < numberOfStates; i++)
        {
            // Rzutujemy i na enum, np. 0 -> AIState.IsInMelee, 1 -> AIState.IsHeavilyWounded, itd.
            AIState stateName = (AIState)i;

            // Sprawdzamy bit i w stateIndex
            bool isSet = (stateIndex & (1 << i)) != 0;

            // Dokładamy kawałek opisu "IsInMelee=True/False"
            parts.Add($"{stateName}={isSet}");
        }

        // Sklejamy całość, np. "IsInMelee=False, IsHeavilyWounded=False, HasRangedWeapon=True"
        return "(" + string.Join(", ", parts) + ")";
    }

    // Wybiera akcję epsilon-greedy na podstawie rasy i stanu.
    private ActionChoice ChooseValidActionEpsilonGreedy(ActionContext context)
    {
        float[,] qTable = GetQTable(context.RaceName);
        Unit unit = context.Unit;
        Stats stats = context.Unit.GetComponent<Stats>();

        Dictionary<Unit, bool[]> statesCache = new Dictionary<Unit, bool[]>();

        // Sprawdzenie, czy jednostka ma jeszcze dostępne akcje
        if (RoundsManager.Instance.UnitsWithActionsLeft.ContainsKey(unit) &&
            RoundsManager.Instance.UnitsWithActionsLeft[unit] == 0)
        {
            return new ActionChoice
            {
                ActionId = 64,
                ChosenTarget = null,
                ChosenStates = new bool[(int)AIState.COUNT]
            };
        }

        List<int> validActions = new List<int>();
        Unit fallbackTarget = null;
        bool[] fallbackStates = new bool[(int)AIState.COUNT];

        for (int i = 0; i < AllActions.Length; i++)
        {
            ActionDefinition def = AllActions[i];
            TargetType tType = def.targetType;
            AttackType aType = def.attackType;

            bool requiresTarget = (tType != TargetType.None);
            Unit potentialTarget = null;
            bool[] states = null;

            if (requiresTarget)
            {
                potentialTarget = GetTargetByType(context.Info, tType);
                if (potentialTarget == null) continue;

                if (!statesCache.ContainsKey(potentialTarget))
                {
                    statesCache[potentialTarget] = DetermineStates(unit, potentialTarget);
                }
                states = statesCache[potentialTarget];
            }
            else
            {
                // Dla akcji bez celu (np. Reload) nie potrzebujemy szukać celu
                states = DetermineStates(unit, null);
            }

            // Ustawienie fallbacku, jeśli to możliwe
            if (fallbackTarget == null && potentialTarget != null)
            {
                fallbackTarget = potentialTarget;
                fallbackStates = states;
            }

            // Sprawdzenie kosztu akcji
            if (GetActionCost(aType) == 2 && states[(int)AIState.CanDoFullAction] == false)
            {
                continue;
            }

            bool isInMelee = states[(int)AIState.IsInMelee];
            bool isHeavilyWounded = states[(int)AIState.IsHeavilyWounded];
            bool hasRangedWeapon = states[(int)AIState.HasRangedWeapon];
            bool isBeyondAttackRange = states[(int)AIState.IsBeyondAttackRange];
            bool isInChargeRange = states[(int)AIState.IsInChargeRange];

            bool isMove = (aType == AttackType.Move || aType == AttackType.MoveAway || aType == AttackType.Retreat);
            bool isRun = (aType == AttackType.Run || aType == AttackType.RunAway);
            bool isAttack = (aType != AttackType.Move
                            && aType != AttackType.DefensiveStance
                            && aType != AttackType.Aim
                            && aType != AttackType.Reload
                            && aType != AttackType.FinishTurn
                            && aType != AttackType.MoveAway
                            && aType != AttackType.RunAway
                            && aType != AttackType.Retreat
                            && aType != AttackType.ChangeWeaponToMelee
                            && aType != AttackType.ChangeWeaponToRanged);

            // ---- WARUNKI BLOKADY DLA ATAKÓW ----
            if (isAttack)
            {
                if (!context.OpponentExist) continue;
                if (!context.CanAttack) continue;
                if (context.CurrentWeapon == null) continue;
                if (context.CurrentWeapon.ReloadLeft > 0) continue;
                if (potentialTarget == null) continue;
                if (!context.Info.Distances.ContainsKey(potentialTarget)) continue;

                if (aType == AttackType.Charge)
                {
                    if (!isInChargeRange) continue;
                    if (!(isBeyondAttackRange && !hasRangedWeapon)) continue;
                }
                else
                {
                    if (hasRangedWeapon)
                    {
                        states[(int)AIState.IsBeyondAttackRange] =
                            !CombatManager.Instance.ValidateRangedAttack(unit, potentialTarget, context.CurrentWeapon, 
                                                                        context.Info.Distances[potentialTarget]);
                    }
                    else
                    {
                        states[(int)AIState.IsBeyondAttackRange] =
                            context.Info.Distances[potentialTarget] > context.CurrentWeapon.AttackRange;
                    }

                    if (isBeyondAttackRange) continue;
                }

                if (hasRangedWeapon && (aType == AttackType.Charge || aType == AttackType.Feint)) continue;
                if (aType == AttackType.Swift && stats != null && stats.A < 2) continue;
            }

            // ---- WARUNKI BLOKADY DLA RUCHU ----
            if (isMove || isRun)
            {
                if (tType == TargetType.Closest && !context.OpponentExist) continue;
                if (tType == TargetType.Furthest && !context.FurthestUnitExist) continue;
                if (tType == TargetType.MostInjured && !context.MostInjuredUnitExist) continue;
                if (tType == TargetType.LeastInjured && !context.LeastInjuredUnitExist) continue;
                if (tType == TargetType.Weakest && !context.WeakestUnitExist) continue;
                if (tType == TargetType.Strongest && !context.StrongestUnitExist) continue;
                if (tType == TargetType.MostAlliesNearby && !context.TargetWithMostAlliesExist) continue;
            }

            if(aType == AttackType.ChangeWeaponToRanged || aType == AttackType.ChangeWeaponToMelee)
            {
                string chosenWeaponType = aType == AttackType.ChangeWeaponToRanged ? "ranged" : "melee";
                bool chosenTypeExist = false;

                if(hasRangedWeapon && aType == AttackType.ChangeWeaponToRanged) continue;
                if(!hasRangedWeapon && aType == AttackType.ChangeWeaponToMelee) continue;

                // Sprawdzenie, czy jednostka posiada więcej niż jedną broń
                if (unit.GetComponent<Inventory>().AllWeapons.Count > 1)
                {
                    //  Zmienia bronie dopóki nie znajdzie takiej, która posuje do wybranego typu
                    foreach (Weapon w in  unit.GetComponent<Inventory>().AllWeapons)
                    {
                        if (w.Type.Contains(chosenWeaponType))
                        {
                            chosenTypeExist = true;
                            break;
                        }
                    }
                    
                    if (chosenTypeExist == false) continue;
                }
                else continue;
            }

            if (aType == AttackType.Retreat && isInMelee == false) continue;

            // ---- AKCJE SPECJALNE ----
            if (aType == AttackType.Reload)
            {
                if (context.CurrentWeapon != null && context.WeaponIsLoaded) continue;
            }

            validActions.Add(i);
        }

        // Jeśli brak dozwolonych akcji => FinishTurn
        if (validActions.Count == 0)
        {
            return new ActionChoice
            {
                ActionId = 64,
                ChosenTarget = fallbackTarget,
                ChosenStates = fallbackStates
            };
        }

        // --- EPSILON-GREEDY ---
        int chosenActionId;
        if (UnityEngine.Random.value < Epsilon)
        {
            int randomIndex = UnityEngine.Random.Range(0, validActions.Count);
            chosenActionId = validActions[randomIndex];
        }
        else
        {
            float bestVal = float.NegativeInfinity;
            List<int> bestActions = new List<int>();

            foreach (int actID in validActions)
            {
                float val = qTable[context.StateIndex, actID];
                if (val > bestVal)
                {
                    bestVal = val;
                    bestActions.Clear();
                    bestActions.Add(actID);
                }
                else if (Mathf.Approximately(val, bestVal))
                {
                    bestActions.Add(actID);
                }
            }

            if (bestActions.Count > 0)
            {
                int randomBestIndex = UnityEngine.Random.Range(0, bestActions.Count);
                chosenActionId = bestActions[randomBestIndex];
            }
            else
            {
                int randomIndex = UnityEngine.Random.Range(0, validActions.Count);
                chosenActionId = validActions[randomIndex];
            }
        }

        ActionDefinition chosenDef = AllActions[chosenActionId];
        Unit chosenTarget = null;
        bool[] chosenStates = null;

        if (chosenDef.targetType != TargetType.None)
        {
            chosenTarget = GetTargetByType(context.Info, chosenDef.targetType);
            if (chosenTarget != null)
            {
                if (statesCache.ContainsKey(chosenTarget))
                    chosenStates = statesCache[chosenTarget];
                else
                    chosenStates = DetermineStates(unit, chosenTarget);
            }
            else
            {
                chosenStates = DetermineStates(unit, null);
            }
        }
        else
        {
            chosenStates = DetermineStates(unit, null);
        }

        return new ActionChoice
        {
            ActionId = chosenActionId,
            ChosenTarget = chosenTarget,
            ChosenStates = chosenStates
        };
    }

    public class ActionChoice
    {
        public int ActionId;         // numer akcji w AllActions
        public Unit ChosenTarget;    // docelowy target
        public bool[] ChosenStates;  // stany dla (unit, target)
    }

    private int GetActionCost(AttackType aType)
    {
        // Dla przykładu:
        // charge, all-out, swift, guarded, defensiveStance itp. => koszt 2
        // reszta => koszt 1

        switch (aType)
        {
            case AttackType.Charge:
            case AttackType.AllOut:
            case AttackType.Swift:
            case AttackType.Guarded:
            case AttackType.DefensiveStance:
            case AttackType.Run:
            case AttackType.RunAway:
            case AttackType.Retreat:
                return 2;
            
            default:
                return 1;
        }
    }

    /// Aktualizuje Q wg formuły Q-learningu dla danej rasy.
    private void UpdateQ(string raceName, int oldState, int action, float reward, int newState)
    {
        float[,] qTable = GetQTable(raceName);

        float oldQ = qTable[oldState, action];
        float maxQnext = float.NegativeInfinity;
        for (int a = 0; a < ACTION_COUNT; a++)
        {
            if (qTable[newState, a] > maxQnext)
            {
                maxQnext = qTable[newState, a];
            }
        }

        // Zabezpieczenie przed float.NegativeInfinity
        if (maxQnext == float.NegativeInfinity)
        {
            maxQnext = 0f;
        }

        float newQ = oldQ + Alpha * (reward + Gamma * maxQnext - oldQ);
        qTable[oldState, action] = newQ;
    }

    // ======================================================================
    //         LOGIKA STANÓW ORAZ GŁÓWNA METODA SimulateUnit
    // ======================================================================

    public bool[] DetermineStates(Unit unit, Unit target)
    {
        bool[] states = new bool[(int)AIState.COUNT];

        // Pobranie komponentów
        Stats stats = unit.GetComponent<Stats>();
        Weapon weapon = InventoryManager.Instance.ChooseWeaponToAttack(unit.gameObject);

        // Sprawdzenie, czy jednostka ma broń zasięgową
        bool hasRanged = weapon != null && weapon.Type.Contains("ranged");
        states[(int)AIState.HasRangedWeapon] = hasRanged;

        if (target != null)
        {
            float distance = CombatManager.Instance.CalculateDistance(unit.gameObject, target.gameObject);

            // Ustawienie stanu IsInMelee
            states[(int)AIState.IsInMelee] = distance <= 1.5f;

            // Pobranie zasięgu ataku z broni
            float attackRange = weapon != null ? weapon.AttackRange : 0f;

            // Ustawienie stanu IsBeyondAttackRange
            if (hasRanged)
            {
                // Dla broni zasięgowej
                states[(int)AIState.IsBeyondAttackRange] = !CombatManager.Instance.ValidateRangedAttack(unit, target, weapon, distance);
            }
            else
            {
                // Dla broni bezzasięgowej, IsBeyondAttackRange jest prawdziwe, gdy odległość > attackRange
                states[(int)AIState.IsBeyondAttackRange] = distance > attackRange;
            }

            // Ustawienie stanu IsInChargeRange
            float chargeRange = stats.TempSz * 2;
            //Ścieżka ruchu szarżującego
            GameObject targetTile = CombatManager.Instance.GetTileAdjacentToTarget(unit.gameObject, target.gameObject);
            Vector2 targetTilePosition = Vector2.zero;
            if(targetTile == null)
            {
                states[(int)AIState.IsInChargeRange] = false;
            }
            else
            {
                List<Vector2> path = MovementManager.Instance.FindPath(unit.transform.position, targetTilePosition);
                if(path == null || path.Count == 0)
                {
                    states[(int)AIState.IsInChargeRange] = false;
                }
                else
                {
                    states[(int)AIState.IsInChargeRange] = path.Count <= chargeRange && path.Count >= 2;
                }
            }

            // Ustalmy, że to ma sens tylko dla broni dystansowych:
            if (hasRanged)
            {
                states[(int)AIState.TargetBehindObstacle] = IsTargetBehindObstacle(unit.gameObject, target.gameObject, distance);
            }
            else
            {
                states[(int)AIState.TargetBehindObstacle] = false;
            }
        }

        // Ustawienie stanu IsHeavilyWounded
        if (stats != null)
        {
            states[(int)AIState.IsHeavilyWounded] = stats.TempHealth <= stats.MaxHealth / 3f;
        }

        // Ustawienie stanu WeaponIsLoaded
        states[(int)AIState.WeaponIsLoaded] = weapon.ReloadLeft == 0 ? true : false;

        // Ustawienie stanu CanParry
        states[(int)AIState.CanParry] = unit.CanParry;

        // Ustawienie stanu CanDoFullAction
        states[(int)AIState.CanDoFullAction] = RoundsManager.Instance.UnitsWithActionsLeft.ContainsKey(unit) && RoundsManager.Instance.UnitsWithActionsLeft[unit] == 2 ? true : false;

        return states;
    }

    // Główna metoda sterująca akcjami jednostki w turze – Q-learning.
    public void SimulateUnit(Unit unit)
    {
        if (unit == null) return;
        Stats stats = unit.GetComponent<Stats>();
        if (stats == null) return;
        if (stats.TempHealth < 0) return; // już martwy?
        
        // (A) Zbierz info o potencjalnych celach
        TargetsInfo info = GatherTargetsInfo(unit);

        // (B) Przygotuj kontekst do decyzji
        ActionContext ctx = new ActionContext
        {
            Unit = unit,
            RaceName = stats.Race,
            StateIndex = 0,
            WeaponIsLoaded = (InventoryManager.Instance.ChooseWeaponToAttack(unit.gameObject)?.ReloadLeft == 0),
            HasRanged = false, 
            IsInMelee = false,
            CanAttack = unit.CanAttack,
            IsBeyondAttackRange = false,
            IsInChargeRange = false,
            CanParry = unit.CanParry,
            TargetBehindObstacle = false,
            CanDoFullAction = RoundsManager.Instance.UnitsWithActionsLeft.ContainsKey(unit) && RoundsManager.Instance.UnitsWithActionsLeft[unit] == 2 ? true : false,

            OpponentExist = (info.Closest != null),
            FurthestUnitExist = (info.Furthest != null),
            MostInjuredUnitExist = (info.MostInjured != null),
            LeastInjuredUnitExist = (info.LeastInjured != null),
            WeakestUnitExist = (info.Weakest != null),
            StrongestUnitExist = (info.Strongest != null),
            TargetWithMostAlliesExist = (info.WithMostAllies != null),

            CurrentWeapon = InventoryManager.Instance.ChooseWeaponToAttack(unit.gameObject),
            Info = info
        };

        // (C) Wybór akcji
        ActionChoice choice = ChooseValidActionEpsilonGreedy(ctx);
        int chosenActionId = choice.ActionId;
        Unit chosenTarget = choice.ChosenTarget;
        bool[] oldStates = choice.ChosenStates;

        // Zakoduj "stary stan" do Q-learningu
        int oldStateIndex = EncodeState(oldStates);

        // Zapisz historię akcji dla jednostki
        if (!unitActionHistory.ContainsKey(unit)) {
            unitActionHistory[unit] = new Queue<(int, int, string)>();
        }
        var historyQueue = unitActionHistory[unit];
        historyQueue.Enqueue((oldStateIndex, chosenActionId, ctx.RaceName));
        if (historyQueue.Count > HISTORY_LIMIT) {
            historyQueue.Dequeue(); // usuń najstarszą akcję, jeśli przekroczono limit
        }

        ActionUsageCount[ctx.RaceName][oldStateIndex, chosenActionId]++;

        // (D) Wykonaj akcję (jedną!), nalicz reward
        int oldHP = stats.TempHealth;
        float reward = PerformParameterAction(chosenActionId, unit, info, oldHP);

        // Zbieranie nagród do logowania
        currentEpochReward += reward;
        actionsThisEpoch++;

        // (E) Po wykonaniu akcji obliczamy nowy stan (dla *tego samego* celu chosenTarget)
        bool[] newStates = DetermineStates(unit, chosenTarget);
        int newStateIndex = EncodeState(newStates);

        // (F) Update Q
        UpdateQ(ctx.RaceName, oldStateIndex, chosenActionId, reward, newStateIndex);

        // (G) W tym momencie kończymy. *Nie* wywołujemy rekurencji. 
        // Zamiast rekurencji, pozwalamy zewnętrznemu kodowi (np. RoundsManager) wywołać SimulateUnitOneAction() ponownie,
        // jeśli jednostka nadal ma wolne akcje w tej turze.

        // (H) Sprawdzamy koniec epoki
        if (actionsThisEpoch >= ActionsPerEpoch)
        {
            float averageReward = currentEpochReward / actionsThisEpoch;
            epochRewards.Add(averageReward);
            Debug.Log($"Epoch completed. Average Reward: {averageReward:F2}");

            currentEpochReward = 0f;
            actionsThisEpoch = 0;

            //Zmniejsz Epsilon
            Epsilon *= 0.99f;
            Epsilon = Mathf.Max(Epsilon, 0.05f);

            SaveAverageReward(averageReward);
            SaveQTables();
        }
    }

    private float PerformParameterAction(int actionID, Unit unit, TargetsInfo info, int oldHP)
    {
        float reward = 0f;
        // Sprawdzamy definicję:
        if (actionID < 0 || actionID >= AllActions.Length)
        {
            // out of range => FinishTurn (ID=64)
            RoundsManager.Instance.FinishTurn();
            return reward;
        }

        ActionDefinition def = AllActions[actionID];
        TargetType tType = def.targetType;
        AttackType aType = def.attackType;

        // Znajdujemy Unit target (jeśli w ogóle)
        Unit target = GetTargetByType(info, tType);

        // 1) Move
        if (aType == AttackType.Move)
        {
            if (target != null)
                MoveTowards(unit, target.gameObject);
            reward += CalculateRewardBasedOnUnitHealth(unit.GetComponent<Stats>(), oldHP);
            return reward;
        }

        // 2) Run
        if (aType == AttackType.Run)
        {
            if (target != null)
                MoveTowards(unit, target.gameObject, 3);
            reward += CalculateRewardBasedOnUnitHealth(unit.GetComponent<Stats>(), oldHP);
            return reward;
        }

        // 3) Specjalne akcje:
        if (aType == AttackType.DefensiveStance)
        {
            CombatManager.Instance.DefensiveStance();
            return reward;
        }
        if (aType == AttackType.Aim)
        {
            CombatManager.Instance.SetAim();
            return reward;
        }
        if (aType == AttackType.Reload)
        {
            CombatManager.Instance.Reload();
            reward++; // drobna nagroda
            return reward;
        }
        if (aType == AttackType.FinishTurn)
        {
            RoundsManager.Instance.FinishTurn();
            return reward;
        }
        if (aType == AttackType.MoveAway)
        {
            GameObject retreatTile = GetTileFarthestFromTarget(unit.gameObject, target.gameObject);
            if (retreatTile != null && target != null)
                MoveTowards(unit, retreatTile, 1, true);
            reward += CalculateRewardBasedOnUnitHealth(unit.GetComponent<Stats>(), oldHP);
            return reward;
        }
        if (aType == AttackType.RunAway)
        {
            GameObject retreatTile = GetTileFarthestFromTarget(unit.gameObject, target.gameObject);
            if (retreatTile != null && target != null)
                MoveTowards(unit, retreatTile, 3, true);
            reward += CalculateRewardBasedOnUnitHealth(unit.GetComponent<Stats>(), oldHP);
            return reward;
        }
        if (aType == AttackType.Retreat)
        {
            GameObject retreatTile = GetTileFarthestFromTarget(unit.gameObject, target.gameObject);
            if (retreatTile != null && target != null)
            {
                MovementManager.Instance.Retreat(true);
                MoveTowards(unit, retreatTile, 1, true);
            }
            reward += CalculateRewardBasedOnUnitHealth(unit.GetComponent<Stats>(), oldHP);
            return reward;
        }
        if(aType == AttackType.ChangeWeaponToRanged)
        {
            ChangeWeapon(unit, "ranged");
            return reward;
        }
        if(aType == AttackType.ChangeWeaponToMelee)
        {
            ChangeWeapon(unit, "melee");
            return reward;
        }

        //Będziemy sprawdzać, czy zdołaliśmy zabić wroga i dać nagrodę, a jeśli my umarliśmy – karę]
        // Najpierw zapisujemy HP wroga (jeśli jest)
        int enemyOverall = 0;
        if (target != null)
        {
            enemyOverall = target.GetComponent<Stats>().Overall; 
        }

        // 4) Różne typy ataku (zwykły, Charge, Feint, Swift, Guarded, AllOut)
        // 'Null' = zwykły atak
        string attackName = null;
        switch (aType)
        {
            case AttackType.Null:    attackName = null; break;
            case AttackType.Charge:  attackName = "Charge"; break;
            case AttackType.Feint:   attackName = "Feint"; break;
            case AttackType.Swift:   attackName = "SwiftAttack"; break;
            case AttackType.Guarded: attackName = "GuardedAttack"; break;
            case AttackType.AllOut:  attackName = "AllOutAttack"; break;
        }

        reward += PerformAttack(unit.gameObject, target? target.gameObject:null, attackName);

        //Sprawdźmy, czy przeciwnik został zabity po ataku]
        if (target == null ||  target.GetComponent<Stats>().TempHealth < 0)
        {
            // Zakładam, że jeśli target jest null, został usunięty z gry (zabity)
            // Nagroda proporcjonalna do overall wroga
            reward += enemyOverall / 5;
        }

        reward += RoundsManager.RoundNumber / 3f; //Nagroda za przeżycie jak najdłużej. Im dłużej przeżyje tym większą nagrodę dostaje za każdą rundę

        return reward;
    }

    private Unit GetTargetByType(TargetsInfo info, TargetType t)
    {
        switch (t)
        {
            case TargetType.Closest:     return info.Closest;
            case TargetType.Furthest:    return info.Furthest;
            case TargetType.MostInjured: return info.MostInjured;
            case TargetType.LeastInjured:return info.LeastInjured;
            case TargetType.Weakest:     return info.Weakest;
            case TargetType.Strongest:   return info.Strongest;
            case TargetType.MostAlliesNearby: return info.WithMostAllies;
            default: return null; // None
        }
    }

    private static readonly ActionDefinition[] AllActions = new ActionDefinition[]
    {
        // 0..6: Ruch
        new ActionDefinition(TargetType.Closest,         AttackType.Move),
        new ActionDefinition(TargetType.Furthest,        AttackType.Move),
        new ActionDefinition(TargetType.MostInjured,     AttackType.Move),
        new ActionDefinition(TargetType.LeastInjured,    AttackType.Move),
        new ActionDefinition(TargetType.Weakest,         AttackType.Move),
        new ActionDefinition(TargetType.Strongest,       AttackType.Move),
        new ActionDefinition(TargetType.MostAlliesNearby,AttackType.Move),

        // 7..13: Bieg
        new ActionDefinition(TargetType.Closest,         AttackType.Run),
        new ActionDefinition(TargetType.Furthest,        AttackType.Run),
        new ActionDefinition(TargetType.MostInjured,     AttackType.Run),
        new ActionDefinition(TargetType.LeastInjured,    AttackType.Run),
        new ActionDefinition(TargetType.Weakest,         AttackType.Run),
        new ActionDefinition(TargetType.Strongest,       AttackType.Run),
        new ActionDefinition(TargetType.MostAlliesNearby,AttackType.Run),

        // 14..20: Zwykły atak (Null)
        new ActionDefinition(TargetType.Closest,     AttackType.Null),
        new ActionDefinition(TargetType.Furthest,    AttackType.Null),
        new ActionDefinition(TargetType.MostInjured, AttackType.Null),
        new ActionDefinition(TargetType.LeastInjured,AttackType.Null),
        new ActionDefinition(TargetType.Weakest,     AttackType.Null),
        new ActionDefinition(TargetType.Strongest,   AttackType.Null),
        new ActionDefinition(TargetType.MostAlliesNearby,AttackType.Null),

        // 21..27: Szarża
        new ActionDefinition(TargetType.Closest,     AttackType.Charge),
        new ActionDefinition(TargetType.Furthest,    AttackType.Charge),
        new ActionDefinition(TargetType.MostInjured, AttackType.Charge),
        new ActionDefinition(TargetType.LeastInjured,AttackType.Charge),
        new ActionDefinition(TargetType.Weakest,     AttackType.Charge),
        new ActionDefinition(TargetType.Strongest,   AttackType.Charge),
        new ActionDefinition(TargetType.MostAlliesNearby,AttackType.Charge),

        // 28..34: Finta
        new ActionDefinition(TargetType.Closest,     AttackType.Feint),
        new ActionDefinition(TargetType.Furthest,    AttackType.Feint),
        new ActionDefinition(TargetType.MostInjured, AttackType.Feint),
        new ActionDefinition(TargetType.LeastInjured,AttackType.Feint),
        new ActionDefinition(TargetType.Weakest,     AttackType.Feint),
        new ActionDefinition(TargetType.Strongest,   AttackType.Feint),
        new ActionDefinition(TargetType.MostAlliesNearby,AttackType.Feint),

        // 35..41: Atak wielokrotny
        new ActionDefinition(TargetType.Closest,     AttackType.Swift),
        new ActionDefinition(TargetType.Furthest,    AttackType.Swift),
        new ActionDefinition(TargetType.MostInjured, AttackType.Swift),
        new ActionDefinition(TargetType.LeastInjured,AttackType.Swift),
        new ActionDefinition(TargetType.Weakest,     AttackType.Swift),
        new ActionDefinition(TargetType.Strongest,   AttackType.Swift),
        new ActionDefinition(TargetType.MostAlliesNearby,AttackType.Swift),

        // 42..48: Ostrożny atak
        new ActionDefinition(TargetType.Closest,     AttackType.Guarded),
        new ActionDefinition(TargetType.Furthest,    AttackType.Guarded),
        new ActionDefinition(TargetType.MostInjured, AttackType.Guarded),
        new ActionDefinition(TargetType.LeastInjured,AttackType.Guarded),
        new ActionDefinition(TargetType.Weakest,     AttackType.Guarded),
        new ActionDefinition(TargetType.Strongest,   AttackType.Guarded),
        new ActionDefinition(TargetType.MostAlliesNearby,AttackType.Guarded),

        // 49..55: Szaleńczy atak
        new ActionDefinition(TargetType.Closest,     AttackType.AllOut),
        new ActionDefinition(TargetType.Furthest,    AttackType.AllOut),
        new ActionDefinition(TargetType.MostInjured, AttackType.AllOut),
        new ActionDefinition(TargetType.LeastInjured,AttackType.AllOut),
        new ActionDefinition(TargetType.Weakest,     AttackType.AllOut),
        new ActionDefinition(TargetType.Strongest,   AttackType.AllOut),
        new ActionDefinition(TargetType.MostAlliesNearby,AttackType.AllOut),

        // 56: Pozycja Obronna
        new ActionDefinition(TargetType.None, AttackType.DefensiveStance),

        // 57: Przycelowanie
        new ActionDefinition(TargetType.None, AttackType.Aim),

        // 58: Przeładowanie
        new ActionDefinition(TargetType.None, AttackType.Reload),

        // 59: Odejście od najbliższego przeciwnika
        new ActionDefinition(TargetType.Closest, AttackType.MoveAway),

        // 60: Bieg jak najdalej od najbliższego przeciwnika
        new ActionDefinition(TargetType.Closest, AttackType.RunAway),

        // 61: Bezpieczny odwrót od najbliższego przeciwnika
        new ActionDefinition(TargetType.Closest, AttackType.Retreat),

        // 62: Zmiana broni na melee
        new ActionDefinition(TargetType.None, AttackType.ChangeWeaponToMelee),

        // 63: Zmiana broni na ranged
        new ActionDefinition(TargetType.None, AttackType.ChangeWeaponToRanged),

        // 64: Zakończenie tury
        new ActionDefinition(TargetType.None, AttackType.FinishTurn),
    };

    // ======================================================================
    //                      POMOCNICZE METODY
    // ======================================================================

    private void MoveTowards(Unit unit, GameObject targetObject, int modifier = 1, bool retreat = false)
    {
        //Bieg
        if(modifier != 1)
        {
            MovementManager.Instance.UpdateMovementRange(modifier);
        }

        GameObject tile;
        if(retreat)
        {
            tile = targetObject;
        }
        else
        {
            tile = CombatManager.Instance.GetTileAdjacentToTarget(unit.gameObject, targetObject);
        }

        if (tile == null) return;
        MovementManager.Instance.MoveSelectedUnit(tile, unit.gameObject);
        Physics2D.SyncTransforms();
    }

    private int PerformAttack(GameObject attacker, GameObject target, string attackType)
    {
        if (attackType != null)
        {
            CombatManager.Instance.ChangeAttackType(attackType);
        }

        int oldHP = target.GetComponent<Stats>().TempHealth;

        if(attackType == "SwiftAttack")
        {
            for (int i = 1; i <= attacker.GetComponent<Stats>().A; i++)
            {
                //Zapobiega kolejnym atakom, jeśli przeciwnik już nie żyje
                if (target == null || target.GetComponent<Stats>().TempHealth < 0) break;

                CombatManager.Instance.Attack(attacker.GetComponent<Unit>(), target.GetComponent<Unit>(), false);
            }
        }
        else
        {
            CombatManager.Instance.Attack(attacker.GetComponent<Unit>(), target.GetComponent<Unit>(), false);
        }

        int newHP = target.GetComponent<Stats>().TempHealth;

        int damageReward = oldHP - newHP;
        int hitReward = HasHitTarget ? 2 : 0;

        return damageReward + hitReward; // Nagroda równa różnicy w HP przeciwnika
    }

    private void ChangeWeapon(Unit unit, string chosenWeaponType)
    {
        Weapon weapon = InventoryManager.Instance.ChooseWeaponToAttack(unit.gameObject);

        if (weapon.Type.Contains(chosenWeaponType)) return;

        // Sprawdzenie, czy jednostka posiada więcej niż jedną broń
        if (InventoryManager.Instance.InventoryScrollViewContent.GetComponent<CustomDropdown>().Buttons.Count > 1)
        {
            int selectedIndex = 1;

            //  Zmienia bronie dopóki nie znajdzie takiej, która posuje do wybranego typu
            for (int i = 0; i < InventoryManager.Instance.InventoryScrollViewContent.GetComponent<CustomDropdown>().Buttons.Count; i++)
            {
                if (weapon.Type.Contains(chosenWeaponType)) break;

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
        }
    }

    private int CalculateRewardBasedOnUnitHealth(Stats stats, int oldAttackerHP)
    {
        int reward = 0;

        // Obliczamy utratę HP atakującego
        int newAttackerHP = stats.TempHealth;
        int lostHP = oldAttackerHP - newAttackerHP;

        // Kara za utracone HP
        if (lostHP > 0)
        {
            reward -= lostHP; // np. -1 za każdy utracony punkt
        }

        //Kara za śmierć
        if (stats == null || stats.TempHealth < 0)
        {
            reward -= 15;
        }

        return reward;
    }

    public void AddTeamWinRewardForUnit(Unit unit)
    {
        if (!unitActionHistory.ContainsKey(unit)) return;

        var historyQueue = unitActionHistory[unit];

        foreach (var record in historyQueue)
        {
            float[,] qTable = GetQTable(record.race);
            int stateIndex = record.state;
            int actionId = record.action;

            // Dodajemy pełen reward do Q dla tej akcji; można zastosować mniejszy procent
            qTable[stateIndex, actionId] += TEAM_WIN_REWARD * 0.01f;
        }
    }

    public TargetsInfo GatherTargetsInfo(Unit currentUnit)
    {
        TargetsInfo info = new TargetsInfo();

        foreach (Unit other in UnitsManager.Instance.AllUnits)
        {
            if (!IsValidTarget(currentUnit, other)) 
                continue;

            // Oblicz distance
            float dist = Vector2.Distance(currentUnit.transform.position, other.transform.position);
            info.Distances[other] = dist;

            // 1. Najbliższy
            if (dist < info.ClosestDistance)
            {
                info.ClosestDistance = dist;
                info.Closest = other;
            }

            // 2. Najdalszy
            if (dist > info.FurthestDistance)
            {
                info.FurthestDistance = dist;
                info.Furthest = other;
            }

            // 3. Najbardziej ranny
            float hp = other.GetComponent<Stats>().TempHealth;
            if (hp < info.MostInjuredHP)
            {
                info.MostInjuredHP = hp;
                info.MostInjured = other;
            }

            // 4. Najmniej ranny
            if (hp > info.LeastInjuredHP)
            {
                info.LeastInjuredHP = hp;
                info.LeastInjured = other;
            }

            // 5. Najniższy Overall
            int ov = other.GetComponent<Stats>().Overall;
            if (ov < info.WeakestOverall)
            {
                info.WeakestOverall = ov;
                info.Weakest = other;
            }

            // 6. Najwyższy Overall
            if (ov > info.StrongestOverall)
            {
                info.StrongestOverall = ov;
                info.Strongest = other;
            }

            // 7. targetWithMostAllies:
            // Znajdujemy jednostkę z największą przewagą liczebną sojuszników
            int adjacentAllies = 0;
            int adjacentOpponents = 0;
            CountAdjacentUnits(other.transform.position, currentUnit.tag, other.tag, ref adjacentAllies, ref adjacentOpponents);
            int advantage = adjacentAllies - adjacentOpponents;
            if (advantage > info.WithMostAlliesScore)
            {
                info.WithMostAlliesScore = advantage;
                info.WithMostAllies = other;
            }
        }

        return info;
    }

    private bool IsTargetBehindObstacle(GameObject attacker, GameObject target, float attackDistance)
    {
        if (attacker == null || target == null) return false;

        Vector2 origin = attacker.transform.position;
        Vector2 direction = target.transform.position - attacker.transform.position;
        
        // RaycastAll w stronę celu
        RaycastHit2D[] raycastHits = Physics2D.RaycastAll(origin, direction, attackDistance);

        foreach (var hit in raycastHits)
        {
            if (hit.collider == null) continue;

            // Pomińmy samego siebie i cel:
            if (hit.collider.gameObject == attacker || hit.collider.gameObject == target)
                continue;

            // Sprawdzamy przeszkodę
            var mapElement = hit.collider.GetComponent<MapElement>();
            var otherUnit  = hit.collider.GetComponent<Unit>();

            if ((mapElement != null && mapElement.IsLowObstacle) || otherUnit != null)
            {
                // Zwracamy true - bo to znaczy, że jest co najmniej jedna przeszkoda
                return true;
            }
        }

        return false;
    }

    public GameObject GetTileFarthestFromTarget(GameObject attacker, GameObject target)
    {
        if (target == null) return null;

        Vector2 attackerPos = attacker.transform.position;
        Vector2 targetPos = target.transform.position;

        // Pobierz zasięg ruchu jednostki
        int movementRange = attacker.GetComponent<Stats>().TempSz;

        Tile farthestTile = null;
        float maxDistance = -1f;
        int shortestPathLengthForFarthest = int.MaxValue;

        foreach (Tile tile in GridManager.Instance.Tiles)
        {
            if (tile.IsOccupied) continue; // Pomijamy zajęte pola

            Vector2 tilePos = tile.transform.position;

            // Oblicz długość ścieżki do danego pola
            List<Vector2> path = MovementManager.Instance.FindPath(attackerPos, tilePos);
            if (path.Count == 0) continue; // Brak ścieżki
            if (path.Count > movementRange) continue; // Poza zasięgiem ruchu

            // Oblicz odległość od przeciwnika
            float distanceToTarget = Vector2.Distance(tilePos, targetPos);

            // Aktualizacja najdalszego pola
            if (distanceToTarget > maxDistance)
            {
                maxDistance = distanceToTarget;
                farthestTile = tile;
                shortestPathLengthForFarthest = path.Count;
            }
        }

        if (farthestTile != null)
        {
            return farthestTile.gameObject;
        }
        else
        {
            return null;
        }
    }

    private void CountAdjacentUnits(Vector2 center, string allyTag, string opponentTag, ref int allies, ref int opponents)
    {
        HashSet<Collider2D> countedOpponents = new HashSet<Collider2D>();
        Vector2[] positions =
        {
            center + Vector2.right, center + Vector2.left,
            center + Vector2.up, center + Vector2.down,
            center + new Vector2(1,1), center + new Vector2(-1,-1),
            center + new Vector2(-1,1), center + new Vector2(1,-1)
        };

        foreach (var pos in positions)
        {
            Collider2D col = Physics2D.OverlapPoint(pos);
            if (col == null) continue;

            if (col.CompareTag(allyTag))
            {
                allies++;
            }
            else if (col.CompareTag(opponentTag) && !countedOpponents.Contains(col))
            {
                opponents++;
                countedOpponents.Add(col);
            }
        }
    }

    private bool IsValidTarget(Unit currentUnit, Unit other)
    {
        if (other == null) return false;
        if (other == currentUnit) return false;
        if (other.CompareTag(currentUnit.tag)) return false;
        if (other.GetComponent<Stats>().TempHealth < 0) return false;

        return true;
    }

    public bool BothTeamsExist()
    {
        bool enemyUnitExists = false;
        bool playerUnitExists = false;

        foreach (Unit unit in UnitsManager.Instance.AllUnits)
        {
            if (unit == null) continue;

            if (unit.CompareTag("PlayerUnit")) playerUnitExists = true;
            else if (unit.CompareTag("EnemyUnit")) enemyUnitExists = true;

            // Jeśli obie drużyny istnieją, zwróć true natychmiast
            if (playerUnitExists && enemyUnitExists) return true;
        }

        if(playerUnitExists == false && enemyUnitExists == true)
        {    
            _enemyWins ++;
        }
        else if(enemyUnitExists == false && playerUnitExists == true)
        {
            _playerWins ++;
        }

        // Jeśli pętla się zakończy, sprawdź, czy którakolwiek drużyna nie istnieje
        return playerUnitExists && enemyUnitExists;
    }

    // ======================================================================
    // ZAPIS / ODCZYT tablic Q
    // ======================================================================
    [System.Serializable]
    public class QTableData
    {
        public string raceName;
        public int rows;
        public int cols;
        public List<float> values = new List<float>();
    }

    [System.Serializable]
    public class QTablesContainer
    {
        public List<QTableData> tables = new List<QTableData>();
    }

    public void SaveQTables()
    {
        QTablesContainer container = new QTablesContainer();

        foreach (var kvp in QTables) // kvp.Key -> raceName, kvp.Value -> float[,]
        {
            QTableData data = new QTableData();
            data.raceName = kvp.Key;

            int rows = kvp.Value.GetLength(0);
            int cols = kvp.Value.GetLength(1);
            data.rows = rows;
            data.cols = cols;

            for (int r=0; r<rows; r++)
            {
                for (int c=0; c<cols; c++)
                {
                    data.values.Add( kvp.Value[r,c] );
                }
            }

            container.tables.Add(data);
        }

        string json = JsonUtility.ToJson(container, true);

        string filePath = Path.Combine(Application.persistentDataPath, "q_tables.json");
        File.WriteAllText(filePath, json);

        Debug.Log($"QTables saved to {filePath}");
    }

    public void LoadQTables()
    {
        string filePath = Path.Combine(Application.persistentDataPath, "q_tables.json");
        if (!File.Exists(filePath))
        {
            Debug.LogWarning("No QTables file found to load.");
            return;
        }

        string json = File.ReadAllText(filePath);
        QTablesContainer container = JsonUtility.FromJson<QTablesContainer>(json);

        foreach (var data in container.tables)
        {
            float[,] table = new float[data.rows, data.cols];
            int idx = 0;

            for (int r=0; r<data.rows; r++)
            {
                for (int c=0; c<data.cols; c++)
                {
                    table[r,c] = data.values[idx];
                    idx++;
                }
            }

            QTables[data.raceName] = table;

            _trainedRaces.Add(data.raceName);
        }
    }

    
    // ======================================================================
    //                        DEBUGOWANIE WYNIKÓW
    // ======================================================================
    public void UpdateTeamWins()
    {
        _teamWinsDisplay.text = $"Player wins: {_playerWins} Enemy wins: {_enemyWins}";
    }

    public void DebugAllFullQTables()
    {
        // Iterujemy po kluczach (nazwach ras) w QTables
        foreach (string raceName in QTables.Keys)
        {
            DebugFullQTable(raceName);
        }
    }

    public void DebugAllBestActions()
    {
        foreach (string raceName in QTables.Keys)
        {
            DebugBestActionPerState(raceName);
        }
    }

    public void DebugAllActionUsageCount()
    {
        foreach (string raceName in ActionUsageCount.Keys)
        {
            DebugActionUsageCount(raceName);
        }
    }

    public void DebugFullQTable(string raceName)
    {
        if (!QTables.ContainsKey(raceName))
        {
            Debug.LogWarning($"Brak Q-tabeli dla rasy {raceName}.");
            return;
        }

        float[,] qTable = QTables[raceName];
        int numStates = qTable.GetLength(0);
        int numActions = qTable.GetLength(1);

        Debug.Log($"=== Q-table for race '{raceName}' ===");
        for (int s = 0; s < numStates; s++)
        {
            // Odczytujemy opis stanu:
            string stateDesc = DescribeState(s);
            // Zbuduj linijkę, np.: "State 4 (Melee=False,Wounded=False,Ranged=True) | A0=0.12 A1=-0.03 ..."
            string line = $"State {s} {stateDesc} | ";
            for (int a = 0; a < numActions; a++)
            {
                float val = qTable[s, a];
                line += $"A{a}={val:F2} ";
            }
            Debug.Log(line);
        }
    }

    public void DebugBestActionPerState(string raceName)
    {
        if (!QTables.ContainsKey(raceName))
        {
            Debug.LogWarning($"Brak Q-tabeli dla rasy {raceName}.");
            return;
        }

        float[,] qTable = QTables[raceName];
        int numStates = qTable.GetLength(0);
        int numActions = qTable.GetLength(1);

        Debug.Log($"=== Best actions for race '{raceName}' ===");
        for (int s = 0; s < numStates; s++)
        {
            float bestVal = float.NegativeInfinity;
            int bestAction = -1;
            for (int a = 0; a < numActions; a++)
            {
                if (qTable[s, a] > bestVal)
                {
                    bestVal = qTable[s, a];
                    bestAction = a;
                }
            }

            // Pomijanie stanów, gdzie najlepsza akcja = 0 i Q = 0
            if (bestAction == 0 && Mathf.Approximately(bestVal, 0f))
            {
                continue;
            }

            // Dołączenie opisu stanu
            string stateDesc = DescribeState(s);
            Debug.Log($"State {s} {stateDesc} => Best Action = {bestAction} (Q={bestVal:F2})");
        }
    }

    public void DebugActionUsageCount(string raceName)
    {
        if (!ActionUsageCount.ContainsKey(raceName))
        {
            Debug.LogWarning($"Brak ActionUsageCount dla rasy {raceName}.");
            return;
        }

        int[,] usage = ActionUsageCount[raceName];
        int numStates = usage.GetLength(0);
        int numActions = usage.GetLength(1);

        Debug.Log($"=== Action usage for race '{raceName}' ===");
        for (int s = 0; s < numStates; s++)
        {
            // Opis stanu:
            string stateDesc = DescribeState(s);
            // Linijka w stylu: "State 4 (Melee=False,Wounded=False,Ranged=True) | A0=12 A1=5 ..."
            string line = $"State {s} {stateDesc} | ";
            
            bool hasActions = false; // Flaga do sprawdzenia, czy są akcje do wyświetlenia

            for (int a = 0; a < numActions; a++)
            {
                if (usage[s, a] > 0)
                {
                    line += $"A{a}={usage[s, a]} ";
                    hasActions = true;
                }
            }

            // Jeśli żadna akcja nie została dodana, pomiń wypisanie tego stanu
            if (hasActions)
            {
                Debug.Log(line.TrimEnd()); // Usunięcie ostatniego spacji
            }
        }
    }

    // ======================================================================
    //                            EKSPORT WYNIKÓW
    // ======================================================================

    public void ExportAllData()
    {
        string folderPath = Application.persistentDataPath;
        ExportAllQToCSV(folderPath);
    }

    public void ExportAllQToCSV(string folderPath)
    {
        // Sprawdzamy, czy folder istnieje
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        foreach (string raceName in QTables.Keys)
        {
            // Tworzymy ścieżkę pliku np. "folderPath/Q_raceName.csv"
            string sanitizedRace = raceName.Replace(" ", "_"); 
            string fileName = $"Q_{sanitizedRace}.csv";
            string filePath = Path.Combine(folderPath, fileName);

            ExportQToCSV(raceName, filePath);
        }
    }

    public void ExportQToCSV(string raceName, string filePath)
    {
        if (!QTables.ContainsKey(raceName)) return;

        float[,] table = QTables[raceName];
        int numStates = table.GetLength(0);
        int numActions = table.GetLength(1);
        int numberOfStates = (int)AIState.COUNT;

        using (StreamWriter sw = new StreamWriter(filePath))
        {
            // Nagłówek: State;IsInMelee;IsHeavilyWounded;...
            List<string> headers = new List<string> { "State" };
            for (int i = 0; i < numberOfStates; i++)
            {
                headers.Add(((AIState)i).ToString());
            }
            headers.Add("Action");
            headers.Add("QValue");
            sw.WriteLine(string.Join(";", headers));

            for (int s = 0; s < numStates; s++)
            {
                // Tworzenie wartości dla kolumn stanów
                List<string> rowValues = new List<string> { s.ToString() };
                for (int i = 0; i < numberOfStates; i++)
                {
                    bool isSet = (s & (1 << i)) != 0;
                    rowValues.Add(isSet.ToString().ToLower()); // true/false w małych literach
                }

                for (int a = 0; a < numActions; a++)
                {
                    float val = table[s, a];
                    List<string> actionValues = new List<string>(rowValues)
                    {
                        a.ToString(),
                        val.ToString("F2") // Formatowanie Q-value do 2 miejsc po przecinku
                    };
                    sw.WriteLine(string.Join(";", actionValues));
                }
            }
        }

        Debug.Log($"Q-values for race '{raceName}' exported to: {filePath}");
    }

    // ======================================================================
    //                        EKSPORT LOGÓW
    // ======================================================================

    private void SaveAverageReward(float averageReward)
    {
        string filePath = Path.Combine(Application.persistentDataPath, "average_rewards.csv");
        bool fileExists = File.Exists(filePath);

        using (StreamWriter sw = new StreamWriter(filePath, append: true))
        {
            if (!fileExists)
            {
                // Nagłówki z separatorem ;
                sw.WriteLine("Epoch;AverageReward");
            }

            int currentEpoch = epochRewards.Count;
            // Dane z separatorem ;
            sw.WriteLine($"{currentEpoch};{averageReward}");
        }

        Debug.Log($"Average reward for epoch {epochRewards.Count} saved to {filePath}");

        // Aktualizacja wykresu
        if (simpleGraph != null)
        {
            simpleGraph.AddValue(averageReward);
        }
    }

    public void ResetLogging()
    {
        epochRewards.Clear();
        currentEpochReward = 0f;
        actionsThisEpoch = 0;

        string filePath = Path.Combine(Application.persistentDataPath, "average_rewards.csv");
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            Debug.Log($"Average rewards log reset. File {filePath} deleted.");
        }

        // Resetowanie wykresu
        if (simpleGraph != null)
        {
            // Implementacja resetowania wykresu w SimpleGraph, jeśli jest taka możliwość
        }
    }
}