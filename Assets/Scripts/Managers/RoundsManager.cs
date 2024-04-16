using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using TMPro;
using UnityEngine.UI;

public class RoundsManager : MonoBehaviour
{   
    // Prywatne statyczne pole przechowujące instancję
    private static RoundsManager instance;

    // Publiczny dostęp do instancji
    public static RoundsManager Instance
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
    public static int RoundNumber;
    [SerializeField] private TMP_Text _roundNumberDisplay;
    [SerializeField] private TMP_Text _nextRoundButtonText;
    public Dictionary <Unit, int> UnitsWithActionsLeft = new Dictionary<Unit, int>();

    private void Start()
    {
        RoundNumber = 0;
        _roundNumberDisplay.text = "Zaczynamy?";
        _nextRoundButtonText.text = "Start";
    }

    public void NextRound()
    {
        RoundNumber++;
        _roundNumberDisplay.text = "Runda: " + RoundNumber;
        _nextRoundButtonText.text = "Następna runda";
 
        Debug.Log($"<color=#4dd2ff>--------------------------------------------- RUNDA {RoundNumber} ---------------------------------------------</color>");

        //Resetuje ilość dostępnych akcji dla wszystkich jednostek
        foreach (var key in UnitsWithActionsLeft.Keys.ToList())
        {
            UnitsWithActionsLeft[key] = 2;
            key.CanParry = true;
            key.CanDodge = true;
            key.CanAttack = true;
            key.GuardedAttackBonus = 0;
        }

        //Wykonuje testy grozy i strachu jeśli na polu bitwy są jednostki straszne lub przerażające
        UnitsManager.Instance.LookForScaryUnits();

        InitiativeQueueManager.Instance.UpdateInitiativeQueue();

        //Wybiera jednostkę zgodnie z kolejką inicjatywy, jeśli ten tryb jest włączony
        if (GameManager.IsAutoSelectUnitMode && InitiativeQueueManager.Instance.ActiveUnit != null)
        {
            InitiativeQueueManager.Instance.SelectUnitByQueue();
        }
    }

    // #region Initiative queue
    // public void AddUnitToInitiativeQueue(Unit unit)
    // {
    //     InitiativeQueue.Add(unit, unit.GetComponent<Stats>().Initiative);
    //     UnitsWithActionsLeft.Add(unit, 2);
    // }

    // public void RemoveUnitFromInitiativeQueue(Unit unit)
    // {
    //     InitiativeQueue.Remove(unit);
    //     UnitsWithActionsLeft.Remove(unit);
    // }

    // public void UpdateInitiativeQueue()
    // {
    //     //Sortowanie malejąco według wartości inicjatywy
    //     InitiativeQueue = InitiativeQueue.OrderByDescending(pair => pair.Value).ToDictionary(pair => pair.Key, pair => pair.Value);

    //     DisplayInitiativeQueue();
    // }

    // private void DisplayInitiativeQueue()
    // {
    //     // Resetuje wyświetlaną kolejkę, usuwając wszystkie obiekty "dzieci"
    //     Transform contentTransform = InitiativeScrollViewContent.transform;
    //     for (int i = contentTransform.childCount - 1; i >= 0; i--)
    //     {
    //         Transform child = contentTransform.GetChild(i);
    //         Destroy(child.gameObject);
    //     }

    //     _activeUnit = null;

    //     // Ustala wyświetlaną kolejkę inicjatywy
    //     foreach (var pair in InitiativeQueue)
    //     {
    //         // Dodaje jednostkę do ScrollViewContent w postaci gameObjectu jako opcja CustomDropdowna
    //         GameObject optionObj = Instantiate(_initiativeOptionPrefab, InitiativeScrollViewContent);

    //         // Odniesienie do nazwy postaci
    //         TextMeshProUGUI nameText = optionObj.transform.Find("Name_Text").GetComponent<TextMeshProUGUI>();
    //         nameText.text = pair.Key.GetComponent<Stats>().Name;

    //         // Odniesienie do wartości inicjatywy
    //         TextMeshProUGUI initiativeText = optionObj.transform.Find("Initiative_Text").GetComponent<TextMeshProUGUI>();
    //         initiativeText.text = pair.Value.ToString();

