using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using SimpleFileBrowser;
using UnityEngine.SceneManagement;
using System.IO;

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
    public static bool IsAutoDiceRollingMode = false;
    [SerializeField] private Button _autoDiceRollingButton;
    public static bool IsAutoDefenseMode = false;
    [SerializeField] private Button _autoDefenseButton;
    public static bool IsAutoKillMode = false;
    [SerializeField] private Button _autoKillButton;
    public static bool IsAutoSelectUnitMode = true;
    [SerializeField] private Button _autoSelectUnitButton;
    public static bool IsFriendlyFire = false;
    [SerializeField] private Button _friendlyFireButton;
    public static bool IsFearIncluded = true;
    [SerializeField] private Button _fearIncludedButton;
    public static bool IsAutoCombatMode = false;
    [SerializeField] private Button _autoCombatButton;
    public static bool IsMapHidingMode = false;
    [SerializeField] private Button _mapCoverButton;
    [SerializeField] private Button _mapUncoverButton;
    public static bool IsStatsHidingMode = true;
    [SerializeField] private Button _statsHidingButton;
    public static bool IsNamesHidingMode = true;
    [SerializeField] private Button _healthPointsHidingButton;
    public static bool IsHealthPointsHidingMode = true;
    [SerializeField] private Button _namesHidingButton;
    private Dictionary<Button, bool> allModes;
    public static bool IsGamePaused;

    [Header("Edytor map")]
    public static bool IsMapElementPlacing;
    public static bool IsMousePressed;
    public string TileCoveringState; //Zmienna przekazująca informacja o tym, czy aktualnie zasłaniamy pola, czy odsłaniamy

    [Header("Panele")]
    public GameObject[] activePanels;
    [SerializeField] private GameObject _mainMenuPanel;
    [SerializeField] private GameObject _tileCoveringPanel; //Panel z informacją o trybie ukrywania mapy

    private void Start()
    {
        if (SceneManager.GetActiveScene().buildIndex == 0)
        {
            SaveAndLoadManager.Instance.LoadSettings();
        }

        // Inicjalizacja słownika z wszystkimi trybami i przyciskami. Ustawienie ich początkowych wartości
        allModes = new Dictionary<Button, bool>()
        {
            {_autoDefenseButton, IsAutoDefenseMode},
            {_autoSelectUnitButton, IsAutoSelectUnitMode},
            {_autoKillButton, IsAutoKillMode},
            {_friendlyFireButton, IsFriendlyFire},
            {_autoDiceRollingButton, IsAutoDiceRollingMode},
            {_autoCombatButton, IsAutoCombatMode},
            {_fearIncludedButton, IsFearIncluded},
            {_mapCoverButton, TileCoveringState == "covering"},
            {_mapUncoverButton, TileCoveringState == "uncovering"},
            {_namesHidingButton, IsNamesHidingMode},
            {_statsHidingButton, IsStatsHidingMode},
            {_healthPointsHidingButton, IsHealthPointsHidingMode}
        };

        // Ustawia kolory przycisków na podstawie początkowych wartości trybów
        foreach (var pair in allModes)
        {
            UpdateButtonColor(pair.Key, pair.Value);
        }

        if (Display.displays.Length > 1)
        {
            IsStatsHidingMode = false;
        }
        else
        {
            IsStatsHidingMode = true;
        }
    }

    private void Update()
    {
        //Pauzuje grę, gdy jest otwarteokno wczytywania plików
        IsGamePaused = FileBrowser.IsOpen? true : false;

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

        // Sprawdza, czy lewy przycisk myszy jest przytrzymany
        if (Input.GetMouseButtonDown(0))
        {
            IsMousePressed = true;
        }
        else if (Input.GetMouseButtonUp(0))
        {
            IsMousePressed = false;
        }

        //Przełączenie na tryb pełnoekranowy lub okno
        if (Input.GetKeyDown(KeyCode.F11))
        {
            ToggleFullscreen();
        }

        // Sprawdzenie, czy wciśnięto Ctrl lub Command (dla macOS)
        if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) || Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand)) && !IsAnyInputFieldFocused())
        {
            if (Input.GetKeyDown(KeyCode.Q))
            {
                if(TileCoveringState != "uncovering")
                {
                    SetCoveringState("covering");
                }

                SetMapHidingMode();
            }
            else if(Input.GetKeyDown(KeyCode.A))
            {
                SetAutoCombatMode();
            }
            else if(Input.GetKeyDown(KeyCode.T))
            {
                SetFearIncludedMode();
            }
            else if(Input.GetKeyDown(KeyCode.D))
            {
                SetAutoDefenseMode();
            }
            else if(Input.GetKeyDown(KeyCode.R))
            {
                SetAutoRollingDiceMode();
            }
            else if(Input.GetKeyDown(KeyCode.S))
            {
                SetAutoSelectUnitMode();
            }
            else if(Input.GetKeyDown(KeyCode.K))
            {
                SetAutoKillMode();
            }     
            else if(Input.GetKeyDown(KeyCode.F))
            {
                SetFriendlyFireMode();
            }  
            else if(Input.GetKeyDown(KeyCode.N))
            {
                SetNamesHidingMode();
            }
            else if(Input.GetKeyDown(KeyCode.H))
            {
                SetHealthPointsHidingMode();
            }     
            else if(Input.GetKeyDown(KeyCode.I))
            {
                SetStatsHidingMode();
            }  
            else if(Input.GetKeyDown(KeyCode.C)) //Kopiuje jednostki do schowka
            {  
                if(AreaSelector.Instance.SelectedUnits.Count > 1) //Kopiuje wszystkie zaznaczone jednostki
                {
                    SaveAndLoadManager.Instance.SaveUnits(AreaSelector.Instance.SelectedUnits, "temp");
                }
                else if(Unit.SelectedUnit != null) //Gdy jest zaznaczona tylko jedna jednostka, to kopiuje tylko ją
                {
                    List <Unit> selectedUnit = new List <Unit>();
                    selectedUnit.Add(Unit.SelectedUnit.GetComponent<Unit>());

                    SaveAndLoadManager.Instance.SaveUnits(selectedUnit, "temp");
                }
            }
            else if(Input.GetKeyDown(KeyCode.V)) //Wkleja jednostki ze schowka
            {   
                SaveAndLoadManager.Instance.IsLoading = true;      
                string saveFolderPath = Path.Combine(Application.persistentDataPath, "temp");

                if(!Directory.Exists(saveFolderPath)) return;

                StartCoroutine(SaveAndLoadManager.Instance.LoadAllUnitsWithDelay(saveFolderPath));
            }

        }
    }

    public void ChangeScene(int index)
    {
        SceneManager.LoadScene(index, LoadSceneMode.Single);
    }

    // Przełącza między trybem pełnoekranowym a oknem
    public void ToggleFullscreen()
    {
        if (Screen.fullScreen)
        {
            Screen.fullScreenMode = FullScreenMode.Windowed;
            Screen.fullScreen = false;
        }
        else
        {
            Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
            Screen.fullScreen = true;
        }
    }

    #region UI panels
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
        CountActivePanels();
        
        foreach (GameObject panel in activePanels)
        {
            panel.SetActive(false);
        }
    }
    public bool IsPointerOverUI()
    {
        // Tworzenie promienia od pozycji myszki
        PointerEventData eventDataCurrentPosition = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition
        };

        List<RaycastResult> results = new List<RaycastResult>();
        // Wykonywanie promieniowania i dodawanie wyników do listy
        EventSystem.current.RaycastAll(eventDataCurrentPosition, results);

        foreach (RaycastResult result in results)
        {
            // Sprawdzanie, czy warstwa obiektu to UI
            if (result.gameObject.layer == LayerMask.NameToLayer("UI"))
            {
                return true;
            }
        }

        return false;
    }
    #endregion

    #region Game modes
    public void SetAutoCombatMode()
    {
        if(!RoundsManager.Instance.NextRoundButton.gameObject.activeSelf)
        {
            Debug.Log("Nie możesz teraz zmienić trybu. Poczekaj aż wszystkie jednostki skończą swoje akcje.");
            return;
        }

        IsAutoCombatMode = !IsAutoCombatMode;

        UpdateButtonColor(_autoCombatButton, IsAutoCombatMode);

        if (IsAutoCombatMode)
        {
            Debug.Log("Tryb automatycznej walki został włączony. Wszystkie akcje będą wykonywane automatycznie.");

            // Upewnia się, że inne tryby powiązane z automatyczną walką są aktywne
            if (!IsAutoDefenseMode) SetAutoDefenseMode();
            if (!IsAutoDiceRollingMode) SetAutoRollingDiceMode();
            if (!IsAutoKillMode) SetAutoKillMode();
            if (!IsAutoSelectUnitMode) SetAutoSelectUnitMode();

            IsAutoDiceRollingMode = true;

            UpdateButtonColor(_autoDiceRollingButton, IsAutoDiceRollingMode);
        }
        else
        {
            Debug.Log("Tryb automatycznej walki został wyłączony. Wszystkie akcje będą wykonywane ręcznie.");
        }
    }
    public void SetAutoRollingDiceMode()
    {
        if(IsAutoCombatMode && IsAutoDiceRollingMode == true)
        {
            Debug.Log("Ten tryb jest wymagany podczas automatycznej walki. Jeśli chcesz go wyłączyć, wyłącz automatyczną walkę.");
            return;
        }

        IsAutoDiceRollingMode = !IsAutoDiceRollingMode;

        UpdateButtonColor(_autoDiceRollingButton, IsAutoDiceRollingMode);

        if (IsAutoDiceRollingMode)
        {
            Debug.Log("Tryb automatycznego rzutu koścmi został włączony. Wszystkie rzuty będą wykonywane automatycznie.");
        }
        else
        {
            Debug.Log("Tryb automatycznego rzutu koścmi został wyłączony. Rzuty koścmi wykonywane przez graczy są rozstrzygane poza aplikacją.");
        }
    }
    public void SetAutoDefenseMode()
    {
        if (IsAutoCombatMode && IsAutoDefenseMode == true)
        {
            Debug.Log("Ten tryb jest wymagany podczas automatycznej walki. Jeśli chcesz go wyłączyć, wyłącz automatyczną walkę.");
            return;
        }

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
        if (IsAutoCombatMode && IsAutoKillMode == true)
        {
            Debug.Log("Ten tryb jest wymagany podczas automatycznej walki. Jeśli chcesz go wyłączyć, wyłącz automatyczną walkę.");
            return;
        }

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
        if (IsAutoCombatMode && IsAutoSelectUnitMode == true)
        {
            Debug.Log("Ten tryb jest wymagany podczas automatycznej walki. Jeśli chcesz go wyłączyć, wyłącz automatyczną walkę.");
            return;
        }

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
            Debug.Log("Możliwość atakowania sojuszników została włączona.");
        }
        else
        {
            Debug.Log("Możliwość atakowania sojuszników została wyłączona.");
        }
    }

    public void SetFearIncludedMode()
    {
        IsFearIncluded = !IsFearIncluded;

        UpdateButtonColor(_fearIncludedButton, IsFearIncluded);

        if (IsFearIncluded)
        {
            Debug.Log("Tryb uwzględniający Strach i Grozę został włączony.");
        }
        else
        {
            Debug.Log("Tryb uwzględniający Strach i Grozę został wyłączony.");
        }
    }

    public void SetMapHidingMode(bool isDisabled = false)
    {
        // Jeśli tryb mapy jest już aktywny i kliknięto aktualnie aktywny przycisk, wyłącz tryb mapy
        if (IsMapHidingMode && ((TileCoveringState == "covering" && _mapCoverButton.GetComponent<Image>().color != Color.white) || (TileCoveringState == "uncovering" && _mapUncoverButton.GetComponent<Image>().color != Color.white)))
        {
            IsMapHidingMode = false;
            UpdateButtonColor(_mapCoverButton, false);
            UpdateButtonColor(_mapUncoverButton, false);

            if(!isDisabled)
            {
                Debug.Log("Tryb ukrywania obszarów mapy został wyłączony.");
            }

            if (SceneManager.GetActiveScene().buildIndex != 0)
            {
                _tileCoveringPanel.SetActive(false);
            }
            return;
        }

        IsMapHidingMode = !isDisabled;

        UpdateButtonColor(_mapCoverButton, TileCoveringState == "covering");
        UpdateButtonColor(_mapUncoverButton, TileCoveringState == "uncovering");

        if (IsMapHidingMode)
        {
            //Debug.Log("Tryb ukrywania obszarów mapy został włączony. Zaznacz przy użyciu LPM pola, które chcesz zakryć lub odsłonić.");

            //Odznacza jednostkę jeśli jest zaznaczona
            if(Unit.SelectedUnit != null)
            {
                Unit.SelectedUnit.GetComponent<Unit>().SelectUnit();
            }

            //Poza sceną edytora map wyświetlamy panel informujący o aktywacji ukrywania mapy
            if(SceneManager.GetActiveScene().buildIndex != 0)
            {
                _tileCoveringPanel.SetActive(true);
            }
        }
        else
        {
            //Debug.Log("Tryb ukrywania obszarów mapy został wyłączony.");

            TileCoveringState = null;

            if(SceneManager.GetActiveScene().buildIndex != 0)
            {
                _tileCoveringPanel.SetActive(false);
            }
        }
    }

    public void SetCoveringState(string state)
    {
        TileCoveringState = state;
    }

    public void SetHealthPointsHidingMode()
    {
        IsHealthPointsHidingMode = !IsHealthPointsHidingMode;

        UpdateButtonColor(_healthPointsHidingButton, IsHealthPointsHidingMode);

        if(SceneManager.GetActiveScene().buildIndex == 0) return;

        if (IsHealthPointsHidingMode)
        {
            foreach(var unit in UnitsManager.Instance.AllUnits)
            {
                unit.HideUnitHealthPoints();
            }
            Debug.Log("Punkty żywotności na tokenach jednostek zostały ukryte.");
        }
        else
        {
            foreach(var unit in UnitsManager.Instance.AllUnits)
            {
                if (IsStatsHidingMode && unit.gameObject.CompareTag("EnemyUnit")) continue;
                unit.DisplayUnitHealthPoints();
            }
            Debug.Log("Punkty żywotności na tokenach jednostek zostały ujawnione.");
        }
    }

    public void SetStatsHidingMode()
    {
        IsStatsHidingMode = !IsStatsHidingMode;

        UpdateButtonColor(_statsHidingButton, IsStatsHidingMode);

        if(SceneManager.GetActiveScene().buildIndex == 0) return;

        if (IsStatsHidingMode)
        {
            foreach(var unit in UnitsManager.Instance.AllUnits)
            {
                if(unit.gameObject.CompareTag("EnemyUnit"))
                {
                    unit.HideUnitHealthPoints();
                }
            }
            Debug.Log("Panel ze statystykami przeciwników został ukryty.");
        }
        else
        {
            foreach(var unit in UnitsManager.Instance.AllUnits)
            {
                if(unit.gameObject.CompareTag("EnemyUnit") && !IsHealthPointsHidingMode)
                {
                    unit.DisplayUnitHealthPoints();
                }
            }
            Debug.Log("Panel ze statystykami przeciwników został ujawniony.");
        }

        if(Unit.SelectedUnit != null)
        {
            UnitsManager.Instance.UpdateUnitPanel(Unit.SelectedUnit);
        }
    }

    public void SetNamesHidingMode()
    {
        IsNamesHidingMode = !IsNamesHidingMode;

        UpdateButtonColor(_namesHidingButton, IsNamesHidingMode);

        if(SceneManager.GetActiveScene().buildIndex == 0) return;

        if (IsNamesHidingMode)
        {
            foreach(var unit in UnitsManager.Instance.AllUnits)
            {
                unit.HideUnitName();
            }
            Debug.Log("Imiona i nazwy jednostek zostały ukryte.");
        }
        else
        {
            foreach(var unit in UnitsManager.Instance.AllUnits)
            {
                unit.DisplayUnitName();
            }
            Debug.Log("Imiona i nazwy jednostek zostały ujawnione.");
        }

        if(Unit.SelectedUnit != null)
        {
            UnitsManager.Instance.UpdateUnitPanel(Unit.SelectedUnit);
        }
    }
    #endregion

    private void UpdateButtonColor(Button button, bool condition)
    {
        if (condition)
        {
            button.GetComponent<Image>().color = new Color(0.15f, 1f, 0.45f);

            //button.GetComponent<Image>().color = new Color(0f, 0.82f, 1f);
            
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
        SaveAndLoadManager.Instance.SaveSettings();
        Application.Quit();
    }
}
