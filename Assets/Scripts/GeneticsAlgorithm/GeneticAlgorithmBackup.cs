// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;
// using System.Linq;
// using System.IO;

// public class GeneticAlgorithmBackup : MonoBehaviour
// {
//     // Prywatne statyczne pole przechowujące instancję
//     private static GeneticAlgorithmBackup instance;

//     // Publiczny dostęp do instancji
//     public static GeneticAlgorithmBackup Instance
//     {
//         get { return instance; }
//     }

//     void Awake()
//     {
//         if (instance == null)
//         {
//             instance = this;
//             //DontDestroyOnLoad(gameObject);
//         }
//         else if (instance != this)
//         {
//             Destroy(gameObject);
//         }
//     }

//     public bool IsManualMode = true;  // Tryb ręczny
//     public int PopulationSize = 0;

//     public int MaxRounds = 30;
//     public float MutationRate = 0.1f;
//     public int TournamentSize = 4; // Rozmiar turnieju do selekcji turniejowej
//     //public int MaxGenerations = 100; // Maksymalna liczba generacji dla adaptacyjnej mutacji

//     public List<UnitGenome> Population = new List<UnitGenome>();
//     //private bool isRunning = true;

//     public static int GenerationNumber = 1;  // Zmienna śledząca numer generacji

//     [SerializeField] private FitnessDisplayManager _fitnessDisplayManager;

//     void Start()
//     {
//         _fitnessDisplayManager.ResetFitnessData(); // Resetujemy dane fitnessu na początku
//         StartCoroutine(ContinuousEvolution());
//     }

//     void Update()
//     {
//         if (Input.GetKeyDown(KeyCode.M)) // Przykładowo klawisz M przełącza tryby
//         {
//             ToggleMode();
//         }
//         if (Input.GetKeyDown(KeyCode.N)) // Przykładowo klawisz N przełącza tryby
//         {
//             ExecuteOneMove();
//         }
//     }

//     public void ExecuteOneMove()
//     {
//         if (IsManualMode)
//         {
//             // Wywołaj symulację dla jednej jednostki
//             if (RoundsManager.RoundNumber < MaxRounds)
//             {
//                 foreach (Unit unit in UnitsManager.Instance.AllUnits.ToList())
//                 {
//                     SimulateUnit(unit);
//                 }

//                 RoundsManager.Instance.NextRound();
//             }
//             else
//             {
//                 Debug.Log("All rounds for this generation are finished.");
//             }
//         }
//     }

//     public void ToggleMode()
//     {
//         IsManualMode = !IsManualMode;
//         Debug.Log(IsManualMode ? "Switched to Manual Mode" : "Switched to Automatic Mode");
//     }

//     IEnumerator ContinuousEvolution()
//     {
//         yield return new WaitForSeconds(1f);

//         if (!IsManualMode) // Jeśli nie jesteśmy w trybie ręcznym
//         {
//             while (true)
//             {
//                 string filePath = Path.Combine(Application.persistentDataPath, "AI_data.json");
//                 if (File.Exists(filePath))
//                 {
//                     LoadBestUnitsDNA();
//                 }
//                 else
//                 {
//                     GenerateInitialPopulation();
//                 }

//                 yield return StartCoroutine(RunEvolution());

//                 // Zapis wyników po zakończeniu generacji
//                 DisplayBestUnits();
//                 SaveBestUnitsDNA();

//                 yield return new WaitForSeconds(42f);

//                 // Usuwamy wszystkie jednostki
//                 ClearUnits();

//                 // Resetowanie stanu dla nowej symulacji
//                 RoundsManager.RoundNumber = 0;
//                 Population.Clear();

//                 // Tworzenie nowej populacji po usunięciu poprzednich jednostek
//                 GenerateInitialPopulation();
//             }
//         }
//         else
//         {
//             string filePath = Path.Combine(Application.persistentDataPath, "AI_data.json");
//             if (File.Exists(filePath))
//             {
//                 LoadBestUnitsDNA();
//             }
//             else
//             {
//                 GenerateInitialPopulation();
//             }

//             yield return null; // W trybie manualnym nie kontynuujemy pętli
//         }
//     }

//     private void ClearUnits()
//     {
//         // Usunięcie wszystkich jednostek
//         for (int i = UnitsManager.Instance.AllUnits.Count - 1; i >= 0; i--)
//         {
//             Unit unit = UnitsManager.Instance.AllUnits[i];
//             UnitsManager.Instance.DestroyUnit(unit.gameObject);
//         }
//     }

