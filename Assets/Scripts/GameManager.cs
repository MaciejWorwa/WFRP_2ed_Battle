using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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
    public bool IsFriendlyFire;
    [SerializeField] private Button _friendlyFireButton;
    private Dictionary<Button, bool> allModes;
    public bool IsGamePaused;

    [Header("Panele")]
    public GameObject[] activePanels;
    [SerializeField] private GameObject _quitGamePanel;

    private void Start()
    {
        // Inicjalizacja słownika z wszystkimi trybami i przyciskami. Ustawienie ich początkowych wartości
        allModes = new Dictionary<Button, bool>()
        {
            {_autoDefenseButton, IsAutoDefenseMode = true},
            {_autoKillButton, IsAutoKillMode = true},
            {_friendlyFireButton, IsFriendlyFire = false}
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
                ShowPanel(_quitGamePanel);
            }
            else //Zamyka aktywne panele
            {
                foreach (GameObject panel in activePanels)
                {
                    panel.SetActive(false);
                }
            }
        }
    }

    private void ShowPanel(GameObject panel)
    {
        panel.SetActive(true);
    }

    private int CountActivePanels()
    {
        activePanels = GameObject.FindGameObjectsWithTag("Panel");

        return activePanels.Length;
    }

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

    private void UpdateButtonColor(Button button, bool condition)
    {
        if (condition)
        {
            button.GetComponent<Image>().color = Color.green;
        }
        else
        {
            button.GetComponent<Image>().color = Color.white;
        }
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}
