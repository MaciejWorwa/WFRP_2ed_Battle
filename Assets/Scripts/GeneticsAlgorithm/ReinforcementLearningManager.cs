using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.IO;
using System;
using UnitStates;

namespace UnitStates
{
    public enum AIState
    {
        IsInMelee,
        IsHeavilyWounded,
        HasRangedWeapon,
        // Dodaj tu kolejne stany
        COUNT // Tyle mamy stanów (3 => 2^3 = 8 kombinacji w wersji bool)
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
    Null,         // Zwykły atak
    Charge,
    Feint,
    Swift,
    Guarded,
    AllOut,

    DefensiveStance, 
    Aim,             
    Reload,          
    FinishTurn       
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

    // Liczba dostępnych akcji
    private const int ACTION_COUNT = 47;

    // Liczba kombinacji stanów (2^(AIState.COUNT))
    private int totalStateCombinations;

    // klucz: nazwa rasy (np. "Human", "Orc", "Elf"), wartość: tablica Q
    private Dictionary<string, float[,]> QTables = new Dictionary<string, float[,]>();

    // Jak często dana akcja została użyta
    private Dictionary<string, int[,]> ActionUsageCount = new Dictionary<string, int[,]>();

    public bool IsLearning;

    // Lista lub zbiór wytrenowanych ras
    private HashSet<string> _trainedRaces = new HashSet<string>();

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
        if (Input.GetKeyDown(KeyCode.R))
        {
            SaveQTables();
        }
    }

