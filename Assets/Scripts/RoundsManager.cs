using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

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

    public void NextRound()
    {
        RoundNumber++;

        //Resetuje ilość dostępnych akcji dla wszystkich jednostek
        foreach (var key in InitiativeQueue.Keys.ToList())
        {
            InitiativeQueue[key] = 2;
        }
    }

    public void AddUnitToInitiativeQueue(Unit unit)
    {
        InitiativeQueue.Add(unit, 2);
    }

    public void UpdateInitiativeQueue()
    {
        //Sortowanie malejąco według wartości inicjatywy
        InitiativeQueue = InitiativeQueue.OrderByDescending(pair => pair.Key.GetComponent<Stats>().Initiative).ToDictionary(pair => pair.Key, pair => pair.Value);

        foreach (var key in InitiativeQueue.Keys.ToList())
        {
            Debug.Log(key + " " + InitiativeQueue[key] + " " + key.GetComponent<Stats>().Initiative);
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
