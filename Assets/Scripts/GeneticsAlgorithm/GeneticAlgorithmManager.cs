using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.IO;

public class GeneticAlgorithmManager : MonoBehaviour
{
    // Prywatne statyczne pole przechowujące instancję
    private static GeneticAlgorithmManager instance;

    // Publiczny dostęp do instancji
    public static GeneticAlgorithmManager Instance
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
            Destroy(gameObject);
        }
    }

    public bool IsWorking; // Określa, czy ewolucja się toczy
    public int PopulationSize = 0;
    public int TournamentSize = 4; // Rozmiar turnieju do selekcji turniejowej
    public float MutationRate = 0.1f;
    public int MaxRounds = 30;
    public List<UnitGenome> Population = new List<UnitGenome>();
    public static int GenerationNumber = 1;  // Zmienna śledząca numer generacji
    [SerializeField] private FitnessDisplayManager _fitnessDisplayManager;
    [SerializeField] private UnityEngine.UI.Toggle _unitTagToggle;
    private List<Unit> _allUnits;
    public List<Stats> AllStats = new List<Stats>();

    void Start()
    {
        _fitnessDisplayManager.ResetFitnessData(); // Resetujemy dane fitnessu na początku

        // Inicjujemy ewolucję
        //ToggleEvolution();
    }

    public void ToggleEvolution()
    {
        IsWorking = !IsWorking;

        if (IsWorking)
        {
            StartCoroutine(ContinuousEvolution());
            // Wyłączenie logów
            Debug.unityLogger.logEnabled = false;
        }
        else
        {
            // Włączenie logów
            Debug.unityLogger.logEnabled = true;
        }
    }

    IEnumerator ContinuousEvolution()
    {
        yield return new WaitForSeconds(5f);

        while (IsWorking == true)
        {
            string filePath = Path.Combine(Application.persistentDataPath, "AI_data.json");
            if (File.Exists(filePath))
            {
                LoadBestUnitsDNA();
            }
            else
            {
                GenerateInitialPopulation();
            }

            RoundsManager.Instance.NextRound();

            yield return StartCoroutine(RunEvolution());

            // Zapis wyników po zakończeniu generacji
            DisplayBestUnits();
            SaveBestUnitsDNA();

            yield return null; //Tu trzeba dodać czekanie, aż zakończą się akcje danej populacji

            // Usuwamy wszystkie jednostki
            ClearAllUnits();

            // Resetowanie stanu dla nowej symulacji
            RoundsManager.RoundNumber = 0;
            Population.Clear();

            // Tworzenie nowej populacji po usunięciu poprzednich jednostek
            GenerateInitialPopulation();
        }
    }

    private void GenerateInitialPopulation()
    {
        // if (Population.Count == 0)
        // {
            //Jeśli wielkość populacji nie jest zdefiniowana to ustawiamy losową wielkość
            if(PopulationSize == 0)
            {
                PopulationSize = Random.Range(4, 21);
            }

            for (int i = 0; i < PopulationSize; i++)
            {
                UnitGenome newGenome = new UnitGenome();
                string unitName = "Unit " + i + " (Gen " + GenerationNumber + ")";
                newGenome.RandomizeGenes(unitName, GenerationNumber);
                Population.Add(newGenome);

                _unitTagToggle.isOn = Random.value > 0.5f;

                GameObject unitObject = CreateUnitOnRandomTile(1, Population[i].UnitName); // Na razie tworzy jednostki z indexem 1, czyli ludzi

                unitObject.GetComponent<Stats>().TempHealth = unitObject.GetComponent<Stats>().MaxHealth;
 
                unitObject.GetComponent<Unit>().Genome = Population[i];
            }
        //}
    }

    // Algorytm genetyczny
    IEnumerator RunEvolution()
    {
        //Kopiujemy listę wszystkich jednostek, żeby móc obliczac fitness również dla tych, które zginą w trakcie symulacji
        foreach (Unit unit in UnitsManager.Instance.AllUnits)
        {
            if(unit == null) continue;
            _allUnits.Add(unit);
            Stats stats = unit.GetComponent<Stats>();
            AllStats.Add(stats);
        }

        while (RoundsManager.RoundNumber < MaxRounds)
        {
            bool enemyUnitExists = false;
            bool playerUnitExists = false;

            foreach (Unit unit in UnitsManager.Instance.AllUnits)
            {
                if (unit == null) continue;

                if (unit.CompareTag("PlayerUnit")) playerUnitExists = true;
                else if (unit.CompareTag("EnemyUnit")) enemyUnitExists = true;

                if (playerUnitExists && enemyUnitExists) break;
            }

            // Jeśli mamy tylko jednostki jednej drużyny, przerywamy pętlę
            if (!(playerUnitExists && enemyUnitExists)) 
            {
                break;
            }

            RoundsManager.Instance.NextRoundButton.gameObject.SetActive(false);
            //_useFortunePointsButton.SetActive(false);

            for(int i=0; i < UnitsManager.Instance.AllUnits.Count; i++)
            {
                if (UnitsManager.Instance.AllUnits[i] == null) continue;

                InitiativeQueueManager.Instance.SelectUnitByQueue();

                yield return new WaitForSeconds(0.1f);

                SimulateUnit(Unit.SelectedUnit.GetComponent<Unit>());

                // Czeka, aż postać skończy ruch, zanim wybierze kolejną postać
                yield return new WaitUntil(() => MovementManager.Instance.IsMoving == false);
                yield return new WaitForSeconds(0.5f);
            }

            RoundsManager.Instance.NextRoundButton.gameObject.SetActive(true);
            //_useFortunePointsButton.SetActive(true);

            // Po zakończeniu wszystkich ustalonych rund przeprowadzamy ewolucję
            if (RoundsManager.RoundNumber == MaxRounds)
            {
                EvolvePopulation();
            }

            RoundsManager.Instance.NextRound();
        }

        // Ocena fitness każdej jednostki
        for (int i = 0; i < AllStats.Count; i++)
        {
            Stats stats = AllStats[i];
            Unit unit = _allUnits[i];

            float fitness = CalculateFitness(stats);  // Możesz przekazać jednostkę lub statystyki
            unit.Genome.Fitness = fitness;

            // Dodajemy fitness do FitnessDisplayManager
            _fitnessDisplayManager.AddFitness(fitness);
        }

        _fitnessDisplayManager.ShowAverageFitness();

        AllStats.Clear();
        _allUnits.Clear();
    }

    // Ewolucja populacji
    private void EvolvePopulation()
    {
        List<UnitGenome> newPopulation = new List<UnitGenome>();

        // Selekcja turniejowa
        List<UnitGenome> selectedUnits = new List<UnitGenome>();
        for (int i = 0; i < PopulationSize; i++)
        {
            selectedUnits.Add(TournamentSelection());
        }

        // Elitarność: zachowaj najlepsze jednostki
        List<UnitGenome> bestUnits = Population.Take(2).ToList();
        newPopulation.AddRange(bestUnits);

        // Krzyżowanie i mutacja reszty populacji
        while (newPopulation.Count < PopulationSize)
        {
            UnitGenome parent1 = selectedUnits[Random.Range(0, selectedUnits.Count)];
            UnitGenome parent2 = selectedUnits[Random.Range(0, selectedUnits.Count)];

            UnitGenome offspring = parent1.Crossover(parent2);
            offspring.Mutate(MutationRate); // Adaptacyjna mutacja

            newPopulation.Add(offspring);
        }

        Population = newPopulation;

        ClearAllUnits();

        GenerateInitialPopulation();

        GenerationNumber++;
    }

     // Selekcja turniejowa
    UnitGenome TournamentSelection()
    {
        List<UnitGenome> tournament = new List<UnitGenome>();
        for (int i = 0; i < TournamentSize; i++)
        {
            UnitGenome randomUnit = Population[Random.Range(0, Population.Count)];
            tournament.Add(randomUnit);
        }
        return tournament.OrderByDescending(u => u.Fitness).First();
    }

    // Symulacja ruchu i akcji dla każdej jednostki
    public void SimulateUnit(Unit unit)
    {
        if(unit.GetComponent<Stats>().TempHealth < 0) return;

        Weapon weapon = InventoryManager.Instance.ChooseWeaponToAttack(unit.gameObject);
        GameObject closestOpponent = AutoCombatManager.Instance.GetClosestOpponent(unit.gameObject, false);

        Unit furthestUnit = FindFurthestUnit(unit);
        Unit mostInjuredUnit = FindMostInjuredUnit(unit);
        Unit leastInjuredUnit = FindLeastInjuredUnit(unit);
        Unit weakestUnit = FindUnitWithLowestOverall(unit);
        Unit strongestUnit = FindUnitWithHighestOverall(unit);
        Unit targetWithMostAllies = FindTargetWithMostAlliesNearby(unit);

        int decision = unit.Genome.GetDecision();

        switch (decision)
        {
            case 0:
                // Ruch w stronę najbliższego przeciwnika
                if (closestOpponent != null)
                {
                    Debug.Log("Ruch w stronę najbliższego przeciwnika");
                    MoveTowards(unit, closestOpponent);
                }
                break;
            case 1:
                // Ruch w stronę najdalszego przeciwnika
                if (furthestUnit != null)
                {
                    Debug.Log("Ruch w stronę najdalszego przeciwnika");
                    MoveTowards(unit, furthestUnit.gameObject);
                }
                break;
            case 2:
                // Ruch w stronę najbardziej zranionej jednostki przeciwnika
                if (mostInjuredUnit != null)
                {
                    Debug.Log("Ruch w stronę najbardziej zranionej jednostki przeciwnika");
                    MoveTowards(unit, mostInjuredUnit.gameObject);
                }
                break;
            case 3:
                // Ruch w stronę najmniej zranionej jednostki przeciwnika
                if (leastInjuredUnit != null)
                {
                    Debug.Log("Ruch w stronę najmniej zranionej jednostki przeciwnika");
                    MoveTowards(unit, leastInjuredUnit.gameObject);
                }
                break;
            case 4:
                // Zaatakowanie najbliższego przeciwnika
                if (closestOpponent != null)
                {
                    Debug.Log("Zaatakowanie najbliższego przeciwnika");
                    CombatManager.Instance.Attack(unit, closestOpponent.GetComponent<Unit>(), false);
                }
                break;
            case 5:
                // Szarża na najbliższego przeciwnika
                if (closestOpponent != null)
                {
                    Debug.Log("Szarża na najbliższego przeciwnika");
                    CombatManager.Instance.ChangeAttackType("Charge");
                    CombatManager.Instance.Attack(unit, closestOpponent.GetComponent<Unit>(), false);
                }

                break;
            case 6:
                // Szaleńczy atak na najbliższego przeciwnika
                if (closestOpponent != null)
                {
                    Debug.Log("Szaleńczy atak na najbliższego przeciwnika");
                    CombatManager.Instance.ChangeAttackType("AllOutAttack");
                    CombatManager.Instance.Attack(unit, closestOpponent.GetComponent<Unit>(), false);
                }
                break;
            case 7:
                // Zajęcie pozycji obronnej
                Debug.Log("Zajęcie pozycji obronnej");
                CombatManager.Instance.DefensiveStance();
                break;
            case 8:
                // Finta na najbliższego przeciwnika
                if (closestOpponent != null)
                {
                    Debug.Log("Finta na najbliższego przeciwnika");
                    CombatManager.Instance.ChangeAttackType("Feint");
                    CombatManager.Instance.Attack(unit, closestOpponent.GetComponent<Unit>(), false);
                }
                break;
            case 9:
                // Ustawienie celowania
                Debug.Log("Ustawienie celowania");
                CombatManager.Instance.SetAim();
                break;
            case 10:
                // Zakończenie tury
                Debug.Log("Zakończenie tury");
                RoundsManager.Instance.FinishTurn();
                break;
        }

        if(RoundsManager.Instance.UnitsWithActionsLeft.ContainsKey(unit) && RoundsManager.Instance.UnitsWithActionsLeft[unit] == 1 && !unit.IsTurnFinished )
        {
            SimulateUnit(unit);
        }
    }

    private GameObject CreateUnitOnRandomTile(int unitIndex, string unitName)
    {
        List<Vector2> availablePositions = GridManager.Instance.AvailablePositions();
        Vector2 position = Vector2.zero;

        if (!SaveAndLoadManager.Instance.IsLoading)
        {
            if (availablePositions.Count == 0)
            {
                Debug.Log("Nie można stworzyć nowej jednostki. Brak wolnych pól.");
                return null;
            }

            // Wybranie losowej pozycji z dostępnych
            int randomIndex = UnityEngine.Random.Range(0, availablePositions.Count);
            position = availablePositions[randomIndex];
        }

        return UnitsManager.Instance.CreateUnit(unitIndex, unitName, position);
    }

    private void ClearAllUnits()
    {
        // Usunięcie wszystkich jednostek
        for (int i = UnitsManager.Instance.AllUnits.Count - 1; i >= 0; i--)
        {
            Unit unit = UnitsManager.Instance.AllUnits[i];
            UnitsManager.Instance.DestroyUnit(unit.gameObject);
        }
    }

    void DisplayBestUnits()
    {
        Population = Population.OrderByDescending(g => g.Fitness).ToList();

        Debug.Log("Najlepsze jednostki po zakończeniu ewolucji:");

        // Wyświetlanie pięciu najlepszych jednostek
        for (int i = 0; i < 6 && i < Population.Count; i++)
        {
            UnitGenome bestUnit = Population[i];
            string genesString = string.Join(",", bestUnit.Genes.Select(g => g.ToString()).ToArray());
            Debug.Log($"<color=cyan>Jednostka {bestUnit.UnitName}: Fitness = {bestUnit.Fitness}, Geny = {genesString}</color>");
        }

        // Aktualizowanie licznika genów dla najlepszej jednostki
        if (Population.Count > 0)
        {
            UnitGenome topUnit = Population[0]; // Tylko najlepsza jednostka
            _fitnessDisplayManager.UpdateGeneCount(topUnit.Genes); // Zaktualizuj liczniki genów tylko dla najlepszej jednostki
        }
    }

     // Obliczanie fitness jednostki
    float CalculateFitness(Stats stats)
    {
        float fitness = stats.TotalDamageDealt + (stats.OpponentsKilled * 20) - stats.TotalDamageTaken;

        // Jeżeli jednostka umarła, obniżamy jej fitness o 10
        if (stats.TempHealth <= 0)
        {
            // Kara za śmierć nie może być mniejsza niż 0
            fitness -= Mathf.Max(0, 20 - stats.RoundsPlayed);  // Kara za śmierć
        }

        return fitness;
    }

    #region Save and load functions
    // Zapis najlepszych jednostek do pliku
    void SaveBestUnitsDNA()
    {
        List<UnitGenome> bestUnits = Population.OrderByDescending(g => g.Fitness).Take(5).ToList();

        List<UnitGenomeData> bestUnitsData = new List<UnitGenomeData>();
        foreach (UnitGenome unit in bestUnits)
        {
            bestUnitsData.Add(new UnitGenomeData
            {
                Genes = unit.Genes,
                Fitness = unit.Fitness,
                UnitName = unit.UnitName,
                GenerationNumber = unit.GenerationNumber  // Zapisujemy numer generacji
            });
        }

        UnitGenomesContainer container = new UnitGenomesContainer
        {
            Genomes = bestUnitsData,
            GenerationNumber = GenerationNumber // Zapisujemy numer generacji
        };

        string json = JsonUtility.ToJson(container);
        string filePath = Path.Combine(Application.persistentDataPath, "AI_data.json");
        File.WriteAllText(filePath, json);
        Debug.Log($"DNA najlepszych jednostek zostao zapisane do pliku: {filePath}");
    }

    // Wczytanie DNA najlepszych jednostek
    void LoadBestUnitsDNA()
    {
        string filePath = Path.Combine(Application.persistentDataPath, "AI_data.json");;
        if (File.Exists(filePath))
        {
            string json = File.ReadAllText(filePath);
            UnitGenomesContainer container = JsonUtility.FromJson<UnitGenomesContainer>(json);

            Population.Clear();
            ClearAllUnits();

            for (int i = 0; i < container.Genomes.Count; i++)
            {
                UnitGenomeData data = container.Genomes[i];
                UnitGenome genome = new UnitGenome
                {
                    Genes = data.Genes,
                    Fitness = data.Fitness,
                    UnitName = data.UnitName,
                    GenerationNumber = data.GenerationNumber
                };

                Population.Add(genome);

                _unitTagToggle.isOn = Random.value > 0.5f;

                GameObject unitObject = CreateUnitOnRandomTile(1, data.UnitName); // Na razie tworzy jednostki z indexem 1, czyli ludzi

                unitObject.GetComponent<Stats>().TempHealth = unitObject.GetComponent<Stats>().MaxHealth;
 
                unitObject.GetComponent<Unit>().Genome = genome;
            }

            // Ustawiamy numer generacji z pliku JSON
            GenerationNumber = container.GenerationNumber;

            GenerationNumber++;

            for (int i = container.Genomes.Count; i < PopulationSize; i++)
            {
                UnitGenome newGenome = new UnitGenome();
                string unitName = "Unit " + i + " (Gen " + GenerationNumber + ")";
                newGenome.RandomizeGenes(unitName, GenerationNumber);
                Population.Add(newGenome);

                _unitTagToggle.isOn = Random.value > 0.5f;

                GameObject unitObject = CreateUnitOnRandomTile(1, Population[i].UnitName); // Na razie tworzy jednostki z indexem 1, czyli ludzi

                unitObject.GetComponent<Stats>().TempHealth = unitObject.GetComponent<Stats>().MaxHealth;
 
                unitObject.GetComponent<Unit>().Genome = Population[i];
            }

            Debug.Log("DNA najlepszych jednostek zostało wczytane.");
        }
        else
        {
            Debug.Log("Brak zapisanych jednostek.");
            GenerateInitialPopulation();
        }
    }

    #endregion

    #region Specific units actions
    private void MoveTowards(Unit unit, GameObject opponent)
    {
        // Szuka wolnej pozycji obok celu, do której droga postaci jest najkrótsza
        GameObject targetTile = CombatManager.Instance.GetTileAdjacentToTarget(unit.gameObject, opponent);
        Vector2 targetTilePosition = Vector2.zero;

        if (targetTile != null)
        {
            targetTilePosition = new Vector2(targetTile.transform.position.x, targetTile.transform.position.y);
        }
        else
        {
            Debug.Log($"{unit.GetComponent<Stats>().Name} nie jest w stanie podejść do {opponent.GetComponent<Stats>().Name}.");

            return;
        }

        MovementManager.Instance.MoveSelectedUnit(targetTile, unit.gameObject);

        // Synchronizuje collidery
        Physics2D.SyncTransforms();
    }
    #endregion

    #region Looking for specific units
    // Znajduje najbardziej zranioną jednostkę przeciwnika
    Unit FindMostInjuredUnit(Unit currentUnit)
    {
        Unit mostInjuredUnit = null;
        float minHealth = float.MaxValue;

        foreach (Unit otherUnit in UnitsManager.Instance.AllUnits)
        {
            if (IsValidTarget(currentUnit, otherUnit))
            {
                if (otherUnit.GetComponent<Stats>().TempHealth < minHealth || mostInjuredUnit == null)
                {
                    minHealth = otherUnit.GetComponent<Stats>().TempHealth;
                    mostInjuredUnit = otherUnit;
                }
            }
        }

        return mostInjuredUnit;
    }

    // Znajduje najmniej zranioną jednostkę przeciwnika
    Unit FindLeastInjuredUnit(Unit currentUnit)
    {
        Unit leastInjuredUnit = null;
        float maxHealth = 0;

        foreach (Unit otherUnit in UnitsManager.Instance.AllUnits)
        {
            if (IsValidTarget(currentUnit, otherUnit))
            {
                // Porównuje zdrowie jednostek, aby znaleźć tę z największą liczbą punktów zdrowia
                if (otherUnit.GetComponent<Stats>().TempHealth > maxHealth || leastInjuredUnit == null)
                {
                    maxHealth = otherUnit.GetComponent<Stats>().TempHealth;
                    leastInjuredUnit = otherUnit;
                }
            }
        }

        return leastInjuredUnit;
    }

    // Znajduje najdalszą jednostkę przeciwnika
    Unit FindFurthestUnit(Unit currentUnit)
    {
        Unit furthestUnit = null;
        float maxDistance = 0;

        foreach (Unit otherUnit in UnitsManager.Instance.AllUnits)
        {
            if (IsValidTarget(currentUnit, otherUnit))
            {
                float distance = Vector2.Distance(currentUnit.transform.position, otherUnit.transform.position);
                // Porównuje odległości między jednostkami, aby znaleźć najdalszą
                if (distance > maxDistance)
                {
                    maxDistance = distance;
                    furthestUnit = otherUnit;
                }
            }
        }

        return furthestUnit;
    }

    //Znajduje jednostkę, wobec której będzie największa przewaga liczebna
    Unit FindTargetWithMostAlliesNearby(Unit currentUnit)
    {
        Unit bestTarget = null;
        int maxAllies = 0;

        foreach (Unit otherUnit in UnitsManager.Instance.AllUnits)
        {
            if (IsValidTarget(currentUnit, otherUnit))
            {
                int adjacentOpponents = 0; // Przeciwnicy atakującego stojący obok celu ataku
                int adjacentAllies = 0;    // Sojusznicy atakującego stojący obok celu ataku

                CountAdjacentUnits(otherUnit.transform.position, currentUnit.tag, otherUnit.tag, ref adjacentAllies, ref adjacentOpponents);

                if (adjacentAllies - adjacentOpponents > maxAllies)
                {
                    maxAllies = adjacentAllies - adjacentOpponents;
                    bestTarget = otherUnit;
                }
            }
        }

        return bestTarget;
    }

    // Funkcja pomocnicza do zliczania jednostek w sąsiedztwie danej pozycji
    private void CountAdjacentUnits(Vector2 center, string allyTag, string opponentTag, ref int allies, ref int opponents)
    {
        HashSet<Collider2D> countedOpponents = new HashSet<Collider2D>(); // Dodano lokalną zmienną
        Vector2[] positions = {
            center,
            center + Vector2.right,
            center + Vector2.left,
            center + Vector2.up,
            center + Vector2.down,
            center + new Vector2(1, 1),
            center + new Vector2(-1, -1),
            center + new Vector2(-1, 1),
            center + new Vector2(1, -1)
        };

        foreach (var pos in positions)
        {
            Collider2D collider = Physics2D.OverlapPoint(pos);
            if (collider == null) continue;

            if (collider.CompareTag(allyTag))
            {
                allies++;
            }
            else if (collider.CompareTag(opponentTag) && !countedOpponents.Contains(collider))
            {
                opponents++;
                countedOpponents.Add(collider); // Dodano do lokalnego zestawu
            }
        }
    }

    //Znajduje jednostkę z najwyższym overallem
    Unit FindUnitWithHighestOverall(Unit currentUnit)
    {
        Unit bestTarget = null;
        int bestOverall = 0;

        foreach (Unit otherUnit in UnitsManager.Instance.AllUnits)
        {
            if (IsValidTarget(currentUnit, otherUnit))
            {
                int overall = otherUnit.GetComponent<Stats>().Overall;
                if (overall > bestOverall)
                {
                    bestOverall = overall;
                    bestTarget = otherUnit;
                }
            }
        }

        return bestTarget;
    }

    // Znajduje jednostkę z najniższym overallem
    Unit FindUnitWithLowestOverall(Unit currentUnit)
    {
        Unit bestTarget = null;
        int lowestOverall = int.MaxValue; // Ustawiamy najwyższą możliwą wartość na początek

        foreach (Unit otherUnit in UnitsManager.Instance.AllUnits)
        {
            if (IsValidTarget(currentUnit, otherUnit))
            {
                int overall = otherUnit.GetComponent<Stats>().Overall;
                if (overall < lowestOverall)
                {
                    lowestOverall = overall;
                    bestTarget = otherUnit;
                }
            }
        }

        return bestTarget;
    }

    private bool IsValidTarget(Unit currentUnit, Unit otherUnit)
    {
        if (otherUnit == currentUnit) return false; // Ignoruj tę samą jednostkę
        if (otherUnit.CompareTag(currentUnit.tag)) return false; // Ignoruj jednostki z tym samym tagiem
        if (otherUnit.GetComponent<Stats>().TempHealth <= 0) return false; // Ignoruj martwe jednostki

        return true;
    }
    #endregion
}
