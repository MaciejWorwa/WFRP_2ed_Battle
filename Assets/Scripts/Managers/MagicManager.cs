using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MagicManager : MonoBehaviour
{
     // Prywatne statyczne pole przechowujące instancję
    private static MagicManager instance;

    // Publiczny dostęp do instancji
    public static MagicManager Instance
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
    private int _castingNumberModifier;
    [SerializeField] private CustomDropdown _spellbookDropdown;
    [SerializeField] private Button _castSpellButton;
    public List <Spell> SpellBook = new List<Spell>();
    public static bool IsTargetSelecting;

    void Start()
    {
        //Wczytuje listę wszystkich zaklęć
        DataManager.Instance.LoadAndUpdateSpells();
    }

    public void ChannelingMagic()
    {
        if(Unit.SelectedUnit == null) return;

        Stats stats = Unit.SelectedUnit.GetComponent<Stats>();

        //Sprawdzenie, czy wybrana postać może splatać magię
        if(stats.Channeling == 0)
        {
            Debug.Log($"Wybrana jednostka nie potrafi splatać magii.");
            return;
        }

        // Wykonanie akcji
        bool canDoAction = RoundsManager.Instance.DoHalfAction(Unit.SelectedUnit.GetComponent<Unit>());
        if(!canDoAction) return;   

        int rollResult = Random.Range(1, 101);
        int modifier = 0;
        if (stats.MagicSense) modifier += 10; //modyfikator za zmysł magii
        modifier += (stats.Channeling * 10) - 10; //modyfikator za umiejętność splatania magii

        if (stats.SW + modifier >= rollResult)
        {
            _castingNumberModifier += stats.Mag;
            Debug.Log($"Wynik rzutu: {rollResult}. Wartość cechy: {stats.SW}. Modyfikator: {modifier}. Splatanie magii zakończone sukcesem.");
        }
        else
        {
            _castingNumberModifier = 0;
            Debug.Log($"Wynik rzutu: {rollResult}. Wartość cechy: {stats.SW}. Modyfikator: {modifier}. Splatanie magii zakończone niepowodzeniem.");
        }
    }

    public void CastingSpellMode()
    {
        if(Unit.SelectedUnit == null) return;

        if(Unit.SelectedUnit.GetComponent<Stats>().Mag == 0)
        {
            Debug.Log("Wybrana jednostka nie może rzucać zaklęć.");
            return;
        }
        
        if(!Unit.SelectedUnit.GetComponent<Unit>().CanCastSpell)
        {
            Debug.Log("Wybrana jednostka nie może w tej rundzie rzucić więcej zaklęć.");
            return;
        }

        IsTargetSelecting = true;

        //Zmienia kolor przycisku na aktywny
        _castSpellButton.GetComponent<Image>().color = Color.green;

        Debug.Log("Kliknij prawym przyciskiem myszy na jednostkę, która ma być celem zaklęcia.");
        return;
    }

    public void CastSpell(Unit target)
    {
        ResetSpellCasting();

        DataManager.Instance.LoadAndUpdateSpells(_spellbookDropdown.GetSelectedIndex());

        bool isSuccessful = CastingNumberRoll(Unit.SelectedUnit.GetComponent<Stats>(), Unit.SelectedUnit.GetComponent<Spell>().CastingNumber) > Unit.SelectedUnit.GetComponent<Spell>().CastingNumber ? true : false;

        //TUTAJ JEST WSZYSTKO DO UZUPEŁNIENIA, NA RAZIE JEST TAK:
        Debug.Log("succesfull " + isSuccessful);  
    }

    public void ResetSpellCasting()
    {
        IsTargetSelecting = false;
        //Zmienia kolor przycisku na nieaktywny
        _castSpellButton.GetComponent<Image>().color = Color.white;

        _castingNumberModifier = 0;
    }

    private int CastingNumberRoll(Stats stats, int spellCastingNumber)
    {
        // Zresetowanie poziomu mocy
        int castingNumber = 0;

        // Lista i słownik wszystkich wyników rzutów, potrzebne do sprawdzenia wystąpienia manifestacji chaosu
        List<int> allRollResults = new List<int>();
        Dictionary<int, int> doubletCount = new Dictionary<int, int>();

        string resultString = "Wynik rzutu na poziom mocy - ";

        // Rzuty na poziom mocy w zależności od wartości Magii
        for (int i = 0; i < stats.Mag; i++)
        {
            int rollResult = Random.Range(1, 11);
            allRollResults.Add(rollResult);
            castingNumber += rollResult;

            resultString += $"kość {i+1}: <color=green>{rollResult}</color> ";

        }
        Debug.Log(resultString);
        Debug.Log($"Uzyskany poziom mocy: {castingNumber + _castingNumberModifier}. Wymagany poziom mocy: {spellCastingNumber}");

        // Liczenie dubletów
        foreach (int rollResult in allRollResults)
        {
            if (!doubletCount.ContainsKey(rollResult))
            {
                doubletCount.Add(rollResult, 1); // jeśli wartość nie istnieje w słowniku, dodajemy ją i ustawiamy licznik na 1
            }
            else
            {
                doubletCount[rollResult] += 1; // jeśli wartość istnieje w słowniku, zwiększamy jej licznik
            }
                
        }

        // Rzuty na manifestację w zależności od ilości wyników, które się powtórzyły
        foreach (KeyValuePair<int, int> kvp in doubletCount)
        {
            int count = kvp.Value;
            if (count == 1) continue;

            int value = kvp.Key;
            int rollResult = Random.Range(1, 101);

            if (count == 2)
            {  
                Debug.Log($"Wartość {value} wypadła {count} razy. Występuje pomniejsza manifestacja Chaosu! Wynik rzutu na manifestację: {rollResult}");
            }
            else if (count == 3)
            {
                Debug.Log($"Wartość {value} wypadła {count} razy. Występuje poważna manifestacja Chaosu! Wynik rzutu na manifestację: {rollResult}");
            }
            else if (count > 3)
            {
                Debug.Log($"Wartość {value} wypadła {count} razy. Występuje katastrofalna manifestacja Chaosu! Wynik rzutu na manifestację: {rollResult}");
            }
        }

        stats.GetComponent<Unit>().CanCastSpell = false;
        return castingNumber;
    }
}