//     void GenerateInitialPopulation()
//     {
//         if (Population.Count == 0)
//         {
//             //Jeśli wielkość populacji nie jest zdefiniowana to ustawiamy losową wielkość
//             if(PopulationSize == 0)
//             {
//                 PopulationSize = Random.Range(4, 21);
//             }

//             for (int i = 0; i < PopulationSize; i++)
//             {
//                 UnitGenome newGenome = new UnitGenome();
//                 string unitName = "Unit " + i + " (Gen " + GenerationNumber + ")";
//                 newGenome.RandomizeGenes(unitName, GenerationNumber);
//                 Population.Add(newGenome);
            
//                 List<Vector2> availablePositions = GridManager.Instance.AvailablePositions();
//                 Vector2 position = Vector2.zero;

//                 if (!SaveAndLoadManager.Instance.IsLoading)
//                 {
//                     if (availablePositions.Count == 0)
//                     {
//                         Debug.Log("Nie można stworzyć nowej jednostki. Brak wolnych pól.");
//                         return;
//                     }

//                     // Wybranie losowej pozycji z dostępnych
//                     int randomIndex = UnityEngine.Random.Range(0, availablePositions.Count);
//                     position = availablePositions[randomIndex];
//                 }

//                 SaveAndLoadManager.Instance.IsLoading = true;

//                 GameObject unitObject = UnitsManager.Instance.CreateUnit(1, Population[i].unitName, position);

//                 SaveAndLoadManager.Instance.IsLoading = false;

//                 unitObject.name = unitObject.GetComponent<Stats>().Name;
 
//                 unitObject.GetComponent<Unit>().genome = Population[i];
//             }
//         }
//     }

//     // Algorytm genetyczny
//     IEnumerator RunEvolution()
//     {
//         while (RoundsManager.RoundNumber < MaxRounds)// && isRunning)
//         {
//             //StartCoroutine(RoundsManager.Instance.AutoCombat());

//             RoundsManager.Instance.NextRoundButton.gameObject.SetActive(false);
//             //_useFortunePointsButton.SetActive(false);

//             for(int i=0; i < UnitsManager.Instance.AllUnits.Count; i++)
//             {
//                 if (UnitsManager.Instance.AllUnits[i] == null) continue;

//                 InitiativeQueueManager.Instance.SelectUnitByQueue();

//                 yield return new WaitForSeconds(0.1f);

//                 SimulateUnit(Unit.SelectedUnit.GetComponent<Unit>());
//                 //AutoCombatManager.Instance.Act(Unit.SelectedUnit.GetComponent<Unit>());

//                 // Czeka, aż postać skończy ruch, zanim wybierze kolejną postać
//                 yield return new WaitUntil(() => MovementManager.Instance.IsMoving == false);
//                 //yield return new WaitUntil(() => UnitsWithActionsLeft[Unit.SelectedUnit.GetComponent<Unit>()] == 0 || Unit.SelectedUnit.GetComponent<Unit>().IsTurnFinished);
//                 yield return new WaitForSeconds(0.5f);
//             }

//             RoundsManager.Instance.NextRoundButton.gameObject.SetActive(true);
//             //_useFortunePointsButton.SetActive(true);
            

//             // Symulacja każdej jednostki w rundzie
//             // foreach (Unit unit in allUnits.ToList())
//             // {
//             //     SimulateUnit(unit);

//             //     yield return new WaitForSeconds(0.1f);
//             // }

//             //yield return new WaitForSeconds(1.7f);

//             // Ocena fitness każdej jednostki po każdej rundzie
//             foreach (Unit unit in UnitsManager.Instance.AllUnits)
//             {
//                 float fitness = CalculateFitness(unit);
//                 unit.genome.Fitness = fitness;

//                 // Dodajemy fitness do FitnessDisplayManager
//                 _fitnessDisplayManager.AddFitness(fitness);
//             }

//             // Po zakończeniu wszystkich ustalonych rund przeprowadzamy ewolucję
//             if (RoundsManager.RoundNumber == MaxRounds)
//             {
//                 EvolvePopulation();
//             }

//             RoundsManager.Instance.NextRound();
//         }
//     }

//     void DisplayBestUnits()
//     {
//         Population = Population.OrderByDescending(g => g.Fitness).ToList();

//         Debug.Log("Najlepsze jednostki po zakończeniu ewolucji:");

//         // Wyświetlanie pięciu najlepszych jednostek
//         for (int i = 0; i < 6 && i < Population.Count; i++)
//         {
//             UnitGenome bestUnit = Population[i];
//             string genesString = string.Join(",", bestUnit.genes.Select(g => g.ToString()).ToArray());
//             Debug.Log($"<color=cyan>Jednostka {bestUnit.unitName}: Fitness = {bestUnit.Fitness}, Geny = {genesString}</color>");
//         }

