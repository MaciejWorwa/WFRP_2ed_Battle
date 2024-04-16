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
    [SerializeField] private GameObject _initiativeOptionPrefab; // Prefab odpowiadający każdej jednostce na liście inicjatywy
    private Color _defaultColor = new Color(0f, 0f, 0f, 0f); // Domyślny kolor przycisku
    private Color _selectedColor = new Color(0f, 0f, 0f, 0.5f); // Kolor wybranego przycisku (zaznaczonej jednostki)
    private Color _activeColor = new Color(0.15f, 1f, 0.45f, 0.2f); // Kolor aktywnego przycisku (jednostka, której tur obecnie trwa)
    private Color _selectedActiveColor = new Color(0.08f, 0.5f, 0.22f, 0.5f); // Kolor wybranego przycisku, gdy jednocześnie jest to aktywna jednostka

    #region Initiative queue
    public void AddUnitToInitiativeQueue(Unit unit)
    {
        InitiativeQueue.Add(unit, unit.GetComponent<Stats>().Initiative);
        RoundsManager.Instance.UnitsWithActionsLeft.Add(unit, 2);
    }

    public void RemoveUnitFromInitiativeQueue(Unit unit)
    {
        InitiativeQueue.Remove(unit);
        RoundsManager.Instance.UnitsWithActionsLeft.Remove(unit);
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
        Transform contentTransform = InitiativeScrollViewContent.transform;
        for (int i = contentTransform.childCount - 1; i >= 0; i--)
        {
            Transform child = contentTransform.GetChild(i);
            Destroy(child.gameObject);
        }

        ActiveUnit = null;

        // Ustala wyświetlaną kolejkę inicjatywy
        foreach (var pair in InitiativeQueue)
        {
            // Dodaje jednostkę do ScrollViewContent w postaci gameObjectu jako opcja CustomDropdowna
            GameObject optionObj = Instantiate(_initiativeOptionPrefab, InitiativeScrollViewContent);

            // Odniesienie do nazwy postaci
            TextMeshProUGUI nameText = optionObj.transform.Find("Name_Text").GetComponent<TextMeshProUGUI>();
            nameText.text = pair.Key.GetComponent<Stats>().Name;

            // Odniesienie do wartości inicjatywy
            TextMeshProUGUI initiativeText = optionObj.transform.Find("Initiative_Text").GetComponent<TextMeshProUGUI>();
            initiativeText.text = pair.Value.ToString();

            //Wyróżnia postać, która obecnie wykonuje turę. Sprawdza, czy postać ma jeszcze dostępne akcje, jeśli tak to jest jej tura (po zakończeniu tury liczba dostępnych akcji spada do 0)
            if(RoundsManager.Instance.UnitsWithActionsLeft[pair.Key] > 0 && ActiveUnit == null)
            {
                ActiveUnit = pair.Key;
                optionObj.GetComponent<Image>().color = _activeColor;
            }
            
            //Wyróżnia postać, która jest obecnie zaznaczona
            if (Unit.SelectedUnit != null && pair.Key == Unit.SelectedUnit.GetComponent<Unit>())
            {
                optionObj.GetComponent<Image>().color = pair.Key == ActiveUnit ? _selectedActiveColor : _selectedColor;
            }
            else if (pair.Key != ActiveUnit)
            {
                optionObj.GetComponent<Image>().color = _defaultColor;
            }
        }
    }

    public void SelectUnitByQueue()
    {
        StartCoroutine(InvokeSelectUnitCoroutine());
            
        IEnumerator InvokeSelectUnitCoroutine()
        {
            yield return new WaitForSeconds(0.1f);

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
            else if (GameManager.IsAutoSelectUnitMode && ActiveUnit == null)
            {
                RoundsManager.Instance.NextRound();
            }     
        }
    }
    #endregion
}
