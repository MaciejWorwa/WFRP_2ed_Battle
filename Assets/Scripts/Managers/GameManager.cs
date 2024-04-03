using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class GameManager : MonoBehaviour
{
    // Prywatne statyczne pole przechowujące instancję
    private static GameManager instance;

    // Publiczny dostęp do instancji
    public static GameManager Instance
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

    [Header("Tryby gry")]
    public bool IsAutoDefenseMode;
    [SerializeField] private Button _autoDefenseButton;
    public bool IsAutoKillMode;
    [SerializeField] private Button _autoKillButton;
    public bool IsAutoSelectUnitMode;
    [SerializeField] private Button _autoSelectUnitButton;
    public bool IsFriendlyFire;
    [SerializeField] private Button _friendlyFireButton;
    private Dictionary<Button, bool> allModes;
    public bool IsGamePaused;

    [Header("Panele")]
    public GameObject[] activePanels;
    [SerializeField] private GameObject _mainMenuPanel;

    private void Start()
    {
        // Inicjalizacja słownika z wszystkimi trybami i przyciskami. Ustawienie ich początkowych wartości
        allModes = new Dictionary<Button, bool>()
        {
            {_autoDefenseButton, IsAutoDefenseMode = true},
            {_autoSelectUnitButton, IsAutoSelectUnitMode = true},
            {_autoKillButton, IsAutoKillMode = true},
            {_friendlyFireButton, IsFriendlyFire = false},
        };

        // Ustawia kolory przycisków na podstawie początkowych wartości trybów
        foreach (var pair in allModes)
        {
            UpdateButtonColor(pair.Key, pair.Value);
        }
    }

    private void Update()
    {
        //Pauzuje grę (możliwość ruchu jednostek), gdy któryś z paneli konkretnej jednostki jest otwarty
        IsGamePaused = CountActivePanels() > 0 ? true : false;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            int activePanelsLength = CountActivePanels();

            if(activePanelsLength == 0) //Otwiera panel wyjścia z gry
            {
                ShowPanel(_mainMenuPanel);
            }
            else //Zamyka aktywne panele
            {
                HideActivePanels();
            }
        }
    }

    public void ShowPanel(GameObject panel)
    {
        panel.SetActive(true);
    }

    public void ShowOrHidePanel(GameObject panel)
    {
        //Gdy panel jest zamknięty to go otwiera, a gdy otwarty to go zamyka
        panel.SetActive(!panel.activeSelf);
    }

    private int CountActivePanels()
    {
        activePanels = GameObject.FindGameObjectsWithTag("Panel");

        return activePanels.Length;
    }
    public void HideActivePanels()
    {
        foreach (GameObject panel in activePanels)
        {
            panel.SetActive(false);
        }
    }
    public bool IsPointerOverPanel()
    {
        // Tworzenie promienia od pozycji myszki
        PointerEventData eventDataCurrentPosition = new PointerEventData(EventSystem.current);
        eventDataCurrentPosition.position = new Vector2(Input.mousePosition.x, Input.mousePosition.y);

        List<RaycastResult> results = new List<RaycastResult>();
         // Wykonywanie promieniowania i dodawanie wyników do listy
        EventSystem.current.RaycastAll(eventDataCurrentPosition, results);

        foreach (RaycastResult result in results)
        {
            if (result.gameObject.CompareTag("Panel") || result.gameObject.CompareTag("SidePanel"))
            {
                return true;
            }
        }

        return false;
    }

    #region Game modes
    public void SetAutoDefenseMode()
    {
        IsAutoDefenseMode = !IsAutoDefenseMode;

        UpdateButtonColor(_autoDefenseButton, IsAutoDefenseMode);

        if (IsAutoDefenseMode)
        {
            Debug.Log("Tryb automatycznej obrony został włączony. Jednostki będą automatycznie podejmować próby parowania lub unikania ataków.");
        }
        else
        {
            Debug.Log("Tryb automatycznej obrony został wyłączony.");
        }
    }

    public void SetAutoKillMode()
    {
        IsAutoKillMode = !IsAutoKillMode;

        UpdateButtonColor(_autoKillButton, IsAutoKillMode);

        if (IsAutoKillMode)
        {
            Debug.Log("Tryb automatycznej śmierci (gdy żywotność spadnie poniżej zera) został włączony.");
        }
        else
        {
            Debug.Log("Tryb automatycznej śmierci (gdy żywotność spadnie poniżej zera) został wyłączony.");
        }
    }

    public void SetAutoSelectUnitMode()
    {
        IsAutoSelectUnitMode = !IsAutoSelectUnitMode;

        UpdateButtonColor(_autoSelectUnitButton, IsAutoSelectUnitMode);

        if (IsAutoSelectUnitMode)
        {
            Debug.Log("Tryb automatycznego wyboru jednostki zgodnie z kolejką inicjatywy został włączony.");
        }
        else
        {
            Debug.Log("Tryb automatycznego wyboru jednostki zgodnie z kolejką inicjatywy został wyłączony.");
        }
    }

    public void SetFriendlyFireMode()
    {
        IsFriendlyFire = !IsFriendlyFire;

        UpdateButtonColor(_friendlyFireButton, IsFriendlyFire);

        if (IsFriendlyFire)
        {
            Debug.Log("Friendly fire został włączony.");
        }
        else
        {
            Debug.Log("Friendly fire został wyłączony.");
        }
    }
    #endregion

    private void UpdateButtonColor(Button button, bool condition)
    {
        if (condition)
        {
            button.GetComponent<Image>().color = new Color(0.15f, 1f, 0.45f);
        }
        else
        {
            button.GetComponent<Image>().color = Color.white;
        }
    }

    public bool IsAnyInputFieldFocused()
    {
        TMP_InputField[] inputFields = FindObjectsByType<TMP_InputField>(FindObjectsSortMode.None);

        if(inputFields.Length < 1) return false;

        foreach (TMP_InputField inputField in inputFields)
        {
            if (inputField.isFocused) return true; // Zwraca true, jeśli którykolwiek z input fields ma focus
        }
        return false; // Zwraca false, jeśli żaden z input fields nie ma focus
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}