//         // Aktualizowanie licznika genów dla najlepszej jednostki
//         if (Population.Count > 0)
//         {
//             UnitGenome topUnit = Population[0]; // Tylko najlepsza jednostka
//             _fitnessDisplayManager.UpdateGeneCount(topUnit.genes); // Zaktualizuj liczniki genów tylko dla najlepszej jednostki
//         }
//     }

//     // Symulacja ruchu i akcji dla każdej jednostki
//     void SimulateUnit(Unit unit)
//     {
//         Weapon weapon = InventoryManager.Instance.ChooseWeaponToAttack(unit.gameObject);

//         // Znajdź najbliższy cel o przeciwnym tagu
//         GameObject closestOpponent = GetClosestOpponent(unit.gameObject, false);
//         Unit closestUnit = closestOpponent.GetComponent<Unit>();
//         Unit mostInjuredUnit = FindMostInjuredUnit(unit);
//         Unit leastInjuredUnit = FindLeastInjuredUnit(unit);
//         Unit furthestUnit = FindFurthestUnit(unit);
//         Unit targetWithMostAllies = FindTargetWithMostAlliesNearby(unit);
//         Unit strongestUnit = FindUnitWithHighestOverall(unit);

//         if (closestOpponent != null)
//         {
//             int decision = unit.genome.GetDecision();
//             float distance = 0;

//             switch (decision)
//             {
//                 case 0:
//                     // Ruch w stronę najbliższego przeciwnika
//                     AttemptToChangeDistanceAndAttack(unit, closestOpponent, weapon);
//                     break;
//                 case 1:
//                     // Ucieczka od najbliższego przeciwnika
//                     MoveTowards(unit, closestUnit, true);
//                     break;
//                 case 2:
//                     // Ruch w stronę najbardziej zranionej jednostki przeciwnika
//                     if (mostInjuredUnit != null)
//                     {
//                         AttemptToChangeDistanceAndAttack(unit, mostInjuredUnit.gameObject, weapon);
//                     }
//                     break;
//                 case 3:
//                     // Ruch w stronę najmniej zranionej jednostki przeciwnika
//                     if (leastInjuredUnit != null)
//                     {
//                         AttemptToChangeDistanceAndAttack(unit, leastInjuredUnit.gameObject, weapon);
//                     }
//                     break;
//                 case 4:
//                     // Zaatakowanie najbliższego przeciwnika
//                     distance = Vector2.Distance(closestOpponent.transform.position, unit.transform.position);
//                     if (unit.CanAttack == true && (distance <= weapon.AttackRange || distance <= weapon.AttackRange * 2 && weapon.Type.Contains("ranged") && !weapon.Type.Contains("short-range-only")))
//                     {
//                         CombatManager.Instance.Attack(unit, closestUnit, false);  
//                     }
//                     break;
//                 case 5:
//                     // Atak dystansowy na najdalszego przeciwnika
//                     if (furthestUnit != null)
//                     {
//                         distance = Vector2.Distance(furthestUnit.transform.position, unit.transform.position);
//                         if (unit.CanAttack == true && (distance <= weapon.AttackRange || distance <= weapon.AttackRange * 2 && weapon.Type.Contains("ranged") && !weapon.Type.Contains("short-range-only")))
//                         {
//                             CombatManager.Instance.Attack(unit, closestUnit, false);  
//                         }
//                     }
//                     break;
//                 case 6:
//                     // Atak dystansowy na najbardziej zranionego przeciwnika
//                     if (mostInjuredUnit != null)
//                     {
//                         CombatManager.Instance.Attack(unit, mostInjuredUnit, false);  
//                     }
//                     break;
//                 case 7:
//                     // Zajęcie pozycji obronnej
//                     CombatManager.Instance.DefensiveStance();
//                     break;
//                 case 8:
//                     // Przeładowanie broni
//                     CombatManager.Instance.Reload();
//                     break;
//                 case 9:
//                     // Ustawienie celowania
//                     CombatManager.Instance.SetAim();
//                     break;
//                 case 10:
//                     // Ruch w stronę przeciwnika, przy którym jest najwięcej sojuszników
//                     if (targetWithMostAllies != null)
//                     {
//                         AttemptToChangeDistanceAndAttack(unit, targetWithMostAllies.gameObject, weapon);
//                     }
//                     break;
//             }
//         }
//     }
//     public GameObject GetClosestOpponent(GameObject attacker, bool closestOpponentInDistanceRangeNeeded)
//     {
//         GameObject closestOpponent = null;
//         float minDistance = Mathf.Infinity;

