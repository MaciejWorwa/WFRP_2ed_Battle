using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Linq;

public class InitiativeQueueManager : MonoBehaviour
{
    // Prywatne statyczne pole przechowujące instancję
    private static InitiativeQueueManager instance;

    // Publiczny dostęp do instancji
    public static InitiativeQueueManager Instance
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
    public Dictionary <Unit, int> InitiativeQueue = new Dictionary<Unit, int>();
    public Unit ActiveUnit;
    public Transform InitiativeScrollViewContent;
    public Transform PlayersCamera_InitiativeScrollViewContent;
    [SerializeField] private GameObject _initiativeOptionPrefab; // Prefab odpowiadający każdej jednostce na liście inicjatywy
    private Color _defaultColor = new Color(0f, 0f, 0f, 0f); // Domyślny kolor przycisku
    private Color _selectedColor = new Color(0f, 0f, 0f, 0.5f); // Kolor wybranego przycisku (zaznaczonej jednostki)
    private Color _activeColor = new Color(0.15f, 1f, 0.45f, 0.2f); // Kolor aktywnego przycisku (jednostka, której tura obecnie trwa)
    private Color _selectedActiveColor = new Color(0.08f, 0.5f, 0.22f, 0.5f); // Kolor wybranego przycisku, gdy jednocześnie jest to aktywna jednostka
    [SerializeField] private UnityEngine.UI.Slider _advantageBar; // Pasek przewagi sił w bitwie
    

    #region Initiative queue
    public void AddUnitToInitiativeQueue(Unit unit)
    {
        //Nie dodaje do kolejki inicjatywy jednostek, które są ukryte
        Collider2D collider = Physics2D.OverlapPoint(unit.gameObject.transform.position);
        if(collider.CompareTag("TileCover")) return;

        InitiativeQueue.Add(unit, unit.GetComponent<Stats>().Initiative);
        if(!RoundsManager.Instance.UnitsWithActionsLeft.ContainsKey(unit))
        {
            RoundsManager.Instance.UnitsWithActionsLeft.Add(unit, 2);
        }

        //Aktualizuje pasek przewagi w bitwie
        unit.GetComponent<Stats>().Overall = unit.GetComponent<Stats>().CalculateOverall();

        CalculateAdvantage(unit.GetComponent<Stats>().Overall, 0, unit.tag);
    }

    public void RemoveUnitFromInitiativeQueue(Unit unit)
    {
        InitiativeQueue.Remove(unit);
        RoundsManager.Instance.UnitsWithActionsLeft.Remove(unit);

        //Aktualizuje pasek przewagi w bitwie
        unit.GetComponent<Stats>().Overall = unit.GetComponent<Stats>().CalculateOverall();
        CalculateAdvantage(0, unit.GetComponent<Stats>().Overall, unit.tag);
    }

    public void UpdateInitiativeQueue()
    {
        //Sortowanie malejąco według wartości inicjatywy
        InitiativeQueue = InitiativeQueue.OrderByDescending(pair => pair.Value).ToDictionary(pair => pair.Key, pair => pair.Value);

        DisplayInitiativeQueue();
    }

    private void DisplayInitiativeQueue()
    {
        // Resetuje wyświetlaną kolejkę, usuwając wszystkie obiekty "dzieci"
        ResetScrollViewContent(InitiativeScrollViewContent);
        ResetScrollViewContent(PlayersCamera_InitiativeScrollViewContent);

        ActiveUnit = null;

        // Ustala wyświetlaną kolejkę inicjatywy
        foreach (var pair in InitiativeQueue)
        {
            // Dodaje jednostkę do głównej kolejki ScrollViewContent
            GameObject optionObj = CreateInitiativeOption(pair, InitiativeScrollViewContent, false);

            // Dodaje jednostkę do Players kolejki ScrollViewContent
            GameObject playersOptionObj = CreateInitiativeOption(pair, PlayersCamera_InitiativeScrollViewContent, true);

            // Sprawdza, czy jest aktywna tura dla tej jednostki
            if (RoundsManager.Instance.UnitsWithActionsLeft[pair.Key] > 0 && ActiveUnit == null && pair.Key.IsTurnFinished != true)
            {
                ActiveUnit = pair.Key;
                SetOptionColor(optionObj, _activeColor);
                SetOptionColor(playersOptionObj, _activeColor);
            }

            // Wyróżnia zaznaczoną jednostkę
            if (Unit.SelectedUnit != null && pair.Key == Unit.SelectedUnit.GetComponent<Unit>())
            {
                Color selectedColor = pair.Key == ActiveUnit ? _selectedActiveColor : _selectedColor;
                SetOptionColor(optionObj, selectedColor);
                SetOptionColor(playersOptionObj, selectedColor);
            }
            else if (pair.Key != ActiveUnit)
            {
                SetOptionColor(optionObj, _defaultColor);
                SetOptionColor(playersOptionObj, _defaultColor);
            }
        }
    }