    // Metoda do sprawdzania, czy rasa jest wytrenowana
    public bool IsRaceTrained(string race)
    {
        Debug.Log(race + " " + _trainedRaces.Contains(race));
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
    private int ChooseValidActionEpsilonGreedy(ActionContext context)
    {
        float[,] qTable = GetQTable(context.RaceName);
        Unit unit = context.Unit;

         // 1. Sprawdź, czy jednostka ma jeszcze dostępne akcje
        if (RoundsManager.Instance.UnitsWithActionsLeft.ContainsKey(unit) &&
            RoundsManager.Instance.UnitsWithActionsLeft[unit] == 0)
        {
            // Jednostka nie ma więcej akcji, wybierz tylko FinishTurn (ID=45)
            return 46; // Zakładam, że 46 to ID akcji FinishTurn
        }

        List<int> validActions = new List<int>();

        for (int i = 0; i < AllActions.Length; i++)
        {
            ActionDefinition def = AllActions[i];
            TargetType tType = def.targetType;
            AttackType aType = def.attackType;

            // Czy to jest ruch, atak, czy akcja specjalna?
            bool isMove = (aType == AttackType.Move);
            bool isAttack = (aType != AttackType.Move 
                        && aType != AttackType.DefensiveStance
                        && aType != AttackType.Aim
                        && aType != AttackType.Reload
                        && aType != AttackType.FinishTurn);

            // ---- WARUNKI BLOKADY DLA ATAKÓW ----
            if (isAttack)
            {
                // Musi być przeciwnik
                if (!context.OpponentExist) continue;
                // Musi mieć CanAttack
                if (!context.CanAttack) continue;
                // Musi mieć broń wybraną
                if (context.CurrentWeapon == null) continue;
                // Broń musi być naładowana
                if (context.CurrentWeapon.ReloadLeft > 0)
                {
                    continue;
                }

                // Znajdź docelowy Unit:
                Unit potentialTarget = GetTargetByType(context.Info, tType);
                if (potentialTarget == null) continue;

                // Sprawdź, czy mamy Distances
                if (!context.Info.Distances.ContainsKey(potentialTarget))
                {
                    // brak dystansu => zablokuj
                    continue;
                }

                float distance = context.Info.Distances[potentialTarget];
                if (distance > context.CurrentWeapon.AttackRange)
                {
                    // za daleko => zablokuj
                    continue;
                }

                // Wymaganie 2 akcji dla Charge, AllOut, Swift, Guarded
                if (aType == AttackType.Charge || aType == AttackType.AllOut || aType == AttackType.Swift || aType == AttackType.Guarded || aType == AttackType.DefensiveStance)
                {
                    if (RoundsManager.Instance.UnitsWithActionsLeft.ContainsKey(unit) &&
                        RoundsManager.Instance.UnitsWithActionsLeft[unit] < 2)
                    {
                        continue;
                    }
                }

                // hasRanged==true => zablokuj Charge, Feint (przykład)
                if (context.HasRanged && 
                (aType == AttackType.Charge || aType == AttackType.Feint))
                {
                    continue;
                }

                // Zablokowanie SwiftAttack dla jednostek z A < 2
                if (aType == AttackType.Swift)
                {
                    Stats stats = context.Unit.GetComponent<Stats>();
                    if (stats != null && stats.A < 2)
                    {
                        continue;
                    }
                }
            }

            // ---- WARUNKI BLOKADY DLA RUCHU ----
            if (isMove)
            {
                // Musi istnieć odpowiedni target
                if (tType == TargetType.Closest     && !context.OpponentExist)          continue;
                if (tType == TargetType.Furthest    && !context.FurthestUnitExist)      continue;
                if (tType == TargetType.MostInjured && !context.MostInjuredUnitExist)   continue;
                if (tType == TargetType.LeastInjured&& !context.LeastInjuredUnitExist)  continue;
                if (tType == TargetType.Weakest     && !context.WeakestUnitExist)       continue;
                if (tType == TargetType.Strongest   && !context.StrongestUnitExist)     continue;
                if (tType == TargetType.MostAlliesNearby   && !context.TargetWithMostAlliesExist) continue;
            }

            // ---- AKCJE SPECJALNE (DefensiveStance, Aim, Reload, FinishTurn) ----
            // AttackType.DefensiveStance => brak warunków? Chyba można zawsze
            // AttackType.Aim => można zawsze? 
            // AttackType.Reload => jeśli CurrentWeapon != null i jest WeaponIsLoaded => ZABLOKUJ
            if (aType == AttackType.Reload)
            {
                if (context.CurrentWeapon != null && context.WeaponIsLoaded)
                {
                    // Broń już jest naładowana => sensu brak
                    continue;
                }
            }

            if (aType == AttackType.DefensiveStance && RoundsManager.Instance.UnitsWithActionsLeft.ContainsKey(unit) && RoundsManager.Instance.UnitsWithActionsLeft[unit] < 2)
            {
                continue;
            }

            validActions.Add(i);
        }

        // Jeśli brak dozwolonych akcji => FinishTurn (lub inny fallback)
        if (validActions.Count == 0)
        {
            // Zakładam ID=46 to jest FinishTurn w AllActions
            return 46;
        }

        // --- EPSILON-GREEDY WYBÓR ---
        if (UnityEngine.Random.value < Epsilon)
        {
            int rndIdx = UnityEngine.Random.Range(0, validActions.Count);
            return validActions[rndIdx];
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
                else if (val == bestVal)
                {
                    bestActions.Add(actID);
                }
            }

            // Losowy wybór spośród najlepszych akcji
            int bestAction = bestActions[UnityEngine.Random.Range(0, bestActions.Count)];
            return bestAction;
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

        float newQ = oldQ + Alpha * (reward + Gamma * maxQnext - oldQ);
        qTable[oldState, action] = newQ;
    }

    // ======================================================================
    //         LOGIKA STANÓW ORAZ GŁÓWNA METODA SimulateUnit
    // ======================================================================

    public bool[] DetermineStates(Unit unit)
    {
        bool[] states = new bool[(int)AIState.COUNT];

        GameObject closestOpponent = AutoCombatManager.Instance.GetClosestOpponent(unit.gameObject, false);
        if (closestOpponent)
        {
            float distance = CombatManager.Instance.CalculateDistance(unit.gameObject, closestOpponent);
            states[(int)AIState.IsInMelee] = (distance <= 1.5f);
        }

        Stats st = unit.GetComponent<Stats>();
        states[(int)AIState.IsHeavilyWounded] = (st.TempHealth <= 3);

        Inventory inventory = unit.GetComponent<Inventory>();
        bool hasRanged = inventory.EquippedWeapons
            .Where(w => w != null)
            .Any(weapon => weapon.Type.Contains("ranged"));
        states[(int)AIState.HasRangedWeapon] = hasRanged;

        return states;
    }

    // Główna metoda sterująca akcjami jednostki w turze – Q-learning.
    public void SimulateUnit(Unit unit)
    {
        if (unit == null) return;
        Stats stats = unit.GetComponent<Stats>();
        if (stats == null) return;
        if (stats.TempHealth < 0) return;

        bool[] oldStates = DetermineStates(unit);
        int oldStateIndex = EncodeState(oldStates);
        int oldHP = stats.TempHealth;

        Weapon weapon = InventoryManager.Instance.ChooseWeaponToAttack(unit.gameObject);
        bool weaponIsLoaded = (weapon != null && weapon.ReloadLeft == 0);

        bool hasRanged = oldStates[(int)AIState.HasRangedWeapon];
        bool canAttack = unit.CanAttack;

        TargetsInfo info = GatherTargetsInfo(unit);
        bool opponentExist = (info.Closest != null);

        ActionContext ctx = new ActionContext
        {
            Unit = unit,
            RaceName = stats.Race,
            StateIndex = oldStateIndex,
            WeaponIsLoaded = weaponIsLoaded,
            HasRanged = hasRanged,
            IsInMelee = oldStates[(int)AIState.IsInMelee],
            CanAttack = canAttack,

            // Wnioskujemy, że OpponentExist => info.Closest != null
            OpponentExist = (info.Closest != null),
            FurthestUnitExist = (info.Furthest != null),
            MostInjuredUnitExist = (info.MostInjured != null),
            LeastInjuredUnitExist = (info.LeastInjured != null),
            WeakestUnitExist = (info.Weakest != null),
            StrongestUnitExist = (info.Strongest != null),
            TargetWithMostAlliesExist = (info.WithMostAllies != null),

            CurrentWeapon = weapon,
            Info = info
        };

        int chosenAction = ChooseValidActionEpsilonGreedy(ctx);
        ActionUsageCount[ctx.RaceName][oldStateIndex, chosenAction]++;

        float reward = PerformParameterAction(chosenAction, unit, info, oldHP);

        bool[] newStates = DetermineStates(unit);
        int newStateIndex = EncodeState(newStates);
        UpdateQ(ctx.RaceName, oldStateIndex, chosenAction, reward, newStateIndex);

        if (RoundsManager.Instance.UnitsWithActionsLeft.ContainsKey(unit)
            && RoundsManager.Instance.UnitsWithActionsLeft[unit] == 1
            && !unit.IsTurnFinished)
        {
            SimulateUnit(unit);
            unit.IsTurnFinished = true;
        }

        Epsilon -= 0.0001f; // Spowolniony spadek Epsilon
        Epsilon = Mathf.Max(Epsilon, 0.05f); // Minimalna wartość Epsilon
    }

    private float PerformParameterAction(int actionID, Unit unit, TargetsInfo info, int oldHP)
    {
        float reward = 0f;
        // Sprawdzamy definicję:
        if (actionID < 0 || actionID >= AllActions.Length)
        {
            // out of range => FinishTurn (ID=46)
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

        // 2) Specjalne akcje:
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

         // [NOWE: Będziemy sprawdzać, czy zdołaliśmy zabić wroga i dać nagrodę, a jeśli my umarliśmy – karę]
        // Najpierw zapisujemy HP wroga (jeśli jest)
        int oldEnemyHP = 0;
        int enemyOverall = 0;
        if (target != null)
        {
            oldEnemyHP = target.GetComponent<Stats>().TempHealth;
            enemyOverall = target.GetComponent<Stats>().Overall; 
        }

        // 3) Różne typy ataku (zwykły, Charge, Feint, Swift, Guarded, AllOut)
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
            default: return null; // None
        }
    }

    private static readonly ActionDefinition[] AllActions = new ActionDefinition[]
    {
        // 0..6: Ruch do (Closest..Strongest, MostAlliesNearby)
        new ActionDefinition(TargetType.Closest,         AttackType.Move),
        new ActionDefinition(TargetType.Furthest,        AttackType.Move),
        new ActionDefinition(TargetType.MostInjured,     AttackType.Move),
        new ActionDefinition(TargetType.LeastInjured,    AttackType.Move),
        new ActionDefinition(TargetType.Weakest,         AttackType.Move),
        new ActionDefinition(TargetType.Strongest,       AttackType.Move),
        new ActionDefinition(TargetType.MostAlliesNearby,AttackType.Move),

        // 7..12: Zwykły atak (Null) na (Closest..Strongest)
        new ActionDefinition(TargetType.Closest,     AttackType.Null),
        new ActionDefinition(TargetType.Furthest,    AttackType.Null),
        new ActionDefinition(TargetType.MostInjured, AttackType.Null),
        new ActionDefinition(TargetType.LeastInjured,AttackType.Null),
        new ActionDefinition(TargetType.Weakest,     AttackType.Null),
        new ActionDefinition(TargetType.Strongest,   AttackType.Null),

        // 13..18: Charge
        new ActionDefinition(TargetType.Closest,     AttackType.Charge),
        new ActionDefinition(TargetType.Furthest,    AttackType.Charge),
        new ActionDefinition(TargetType.MostInjured, AttackType.Charge),
        new ActionDefinition(TargetType.LeastInjured,AttackType.Charge),
        new ActionDefinition(TargetType.Weakest,     AttackType.Charge),
        new ActionDefinition(TargetType.Strongest,   AttackType.Charge),

        // 19..24: Feint
        new ActionDefinition(TargetType.Closest,     AttackType.Feint),
        new ActionDefinition(TargetType.Furthest,    AttackType.Feint),
        new ActionDefinition(TargetType.MostInjured, AttackType.Feint),
        new ActionDefinition(TargetType.LeastInjured,AttackType.Feint),
        new ActionDefinition(TargetType.Weakest,     AttackType.Feint),
        new ActionDefinition(TargetType.Strongest,   AttackType.Feint),

        // 25..30: Swift
        new ActionDefinition(TargetType.Closest,     AttackType.Swift),
        new ActionDefinition(TargetType.Furthest,    AttackType.Swift),
        new ActionDefinition(TargetType.MostInjured, AttackType.Swift),
        new ActionDefinition(TargetType.LeastInjured,AttackType.Swift),
        new ActionDefinition(TargetType.Weakest,     AttackType.Swift),
        new ActionDefinition(TargetType.Strongest,   AttackType.Swift),

        // 31..36: Guarded
        new ActionDefinition(TargetType.Closest,     AttackType.Guarded),
        new ActionDefinition(TargetType.Furthest,    AttackType.Guarded),
        new ActionDefinition(TargetType.MostInjured, AttackType.Guarded),
        new ActionDefinition(TargetType.LeastInjured,AttackType.Guarded),
        new ActionDefinition(TargetType.Weakest,     AttackType.Guarded),
        new ActionDefinition(TargetType.Strongest,   AttackType.Guarded),

        // 37..42: AllOut
        new ActionDefinition(TargetType.Closest,     AttackType.AllOut),
        new ActionDefinition(TargetType.Furthest,    AttackType.AllOut),
        new ActionDefinition(TargetType.MostInjured, AttackType.AllOut),
        new ActionDefinition(TargetType.LeastInjured,AttackType.AllOut),
        new ActionDefinition(TargetType.Weakest,     AttackType.AllOut),
        new ActionDefinition(TargetType.Strongest,   AttackType.AllOut),

        // 43: DefensiveStance (targetType.None)
        new ActionDefinition(TargetType.None, AttackType.DefensiveStance),

        // 44: Aim
        new ActionDefinition(TargetType.None, AttackType.Aim),

        // 45: Reload
        new ActionDefinition(TargetType.None, AttackType.Reload),

        // 46: FinishTurn
        new ActionDefinition(TargetType.None, AttackType.FinishTurn),
    };

    // ======================================================================
    //                      POMOCNICZE METODY
    // ======================================================================

    private void MoveTowards(Unit unit, GameObject opponent)
    {
        GameObject tile = CombatManager.Instance.GetTileAdjacentToTarget(unit.gameObject, opponent);
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

                Debug.Log("ATTACKER " +attacker.GetComponent<Stats>().Name);

                CombatManager.Instance.Attack(attacker.GetComponent<Unit>(), target.GetComponent<Unit>(), false);
            }
        }
        else
        {
            CombatManager.Instance.Attack(attacker.GetComponent<Unit>(), target.GetComponent<Unit>(), false);
        }

