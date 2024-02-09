using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class MovementManager : MonoBehaviour
{
    private bool _isMoving;
    [SerializeField] private GridManager _gridManager;

    public void MoveSelectedUnit(GameObject selectedTile, GameObject unit)
    {
        // Nie pozwala wykonać akcji ruchu, dopóki poprzedni ruch nie zostanie zakończony
        if( _isMoving == true)
            return;

        // Sprawdza zasięg ruchu postaci
        int movementRange = unit.GetComponent<Stats>().TempSz;

        // Pozycja postaci przed zaczęciem wykonywania ruchu
        Vector3 startCharPos = unit.transform.position;
        
        // Aktualizuje informację o zajęciu pola, które postać opuszcza
        _gridManager.ResetTileOccupancy(startCharPos);

        // Pozycja pola wybranego jako cel ruchu
        Vector3 selectedTilePos = new Vector3(selectedTile.transform.position.x, selectedTile.transform.position.y, 0);

        // Znajdź najkrótszą ścieżkę do celu
        List<Vector3> path = FindPath(startCharPos, selectedTilePos, movementRange);

        // Sprawdza czy wybrane pole jest w zasięgu ruchu postaci. Warunek ten nie jest konieczny w przypadku automatycznej walki, dlatego dochodzi drugi warunek.
        if (path.Count > 0 && path.Count <= movementRange)
        {
            // Oznacza wybrane pole jako zajęte (gdyż trochę potrwa, zanim postać tam dojdzie i gdyby nie zaznaczyć, to można na nie ruszyć inną postacią)
            selectedTile.GetComponent<Tile>().IsOccupied = true;

            // Resetuje kolor pól w zasięgu ruchu na czas jego wykonywania
            _gridManager.ResetColorOfTilesInMovementRange();

            // Wykonuje pojedynczy ruch tyle razy ile wynosi zasięg ruchu postaci
            StartCoroutine(MoveWithDelay(unit, path, movementRange));
        }
        else
        {
            Debug.Log("Wybrane pole jest poza zasięgiem ruchu postaci.");
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

            while (elapsedTime < duration)
            {
                _isMoving = true;

                unit.transform.position = Vector3.Lerp(unit.transform.position, nextPos, elapsedTime / duration);
                elapsedTime += Time.deltaTime;
                yield return null; // Poczekaj na odświeżenie klatki animacji
            }

            unit.transform.position = nextPos;
        }

        if (unit.transform.position == path[iterations - 1])
        {
            _isMoving = false;
            _gridManager.HighlightTilesInMovementRange(Unit.SelectedUnit.GetComponent<Stats>());
        }
    }

    private List<Vector3> FindPath(Vector3 start, Vector3 goal, int movementRange)
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
                Collider2D collider = Physics2D.OverlapCircle(neighbor.Position, 0.1f);

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

    // Funkcja obliczająca odległość pomiędzy dwoma punktami na płaszczyźnie XY
    private int CalculateDistance(Vector3 a, Vector3 b)
    {
        return (int)(Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y));
    }
}

public class Node
{
    public Vector3 Position; // Pozycja węzła na siatce
    public int G; // Koszt dotarcia do węzła
    public int H; // Szacowany koszt dotarcia z węzła do celu
    public int F; // Całkowity koszt (G + H)
    public Node Parent; // Węzeł nadrzędny w ścieżce
}