    private void ResetScrollViewContent(Transform scrollViewContent)
    {
        for (int i = scrollViewContent.childCount - 1; i >= 0; i--)
        {
            Transform child = scrollViewContent.GetChild(i);
            Destroy(child.gameObject);
        }
    }

    private GameObject CreateInitiativeOption(KeyValuePair<Unit, int> pair, Transform scrollViewContent, bool IsPlayersCamera_InitiativeQueue)
    {
        GameObject optionObj = Instantiate(_initiativeOptionPrefab, scrollViewContent);

        // Odniesienie do nazwy postaci
        TextMeshProUGUI nameText = optionObj.transform.Find("Name_Text").GetComponent<TextMeshProUGUI>();
        nameText.text = pair.Key.GetComponent<Stats>().Name;

        // Odniesienie do wartości inicjatywy
        TextMeshProUGUI initiativeText = optionObj.transform.Find("Initiative_Text").GetComponent<TextMeshProUGUI>();
        initiativeText.text = pair.Value.ToString();

        return optionObj;
    }

    private void SetOptionColor(GameObject optionObj, Color color)
    {
        optionObj.GetComponent<Image>().color = color;
    }

    public void SelectUnitByQueue()
    {
        StartCoroutine(InvokeSelectUnitCoroutine());
            
        IEnumerator InvokeSelectUnitCoroutine()
        {
            yield return new WaitForSeconds(0.05f);

            //Czeka ze zmianą postaci, aż obecna postać zakończy ruch
            while (MovementManager.Instance.IsMoving == true)
            {
                yield return null; // Czekaj na następną klatkę
            }

            DisplayInitiativeQueue();

            //Gdy jest aktywny tryb automatycznego wybierania postaci na podstawie kolejki inicjatywy to taka postać jest wybierana. Jeżeli wszystkie wykonały akcje to następuje kolejna runda
            if (GameManager.IsAutoSelectUnitMode && ActiveUnit != null && ActiveUnit.gameObject != Unit.SelectedUnit)
            {
                ActiveUnit.SelectUnit();
            }
            else if (GameManager.IsAutoSelectUnitMode && ActiveUnit == null && !GameManager.IsAutoCombatMode || GameManager.IsStatsHidingMode && ActiveUnit == null)
            {
                RoundsManager.Instance.NextRound();
            }

            //Jeżeli wybrana postać jest unieruchomiona to wykonuje próbę uwolnienia się (bo to jedyne, co może w tej rundzie zrobić)
            if(ActiveUnit != null && ActiveUnit.Trapped == true)
            {
                CombatManager.Instance.EscapeFromTheSnare(ActiveUnit);
            }     
        }
    }
    #endregion
    
    public void CalculateAdvantage(int addedOverall, int substractedOverall, string unitTag)
    {
        // Aktualizacja maksymalnej wartości przewagi
        if(_advantageBar.maxValue == 1) // Początkowa wartość Slidera (nie da sie ustawić na 0)
        { 
            _advantageBar.maxValue = addedOverall;
        }
        else
        {
            _advantageBar.maxValue += addedOverall - substractedOverall;
        }

        // Aktualizacja wartości przewagi gracza (tylko jeśli jednostka należy do gracza)
        if (unitTag == "PlayerUnit")
        {
            _advantageBar.value += addedOverall - substractedOverall;
        }

        // Aktywacja paska, jeśli ma sens go wyświetlić
        if (_advantageBar.maxValue > 1 && !_advantageBar.gameObject.activeSelf)
        {
            _advantageBar.gameObject.SetActive(true);
        }
    }
}
