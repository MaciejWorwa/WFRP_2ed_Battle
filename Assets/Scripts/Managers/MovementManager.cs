using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using TMPro;
using UnityEngine.UI;

public class MovementManager : MonoBehaviour
{
    // Prywatne statyczne pole przechowujące instancję
    private static MovementManager instance;

    // Publiczny dostęp do instancji
    public static MovementManager Instance
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
    [HideInInspector] public bool IsMoving;
    [SerializeField] private Button _chargeButton;
    [SerializeField] private Button _runButton;
    [SerializeField] private Button _retreatButton;

    #region Move functions
    public void MoveSelectedUnit(GameObject selectedTile, GameObject unit)
    {
        // Nie pozwala wykonać akcji ruchu, dopóki poprzedni ruch nie zostanie zakończony. Sprawdza też, czy gra nie jest wstrzymana (np. poprzez otwarcie dodatkowych paneli)
        if( IsMoving == true || GameManager.IsGamePaused) return;

        // Sprawdza zasięg ruchu postaci
        int movementRange = unit.GetComponent<Stats>().TempSz;

        // Pozycja postaci przed zaczęciem wykonywania ruchu
        Vector3 startCharPos = unit.transform.position;
        
        // Aktualizuje informację o zajęciu pola, które postać opuszcza
        GridManager.Instance.ResetTileOccupancy(startCharPos);

        // Pozycja pola wybranego jako cel ruchu
        Vector3 selectedTilePos = new Vector3(selectedTile.transform.position.x, selectedTile.transform.position.y, 0);

        // Znajdź najkrótszą ścieżkę do celu
        List<Vector3> path = FindPath(startCharPos, selectedTilePos, movementRange);

        // Sprawdza czy wybrane pole jest w zasięgu ruchu postaci. W przypadku automatycznej walki ten warunek nie jest wymagany.
        if (path.Count > 0 && path.Count <= movementRange || GameManager.IsAutoCombatMode)
        {
            //Wykonuje akcję
            bool canDoAction = true;
            if(unit.GetComponent<Unit>().IsRunning || unit.GetComponent<Unit>().IsRetreating) // Bieg lub bezpieczny odwrót
            {
                canDoAction = RoundsManager.Instance.DoFullAction(unit.GetComponent<Unit>());
            }
            else if(!unit.GetComponent<Unit>().IsCharging) // Zwykły ruch. Akcje za szarże są zużywane podczas ataku
            {
                canDoAction = RoundsManager.Instance.DoHalfAction(unit.GetComponent<Unit>());
            }

            if(!canDoAction) return;   

            //Resetuje przycelowanie, jeśli było aktywne
            if (Unit.SelectedUnit.GetComponent<Unit>().AimingBonus != 0)
            {
                CombatManager.Instance.SetAim();
            }
            //Resetuje pozycję obronną, jeśli była aktywna
            if (Unit.SelectedUnit.GetComponent<Unit>().DefensiveBonus != 0)
            {
                CombatManager.Instance.DefensiveStance();
            }

            // Oznacza wybrane pole jako zajęte (gdyż trochę potrwa, zanim postać tam dojdzie i gdyby nie zaznaczyć, to można na nie ruszyć inną postacią)
            selectedTile.GetComponent<Tile>().IsOccupied = true;

            //Zapobiega zaznaczeniu jako zajęte pola docelowego, do którego jednostka w trybie automatycznej walki niekoniecznie da radę dojść
            if(GameManager.IsAutoCombatMode)
            {
                AutoCombatManager.Instance.TargetTile = selectedTile.GetComponent<Tile>();
            }

            // Resetuje kolor pól w zasięgu ruchu na czas jego wykonywania
            GridManager.Instance.ResetColorOfTilesInMovementRange();

            //Sprwadza, czy ruch powoduje ataki okazyjne
            CheckForOpportunityAttack(unit, selectedTilePos);

            // Wykonuje pojedynczy ruch tyle razy ile wynosi zasięg ruchu postaci
            StartCoroutine(MoveWithDelay(unit, path, movementRange));
        }
        else
        {
            Debug.Log("Wybrane pole jest poza zasięgiem ruchu postaci lub jest zajęte.");
        }
    }