//         foreach (Unit unit in UnitsManager.Instance.AllUnits)
//         {
//             if (unit.gameObject == attacker || unit.CompareTag(attacker.tag) == true) continue;

//             float distance = Vector2.Distance(unit.transform.position, attacker.transform.position);

//             if (closestOpponentInDistanceRangeNeeded == true && distance < 1.5f) continue;

//             if (distance < minDistance)
//             {
//                 closestOpponent = unit.gameObject;
//                 minDistance = distance;
//             }
//         }

//         return closestOpponent;
//     }

//     private void AttemptToChangeDistanceAndAttack(Unit unit, GameObject closestOpponent, Weapon weapon)
//     {
//         // Szuka wolnej pozycji obok celu, do której droga postaci jest najkrótsza
//         GameObject targetTile = CombatManager.Instance.GetTileAdjacentToTarget(unit.gameObject, closestOpponent);
//         Vector2 targetTilePosition = Vector2.zero;

//         if (targetTile != null)
//         {
//             targetTilePosition = new Vector2(targetTile.transform.position.x, targetTile.transform.position.y);
//         }
//         else
//         {
//             Debug.Log($"{unit.GetComponent<Stats>().Name} nie jest w stanie podejść do {closestOpponent.GetComponent<Stats>().Name}.");
//             //Kończy turę, żeby zostawić sobie akcję na parowanie
//             RoundsManager.Instance.FinishTurn();

//             return;
//         }

//         //Ścieżka ruchu atakującego
//         List<Vector2> path = MovementManager.Instance.FindPath(unit.transform.position, targetTilePosition); 

//         if ((!weapon.Type.Contains("ranged")) && unit.CanAttack == true && path.Count <= unit.GetComponent<Stats>().Sz * 2 && path.Count >= 3 && RoundsManager.Instance.UnitsWithActionsLeft[unit] == 2) // Jeśli rywal jest w zasięgu szarży to wykonuje szarżę
//         {
//             Debug.Log($"{unit.GetComponent<Stats>().Name} szarżuje na {closestOpponent.GetComponent<Stats>().Name}.");

//             MovementManager.Instance.UpdateMovementRange(2);
//             CombatManager.Instance.ChangeAttackType("Charge");

//             CombatManager.Instance.Attack(unit, closestOpponent.GetComponent<Unit>(), false);
//         }
//         else if (path.Count < 3 && path.Count > 0 && unit.CanAttack == true && RoundsManager.Instance.UnitsWithActionsLeft[unit] == 2) //Wykonuje ruch w kierunku przeciwnika, a następnie atak
//         {
//             // // Uruchomia korutynę odpowiedzialną za ruch i atak
//             // StartCoroutine(MoveAndAttack(unit, targetTile, closestOpponent.GetComponent<Unit>(), weapon));
//         }
//         else if (path.Count > 0) //Wykonuje ruch w kierunku przeciwnika
//         {
//             if (RoundsManager.Instance.UnitsWithActionsLeft[unit] == 2) //Wykonuje bieg
//             {
//                 MovementManager.Instance.UpdateMovementRange(3);
//                 unit.IsRunning = true;

//                 Debug.Log($"{unit.GetComponent<Stats>().Name} biegnie w stronę {closestOpponent.GetComponent<Stats>().Name}.");
//             }
//             else
//             {
//                 unit.IsRunning = false;
//                 Debug.Log($"{unit.GetComponent<Stats>().Name} idzie w stronę {closestOpponent.GetComponent<Stats>().Name}.");
//             }

//             MovementManager.Instance.MoveSelectedUnit(targetTile, unit.gameObject);
//         }
//         else //Gdy nie jest w stanie podejść do najbliższego przeciwnika, a stoi on poza zasięgiem jego ataku
//         {
//             RoundsManager.Instance.FinishTurn();
//         }

//         // Synchronizuje collidery
//         Physics2D.SyncTransforms();
//     }

//     // Ruch w kierunku przeciwnika
//     void MoveTowards(Unit unit, Unit targetUnit, bool moveAway)
//     {
//         Vector2 targetPos = targetUnit.transform.position;

//         Vector2[] adjacentPositions = {
//             targetPos + Vector2.right,
//             targetPos + Vector2.left,
//             targetPos + Vector2.up,
//             targetPos + Vector2.down,
//             targetPos + new Vector2(1, 1),
//             targetPos + new Vector2(-1, 1),
//             targetPos + new Vector2(1, -1),
//             targetPos + new Vector2(-1, -1)
//         };

