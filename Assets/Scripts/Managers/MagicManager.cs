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
    public List<Stats> UnitsStatsAffectedBySpell; // Lista jednostek, na które w danym momencie wpływa jakieś zaklęcie z czasem trwania, np. Pancerz Eteru

    [SerializeField] private GameObject _applyCastingNumberPanel;
    [SerializeField] private TMP_InputField _castingNumberInputField;
    [SerializeField] private GameObject _channelingRollPanel;
    [SerializeField] private TMP_InputField _channelingRollInputField;
    [SerializeField] private GameObject _applyDamagePanel;
    [SerializeField] private TMP_InputField _damageInputField;
    // Zmienne do przechowywania wyniku
    private int _manualRollResult;
    private bool _isWaitingForRoll;

    void Start()
    {
        //Wczytuje listę wszystkich zaklęć
        DataManager.Instance.LoadAndUpdateSpells();

        _targetsStats = new List<Stats>();
        UnitsStatsAffectedBySpell = new List<Stats>();
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

        StartCoroutine(ChannelingMagicCoroutine());

        // Test poziomu mocy zaklęcia
        IEnumerator ChannelingMagicCoroutine()
        {
            int rollResult;

            if(!GameManager.IsAutoDiceRollingMode)
            {
                // Czekaj na kliknięcie przycisku
                Debug.Log("Wpisz wynik rzutu na SW.");
                _channelingRollPanel.SetActive(true);
                yield return new WaitUntil(() => _channelingRollPanel.activeSelf == false);

                rollResult = _manualRollResult;
            }
            else
            {
                rollResult = UnityEngine.Random.Range(1, 101);
            }

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

        if(!GameManager.IsAutoDiceRollingMode)
        {
            CombatManager.Instance.IsManualPlayerAttack = true;
        }

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
        if (spell.CastingTimeLeft >= 2 && RoundsManager.Instance.UnitsWithActionsLeft[spellcasterUnit] == 2)
        {
            bool canDoAction = RoundsManager.Instance.DoFullAction(spellcasterUnit);

            if (canDoAction) spell.CastingTimeLeft -= 2;
            else return;
        }
        else if (spell.CastingTimeLeft == 1 || (spell.CastingTimeLeft >= 2 && RoundsManager.Instance.UnitsWithActionsLeft[spellcasterUnit] == 1))
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

                StartCoroutine(CombatManager.Instance.CheckForParryAndDodge(attackerWeapon, targetWeapon, target.GetComponent<Stats>(), target.GetComponent<Unit>(), true));

                isSuccessful = CombatManager.Instance.TargetIsDefended;
            }
        }

        StartCoroutine(CastingNumberRollCoroutine());

        // Test poziomu mocy zaklęcia
        IEnumerator CastingNumberRollCoroutine()
        {
            if(!GameManager.IsAutoDiceRollingMode && isSuccessful != false)
            {
                // Czekaj na kliknięcie przycisku
                Debug.Log("Wpisz poziom mocy uzyskany na kościach.");
                _applyCastingNumberPanel.SetActive(true);
                yield return new WaitUntil(() => _applyCastingNumberPanel.activeSelf == false);

                isSuccessful = _manualRollResult >= spell.CastingNumber ? true : false;

                CombatManager.Instance.IsManualPlayerAttack = false;

                //Aktualizuje aktywną postać na kolejce inicjatywy, jeśli atakujący nie ma już dostępnych akcji. Ta funkcja jest tu wywołana, dlatego że chcemy zastosować opóźnienie i poczekać ze zmianą jednostki do momentu wpisania wartości rzutów
                if(RoundsManager.Instance.UnitsWithActionsLeft[spellcasterUnit] == 0)
                {
                    InitiativeQueueManager.Instance.SelectUnitByQueue();
                }
            }
            else if(isSuccessful != false)
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
                yield break;
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
    }

    public void ResetSpellCasting()
    {
        IsTargetSelecting = false;
        //Zmienia kolor przycisku na nieaktywny
        _castSpellButton.GetComponent<Image>().color = Color.white;
    }

    public void ResetSpellEffect(Unit unit)
    {
        for (int i = 0; i < UnitsStatsAffectedBySpell.Count; i++)
        {
            if (unit.UnitId == UnitsStatsAffectedBySpell[i].GetComponent<Unit>().UnitId)
            {
                // Przywraca pierwotne wartości (sprzed działania zaklęcia) dla wszystkich cech. Celowo pomija obecne punkty żywotności, bo mogły ulec zmianie w trakcie działania zaklęcia.
                FieldInfo[] fields = typeof(Stats).GetFields(BindingFlags.Public | BindingFlags.Instance);
                foreach (FieldInfo field in fields)
                {
                    if (field.FieldType == typeof(int) && field.Name != "TempHealth")
                    {
                        int currentValue = (int)field.GetValue(unit.GetComponent<Stats>());
                        int otherValue = (int)field.GetValue(UnitsStatsAffectedBySpell[i]);

                        if (currentValue != otherValue)
                        {
                            field.SetValue(unit.GetComponent<Stats>(), otherValue);
                        }
                    }
                }

                UnitsStatsAffectedBySpell.RemoveAt(i);
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

        string resultString = "Wynik rzutu na poziom mocy - ";

        // Rzuty na poziom mocy w zależności od wartości Magii
        for (int i = 0; i < stats.Mag; i++)
        {
            int rollResult = UnityEngine.Random.Range(1, 11);
            allRollResults.Add(rollResult);
            castingNumber += rollResult;

            resultString += $"kość {i+1}: <color=#4dd2ff>{rollResult}</color> ";
        }

        int modifier = CalculateCastingNumberModifier();
        castingNumber += modifier;

        string modifierString = "";
        if(modifier != 0)
        {
            modifierString = $" Modyfikator: {modifier}.";
        }

        Debug.Log(resultString);
        if((castingNumber + modifier) < spellCastingNumber)
        {
            Debug.Log($"Uzyskany poziom mocy na kościach: {castingNumber - modifier}.{modifierString} Wymagany poziom mocy: <color=red>{spellCastingNumber}</color>");
        }
        else
        {
            Debug.Log($"Uzyskany poziom mocy na kościach: {castingNumber - modifier}.{modifierString} Wymagany poziom mocy: <color=green>{spellCastingNumber}</color>");
        }

        CheckForChaosManifestation(allRollResults);

        stats.GetComponent<Unit>().CanCastSpell = false;

        return castingNumber;
    }

    //Modyfikator do poziomu mocy
    private int CalculateCastingNumberModifier()
    {
        if(Unit.SelectedUnit == null) return 0;

        //Modyfikator do poziomu mocy
        int modifier = 0;

        Stats stats = Unit.SelectedUnit.GetComponent<Stats>() ;

        //Uwzględnienie splecenia magii
        modifier += Unit.SelectedUnit.GetComponent<Unit>().CastingNumberBonus;

        bool etherArmor = false;

        if(UnitsStatsAffectedBySpell != null && UnitsStatsAffectedBySpell.Count > 0)
        {
            //Przeszukanie statystyk jednostek, na które działają zaklęcia czasowe
            for (int i = 0; i < UnitsStatsAffectedBySpell.Count; i++)
            {
                //Jeżeli wcześniejsza wartość zbroi (w tym przypadku na głowie, ale to może być dowolna lokalizacja) jest inna niż obecna, świadczy to o użyciu Pancerzu Eteru
                if (UnitsStatsAffectedBySpell[i].Name == stats.Name && UnitsStatsAffectedBySpell[i].Armor_head != stats.Armor_head)
                {
                    etherArmor = true;
                }
            }
        }

        //Uwzględnienie ujemnego modyfikatora za zbroję (z wyjątkiem Pancerza Eteru)
        if (etherArmor == false)
        {
            int[] armors = { stats.Armor_head, stats.Armor_arms, stats.Armor_torso, stats.Armor_legs };
            int armouredCastingModifier = stats.ArmouredCasting == true ? 3 : 0;
            modifier -= Math.Max(0, armors.Max() - armouredCastingModifier); //Odejmuje największa wartość zbroi i uwzględnia Pancerz Wiary
        }

        return modifier;
    }

    // Korutyna czekająca na wpisanie wyniku przez użytkownika
    private IEnumerator WaitForRollValue()
    {
        _isWaitingForRoll = true;
        _manualRollResult = 0;

        // Wyświetl panel do wpisania wyniku
        if (_applyCastingNumberPanel != null)
        {
           _applyCastingNumberPanel.SetActive(true);
        }

        // Wyzeruj pole tekstowe
        if (_castingNumberInputField != null)
        {
            _castingNumberInputField.text = "";
        }

        // Czekaj aż użytkownik wpisze wartość i kliknie Submit
        while (_isWaitingForRoll)
        {
            yield return null; // Czekaj na następną ramkę
        }

        // Ukryj panel po wpisaniu
        if (_applyCastingNumberPanel != null)
        {
            _applyCastingNumberPanel.SetActive(false);
        }
    }

    public void OnSubmitRoll(bool isChanneling)
    {
        if(Unit.SelectedUnit == null) return;

        if (!isChanneling && _castingNumberInputField != null && int.TryParse(_castingNumberInputField.text, out int result))
        {
            _manualRollResult = result + CalculateCastingNumberModifier();
            _castingNumberInputField.text = ""; // Czyścimy pole
        }
        else if (isChanneling && _channelingRollInputField != null && int.TryParse(_channelingRollInputField.text, out int channelingRollResult))
        {
            _manualRollResult = channelingRollResult;
            _channelingRollInputField.text = ""; // Czyścimy pole
        }

        _isWaitingForRoll = false; // Przerywamy oczekiwanie
    }

    private void CheckForChaosManifestation(List<int> allRollResults)
    {
        var groupedResults = allRollResults.GroupBy(x => x).Where(g => g.Count() > 1);

        foreach (var group in groupedResults)
        {
            string type = group.Count() switch
            {
                2 => "pomniejsza",
                3 => "poważna",
                _ => "katastrofalna"
            };

            int roll = UnityEngine.Random.Range(1, 101);
            Debug.Log($"Wartość {group.Key} wypadła {group.Count()} razy. <color=red>Występuje {type} manifestacja Chaosu!</color> Wynik rzutu na manifestację: {roll}.");
        }
    }

    private void HandleSpellEffect(Stats spellcasterStats, Stats targetStats, Spell spell)
    {
        Unit targetUnit = targetStats.GetComponent<Unit>();

        //Uwzględnienie czasu trwania zaklęcia, które wpływa na statystyki postaci
        if(spell.Duration != 0 && spell.Type.Contains("buff"))
        {
            //Zakończenie wpływu poprzedniego zaklęcia, jeżeli na wybraną jednostkę już jakieś działało. JEST TO ZROBIONE TYMCZASOWO. TEN LIMIT ZOSTAŁ WPROWADZONY DLA UPROSZCZENIA KODU.
            if(UnitsStatsAffectedBySpell != null && UnitsStatsAffectedBySpell.Any(stat => stat.GetComponent<Unit>().UnitId == targetUnit.UnitId))
            {
                ResetSpellEffect(targetUnit);
                Debug.Log($"Poprzednie zaklęcie wpływające na {targetStats.Name} zostało zresetowane. W obecnej wersji symulatora nie ma możliwości kumulowania efektów wielu zaklęć.");
            }

            targetUnit.SpellDuration = spell.Duration;

            UnitsStatsAffectedBySpell.Add(targetStats.Clone());
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
            if (!GameManager.IsAutoDiceRollingMode) //Tryb ręczny. Póki co nie działa z zaklęciami, które wystrzeliwują kilka pocisków
            {
                StartCoroutine(WaitForDamageValue());
                IEnumerator WaitForDamageValue()
                {
                    _applyDamagePanel.SetActive(true);
                    // Czekaj na kliknięcie przycisku
                    yield return new WaitUntil(() => _applyDamagePanel.activeSelf == false);

                    if (int.TryParse(_damageInputField.text, out int inputDamage))
                    {
                        DealMagicDamage(spellcasterStats, targetStats, spell, inputDamage);
                        _damageInputField.text = null;
                    }
                }
            }
            else
            {
                DealMagicDamage(spellcasterStats, targetStats, spell);
            }
        }
    }

    private void DealMagicDamage(Stats spellcasterStats, Stats targetStats, Spell spell, int manualRollResult = 0)
    {
        int rollResult = manualRollResult == 0 ? UnityEngine.Random.Range(1, 11) : manualRollResult;
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

        //Aktualizuje osiągnięcia
        spellcasterStats.TotalDamageDealt += damage;
        if(damage > spellcasterStats.HighestDamageDealt)
        {
            spellcasterStats.HighestDamageDealt = damage;
        }

        targetStats.TotalDamageTaken += damage;
        if(damage > targetStats.HighestDamageTaken)
        {
            targetStats.HighestDamageTaken = damage;
        }

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
            CombatManager.Instance.HandleDeath(targetStats, targetStats.gameObject, spellcasterStats);
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