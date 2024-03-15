using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DataManager : MonoBehaviour
{ 
    // Prywatne statyczne pole przechowujące instancję
    private static DataManager instance;

    // Publiczny dostęp do instancji
    public static DataManager Instance
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
    [SerializeField] private TMP_Dropdown _unitsDropdown;

    public void LoadAndUpdateStats(GameObject unit)
    {
        // Ładowanie danych JSON
        TextAsset jsonFile = Resources.Load<TextAsset>("data");
        if (jsonFile == null)
        {
            Debug.LogError("Nie znaleziono pliku JSON.");
            return;
        }

        // Deserializacja danych JSON
        StatsData[] statsArray = JsonHelper.FromJson<StatsData>(jsonFile.text);
        if (statsArray == null)
        {
            Debug.LogError("Deserializacja JSON nie powiodła się. Sprawdź strukturę JSON.");
            return;
        }

        //Odniesienie do statystyk postaci
        Stats statsToUpdate = unit.GetComponent<Stats>();

        if (statsToUpdate == null)
        {
            Debug.LogError("Aby wczytać statystyki musisz wybrać jednostkę.");
            return;
        }

        //Czyści listę dostępnych do wyboru jednostek
        _unitsDropdown.options.Clear();

        foreach (var stats in statsArray)
        {
            if (stats.Id == statsToUpdate.Id)
            {
                //statsToUpdate.Name = stats.Name;
                statsToUpdate.Race = stats.Race;
                statsToUpdate.Sz = stats.Sz;
                statsToUpdate.MaxHealth = stats.MaxHealth;

                Debug.Log("Wczytano statystyki dla jednostki z Id: " + statsToUpdate.Id);

                // Aktualizuje wyświetlaną nazwę postaci i jej punkty żywotności, jeśli ta postać jest aktualizowana, a nie tworzona po raz pierwszy
                if(unit.GetComponent<Unit>().Stats != null)
                {
                    unit.GetComponent<Unit>().DisplayUnitName();
                    unit.GetComponent<Unit>().DisplayUnitHealthPoints();
                }
            }

            //Dodaje jednostkę do dropdowna
            _unitsDropdown.options.Add(new TMP_Dropdown.OptionData(stats.Race));
        }

        //Odświeża wyświetlaną wartość dropdowna
        _unitsDropdown.RefreshShownValue();
    }
}

[System.Serializable]
public class StatsData
{
    public int Id;
    //public string Name;
    public string Race;
    public int MaxHealth;
    public int Sz;
}

public static class JsonHelper
{
    public static T[] FromJson<T>(string json)
    {
        Wrapper<T> wrapper = JsonUtility.FromJson<Wrapper<T>>(json);

        if (wrapper == null || wrapper.Units == null)
        {
            Debug.LogError("Deserializacja JSON nie powiodła się. Sprawdź składnię i strukturę JSON.");
            Debug.Log(wrapper);
            Debug.Log(wrapper.Units);
            return null;
        }
        return wrapper.Units;
    }

    [System.Serializable]
    private class Wrapper<T>
    {
        public T[] Units;
    }
}