//         GameObject bestTile = null;
//         int shortestPathLength = int.MaxValue;

//         Vector2[] validMovePositions = {
//             targetPos + Vector2.right,
//             targetPos + Vector2.left,
//             targetPos + Vector2.up,
//             targetPos + Vector2.down
//         };

//         foreach (Vector2 pos in validMovePositions)
//         {
//             GameObject tile = GameObject.Find($"Tile {pos.x - GridManager.Instance.transform.position.x} {pos.y - GridManager.Instance.transform.position.y}");

//             if (tile == null || tile.GetComponent<Tile>().IsOccupied) continue;

//             List<Vector2> path = MovementManager.Instance.FindPath(unit.transform.position, pos);

//             if (path.Count == 0) continue;

//             if (path.Count < shortestPathLength)
//             {
//                 shortestPathLength = path.Count;
//                 bestTile = tile;
//             }
//         }

//         if (bestTile != null && shortestPathLength <= unit.GetComponent<Stats>().TempSz)
//         {
//             Vector2 bestTilePosition = new Vector2(bestTile.transform.position.x, bestTile.transform.position.y);
//             List<Vector2> path = MovementManager.Instance.FindPath(unit.transform.position, bestTilePosition);

//             if (path.Count > 0)
//             {
//                 unit.transform.position = path[0];
//             }
//         }
//         else
//         {
//             Vector2 direction = targetPos - (Vector2)unit.transform.position;
//             direction = moveAway ? -direction : direction;

//             Vector2 newPosition = (Vector2)unit.transform.position + direction.normalized * unit.GetComponent<Stats>().TempSz;
//             newPosition = new Vector2(Mathf.Round(newPosition.x), Mathf.Round(newPosition.y));

//             List<Vector2> path = MovementManager.Instance.FindPath(unit.transform.position, newPosition);

//             if (path.Count > 0)
//             {
//                 unit.transform.position = path[0];
//             }
//         }
//     }

//     // Znajduje najbardziej zranioną jednostkę przeciwnika
//     Unit FindMostInjuredUnit(Unit currentUnit)
//     {
//         Unit mostInjuredUnit = null;
//         float minHealth = float.MaxValue;

//         foreach (Unit otherUnit in UnitsManager.Instance.AllUnits)
//         {
//             // Szuka jednostki o przeciwnym tagu (Enemy vs Player)
//             if (otherUnit != currentUnit && otherUnit.tag != currentUnit.tag && otherUnit.GetComponent<Stats>().TempHealth > 0)
//             {
//                 if (otherUnit.GetComponent<Stats>().TempHealth < minHealth)
//                 {
//                     minHealth = otherUnit.GetComponent<Stats>().TempHealth;
//                     mostInjuredUnit = otherUnit;
//                 }
//             }
//         }

//         return mostInjuredUnit;
//     }

//     // Znajduje najmniej zranioną jednostkę przeciwnika
//     Unit FindLeastInjuredUnit(Unit currentUnit)
//     {
//         Unit leastInjuredUnit = null;
//         float maxHealth = 0;

//         foreach (Unit otherUnit in UnitsManager.Instance.AllUnits)
//         {
//             // Szuka jednostki o przeciwnym tagu (Enemy vs Player)
//             if (otherUnit != currentUnit && otherUnit.tag != currentUnit.tag && otherUnit.GetComponent<Stats>().TempHealth > 0)
//             {
//                 // Porównuje zdrowie jednostek, aby znaleźć tę z największą liczbą punktów zdrowia
//                 if (otherUnit.GetComponent<Stats>().TempHealth > maxHealth)
//                 {
//                     maxHealth = otherUnit.GetComponent<Stats>().TempHealth;
//                     leastInjuredUnit = otherUnit;
//                 }
//             }
//         }

//         return leastInjuredUnit;
//     }

//     // Znajduje najdalszą jednostkę przeciwnika
//     Unit FindFurthestUnit(Unit currentUnit)
//     {
//         Unit furthestUnit = null;
//         float maxDistance = 0;

//         foreach (Unit otherUnit in UnitsManager.Instance.AllUnits)
//         {
//             // Szuka jednostki o przeciwnym tagu (Enemy vs Player)
//             if (otherUnit != currentUnit && otherUnit.tag != currentUnit.tag && otherUnit.GetComponent<Stats>().TempHealth > 0)
//             {
//                 float distance = Vector2.Distance(currentUnit.transform.position, otherUnit.transform.position);
//                 // Porównuje odległości między jednostkami, aby znaleźć najdalszą
//                 if (distance > maxDistance)
//                 {
//                     maxDistance = distance;
//                     furthestUnit = otherUnit;
//                 }
//             }
//         }

