using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.TextCore.Text;
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

    [SerializeField] private CustomDropdown _spellbookDropdown;
    [SerializeField] private Button _castSpellButton;
    public List <Spell> SpellBook = new List<Spell>();
    public static bool IsTargetSelecting;
    private float _spellDistance;

    void Start()
    {
        //Wczytuje listę wszystkich zaklęć
        DataManager.Instance.LoadAndUpdateSpells();
    }

    public void ChannelingMagic()
    {
        if(Unit.SelectedUnit == null) return;

        if(Unit.SelectedUnit.GetComponent<Unit>().CastingNumberBonus > 0)
        {
            Debug.Log("Ta jednostka już wcześniej splotła magię.");
            return;
        }

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
            Unit.SelectedUnit.GetComponent<Unit>().CastingNumberBonus += stats.Mag;
            Debug.Log($"Wynik rzutu: {rollResult}. Wartość cechy: {stats.SW}. Modyfikator: {modifier}. Splatanie magii zakończone sukcesem.");
        }
        else
        {
            Unit.SelectedUnit.GetComponent<Unit>().CastingNumberBonus = 0;
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

        GridManager.Instance.ResetColorOfTilesInMovementRange();

        IsTargetSelecting = true;
        DataManager.Instance.LoadAndUpdateSpells(_spellbookDropdown.GetSelectedIndex());

        //Zmienia kolor przycisku na aktywny
        _castSpellButton.GetComponent<Image>().color = Color.green;

        Debug.Log("Kliknij prawym przyciskiem myszy na jednostkę, która ma być celem zaklęcia.");
    }

    public void CastSpell(GameObject target)
    {
        if (Unit.SelectedUnit == null) return;

        Stats spellcasterStats = Unit.SelectedUnit.GetComponent<Stats>();
        Unit spellcasterUnit = Unit.SelectedUnit.GetComponent<Unit>();
        Spell spell = Unit.SelectedUnit.GetComponent<Spell>();

        //Sprawdza dystans
        _spellDistance = CalculateDistance(Unit.SelectedUnit, target.gameObject);
        if (_spellDistance > spell.Range)
        {
            Debug.Log("Cel znajduje się poza zasięgiem zaklęcia.");
            return;
        }

        //Sprawdza wszystkie jednostki w obszarze działania zaklęcia
        List<Collider2D> allTargets = Physics2D.OverlapCircleAll(target.transform.position, spell.AreaSize / 2).ToList();

        // Usuwa wszystkie collidery, które nie są jednostkami
        for (int i = allTargets.Count - 1; i >= 0; i--)
        {
            //Usuwa collidery, które nie są jednostkami oraz rzucającego zaklęcie w przypadku zaklęć ofensywnych 
            if (allTargets[i].GetComponent<Unit>() == null || (allTargets[i].gameObject == Unit.SelectedUnit && spell.Type.Contains("offensive")))
            {
                allTargets.RemoveAt(i);
            }
        }

        if (allTargets.Count == 0)
        {
            Debug.Log($"W obszarze działania zaklęcia musi znaleźć się jakaś jednostka.");
            return;
        }

        //Wykonuje akcję
        if (spell.CastingTime >= 2 && RoundsManager.Instance.UnitsWithActionsLeft[spellcasterUnit] == 2)
        {
            bool canDoAction = RoundsManager.Instance.DoFullAction(spellcasterUnit);

            if (canDoAction) spell.CastingTimeLeft -= 2;
            else return;
        }
        else if (spell.CastingTime == 1 || (spell.CastingTime >= 2 && RoundsManager.Instance.UnitsWithActionsLeft[spellcasterUnit] == 1))
        {
            bool canDoAction = RoundsManager.Instance.DoHalfAction(spellcasterUnit);

            if (canDoAction) spell.CastingTimeLeft--;
            else return;
        }

        if (spell.CastingTimeLeft > 0)
        {
            Debug.Log($"{Unit.SelectedUnit.GetComponent<Stats>().Name} splata zaklęcie. Pozostała/y {spell.CastingTimeLeft} akcja/e do końca.");
            return;
        }

        bool isSuccessful = CastingNumberRoll(spellcasterStats, spell.CastingNumber) >= spell.CastingNumber ? true : false;

        ResetSpellCasting();
        spell.CastingTimeLeft = spell.CastingTime;
        spellcasterUnit.CastingNumberBonus = 0;

        if (isSuccessful == false)
        {
            Debug.Log("Rzucanie zaklęcia nie powiodło się.");
            return;
        }

        if (spell.Type.Contains("offensive"))
        {
            foreach(var collider in allTargets)
            {
                DealMagicDamage(spellcasterStats, collider.GetComponent<Unit>(), spell);
            }
        }
    }

    public void ResetSpellCasting()
    {
        IsTargetSelecting = false;
        //Zmienia kolor przycisku na nieaktywny
        _castSpellButton.GetComponent<Image>().color = Color.white;
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

        //Uwzględnienie splecenia magii
        castingNumber += Unit.SelectedUnit.GetComponent<Unit>().CastingNumberBonus;

        Debug.Log(resultString);
        Debug.Log($"Uzyskany poziom mocy: {castingNumber}. Wymagany poziom mocy: {spellCastingNumber}");

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
        return castingNumber+ Unit.SelectedUnit.GetComponent<Unit>().CastingNumberBonus;
    }

    private void DealMagicDamage(Stats spellcasterStats, Unit target, Spell spell)
    {
        int rollResult = Random.Range(1, 11);
        int damage = rollResult + spell.Strength;

        Debug.Log($"{spellcasterStats.Name} wyrzucił {rollResult} i zadał {damage} obrażeń.");

        Stats targetStats = target.GetComponent<Stats>();
        int armor = CalculateArmor(targetStats);

        //Zadanie obrażeń
        if (damage > (targetStats.Wt + armor))
        {
            targetStats.TempHealth -= damage - (targetStats.Wt + armor);

            Debug.Log(targetStats.Name + " znegował " + (targetStats.Wt + armor) + " obrażeń.");

            //Zaktualizowanie punktów żywotności
            target.GetComponent<Unit>().DisplayUnitHealthPoints();
            Debug.Log($"Punkty żywotności {targetStats.Name}: {targetStats.TempHealth}/{targetStats.MaxHealth}");
        }
        else
        {
            Debug.Log($"Atak {spellcasterStats.Name} nie przebił się przez pancerz.");
        }
    }

    private int CalculateArmor(Stats targetStats)
    {
        int attackLocalization = Random.Range(1, 101);
        int armor = 0;

        switch (attackLocalization)
        {
            case int n when (n >= 1 && n <= 15):
                Debug.Log("Trafienie w głowę.");
                armor = targetStats.Armor_head;
                break;
            case int n when (n >= 16 && n <= 35):
                Debug.Log("Trafienie w prawą rękę.");
                armor = targetStats.Armor_arms;
                break;
            case int n when (n >= 36 && n <= 55):
                Debug.Log("Trafienie w lewą rękę.");
                armor = targetStats.Armor_arms;
                break;
            case int n when (n >= 56 && n <= 80):
                Debug.Log("Trafienie w korpus.");
                armor = targetStats.Armor_torso;
                break;
            case int n when (n >= 81 && n <= 90):
                Debug.Log("Trafienie w prawą nogę.");
                armor = targetStats.Armor_legs;
                break;
            case int n when (n >= 91 && n <= 100):
                Debug.Log("Trafienie w lewą nogę.");
                armor = targetStats.Armor_legs;
                break;
        }

        return armor;
    }

    private float CalculateDistance(GameObject spellcaster, GameObject target)
    {
        if (spellcaster != null && target != null)
        {
            _spellDistance = Vector3.Distance(spellcaster.transform.position, target.transform.position);

            return _spellDistance;
        }
        else
        {
            Debug.LogError("Nie udało się ustalić odległości pomiędzy rzucającym zaklęcie a celem.");
            return 0;
        }
    }
}
