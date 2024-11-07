using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

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
    public Tile[,] Tiles;
    public static int Width = 22;
    public static int Height = 16;

    [SerializeField] private TMP_Text _widthDisplay;
    [SerializeField] private TMP_Text _heightDisplay;
    [SerializeField] private Slider _sliderX;
    [SerializeField] private Slider _sliderY;

    void Start()
    {
        GenerateGrid();

        if(SceneManager.GetActiveScene().buildIndex != 0 && MapEditor.Instance != null)
        {
            MapEditor.Instance.SetAllElementsColliders(false);
        }
        else if (MapEditor.Instance != null)
        {
            MapEditor.Instance.SetAllElementsColliders(true);
        }

        CheckTileOccupancy();
    }

    public void GenerateGrid()
    {
        //Usuwa poprzednią siatkę
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;
            Destroy(child);
        }

        Tiles = new Tile[Width, Height];
        bool isOffset;
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                Tile spawnedTile = Instantiate(_tilePrefab, new Vector3(x, y, 1), Quaternion.identity);
                spawnedTile.name = $"Tile {x} {y}";

                isOffset = (x % 2 == 0 && y % 2 != 0) || (x % 2 != 0 && y % 2 == 0);
                spawnedTile.Init(isOffset);

                Tiles[x, y] = spawnedTile;
                spawnedTile.transform.SetParent(this.transform, false); // Ustawianie rodzica bez zmiany lokalej pozycji
            }
        }

        // Przesunięcie rodzica do centrum generowanej siatki
        transform.position = new Vector3(-(Width / 2), -(Height / 2), 1);

        if (_widthDisplay != null && _heightDisplay != null)
        {
            _widthDisplay.text = Width.ToString();
            _heightDisplay.text = Height.ToString();

            int width = Width;
            int height = Height;
            _sliderX.value = width;
            _sliderY.value = height;
        }
    }

    public void ChangeGridSize()
    {
        Width = (int)_sliderX.value;
        Height = (int)_sliderY.value;

        // Generuje nową siatkę ze zmienionymi wartościami
        GenerateGrid();

        StartCoroutine(RemoveElementsOutsideTheGrid());
    }

    IEnumerator RemoveElementsOutsideTheGrid()
    {
        //Opóźnienie, żeby wartości Sliderów zdążyły się zaktualizować w przypadku uruchamiania MapEditor z poziomu BattleScene. Inaczej elementy są usuwane nim sliderY zaktualizuje swoją wartość.
        yield return new WaitForSeconds(0.02f);

        // Usuwa przeszkody poza obszarem siatki
        MapEditor.Instance.RemoveElementsOutsideTheGrid();
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
        foreach (Tile tile in Tiles)
            tile.GetComponent<Tile>().ResetRangeColor();
    }

    public void HighlightTilesInSpellArea(GameObject tileUnderCursor)
    {
        ResetColorOfTilesInMovementRange();

        Collider2D[] allColliders = Physics2D.OverlapCircleAll(tileUnderCursor.transform.position, Unit.SelectedUnit.GetComponent<Spell>().AreaSize / 2);

        foreach (var collider in allColliders)
        {
            if (collider != null && collider.gameObject.CompareTag("Tile"))
            {
                collider.GetComponent<Tile>().SetRangeColor();
            }
        }
    }

    public void CheckTileOccupancy()
    {
        foreach (Tile tile in Tiles)
        {
            Vector2 tilePosition = new Vector2(tile.transform.position.x, tile.transform.position.y);
            Collider2D hitCollider = Physics2D.OverlapCircle(tilePosition, 0.1f);

            if (hitCollider != null && !hitCollider.CompareTag("Tile"))
            {
                tile.IsOccupied = true;
            }
            else
            {
                tile.IsOccupied = false;
            }
        }
    }

    public void ResetTileOccupancy(Vector3 unitPosition)
    {
        foreach (Tile tile in Tiles)
        {
            if(unitPosition.x == tile.transform.position.x && unitPosition.y == tile.transform.position.y)
                tile.IsOccupied = false;
        }
    }

    public void LoadGridManagerData(GridManagerData data)
    {
        Width = data.Width;
        Height = data.Height;
    }
}