    //         //Wyróżnia postać, która obecnie wykonuje turę. Sprawdza, czy postać ma jeszcze dostępne akcje, jeśli tak to jest jej tura (po zakończeniu tury liczba dostępnych akcji spada do 0)
    //         if(UnitsWithActionsLeft[pair.Key] > 0 && _activeUnit == null)
    //         {
    //             _activeUnit = pair.Key;
    //             optionObj.GetComponent<Image>().color = new Color(0.15f, 1f, 0.45f, 0.2f);
    //         }
    //         else
    //         {
    //             optionObj.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
    //         }
    //     }
    // }

    // public void SelectUnitByQueue()
    // {
    //     StartCoroutine(InvokeSelectUnitCoroutine());
            
    //     IEnumerator InvokeSelectUnitCoroutine()
    //     {
    //         yield return new WaitForSeconds(0.1f);

    //         //Czeka ze zmianą postaci, aż obecna postać zakończy ruch
    //         while (MovementManager.Instance.IsMoving == true)
    //         {
    //             yield return null; // Czekaj na następną klatkę
    //         }

    //         DisplayInitiativeQueue();

    //         //Gdy jest aktywny tryb automatycznego wybierania postaci na podstawie kolejki inicjatywy to taka postać jest wybierana. Jeżeli wszystkie wykonały akcje to następuje kolejna runda
    //         if (GameManager.IsAutoSelectUnitMode && _activeUnit != null && _activeUnit.gameObject != Unit.SelectedUnit)
    //         {
    //             _activeUnit.SelectUnit();
    //         }
    //         else if (GameManager.IsAutoSelectUnitMode && _activeUnit == null)
    //         {
    //             NextRound();
    //         }     
    //     }
    // }

    // public void UnselectAllOptionsInInitiativeQueue()
    // {
    //     //Odznaczenie wszystkich pozostałych opcji
    //     for (int i = 0; i < InitiativeScrollViewContent.childCount; i++)
    //     {
    //         InitiativeScrollViewContent.GetChild(i).transform.Find("selected_option_background").gameObject.SetActive(false);
    //     }
    // }
    // #endregion

    #region Units actions
    public bool DoHalfAction(Unit unit)
    {
        if (UnitsWithActionsLeft.ContainsKey(unit) && UnitsWithActionsLeft[unit] >= 1)
        {
            UnitsWithActionsLeft[unit]--;

            Debug.Log($"<color=green> {unit.GetComponent<Stats>().Name} wykonał/a akcję pojedynczą. </color>");

            //Zresetowanie szarży lub biegu, jeśli były aktywne (po zużyciu jednej akcji szarża i bieg nie mogą być możliwe)
            MovementManager.Instance.UpdateMovementRange(1);

            //Aktualizuje aktywną postać na kolejce inicjatywy, gdy obecna postać wykona wszystkie akcje w tej rundzie
            if(UnitsWithActionsLeft[unit] == 0)
            {
                InitiativeQueueManager.Instance.SelectUnitByQueue();
            }
            return true;
        }
        else
        {
            Debug.Log("Ta jednostka nie może w tej rundzie wykonać więcej akcji.");
            return false;
        }     
    }

    public bool DoFullAction(Unit unit)
    {
        if (UnitsWithActionsLeft.ContainsKey(unit) && UnitsWithActionsLeft[unit] == 2)
        {
            UnitsWithActionsLeft[unit] -= 2;

            Debug.Log($"<color=green> {unit.GetComponent<Stats>().Name} wykonał/a akcję podwójną. </color>");
            
            //Aktualizuje aktywną postać na kolejce inicjatywy, bo obecna postać wykonała wszystkie akcje w tej rundzie. Wyjątkiem jest atak wielokrotny
            if(!CombatManager.Instance.AttackTypes["MultipleAttack"])
            {
                InitiativeQueueManager.Instance.SelectUnitByQueue();
            }    

            return true;
        }
        else
        {
            Debug.Log("Ta jednostka nie może w tej rundzie wykonać akcji podwójnej.");
            return false;
        }     
    }
    #endregion

    public void LoadRoundsManagerData(RoundsManagerData data)
    {
        RoundNumber = data.RoundNumber;
        
        UnitsWithActionsLeft.Clear(); // Czyści słownik przed uzupełnieniem nowymi danymi

        foreach (var entry in data.Entries)
        {
            GameObject unitObject = GameObject.Find(entry.UnitName);

            if (unitObject != null)
            {
                Unit matchedUnit = unitObject.GetComponent<Unit>();
                UnitsWithActionsLeft[matchedUnit] = entry.ActionsLeft;
            }
        }
    }
}
