using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GridManager : MonoBehaviour
{
    // Prywatne statyczne pole przechowujące instancję
    private static GridManager instance;

    // Publiczny dostęp do instancji
    public static GridManager Instance
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

    [SerializeField] private Tile _tilePrefab;
    private Tile[,] _tiles;
    [SerializeField] private int _width;
    public int Width 
    {
        get { return _width; }
        set { _width = value; }
    }
    [SerializeField] private int _height;
    public int Height
    {
        get { return _height; }
        set { _height = value; }
    }
    public static GridManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Zapobiega niszczeniu przy zmianie sceny
        }
        else if (Instance != this)
        {
            Destroy(gameObject); // Zniszczenie nadmiarowej instancji
        }
    }

    void Start()
    {
        GenerateGrid();
    }

    public void GenerateGrid()
    {
        _tiles = new Tile[_width, _height];
        bool isOffset;
        for (int x = 0; x < _width; x++)
        {
            for (int y = 0; y < _height; y++)
            { 
                Tile spawnedTile = Instantiate(_tilePrefab, new Vector3(x, y, 1), Quaternion.identity);
                spawnedTile.name = $"Tile {x} {y}";

                isOffset = (x % 2 == 0 && y % 2 != 0) || (x % 2 != 0 && y % 2 == 0);
                spawnedTile.Init(isOffset);

                _tiles[x, y] = spawnedTile;
            }
        }

        foreach (Tile tile in _tiles)
        {
            tile.transform.parent = this.gameObject.transform;
        }

        transform.position = new Vector3(-(_width / 2), -(_height / 2), 1);
    }

    public void HighlightTilesInMovementRange(Stats unitStats)
    {
        ResetColorOfTilesInMovementRange();

        List <GameObject> objectsInMovementRange = new List<GameObject>();

        // Sprawdza zasieg ruchu postaci
        int movementRange = unitStats.TempSz;

        if (movementRange == 0) return;
            
        // Wrzucajac do listy postac, dodajemy punkt początkowy, który jest potrzebny do późniejszej petli wyszukującej dostępne pozycje
        objectsInMovementRange.Add(unitStats.gameObject);

        // wektor w prawo, lewo, góra, dół
        Vector3[] directions = { Vector3.right, Vector3.left, Vector3.up, Vector3.down };

        // Wykonuje pojedynczy ruch tyle razy ile wynosi zasieg ruchu postaci
        for (int i = 0; i < movementRange; i++)
        {
            // Lista pol, ktore bedziemy dodawac do listy wszystkich pol w zasiegu ruchu
            List<GameObject> tilesToAdd = new List<GameObject>();

            foreach (var obj in objectsInMovementRange)
            {
                // Szuka pol w każdym kierunku
                foreach (Vector3 direction in directions)
                {
                    // Szuka colliderów w czterech kierunkach
                    Collider2D[] colliders = Physics2D.OverlapCircleAll(obj.transform.position + direction, 0.1f);

                    // Jeżeli collider to 'Tile' to dodajemy go do listy
                    if (colliders != null && colliders.Length == 1 && colliders[0].gameObject.CompareTag("Tile"))
                    {
                        tilesToAdd.Add(colliders[0].gameObject);
                    }
                }
            }
            // Dodajemy do listy wszystkie pola, ktorych tam jeszcze nie ma
            foreach (var tile in tilesToAdd)
            {
                if(!objectsInMovementRange.Contains(tile))
                    objectsInMovementRange.Add(tile);
            }

            // Usuwamy postac z listy, bo nie jest ona 'Tile' :)
            objectsInMovementRange.Remove(unitStats.gameObject);
        }

        foreach (var tile in objectsInMovementRange)
        {
            tile.GetComponent<Tile>().SetRangeColor();
        }
    }

    public void ResetColorOfTilesInMovementRange()
    {
        // Resetuje wszystkie pola
        foreach (Tile tile in _tiles)
            tile.GetComponent<Tile>().ResetRangeColor();
    }

    public void ResetTileOccupancy(Vector3 unitPosition)
    {
        foreach (Tile tile in _tiles)
        {
            if(unitPosition.x == tile.transform.position.x && unitPosition.y == tile.transform.position.y)
                tile.IsOccupied = false;
        }
    }
}