//         return furthestUnit;
//     }

//     Unit FindTargetWithMostAlliesNearby(Unit attacker)
//     {
//         Unit bestTarget = null;
//         int maxAllies = 0;

//         foreach (Unit potentialTarget in UnitsManager.Instance.AllUnits)
//         {
//             // Sprawdź, czy potencjalny cel jest przeciwnikiem
//             if (potentialTarget.tag != attacker.tag && potentialTarget.GetComponent<Stats>().TempHealth > 0)
//             {
//                 int alliesCount = CountAdjacentAllies(potentialTarget, attacker);

//                 if (alliesCount > maxAllies)
//                 {
//                     maxAllies = alliesCount;
//                     bestTarget = potentialTarget;
//                 }
//             }
//         }

//         return bestTarget;
//     }

//     Unit FindUnitWithHighestOverall(Unit currentUnit)
//     {
//         Unit bestTarget = null;
//         int bestOverall = 0;

//         foreach (Unit potentialTarget in UnitsManager.Instance.AllUnits)
//         {
//             if (potentialTarget != currentUnit)
//             {
//                 int overall = potentialTarget.GetComponent<Stats>().Overall;
//                 if (overall > bestOverall)
//                 {
//                     bestOverall = overall;
//                     bestTarget = potentialTarget;
//                 }
//             }
//         }

//         return bestTarget;
//     }

//     int CountAdjacentAllies(Unit target, Unit attacker)
//     {
//         int adjacentAllies = 0;
//         Vector2 targetPos = target.transform.position;

//         // Wszystkie przylegające pozycje do atakowanego
//         Vector2[] positions = {
//         targetPos + Vector2.right,
//         targetPos + Vector2.left,
//         targetPos + Vector2.up,
//         targetPos + Vector2.down,
//         targetPos + new Vector2(1, 1),
//         targetPos + new Vector2(-1, -1),
//         targetPos + new Vector2(-1, 1),
//         targetPos + new Vector2(1, -1)
//     };

//         foreach (var pos in positions)
//         {
//             Collider2D collider = Physics2D.OverlapPoint(pos);

//             // Sprawdź, czy są sojusznicy w pobliżu celu
//             if (collider != null && collider.CompareTag(attacker.tag))
//             {
//                 adjacentAllies++;
//             }
//         }

//         return adjacentAllies;
//     }

//     // Obliczanie fitness jednostki
//     float CalculateFitness(Unit unit)
//     {
//         int damageDealt = unit.genome.DamageDealt;
//         int currentHealth = unit.GetComponent<Stats>().TempHealth;

//         // Bazowy fitness to zdrowie + zadane obrażenia
//         float fitness = currentHealth + damageDealt;

//         // Jeżeli jednostka umarła, obniżamy jej fitness o 10
//         if (unit.GetComponent<Stats>().TempHealth <= 0)
//         {
//             fitness -= 10;  // Kara za śmierć
//         }

//         return fitness;
//     }


//     // Ewolucja populacji
//     void EvolvePopulation()
//     {
//         List<UnitGenome> newPopulation = new List<UnitGenome>();

//         // Selekcja turniejowa
//         List<UnitGenome> selectedUnits = new List<UnitGenome>();
//         for (int i = 0; i < PopulationSize; i++)
//         {
//             selectedUnits.Add(TournamentSelection());
//         }

//         // Elitarność: zachowaj najlepsze jednostki
//         List<UnitGenome> bestUnits = Population.Take(2).ToList();
//         newPopulation.AddRange(bestUnits);

//         // Krzyżowanie i mutacja reszty populacji
//         while (newPopulation.Count < PopulationSize)
//         {
//             UnitGenome parent1 = selectedUnits[Random.Range(0, selectedUnits.Count)];
//             UnitGenome parent2 = selectedUnits[Random.Range(0, selectedUnits.Count)];

//             UnitGenome offspring = parent1.Crossover(parent2);
//             offspring.Mutate(MutationRate); // Adaptacyjna mutacja

//             newPopulation.Add(offspring);
//         }

//         Population = newPopulation;

//         ClearUnits();

//         for (int i = 0; i < Population.Count; i++)
//         {
//             List<Vector2> availablePositions = GridManager.Instance.AvailablePositions();
//             Vector2 position = Vector2.zero;

