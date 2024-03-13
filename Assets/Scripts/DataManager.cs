using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DataManager : MonoBehaviour
{

    [SerializeField] private TMP_Dropdown _unitsDropdown;

    public void LoadAndUpdateStats()
    {
        // £adowanie danych JSON
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
            Debug.LogError("Deserializacja JSON nie powiod³a siê. SprawdŸ strukturê JSON.");
            return;
        }

        // Aktualizacja statystyk
        Stats statsToUpdate = Unit.SelectedUnit.GetComponent<Stats>();
        if (statsToUpdate == null)
        {
            Debug.LogError("Aby zaktualizowaæ statystyki musisz wybraæ postaæ.");
            return;
        }

        //Czyœci listê dostêpnych do wyboru jednostek
        _unitsDropdown.options.Clear();

        foreach (var stats in statsArray)
        {
            if (stats.Id == statsToUpdate.Id)
            {
                statsToUpdate.Name = stats.Name;
                statsToUpdate.Sz = stats.Sz;
                statsToUpdate.TempSz = stats.TempSz;

                Debug.Log("Zaktualizowano statystyki dla postaci z Id: " + statsToUpdate.Id);

                // Aktualizuje wyœwietlan¹ nazwê postaci i jej punkty ¿ywotnoœci
                Unit.SelectedUnit.GetComponent<Unit>().DisplayUnitName();
                Unit.SelectedUnit.GetComponent<Unit>().DisplayUnitHealthPoints();
            }

            //Dodaje jednostkê do dropdowna
            _unitsDropdown.options.Add(new TMP_Dropdown.OptionData(stats.Name));
        }

        //Odœwie¿a wyœwietlan¹ wartoœæ dropdowna
        _unitsDropdown.RefreshShownValue();
    }
}

[System.Serializable]
public class StatsData
{
    public int Id;
    public string Name;
    public int Sz;
    public int TempSz;
}

public static class JsonHelper
{
    public static T[] FromJson<T>(string json)
    {
        Wrapper<T> wrapper = JsonUtility.FromJson<Wrapper<T>>(json);

        if (wrapper == null || wrapper.Units == null)
        {
            Debug.LogError("Deserializacja JSON nie powiod³a siê. SprawdŸ sk³adniê i strukturê JSON.");
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
