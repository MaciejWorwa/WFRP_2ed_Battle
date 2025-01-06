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
        Vector2 startCharPos = unit.transform.position;
        
        // Aktualizuje informację o zajęciu pola, które postać opuszcza
        GridManager.Instance.ResetTileOccupancy(startCharPos);

        // Pozycja pola wybranego jako cel ruchu
        Vector2 selectedTilePos = new Vector2(selectedTile.transform.position.x, selectedTile.transform.position.y);

        // Znajdź najkrótszą ścieżkę do celu
        List<Vector2> path = FindPath(startCharPos, selectedTilePos);

        // Sprawdza czy wybrane pole jest w zasięgu ruchu postaci. W przypadku automatycznej walki ten warunek nie jest wymagany.
        if (path.Count > 0 && (path.Count <= movementRange || GameManager.IsAutoCombatMode || ReinforcementLearningManager.Instance.IsLearning))
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
            if(GameManager.IsAutoCombatMode || ReinforcementLearningManager.Instance.IsLearning)
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
            Debug.Log("Wybrane pole jest poza zasięgiem ruchu lub jest zajęte.");
        }
    }

    private IEnumerator MoveWithDelay(GameObject unit, List<Vector2> path, int movementRange)
    {
        // Ogranicz iterację do mniejszej wartości: movementRange lub liczby elementów w liście path
        int iterations = Mathf.Min(movementRange, path.Count);

        for (int i = 0; i < iterations; i++)
        {
            Vector2 nextPos = path[i];

            float elapsedTime = 0f;
            float duration = 0.2f; // Czas trwania interpolacji

            while (elapsedTime < duration && unit != null && !ReinforcementLearningManager.Instance.IsLearning)
            {
                IsMoving = true;

                unit.transform.position = Vector2.Lerp(unit.transform.position, nextPos, elapsedTime / duration);
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

        if ((Vector2)unit.transform.position == path[iterations - 1])
        {
            IsMoving = false;
            Retreat(false);
            
            if(Unit.SelectedUnit != null)
            {
                GridManager.Instance.HighlightTilesInMovementRange(Unit.SelectedUnit.GetComponent<Stats>());
            }
        }

        //Zaznacza jako zajęte faktyczne pole, na którym jednostka zakończy ruch, a nie pole do którego próbowała dojść
        if(GameManager.IsAutoCombatMode || ReinforcementLearningManager.Instance.IsLearning)
        {
            AutoCombatManager.Instance.CheckForTargetTileOccupancy(unit);
        }
    }

    public List<Vector2> FindPath(Vector2 start, Vector2 goal)
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
        List<Vector2> closedNodes = new List<Vector2>();

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
                List<Vector2> path = new List<Vector2>();
                Node node = current;

                while (node.Position != start)
                {
                    path.Add(new Vector2(node.Position.x, node.Position.y));
                    node = node.Parent;
                }

                // Odwraca kolejność punktów w liście, aby uzyskać ścieżkę od początkowego do docelowego
                path.Reverse();

                return path;
            }

            // Pobiera sąsiadów bieżącego węzła
            List<Node> neighbors = new List<Node>();
            neighbors.Add(new Node { Position = current.Position + Vector2.up });
            neighbors.Add(new Node { Position = current.Position + Vector2.down });
            neighbors.Add(new Node { Position = current.Position + Vector2.left });
            neighbors.Add(new Node { Position = current.Position + Vector2.right });

            // Przetwarza każdego sąsiada
            foreach (Node neighbor in neighbors)
            {
                // Sprawdza, czy sąsiad jest w liście zamkniętych węzłów
                if (closedNodes.Contains(neighbor.Position))
                {
                    continue;
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
        return new List<Vector2>();
    }
    #endregion

    // Funkcja obliczająca odległość pomiędzy dwoma punktami na płaszczyźnie XY
    private int CalculateDistance(Vector2 a, Vector2 b)
    {
        return (int)(Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y));
    }

    #region Charge and Run modes
    public void UpdateMovementRange(int modifier, Unit unit = null)
    {
        if (Unit.SelectedUnit != null)
        {
            unit = Unit.SelectedUnit.GetComponent<Unit>();
        }

        if(unit == null) return;

        Stats stats = unit.GetComponent<Stats>();

        int actions_left = RoundsManager.Instance.UnitsWithActionsLeft[unit];

        //Jeżeli postać już jest w trybie szarży lub biegu, resetuje je
        if (unit.IsCharging && modifier == 2 || unit.IsRunning && modifier == 3)
        {
            modifier = 1;
        }

        //Sprawdza, czy jednostka może wykonać bieg lub szarże
        if ((modifier == 2 || modifier == 3) && actions_left < 2)
        {
            Debug.Log("Ta jednostka nie może w tej rundzie wykonać akcji podwójnej.");
            return;
        } 
        else if( modifier == 3 && stats.Race == "Zombie")
        {
            Debug.Log("Ta jednostka nie może wykonywać akcji biegu.");
            return;
        } 

        //Aktualizuje obecny tryb poruszania postaci
        unit.IsCharging = modifier == 2;
        unit.IsRunning = modifier == 3;

        //Zmienia typ ataku w menadżerze walki
        if(unit.IsRunning)
        {
            CombatManager.Instance.ChangeAttackType("StandardAttack"); //Resetuje szarże jako obecny typ ataku i ustawia standardowy atak
        }
        
        //Sprawdza, czy zbroja nie jest wynikiem zaklęcia Pancerz Eteru
        bool etherArmor = false;
        if(MagicManager.Instance.UnitsStatsAffectedBySpell != null && MagicManager.Instance.UnitsStatsAffectedBySpell.Count > 0)
        {
            //Przeszukanie statystyk jednostek, na które działają zaklęcia czasowe
            for (int i = 0; i < MagicManager.Instance.UnitsStatsAffectedBySpell.Count; i++)
            {
                //Jeżeli wcześniejsza wartość zbroi (w tym przypadku na głowie, ale to może być dowolna lokalizacja) jest inna niż obecna, świadczy to o użyciu Pancerzu Eteru
                if (MagicManager.Instance.UnitsStatsAffectedBySpell[i].Name == stats.Name && MagicManager.Instance.UnitsStatsAffectedBySpell[i].Armor_head != stats.Armor_head)
                {
                    etherArmor = true;
                }
            }
        }
        //Uwzględnia karę do Szybkości za zbroję płytową
        bool has_plate_armor = (stats.Armor_head >= 5 || stats.Armor_torso >= 5 || stats.Armor_arms >= 5 || stats.Armor_legs >= 5);
        bool is_sturdy = stats.Sturdy;
        int movement_armor_penalty = (has_plate_armor && !is_sturdy && !etherArmor) ? 1 : 0;

        //Oblicza obecną szybkość
        stats.TempSz = (stats.Sz - movement_armor_penalty) * modifier;

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
        _runButton.GetComponent<Image>().color = modifier == 3 ? Color.green : Color.white;   
    }
    #endregion

    #region Check for opportunity attack
    // Sprawdza czy ruch powoduje atak okazyjny
    public void CheckForOpportunityAttack(GameObject movingUnit, Vector2 selectedTilePosition)
    {
        //Przy bezpiecznym odwrocie nie występuje atak okazyjny
        if(Unit.SelectedUnit != null && Unit.SelectedUnit.GetComponent<Unit>().IsRetreating) return;

        List<Unit> adjacentOpponents = AdjacentOpponents(movingUnit.transform.position, movingUnit.tag);

        if(adjacentOpponents.Count == 0) return;

        // Atak okazyjny wywolywany dla kazdego wroga bedacego w zwarciu z bohaterem gracza
        foreach (Unit unit in adjacentOpponents)
        {
            Weapon weapon = InventoryManager.Instance.ChooseWeaponToAttack(unit.gameObject);

            //Jeżeli jest tojednostka ogłuszona, unieruchomiona, bezbronna lub jednostka z bronią dystansową to ją pomijamy
            if (weapon.Type.Contains("ranged") || unit.Trapped || unit.StunDuration > 0 || unit.HelplessDuration > 0) continue;

            // Sprawdzenie czy ruch powoduje oddalenie się od przeciwników (czyli atak okazyjny)
            float distanceFromOpponentAfterMove = Vector2.Distance(selectedTilePosition, unit.transform.position);

            if (distanceFromOpponentAfterMove > 1.8f)
            {
                Debug.Log($"Ruch spowodował atak okazyjny od {unit.GetComponent<Stats>().Name}.");

                // Wywołanie ataku okazyjnego
                CombatManager.Instance.Attack(unit, movingUnit.GetComponent<Unit>(), true);             
            }
        }
    }

    // Funkcja pomocnicza do sprawdzania jednostek w sąsiedztwie danej pozycji
    private List<Unit> AdjacentOpponents(Vector2 center, string movingUnitTag)
    {
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

        List<Unit> units = new List<Unit>();

        foreach (var pos in positions)
        {
            Collider2D collider = Physics2D.OverlapPoint(pos);
            if (collider == null || collider.GetComponent<Unit>() == null) continue;

            if (!collider.CompareTag(movingUnitTag))
            {
                units.Add(collider.GetComponent<Unit>());
            }
        }

        return units;
    }
    #endregion

    #region Highlight path
    public void HighlightPath(GameObject unit, GameObject tile)
    {
        var path = FindPath(unit.transform.position, new Vector2 (tile.transform.position.x, tile.transform.position.y));

        if(path.Count <= unit.GetComponent<Stats>().TempSz)
        {
            foreach (Vector2 tilePosition in path)
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
    public Vector2 Position; // Pozycja węzła na siatce
    public int G; // Koszt dotarcia do węzła
    public int H; // Szacowany koszt dotarcia z węzła do celu
    public int F; // Całkowity koszt (G + H)
    public Node Parent; // Węzeł nadrzędny w ścieżce
}