//             if (!SaveAndLoadManager.Instance.IsLoading)
//             {
//                 if (availablePositions.Count == 0)
//                 {
//                     Debug.Log("Nie można stworzyć nowej jednostki. Brak wolnych pól.");
//                     return;
//                 }

//                 // Wybranie losowej pozycji z dostępnych
//                 int randomIndex = UnityEngine.Random.Range(0, availablePositions.Count);
//                 position = availablePositions[randomIndex];
//             }
   
//             SaveAndLoadManager.Instance.IsLoading = true;

//             GameObject unitObject = UnitsManager.Instance.CreateUnit(1, Population[i].unitName, position);

//             SaveAndLoadManager.Instance.IsLoading = false;

//             unitObject.name = unitObject.GetComponent<Stats>().Name;

//             unitObject.GetComponent<Unit>().genome = Population[i];
//         }

//         GenerationNumber++;
//     }

//     // Selekcja turniejowa
//     UnitGenome TournamentSelection()
//     {
//         List<UnitGenome> tournament = new List<UnitGenome>();
//         for (int i = 0; i < TournamentSize; i++)
//         {
//             UnitGenome randomUnit = Population[Random.Range(0, Population.Count)];
//             tournament.Add(randomUnit);
//         }
//         return tournament.OrderByDescending(u => u.Fitness).First();
//     }

//     // Zapis najlepszych jednostek do pliku
//     void SaveBestUnitsDNA()
//     {
//         List<UnitGenome> bestUnits = Population.OrderByDescending(g => g.Fitness).Take(5).ToList();

//         List<UnitGenomeData> bestUnitsData = new List<UnitGenomeData>();
//         foreach (UnitGenome unit in bestUnits)
//         {
//             bestUnitsData.Add(new UnitGenomeData
//             {
//                 genes = unit.genes,
//                 fitness = unit.Fitness,
//                 unitName = unit.unitName,
//                 populationNumber = unit.populationNumber  // Zapisujemy numer generacji
//             });
//         }

//         UnitGenomesContainer container = new UnitGenomesContainer
//         {
//             genomes = bestUnitsData,
//             GenerationNumber = GenerationNumber // Zapisujemy numer generacji
//         };

//         string json = JsonUtility.ToJson(container);
//         string filePath = Path.Combine(Application.persistentDataPath, "AI_data.json");
//         File.WriteAllText(filePath, json);
//         Debug.Log($"DNA najlepszych jednostek zostao zapisane do pliku: {filePath}");
//     }

//     // Wczytanie DNA najlepszych jednostek
//     void LoadBestUnitsDNA()
//     {
//         string filePath = Path.Combine(Application.persistentDataPath, "AI_data.json");;
//         if (File.Exists(filePath))
//         {
//             string json = File.ReadAllText(filePath);
//             UnitGenomesContainer container = JsonUtility.FromJson<UnitGenomesContainer>(json);

//             Population.Clear();
//             UnitsManager.Instance.AllUnits.ForEach(unit => Destroy(unit.gameObject));
//             ClearUnits();

//             for (int i = 0; i < container.genomes.Count; i++)
//             {
//                 UnitGenomeData data = container.genomes[i];
//                 UnitGenome genome = new UnitGenome
//                 {
//                     genes = data.genes,
//                     Fitness = data.fitness,
//                     unitName = data.unitName,
//                     populationNumber = data.populationNumber
//                 };

//                 Population.Add(genome);

//                 List<Vector2> availablePositions = GridManager.Instance.AvailablePositions();
//                 Vector2 position = Vector2.zero;

//                 if (!SaveAndLoadManager.Instance.IsLoading)
//                 {
//                     if (availablePositions.Count == 0)
//                     {
//                         Debug.Log("Nie można stworzyć nowej jednostki. Brak wolnych pól.");
//                         return;
//                     }

//                     // Wybranie losowej pozycji z dostępnych
//                     int randomIndex = UnityEngine.Random.Range(0, availablePositions.Count);
//                     position = availablePositions[randomIndex];
//                 }

//                 SaveAndLoadManager.Instance.IsLoading = true;

//                 GameObject unitObject = UnitsManager.Instance.CreateUnit(1, Population[i].unitName, position);

//                 SaveAndLoadManager.Instance.IsLoading = false;

//                 unitObject.name = unitObject.GetComponent<Stats>().Name;

//                 unitObject.GetComponent<Unit>().genome = Population[i];
//             }

//             // Ustawiamy numer generacji z pliku JSON
//             GenerationNumber = container.GenerationNumber;

