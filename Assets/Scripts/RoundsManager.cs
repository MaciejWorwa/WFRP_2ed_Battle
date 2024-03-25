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
    public Dictionary <Unit, int> InitiativeQueue = new Dictionary<Unit, int>();
    public Transform InitiativeScrollViewContent; // Lista ekwipunku postaci
    [SerializeField] private GameObject _initiativeOptionPrefab; // Prefab odpowiadający każdej jednostce na liście inicjatywy

    public void NextRound()
    {
        RoundNumber++;

        //Resetuje ilość dostępnych akcji dla wszystkich jednostek
        foreach (var key in InitiativeQueue.Keys.ToList())
        {
            InitiativeQueue[key] = 2;
        }

        UpdateInitiativeQueue();
    }

    public void AddUnitToInitiativeQueue(Unit unit)
    {
        InitiativeQueue.Add(unit, 2);
    }

    public void RemoveUnitFromInitiativeQueue(Unit unit)
    {
        InitiativeQueue.Remove(unit);
    }

    public void UpdateInitiativeQueue()
    {
        //Sortowanie malejąco według wartości inicjatywy
        InitiativeQueue = InitiativeQueue.OrderByDescending(pair => pair.Key.GetComponent<Stats>().Initiative).ToDictionary(pair => pair.Key, pair => pair.Value);

        DisplayInitiativeQueue();
    }

   private void DisplayInitiativeQueue()
    {
        // // Sortujemy słownik InitiativeQueue rosnąco według wartości inicjatywy
        // var sortedInitiativeQueue = InitiativeQueue.OrderBy(pair => pair.Value);

        // Resetujemy wyświetlaną kolejkę usuwając jednostki, których nie ma w słowniku
        Transform contentTransform = InitiativeScrollViewContent.transform;
        for (int i = contentTransform.childCount - 1; i >= 0; i--)
        {
            Transform child = contentTransform.GetChild(i);
            string nameText = child.transform.Find("Name_Text").GetComponent<TextMeshProUGUI>().text;

            // Jeżeli w kolejce inicjatywy nie ma postaci, która jest wypisana na przycisku, usuń przycisk
            if (!InitiativeQueue.Any(pair => pair.Key.GetComponent<Stats>().Name == nameText))
            {
                Destroy(child.gameObject);
            }
        }

        // Ustala wyświetlaną kolejkę inicjatywy
        foreach (var pair in InitiativeQueue)
        {
            Debug.Log(pair.Key.GetComponent<Stats>().Initiative);

            bool buttonExists = InitiativeScrollViewContent.GetComponentsInChildren<TextMeshProUGUI>().Any(t => t.text == pair.Key.GetComponent<Stats>().Name);

            if (!buttonExists)
            {
                // Dodaje jednostkę do ScrollViewContent w postaci gameObjectu jako opcja CustomDropdowna
                GameObject optionObj = Instantiate(_initiativeOptionPrefab, InitiativeScrollViewContent);

                // Odniesienie do nazwy postaci
                TextMeshProUGUI nameText = optionObj.transform.Find("Name_Text").GetComponent<TextMeshProUGUI>();
                nameText.text = pair.Key.GetComponent<Stats>().Name;

                // Odniesienie do wartości inicjatywy
                TextMeshProUGUI initiativeText = optionObj.transform.Find("Initiative_Text").GetComponent<TextMeshProUGUI>();
                initiativeText.text = pair.Key.GetComponent<Stats>().Initiative.ToString();
            }
        }
    }


    public bool DoHalfAction(Unit unit)
    {
        if (InitiativeQueue.ContainsKey(unit) && InitiativeQueue[unit] >= 1)
        {
            InitiativeQueue[unit]--;
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
        if (InitiativeQueue.ContainsKey(unit) && InitiativeQueue[unit] == 2)
        {
            InitiativeQueue[unit] -= 2;
            return true;
        }
        else
        {
            Debug.Log("Ta jednostka nie może w tej rundzie wykonać akcji podwójnej.");
            return false;
        }     
    }

}