    private IEnumerator MoveWithDelay(GameObject unit, List<Vector3> path, int movementRange)
    {
        // Ogranicz iterację do mniejszej wartości: movementRange lub liczby elementów w liście path
        int iterations = Mathf.Min(movementRange, path.Count);

        for (int i = 0; i < iterations; i++)
        {
            Vector3 nextPos = path[i];
            nextPos.z = 0f;

            float elapsedTime = 0f;
            float duration = 0.2f; // Czas trwania interpolacji

            while (elapsedTime < duration && unit != null)
            {
                IsMoving = true;

                unit.transform.position = Vector3.Lerp(unit.transform.position, nextPos, elapsedTime / duration);
                elapsedTime += Time.deltaTime;
                yield return null; // Poczekaj na odświeżenie klatki animacji
            }

            //Na wypadek, gdyby w wyniku ataku okazyjnego podczas ruchu jednostka została zabita i usunięta
            if(unit == null)
            {
                IsMoving = false;
                yield break;
            } 

            unit.transform.position = nextPos;
        }

        if (unit.transform.position == path[iterations - 1])
        {
            IsMoving = false;
            Retreat(false);
            
            GridManager.Instance.HighlightTilesInMovementRange(Unit.SelectedUnit.GetComponent<Stats>());
        }

        //Zaznacza jako zajęte faktyczne pole, na którym jednostka zakończy ruch, a nie pole do którego próbowała dojść
        if(GameManager.IsAutoCombatMode)
        {
            AutoCombatManager.Instance.CheckForTargetTileOccupancy(unit);
        }
    }

    public List<Vector3> FindPath(Vector3 start, Vector3 goal, int movementRange)
    {
        // Tworzy listę otwartych węzłów
        List<Node> openNodes = new List<Node>();

        // Dodaje węzeł początkowy do listy otwartych węzłów
        Node startNode = new Node
        {
            Position = start,
            G = 0,
            H = CalculateDistance(start, goal),
            F = 0 + CalculateDistance(start, goal),
            Parent = default
        };
        openNodes.Add(startNode);

        // Tworzy listę zamkniętych węzłów
        List<Vector3> closedNodes = new List<Vector3>();

        while (openNodes.Count > 0)
        {
            // Znajduje węzeł z najmniejszym kosztem F i usuwa go z listy otwartych węzłów
            Node current = openNodes.OrderBy(n => n.F).First();
            openNodes.Remove(current);

            // Dodaje bieżący węzeł do listy zamkniętych węzłów
            closedNodes.Add(current.Position);

            // Sprawdza, czy bieżący węzeł jest węzłem docelowym
            if (current.Position == goal)
            {
                // Tworzy listę punktów i dodaje do niej węzły od węzła docelowego do początkowego
                List<Vector3> path = new List<Vector3>();
                Node node = current;

                while (node.Position != start)
                {
                    path.Add(new Vector3(node.Position.x, node.Position.y, 0));
                    node = node.Parent;
                }

                // Odwraca kolejność punktów w liście, aby uzyskać ścieżkę od początkowego do docelowego
                path.Reverse();

                return path;
            }

            // Pobiera sąsiadów bieżącego węzła
            List<Node> neighbors = new List<Node>();
            neighbors.Add(new Node { Position = current.Position + Vector3.up });
            neighbors.Add(new Node { Position = current.Position + Vector3.down });
            neighbors.Add(new Node { Position = current.Position + Vector3.left });
            neighbors.Add(new Node { Position = current.Position + Vector3.right });

            // Przetwarza każdego sąsiada
            foreach (Node neighbor in neighbors)
            {
                // Sprawdza, czy sąsiad jest w liście zamkniętych węzłów lub poza zasięgiem ruchu postaci
                if (closedNodes.Contains(neighbor.Position) || CalculateDistance(current.Position, neighbor.Position) > movementRange)
                {
                    continue; // przerywa tą iterację i przechodzi do kolejnej bez wykonywania w obecnej iteracji kodu, który jest poniżej. Natomiast 'break' przerywa całą pętle i kolejne iteracje nie wystąpią
                }

                // Sprawdza, czy na miejscu sąsiada występuje inny collider niż tile
                Collider2D collider = Physics2D.OverlapPoint(neighbor.Position);

                if (collider != null)
                {
                    bool isTile = false;

                    if (collider.gameObject.CompareTag("Tile") && !collider.gameObject.GetComponent<Tile>().IsOccupied)
                    {
                        isTile = true;
                    }

                    if (isTile)
                    {
                        // Oblicza koszt G dla sąsiada
                        int gCost = current.G + 1;

                        // Sprawdza, czy sąsiad jest już na liście otwartych węzłów
                        Node existingNode = openNodes.Find(n => n.Position == neighbor.Position);

                        if (existingNode != null)
                        {
                            // Jeśli koszt G dla bieżącego węzła jest mniejszy niż dla istniejącego węzła, to aktualizuje go
                            if (gCost < existingNode.G)
                            {
                                existingNode.G = gCost;
                                existingNode.F = existingNode.G + existingNode.H;
                                existingNode.Parent = current;
                            }
                        }
                        else
                        {
                            // Jeśli sąsiad nie jest jeszcze na liście otwartych węzłów, to dodaje go
                            Node newNode = new Node
                            {
                                Position = neighbor.Position,
                                G = gCost,
                                H = CalculateDistance(neighbor.Position, goal),
                                F = gCost + CalculateDistance(neighbor.Position, goal),
                                Parent = current
                            };
                            openNodes.Add(newNode);
                        }
                    }
                }
            }
        }

        // Jeśli nie udało się znaleźć ścieżki, to zwraca pustą listę
        return new List<Vector3>();
    }
    #endregion