//             GenerationNumber++;

//             for (int i = container.genomes.Count; i < PopulationSize; i++)
//             {
//                 UnitGenome newGenome = new UnitGenome();
//                 string unitName = "Unit " + i + " (Gen " + GenerationNumber + ")";
//                 newGenome.RandomizeGenes(unitName, GenerationNumber);
//                 Population.Add(newGenome);

//                 List<Vector2> availablePositions = GridManager.Instance.AvailablePositions();
//                 Vector2 position = Vector2.zero;

//                 if (!SaveAndLoadManager.Instance.IsLoading)                                                                                                                                                                                                          
//                 {
//                     if (availablePositions.Count == 0)
//                     {
//                         Debug.Log("Nie można stworzyć nowej jednostki. Brak wolnych pól.");
//                         return;
//                     }

//                     // Wybranie losowej pozycji z dostępnych
//                     int randomIndex = UnityEngine.Random.Range(0, availablePositions.Count);
//                     position = availablePositions[randomIndex];
//                 }

//                 SaveAndLoadManager.Instance.IsLoading = true;

//                 GameObject unitObject = UnitsManager.Instance.CreateUnit(1, Population[i].unitName, position);

//                 SaveAndLoadManager.Instance.IsLoading = false;

//                 unitObject.name = unitObject.GetComponent<Stats>().Name;

//                 unitObject.GetComponent<Unit>().genome = newGenome;
//             }

//             Debug.Log("DNA najlepszych jednostek zostało wczytane.");
//         }
//         else
//         {
//             Debug.Log("Brak zapisanych jednostek.");
//             GenerateInitialPopulation();
//         }
//     }
// }

// // Struktura do serializacji genomów
// [System.Serializable]
// public class UnitGenomeSerializable
// {
//     public int[] genes;
//     public float Fitness;
// }

// // Kontener na listę genomów do serializacji
// [System.Serializable]
// public class UnitGenomesContainer
// {
//     public List<UnitGenomeData> genomes; // Lista genomów jednostek
//     public int GenerationNumber; // Numer generacji do zapisania w pliku JSON
// }


// [System.Serializable]
// public class UnitGenomeData
// {
//     public int[] genes;
//     public float fitness;
//     public string unitName;
//     public int populationNumber;
// }

// public class UnitGenome
// {
//     public int[] genes = new int[30];
//     public int DamageDealt = 0;
//     public float Fitness = 0;
//     public string unitName; // Nazwa jednostki
//     public int populationNumber; // Numer populacji, z której jednostka pochodzi

//     private int currentGeneIndex = 0; // Indeks aktualnie wykorzystywanego genu

//     // Zamiast losowego wyboru, teraz wybieramy geny sekwencyjnie
//     public int GetDecision()
//     {
//         // Pobieramy decyzję na podstawie aktualnego indeksu genu
//         int decision = genes[currentGeneIndex];

//         // Zwiększamy licznik genu. Jeśli dojdziemy do końca tablicy genów, wracamy do początku.
//         currentGeneIndex = (currentGeneIndex + 1) % genes.Length;

//         return decision;
//     }

//     // Inicjalizacja losowych genów
//     public void RandomizeGenes(string name, int populationNumber)
//     {
//         unitName = name;
//         this.populationNumber = populationNumber;

//         for (int i = 0; i < genes.Length; i++)
//         {
//             genes[i] = UnityEngine.Random.Range(0, 11); // Zakres od 0 do 10
//         }

//         currentGeneIndex = 0; // Resetujemy licznik przy randomizacji
//     }

//     // Krzyżowanie genomów
//     public UnitGenome Crossover(UnitGenome otherParent)
//     {
//         UnitGenome offspring = new UnitGenome();

//         for (int i = 0; i < genes.Length; i++)
//         {
//             offspring.genes[i] = (UnityEngine.Random.Range(0, 2) == 0) ? this.genes[i] : otherParent.genes[i];
//         }

//         offspring.unitName = "Unit " + (GeneticAlgorithmManager.GenerationNumber) + " " + Random.Range(0, 1000);
//         offspring.populationNumber = GeneticAlgorithmManager.GenerationNumber;

//         return offspring;
//     }

//     // Mutacja genów
//     public void Mutate(float mutationRate)
//     {
//         for (int i = 0; i < genes.Length; i++)
//         {
//             if (UnityEngine.Random.Range(0f, 1f) < mutationRate)
//             {
//                 genes[i] = UnityEngine.Random.Range(0, 11); // Mutujemy na nową wartość
//             }
//         }
//     }
// }
