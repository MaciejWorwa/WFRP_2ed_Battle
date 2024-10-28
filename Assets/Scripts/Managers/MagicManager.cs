using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UI;
using System;
using static UnityEngine.GraphicsBuffer;

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

    private List<Stats> _targetsStats; // Lista jednostek, które są wybierane jako cele zaklęcia, które pozwala wybrać więcej niż jeden cel
    private List<Stats> _unitsStatsAffectedBySpell; // Lista jednostek, na które w danym momencie wpływa jakieś zaklęcie z czasem trwania, np. Pancerz Eteru

    void Start()
    {
        //Wczytuje listę wszystkich zaklęć
        DataManager.Instance.LoadAndUpdateSpells();

        _targetsStats = new List<Stats>();
        _unitsStatsAffectedBySpell = new List<Stats>();
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

        int rollResult = UnityEngine.Random.Range(1, 101);
        int modifier = 0;
        if (stats.MagicSense) modifier += 10; //modyfikator za zmysł magii
        modifier += (stats.Channeling * 10) - 10; //modyfikator za umiejętność splatania magii

        string message = $"Wynik rzutu: {rollResult}. Wartość cechy: {stats.SW}.";
        if (modifier != 0)
        {
            message += $"  Modyfikator: {modifier}.";
        }

        if (stats.SW + modifier >= rollResult)
        {
            Unit.SelectedUnit.GetComponent<Unit>().CastingNumberBonus += stats.Mag;
            Debug.Log($"{message} Splatanie magii zakończone sukcesem.");
        }
        else
        {
            Unit.SelectedUnit.GetComponent<Unit>().CastingNumberBonus = 0;
            Debug.Log($"{message} Splatanie magii zakończone niepowodzeniem.");
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

        if (_spellbookDropdown.SelectedButton == null)
        {
            Debug.Log("Musisz najpierw wybrać zaklęcie z listy.");
            return;
        }

        GridManager.Instance.ResetColorOfTilesInMovementRange();

        IsTargetSelecting = true;

        string selectedSpellName = _spellbookDropdown.SelectedButton.GetComponentInChildren<TextMeshProUGUI>().text;
        DataManager.Instance.LoadAndUpdateSpells(selectedSpellName);

        _targetsStats.Clear();

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

        if (allTargets == null)
        {
            Debug.Log($"W obszarze działania zaklęcia musi znaleźć się odpowiedni cel.");
            return;
        }

        // Usuwa wszystkie collidery, które nie są jednostkami
        for (int i = allTargets.Count - 1; i >= 0; i--)
        {
            //Usuwa collidery, które nie są jednostkami oraz rzucającego zaklęcie w przypadku zaklęć ofensywnych. Uwzględnia także zaklęcia, które czarodziej może rzucić tylko na siebie, usuwając wszelkie jednostki, które nim nie są.
            if (allTargets[i].GetComponent<Unit>() == null || (allTargets[i].gameObject == Unit.SelectedUnit && spell.Type.Contains("offensive")) || (allTargets[i].gameObject != Unit.SelectedUnit && spell.Type.Contains("self-only")))
            {
                allTargets.RemoveAt(i);
            }
        }

        if (allTargets.Count == 0)
        {
            Debug.Log($"W obszarze działania zaklęcia musi znaleźć się odpowiedni cel.");
            return;
        }

        //Zablokowanie możliwości rzucenia Pancerza Eteru jednostkom w zbroi
        if (spell.Name == "Pancerz Eteru" && (allTargets[0].GetComponent<Stats>().Armor_head > 0 || allTargets[0].GetComponent<Stats>().Armor_arms > 0 || allTargets[0].GetComponent<Stats>().Armor_torso > 0 || allTargets[0].GetComponent<Stats>().Armor_legs > 0))
        {
            Debug.Log($"Jednostki noszące zbroję nie mogą używać Pancerzu Eteru.");
            return;
        }

        // W przypadku zaklęć, które atakują wiele celów naraz pozwala na wybranie kilku celów zanim zacznie rzucać zaklęcie
        if (spell.Type.Contains("multiple-targets") && spell.Type.Contains("magic-level-related") && _targetsStats.Count < spellcasterStats.Mag)
        {
            _targetsStats.Add(allTargets[0].GetComponent<Stats>());

            if (_targetsStats.Count < spellcasterStats.Mag)
            {
                Debug.Log("Wskaż prawym przyciskiem myszy kolejny cel. Możesz wskakać kilkukrotnie tę samą jednostkę.");
                return;
            }
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

        bool isSuccessful = true;

        //Czary dotykowe (ofensywne)
        if (spell.Range <= 1.5f && spell.Type.Contains("offensive"))
        {
            // Rzut na trafienie
            int rollResult = UnityEngine.Random.Range(1, 101);

            //Uwzględnienie zdolności Dotyk Mocy
            int modifier = spellcasterStats.FastHands ? 20 : 0;

            string message = $"{spellcasterStats.Name} próbuje dotknąć {target.GetComponent<Stats>().Name}. Wynik rzutu: {rollResult}. Wartość WW: {spellcasterStats.WW}.";
            if (modifier != 0)
            {
                message += $"  Modyfikator: {modifier}";
            }
            Debug.Log(message);

            if (rollResult > spellcasterStats.WW + modifier)
            {
                Debug.Log($"{spellcasterStats.Name} chybił.");
                isSuccessful = false;
            }
            else
            {
                //Zresetowanie broni, aby zaklęcie dotykowe było wykonywane przy pomocy rąk
                spellcasterUnit.GetComponent<Weapon>().ResetWeapon();
                Weapon attackerWeapon = spellcasterUnit.GetComponent<Weapon>();

                // Próba parowania lub uniku
                Weapon targetWeapon = InventoryManager.Instance.ChooseWeaponToAttack(target.gameObject);
                isSuccessful = CombatManager.Instance.CheckForParryAndDodge(attackerWeapon, targetWeapon, target.GetComponent<Stats>(), target.GetComponent<Unit>(), true);
            }
        }

        // Test poziomu mocy zaklęcia
        if (isSuccessful != false)
        {
            isSuccessful = CastingNumberRoll(spellcasterStats, spell.CastingNumber) >= spell.CastingNumber ? true : false;
        }

        ResetSpellCasting();
        spell.CastingTimeLeft = spell.CastingTime;
        spellcasterUnit.CastingNumberBonus = 0;

        if (isSuccessful == false)
        {
            Debug.Log("Rzucanie zaklęcia nie powiodło się.");
            _targetsStats.Clear();
            return;
        }
        else
        {
            Debug.Log("Rzucanie zaklęcia powiodło się.");
        }

        if (spell.Type.Contains("multiple-targets"))
        {
            foreach (var targetStats in _targetsStats)
            {
                HandleSpellEffect(spellcasterStats, targetStats, spell);
            }
            _targetsStats.Clear();
        }
        else
        {
            foreach (var collider in allTargets)
            {
                HandleSpellEffect(spellcasterStats, collider.GetComponent<Stats>(), spell);
            }
        }
    }

    public void ResetSpellCasting()
    {
        IsTargetSelecting = false;
        //Zmienia kolor przycisku na nieaktywny
        _castSpellButton.GetComponent<Image>().color = Color.white;
    }

    public void ResetSpellEffect(Unit unit)
    {
        for (int i = 0; i < _unitsStatsAffectedBySpell.Count; i++)
        {
            if (unit.UnitId == _unitsStatsAffectedBySpell[i].GetComponent<Unit>().UnitId)
            {
                // Przywraca pierwotne wartości (sprzed działania zaklęcia) dla wszystkich cech. Celowo pomija obecne punkty żywotności, bo mogły ulec zmianie w trakcie działania zaklęcia.
                FieldInfo[] fields = typeof(Stats).GetFields(BindingFlags.Public | BindingFlags.Instance);
                foreach (FieldInfo field in fields)
                {
                    if (field.FieldType == typeof(int) && field.Name != "TempHealth")
                    {
                        int currentValue = (int)field.GetValue(unit.GetComponent<Stats>());
                        int otherValue = (int)field.GetValue(_unitsStatsAffectedBySpell[i]);

                        if (currentValue != otherValue)
                        {
                            field.SetValue(unit.GetComponent<Stats>(), otherValue);
                        }
                    }
                }

                _unitsStatsAffectedBySpell.RemoveAt(i);
            }
        }

        UnitsManager.Instance.UpdateUnitPanel(Unit.SelectedUnit);
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
            int rollResult = UnityEngine.Random.Range(1, 11);
            allRollResults.Add(rollResult);
            castingNumber += rollResult;

            resultString += $"kość {i+1}: <color=green>{rollResult}</color> ";
        }

        //Modyfikator do poziomu mocy
        int modifier = 0;

        //Uwzględnienie splecenia magii
        modifier += Unit.SelectedUnit.GetComponent<Unit>().CastingNumberBonus;

        bool etherArmor = false;

        if(_unitsStatsAffectedBySpell != null && _unitsStatsAffectedBySpell.Count > 0)
        {
            //Przeszukanie statystyk jednostek, na które działają zaklęcia czasowe
            for (int i = 0; i < _unitsStatsAffectedBySpell.Count; i++)
            {
                //Jeżeli wcześniejsza wartość zbroi (w tym przypadku na głowie, ale to może być dowolna lokalizacja) jest inna niż obecna, świadczy to o użyciu Pancerzu Eteru
                if (_unitsStatsAffectedBySpell[i].Name == stats.Name && _unitsStatsAffectedBySpell[i].Armor_head != stats.Armor_head)
                {
                    etherArmor = true;
                }
            }
        }

        //Uwzględnienie ujemnego modyfikatora za zbroję (z wyjątkiem Pancerza Eteru)
        if (etherArmor == false)
        {
            modifier -= Math.Max(Math.Max(stats.Armor_head, stats.Armor_arms), Math.Max(stats.Armor_legs, stats.Armor_legs));
        }

        castingNumber += modifier;

        string modifierString = "";
        if(modifier != 0)
        {
            modifierString = $" Modyfikator: {modifier}.";
        }

        Debug.Log(resultString);
        Debug.Log($"Uzyskany poziom mocy na kościach: {castingNumber - modifier}.{modifierString} Wymagany poziom mocy: {spellCastingNumber}");

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
            int rollResult = UnityEngine.Random.Range(1, 101);

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

    private void HandleSpellEffect(Stats spellcasterStats, Stats targetStats, Spell spell)
    {
        Unit targetUnit = targetStats.GetComponent<Unit>();

        //Uwzględnienie czasu trwania zaklęcia, które wpływa na statystyki postaci
        if(spell.Duration != 0 && spell.Type.Contains("buff"))
        {
            //Zakończenie wpływu poprzedniego zaklęcia, jeżeli na wybraną jednostkę już jakieś działało. JEST TO ZROBIONE TYMCZASOWO. TEN LIMIT ZOSTAŁ WPROWADZONY DLA UPROSZCZENIA KODU.
            if(_unitsStatsAffectedBySpell != null && _unitsStatsAffectedBySpell.Any(stat => stat.GetComponent<Unit>().UnitId == targetUnit.UnitId))
            {
                ResetSpellEffect(targetUnit);
                Debug.Log($"Poprzednie zaklęcie wpływające na {targetStats.Name} zostało zresetowane. W obecnej wersji symulatora nie ma możliwości kumulowania efektów wielu zaklęć.");
            }

            targetUnit.SpellDuration = spell.Duration;









            if (_unitsStatsAffectedBySpell == null) Debug.Log("_unitsStatsAffectedBySpell== null");
            if (targetStats == null) Debug.Log("targetStats == null");










            _unitsStatsAffectedBySpell.Add(targetStats.Clone());
        }

        //Uwzględnienie testu obronnego
        if (spell.SaveTestRequiring == true && spell.Attribute.Length > 0)
        {
            //Szuka odpowiedniej cechy w statystykach celu
            FieldInfo field = targetStats.GetType().GetField(spell.Attribute[0]);

            if (field == null || field.FieldType != typeof(int)) return;

            int value = (int)field.GetValue(targetStats);

            int saveRollResult = UnityEngine.Random.Range(1, 101);

            if (saveRollResult > value)
            {
                Debug.Log($"{targetStats.Name} wykonał test na {spell.Attribute[0]} i wyrzucił {saveRollResult}. Wartość cechy: {value}. Nie udało mu się przeciwstawić zaklęciu.");
            }
            else
            {
                Debug.Log($"{targetStats.Name} wykonał test na {spell.Attribute[0]} i wyrzucił {saveRollResult}. Wartość cechy: {value}. Udało mu się przeciwstawić zaklęciu.");
                return;
            }
        }
        else if (spell.Attribute != null && spell.Attribute.Length > 0) // Zaklęcia wpływające na cechy, np. Uzdrowienie i Pancerz Eteru
        {
            for (int i = 0; i < spell.Attribute.Length; i++)
            {
                //Szuka odpowiedniej cechy w statystykach celu
                FieldInfo field = targetStats.GetType().GetField(spell.Attribute[i]);

                if (field == null || field.FieldType != typeof(int)) return;

                int value = spell.Strength;

                if (spell.Type.Contains("magic-level-related"))
                {
                    value += spellcasterStats.Mag;
                }

                // Zaklęcia leczące
                if (spell.Attribute[0] == "TempHealth")
                {
                    // Zapobiega leczeniu ponad maksymalną wartość żywotności
                    if (value + targetStats.TempHealth > targetStats.MaxHealth)
                    {
                        value = targetStats.MaxHealth - targetStats.TempHealth;
                    }

                    field.SetValue(targetStats, (int)field.GetValue(targetStats) + value);

                    //Zaktualizowanie punktów żywotności
                    targetStats.GetComponent<Unit>().DisplayUnitHealthPoints();
                    UnitsManager.Instance.UpdateUnitPanel(Unit.SelectedUnit);

                    Debug.Log($"{targetStats.Name} odzyskał {value} punktów Żywotności.");
                    return;
                }

                field.SetValue(targetStats, (int)field.GetValue(targetStats) + value);
            }

            UnitsManager.Instance.UpdateUnitPanel(Unit.SelectedUnit);
        }

        // Zaklęcia ogłuszające lub usypiające/paraliżujące
        if (spell.Paralyzing == true || spell.Stunning == true)
        {
            int duration = spell.Duration;
            int initialDuration = duration; // Przechowuje oryginalną wartość czasu trwania zaklęcia

            if (spell.Type.Contains("random-duration"))
            {
                duration = UnityEngine.Random.Range(1, 11);
                initialDuration = duration; // Aktualizuje, jeśli jest losowa długość
            }

            if (RoundsManager.Instance.UnitsWithActionsLeft[targetUnit] > 0)
            {
                RoundsManager.Instance.UnitsWithActionsLeft[targetUnit] = 0;
                duration--; // Zapobiega temu, żeby cel zaklęcia stracił dodatkową rundę, jeśli jego inicjatywa jest mniejsza niż rzucającego zaklęcie
            }

            if (spell.Paralyzing == true)
            {
                targetUnit.HelplessDuration += duration;
                Debug.Log($"{targetStats.Name} zostaje sparaliżowany/uśpiony na {initialDuration} rund/y.");
            }
            else if (spell.Stunning == true)
            {
                targetUnit.StunDuration += duration;
                Debug.Log($"{targetStats.Name} zostaje ogłuszony na {initialDuration} rund/y.");
            }
        }

        //Zaklęcia zadające obrażenia
        if (!spell.Type.Contains("no-damage") && spell.Type.Contains("offensive"))
        {
            DealMagicDamage(spellcasterStats, targetStats, spell);
        }

    }

    private void DealMagicDamage(Stats spellcasterStats, Stats targetStats, Spell spell)
    {

        int rollResult = UnityEngine.Random.Range(1, 11);
        int damage = rollResult + spell.Strength;

        int armor = CalculateArmor(targetStats);

        //Uwzględnienie zdolności Ignorujący Zbroję
        if (spell.ArmourIgnoring == true)
        {
            armor = 0;
        }

        //Uwzględnienie zdolności Morderczy Pocisk
        if (spell.Type.Contains("magic-missile") && spellcasterStats.MightyMissile == true)
        {
            damage++;
        }

        Debug.Log($"{spellcasterStats.Name} wyrzucił {rollResult} i zadał {damage} obrażeń.");

        //Zadanie obrażeń
        if (damage > (targetStats.Wt + armor))
        {
            targetStats.TempHealth -= damage - (targetStats.Wt + armor);

            Debug.Log(targetStats.Name + " znegował " + (targetStats.Wt + armor) + " obrażeń.");

            //Zaktualizowanie punktów żywotności
            targetStats.GetComponent<Unit>().DisplayUnitHealthPoints();
            Debug.Log($"Punkty żywotności {targetStats.Name}: {targetStats.TempHealth}/{targetStats.MaxHealth}");
        }
        else
        {
            Debug.Log($"Atak {spellcasterStats.Name} nie przebił się przez pancerz.");
        }

        //Śmierć
        if (targetStats.TempHealth < 0 && GameManager.IsAutoKillMode)
        {
            UnitsManager.Instance.DestroyUnit(targetStats.gameObject);

            //Aktualizuje podświetlenie pól w zasięgu ruchu atakującego (inaczej pozostanie puste pole w miejscu usuniętego przeciwnika)
            GridManager.Instance.HighlightTilesInMovementRange(spellcasterStats);
        }
    }

    private int CalculateArmor(Stats targetStats)
    {
        int attackLocalization = UnityEngine.Random.Range(1, 101);
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