    // Funkcja obliczająca odległość pomiędzy dwoma punktami na płaszczyźnie XY
    private int CalculateDistance(Vector3 a, Vector3 b)
    {
        return (int)(Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y));
    }

    #region Charge and Run modes
    public void UpdateMovementRange(int modifier)
    {
        if (Unit.SelectedUnit == null) return;

        Unit unit = Unit.SelectedUnit.GetComponent<Unit>();
        Stats stats = Unit.SelectedUnit.GetComponent<Stats>();

        //Uwzględnia, że Zombie nie mogą biegać
        if(stats.Race == "Zombie" && modifier == 3)
        {
            Debug.Log("Ta jednostka nie może wykonywać akcji biegu.");
            return;
        }

        //Jeżeli postać już jest w trybie szarży lub biegu, resetuje je
        if (unit.IsCharging && modifier == 2 || unit.IsRunning && modifier == 3)
        {
            modifier = 1;
        }

        if(modifier > 1 && RoundsManager.Instance.UnitsWithActionsLeft[unit.GetComponent<Unit>()] < 2) //Sprawdza, czy jednostka może wykonać akcję podwójną
        {
            Debug.Log("Ta jednostka nie może w tej rundzie wykonać akcji podwójnej.");
            return;
        }

        // //Sprawdzenie, czy postać walczy bronia dystansową. Jeśli tak, to szarża nie jest możliwa
        // if(modifier == 2 && unit.GetComponent<Inventory>().EquippedWeapons[0] != null && unit.GetComponent<Inventory>().EquippedWeapons[0].Type.Contains("ranged"))
        // {
        //     Debug.Log("Jednostka walcząca bronią dystansową nie może wykonywać szarży.");
        //     return;
        // }

        //Aktualizuje obecny tryb poruszania postaci
        unit.IsCharging = modifier == 2; //operator trójargumentowegy. Jeśli modifier == 2 to wartość == true, jeśli nie to wartość == false
        unit.IsRunning = modifier == 3; //operator trójargumentowegy. Jeśli modifier == 3 to wartość == true, jeśli nie to wartość == false

        //Zmienia typ ataku w menadżerze walki
        if(unit.IsRunning)
        {
            CombatManager.Instance.ChangeAttackType("StandardAttack"); //Resetuje szarże jako obecny typ ataku i ustawia standardowy atak
        }

        //Oblicza obecną szybkość
        //Uwzględnia karę do Szybkości za zbroję płytową
        if(stats.Sturdy == false && (stats.Armor_head >= 5 || stats.Armor_torso >= 5 || stats.Armor_arms >= 5 || stats.Armor_legs >= 5))
        {
            stats.TempSz = (stats.Sz - 1) * modifier;
        }
        else
        {
            stats.TempSz = stats.Sz * modifier;
        }

        // Aktualizuje podświetlenie pól w zasięgu ruchu
        GridManager.Instance.HighlightTilesInMovementRange(stats);

        ChangeButtonColor(modifier);
    }

    //Bezpieczny odwrót
    public void Retreat(bool value)
    {
        if (Unit.SelectedUnit == null) return;
        Unit unit = Unit.SelectedUnit.GetComponent<Unit>();

        if(value == true && RoundsManager.Instance.UnitsWithActionsLeft[unit] < 2) //Sprawdza, czy jednostka może wykonać akcję podwójną
        {
            Debug.Log("Ta jednostka nie może w tej rundzie wykonać akcji podwójnej.");
            return;
        }

        unit.IsRetreating = value;
        _retreatButton.GetComponent<Image>().color = unit.IsRetreating ? Color.green : Color.white;
    }

    private void ChangeButtonColor(int modifier)
    {  
        //_chargeButton.GetComponent<Image>().color = modifier == 1 ? Color.white : modifier == 2 ? Color.green : Color.white;
        _runButton.GetComponent<Image>().color = modifier == 1 ? Color.white : modifier == 3 ? Color.green : Color.white;    
    }
    #endregion

    #region Check for opportunity attack
    // Sprawdza czy ruch powoduje atak okazyjny
    public void CheckForOpportunityAttack(GameObject movingUnit, Vector3 selectedTilePosition)
    {
        //Przy bezpiecznym odwrocie nie występuje atak okazyjny
        if(Unit.SelectedUnit != null && Unit.SelectedUnit.GetComponent<Unit>().IsRetreating) return;

        //Stworzenie tablicy wszystkich jednostek
        Unit[] units = FindObjectsByType<Unit>(FindObjectsSortMode.None);

        // Atak okazyjny wywolywany dla kazdego wroga bedacego w zwarciu z bohaterem gracza
        foreach (Unit unit in units)
        {
            Weapon weapon = InventoryManager.Instance.ChooseWeaponToAttack(unit.gameObject);

            //Jeżeli jest to sojusznik, jednostka ogłuszona, unieruchomiona, bezbronna lub jednostka z bronią dystansową to ją pomijamy
            if (unit.CompareTag(movingUnit.tag) || weapon.Type.Contains("ranged") || unit.Trapped || unit.StunDuration > 0 || unit.HelplessDuration > 0) continue;

            // Sprawdzenie ilu przeciwników jest w zwarciu z aktywną jednostką i czy jej ruch powoduje oddalenie się od nich (czyli atak okazyjny)
            float distanceFromOpponent = Vector3.Distance(movingUnit.transform.position, unit.transform.position);
            float distanceFromOpponentAfterMove = Vector3.Distance(selectedTilePosition, unit.transform.position);

            if (distanceFromOpponent <= 1.8f && distanceFromOpponentAfterMove > 1.8f)
            {
                Debug.Log($"Ruch spowodował atak okazyjny od {unit.GetComponent<Stats>().Name}.");

                // Wywołanie ataku okazyjnego
                CombatManager.Instance.Attack(unit, movingUnit.GetComponent<Unit>(), true);             
            }
        }
    }
    #endregion

    #region Highlight path
    public void HighlightPath(GameObject unit, GameObject tile)
    {
        var path = FindPath(unit.transform.position, new Vector3 (tile.transform.position.x, tile.transform.position.y, 0), unit.GetComponent<Stats>().TempSz);

        if(path.Count <= unit.GetComponent<Stats>().TempSz)
        {
            foreach (Vector3 tilePosition in path)
            {
                Collider2D collider = Physics2D.OverlapPoint(tilePosition);
                collider.gameObject.GetComponent<Tile>().HighlightTile();
            }
        }
    }
    #endregion
}

public class Node
{
    public Vector3 Position; // Pozycja węzła na siatce
    public int G; // Koszt dotarcia do węzła
    public int H; // Szacowany koszt dotarcia z węzła do celu
    public int F; // Całkowity koszt (G + H)
    public Node Parent; // Węzeł nadrzędny w ścieżce
}