        int newHP = target.GetComponent<Stats>().TempHealth;

        return oldHP - newHP; // Nagroda równa różnicy w HP przeciwnika
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
            reward -= 20;
            reward += RoundsManager.RoundNumber; //Nagroda za przeżycie jak najdłużej
        }

        return reward;
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

    private void CountAdjacentUnits(Vector2 center, string allyTag, string opponentTag, ref int allies, ref int opponents)
    {
        HashSet<Collider2D> countedOpponents = new HashSet<Collider2D>();
        Vector2[] positions =
        {
            center,
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
                if (qTable[s,a] > bestVal)
                {
                    bestVal = qTable[s,a];
                    bestAction = a;
                }
            }
            // Dołącz opis stanu:
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
            for (int a = 0; a < numActions; a++)
            {
                line += $"A{a}={usage[s,a]} ";
            }
            Debug.Log(line);
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

        using (StreamWriter sw = new StreamWriter(filePath))
        {
            // Nagłówek
            sw.WriteLine("State,StateDescription,Action,QValue");

            for (int s = 0; s < numStates; s++)
            {
                string stateDesc = DescribeState(s);
                for (int a = 0; a < numActions; a++)
                {
                    float val = table[s,a];
                    // Dodajemy stateDesc w drugiej kolumnie
                    sw.WriteLine($"{s},{stateDesc},{a},{val}");
                }
            }
        }
        Debug.Log($"Q-values for race '{raceName}' exported to: {filePath}");
    }

}
