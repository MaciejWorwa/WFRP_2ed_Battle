using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UI;
using UnityEngine.UIElements;
using System.Linq;

public class CombatManager : MonoBehaviour
{
    // Prywatne statyczne pole przechowujące instancję
    private static CombatManager instance;

    // Publiczny dostęp do instancji
    public static CombatManager Instance
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

    private int _attackModifier;
    private float _attackDistance;
    private int _availableAttacks;
    [SerializeField] private UnityEngine.UI.Button _aimButton;
    [SerializeField] private UnityEngine.UI.Button _defensivePositionButton;
    [SerializeField] private UnityEngine.UI.Button _standardAttackButton;
    [SerializeField] private UnityEngine.UI.Button _allOutAttackButton;
    [SerializeField] private UnityEngine.UI.Button _guardedAttackButton;
    [SerializeField] private UnityEngine.UI.Button _multipleAttackButton;
    public Dictionary<string, bool> AttackTypes = new Dictionary<string, bool>();

    // Metoda inicjalizująca słownik ataków
    void Start()
    {
        InitializeAttackTypes();
        UpdateAttackTypeButtonsColor();
    }

    #region Attack types
    private void InitializeAttackTypes()
    {
        // Dodajemy typy ataków do słownika
        AttackTypes.Add("StandardAttack", true);
        AttackTypes.Add("Charge", false);
        AttackTypes.Add("AllOutAttack", false);  // Szaleńczy atak
        AttackTypes.Add("GuardedAttack", false);  // Ostrożny atak
        AttackTypes.Add("MultipleAttack", false);  // Atak wielokrotny
    }

    // Metoda ustawiająca dany typ ataku
    public void ChangeAttackType(string attackTypeName = null)
    {
        if(Unit.SelectedUnit == null) return;

        Unit unit = Unit.SelectedUnit.GetComponent<Unit>();

        if(attackTypeName == null) attackTypeName = "StandardAttack";

        //Resetuje szarżę lub bieg, jeśli były aktywne
        if(attackTypeName != "Charge" && unit.IsCharging)
        {
            MovementManager.Instance.UpdateMovementRange(1);
        }

        // Sprawdzamy, czy słownik zawiera podany typ ataku
        if (AttackTypes.ContainsKey(attackTypeName))
        {
            // Ustawiamy wszystkie typy ataków na false
            List<string> keysToReset = new List<string>();

            foreach (var key in AttackTypes.Keys)
            {
                if (key != attackTypeName)
                {
                    keysToReset.Add(key);
                }
            }

            foreach (var key in keysToReset)
            {
                AttackTypes[key] = false;
            }

            // Zmieniamy wartość bool dla danego typu ataku na true, a jeśli już był true to zmieniamy na standardowy atak. Sprawdzamy też, czy jednostka może wykonać akcję podwójną
            if(!AttackTypes[attackTypeName] && RoundsManager.Instance.UnitsWithActionsLeft.ContainsKey(unit) && RoundsManager.Instance.UnitsWithActionsLeft[unit] == 2)
            {
                AttackTypes[attackTypeName] = true;
            }
            else
            {
                AttackTypes[attackTypeName] = false;
                AttackTypes["StandardAttack"] = true;
            }

            if(AttackTypes["MultipleAttack"] == true)
            {
                //Atak wielokrotny jest dostępny tylko dla jednostek z ilością ataków większą niż 1
                if(unit.GetComponent<Stats>().A > 1)
                {
                    //Ustala dostępne ataki
                    _availableAttacks = unit.GetComponent<Stats>().A;
                }
                else
                {
                    AttackTypes[attackTypeName] = false;
                    AttackTypes["StandardAttack"] = true;
                    Debug.Log("Atak wielokrotny jest dostępny tylko dla jednostek z ilością ataków większą niż jeden.");
                }

            }
        }

        UpdateAttackTypeButtonsColor();
    }

    public void UpdateAttackTypeButtonsColor()
    {
        _standardAttackButton.GetComponent<UnityEngine.UI.Image>().color = AttackTypes["StandardAttack"] ? new Color(0.15f, 1f, 0.45f) : Color.white;
        _allOutAttackButton.GetComponent<UnityEngine.UI.Image>().color = AttackTypes["AllOutAttack"] ? new Color(0.15f, 1f, 0.45f) : Color.white;
        _guardedAttackButton.GetComponent<UnityEngine.UI.Image>().color = AttackTypes["GuardedAttack"] ? new Color(0.15f, 1f, 0.45f) : Color.white;
        _multipleAttackButton.GetComponent<UnityEngine.UI.Image>().color = AttackTypes["MultipleAttack"] ? new Color(0.15f, 1f, 0.45f) : Color.white;       
    }

    #endregion

    #region Attack function
    public void Attack(Unit attacker, Unit target, bool opportunityAttack) 
    {
        //Sprawdza, czy gra nie jest wstrzymana (np. poprzez otwarcie dodatkowych paneli)
        if(GameManager.IsGamePaused)
        {
            Debug.Log("Gra została wstrzymana. Aby ją wznowić musisz wyłączyć okno znajdujące się na polu gry.");
            return; 
        }

        if(attacker.CanAttack == false && opportunityAttack == false)
        {
            Debug.Log("Wybrana jednostka nie może wykonać kolejnego ataku w tej rundzie.");
            return;
        } 

        Stats attackerStats = attacker.Stats;
        Stats targetStats = target.Stats;

        Weapon attackerWeapon = InventoryManager.Instance.ChooseWeaponToAttack(attacker.gameObject);
        Weapon targetWeapon = target.GetComponent<Weapon>();

        //Jeżeli postać nie posiada w rękach broni to odnosimy się bezpośrednio do jego komponentu Weapon, który odpowiada w tym przypadku walce bez broni
        if(attackerWeapon == null)
        {
            attackerWeapon = attacker.GetComponent<Weapon>();
        }

        //Liczy dystans pomiedzy walczącymi
        _attackDistance = CalculateDistance(attacker.gameObject, target.gameObject);

        //Wykonuje atak, jeśli cel jest w zasięgu
        if (_attackDistance <= attackerWeapon.AttackRange || _attackDistance <= attackerWeapon.AttackRange * 2 && attackerWeapon.Type.Contains("ranged"))
        {
            //Sprawdza konieczne warunki do wykonania ataku dystansowego
            if(attackerWeapon.Type.Contains("ranged"))
            {
                //Sprawdza, czy broń jest naładowana
                if (attackerWeapon.ReloadLeft != 0)
                {
                    Debug.Log($"Broń wymaga przeładowania.");
                    return;
                }

                //Sprawdza, czy cel nie znajduje się zbyt blisko
                if (_attackDistance <= 1.5f)
                {
                    Debug.Log($"Stoisz zbyt blisko celu aby wykonać atak dystansowy.");
                    return;
                }

                // Sprawdza, czy na linii strzału znajduje się przeszkoda
                RaycastHit2D[] raycastHits = Physics2D.RaycastAll(attacker.transform.position, target.transform.position - attacker.transform.position, _attackDistance);

                foreach (var raycastHit in raycastHits)
                {
                    if (raycastHit.collider != null && raycastHit.collider.GetComponent<MapElement>() != null && raycastHit.collider.GetComponent<MapElement>().IsHighObstacle)
                    {
                        Debug.Log("Na linii strzału znajduje się przeszkoda.");
                        return;
                    }
                    if (raycastHit.collider != null && raycastHit.collider.GetComponent<MapElement>() != null && raycastHit.collider.GetComponent<MapElement>().IsLowObstacle)
                    {
                        _attackModifier -= 20;

                        Debug.Log("Strzał jest wykonywany w jednostkę znajdującą się za niewielką przeszkodą. Zastosowano ujemny modyfikator do trafienia.");

                        break; //Żeby modyfikator nie kumolował się za każdą przeszkodę
                    }
                }
            }

            //Wykonuje akcję (pomija tak okazyjny)
            bool canDoAction = true;
            if (attacker.IsCharging && _attackModifier != 0) //Szarża
            {            
                canDoAction = RoundsManager.Instance.DoFullAction(attacker);
            }
            else if (AttackTypes["AllOutAttack"] == true || AttackTypes["GuardedAttack"] == true || (AttackTypes["MultipleAttack"] == true && _availableAttacks == attackerStats.A)) //Specjalne ataki (szaleńczy, ostrożny i wielokrotny)
            {
                canDoAction = RoundsManager.Instance.DoFullAction(attacker);
            }
            else if(AttackTypes["StandardAttack"] == true && opportunityAttack == false) //Zwykły atak
            {
                canDoAction = RoundsManager.Instance.DoHalfAction(attacker);
            }

            if(!canDoAction) return;

            //Zaznacza, że jednostka wykonała już akcję ataku w tej rundzie. Uniemożliwia to wykonanie kolejnej. Nie dotyczy ataku okazyjnego, a w wielokrotnym sprawdza ilość dostępnych ataków
            if(!opportunityAttack && !AttackTypes["MultipleAttack"] || AttackTypes["MultipleAttack"] && _availableAttacks == 0) attacker.CanAttack = false;

            //Resetuje pozycję obronną, jeśli była aktywna
            if (attacker.DefensiveBonus != 0)
            {
                DefensivePosition();
            }  

            //Uniemożliwia parowanie i unikanie do końca rundy w przypadku szaleńczego ataku, a w przypadku ostrożnego dodaje modyfikator do parowania i uników
            if(AttackTypes["AllOutAttack"] == true)
            {
                attacker.CanParry = false;
                attacker.CanDodge = false;
            }
            else if(AttackTypes["GuardedAttack"] == true)
            {
                attacker.GuardedAttackBonus += 10;
            }
            else if(AttackTypes["MultipleAttack"] == true)
            {
                if(_availableAttacks <= 0)
                {
                    Debug.Log("Wybrana jednostka nie może wykonać kolejnego ataku w tej rundzie.");
                    return;
                }

                _availableAttacks --; 

                //Zmienia jednostkę wg kolejności inicjatywy
                if(_availableAttacks <= 0) RoundsManager.Instance.SelectUnitByQueue();
            }

            //Aktualizuje modyfikator ataku o celowanie
            _attackModifier += attacker.AimingBonus;

            //Skuteczność ataku
            bool isSuccessful;

            //W przypadku, gdy atak następuje po udanym rzucie na trafienie rzeczywistymi kośćmi to nie sprawdzamy rzutu na trafienie. W przeciwnym razie sprawdzamy
            if(attacker.CompareTag("PlayerUnit") && GameManager.IsAutoDiceRollingMode == false)
            {
                isSuccessful = true;
                
                //Sprawia, że po ataku należy przeładować broń. Uwzględnia błyskawiczne przeładowanie
                if (attackerWeapon.Type.Contains("ranged"))
                {
                    ResetWeaponLoad(attackerWeapon, attackerStats);
                }
            }
            else
            {
                //Rzut na trafienie
                int rollResult = Random.Range(1, 101);

                //Sprawdza, czy atak jest atakiem dystansowym, czy atakiem w zwarciu i ustala jego skuteczność
                isSuccessful = CheckAttackEffectiveness(rollResult, attackerStats, attackerWeapon, target);

                //Niepowodzenie przy pechu
                if(rollResult >= 96) isSuccessful = false;
            }         

            //Zresetowanie bonusu do trafienia
            _attackModifier = 0;

            //Zresetowanie celowania, jeżeli było aktywne
            if(attacker.AimingBonus != 0)
            {
                Unit.SelectedUnit.GetComponent<Unit>().AimingBonus = 0;
                UpdateAimButtonColor();
            }

            //Atakowany próbuje parować lub unikać.
            if (isSuccessful && attackerWeapon.Type.Contains("melee"))
            {
                isSuccessful = CheckForParryAndDodge(attackerWeapon, targetWeapon, targetStats, target);
            }

            if (isSuccessful)
            {
                int damageRollResult = DamageRoll(attackerStats, attackerWeapon);
                int damage = CalculateDamage(damageRollResult, attackerStats, attackerWeapon);
                int armor = CalculateArmor(targetStats, attackerWeapon);

                //Uwzględnienie strzału przebijającego zbroję (zdolność)
                if(attackerStats.SureShot && _attackDistance <= 1.5f && attackerWeapon.Type.Contains("ranged") && armor > 0) armor --;
            
                Debug.Log($"{attackerStats.Name} wyrzucił {damageRollResult} i zadał {damage} obrażeń.");

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
                    Debug.Log($"Atak {attackerStats.Name} nie przebił się przez pancerz.");
                }

                //Śmierć
                if (targetStats.TempHealth < 0 && GameManager.IsAutoKillMode)
                {
                    UnitsManager.Instance.DestroyUnit(target.gameObject);

                    //Aktualizuje podświetlenie pól w zasięgu ruchu atakującego (inaczej pozostanie puste pole w miejscu usuniętego przeciwnika)
                    GridManager.Instance.HighlightTilesInMovementRange(attackerStats);
                }    
            }
            else
            {
                Debug.Log($"Atak {attackerStats.Name} chybił.");
            }
        }
        else if (attacker.GetComponent<Unit>().IsCharging)
        {
            Charge(attacker.gameObject, target.gameObject);
        }
        else
        {
            Debug.Log("Cel ataku stoi poza zasięgiem.");
        }
    }
    #endregion

    #region Calculating distance
    private float CalculateDistance(GameObject attacker, GameObject target)
    {
        if (attacker != null && target != null)
        {
            _attackDistance = Vector3.Distance(attacker.transform.position, target.transform.position);

            return _attackDistance;
        }
        else
        {
            Debug.LogError("Nie udało się ustalić odległości pomiędzy walczącymi.");
            return 0;
        }
    }
    #endregion

    #region Check attack effectiveness
    private bool CheckAttackEffectiveness(int rollResult, Stats attackerStats, Weapon attackerWeapon, Unit targetUnit)
    {
        bool isSuccessful = false;

        //Uwzględnia utrudnienie za atak słabszą ręką (sprawdza, czy dominująca ręka jest pusta lub inna od broni, którą wykonywany jest atak)
        if(attackerStats.GetComponent<Inventory>().EquippedWeapons[0] == null || attackerWeapon.Name != attackerStats.GetComponent<Inventory>().EquippedWeapons[0].Name)
        {
            //Sprawdza, czy postać nie jest oburęczna albo nie uderza pięściami
            if (!attackerStats.Ambidextrous && attackerWeapon.Id != 0)
            {
                _attackModifier -= 20;
            }
        }

        //Modyfikatory za szaleńczy lub ostrożny atak
        if (AttackTypes["AllOutAttack"] == true) _attackModifier += 20;
        else if (AttackTypes["GuardedAttack"] == true) _attackModifier -= 10;

        //Modyfikator za jakość wykonania broni
        if(attackerWeapon.Quality == "Kiepska") _attackModifier -= 5;
        else if(attackerWeapon.Quality == "Najlepsza") _attackModifier += 5;

        //Sprawdza, czy atak jest atakiem dystansowym
        if (attackerWeapon.Type.Contains("ranged"))
        {
            _attackModifier -= _attackDistance > attackerWeapon.AttackRange ? 20 : 0;

            //Uwzględnienie utrudnienia za tarcze
            int shieldModifier = 0;

            //Sprawdza, czy atakowany ma tarczę
            if(targetUnit.GetComponent<Inventory>().EquippedWeapons.Length > 0)
            {
                foreach (var weapon in targetUnit.GetComponent<Inventory>().EquippedWeapons)
                {
                    if (weapon != null && weapon.Type.Contains("shield")) 
                    {
                        shieldModifier = 20;
                        break;
                    }
                }
            }        

            isSuccessful = rollResult <= (attackerStats.US + _attackModifier - targetUnit.DefensiveBonus - shieldModifier);

            if (_attackModifier != 0 || targetUnit.DefensiveBonus != 0 || shieldModifier != 0)
            {
                Debug.Log($"{attackerStats.Name} atakuje przy użyciu {attackerWeapon.Name}. Rzut na US: {rollResult} Wartość cechy: {attackerStats.US} Modyfikator: {_attackModifier - targetUnit.DefensiveBonus - shieldModifier}");
            }
            else
            {
                Debug.Log($"{attackerStats.Name} atakuje przy użyciu {attackerWeapon.Name}. Rzut na US: {rollResult} Wartość cechy: {attackerStats.US}");
            }

            //Sprawia, że po ataku należy przeładować broń. Uwzględnia błyskawiczne przeładowanie
            ResetWeaponLoad(attackerWeapon, attackerStats);
        }

        //Sprawdza czy atak jest atakiem w zwarciu
        if (attackerWeapon.Type.Contains("melee"))
        {
            //Uwzględnienie zdolności bijatyka, w przypadku walki Pięściami (Id broni = 0)
            if(attackerWeapon.Id == 0 && attackerStats.StreetFighting == true)
            {
                _attackModifier += 10;
            }

            isSuccessful = rollResult <= (attackerStats.WW + _attackModifier - targetUnit.DefensiveBonus);

            if (_attackModifier != 0 || targetUnit.DefensiveBonus != 0)
            {
                Debug.Log($"{attackerStats.Name} atakuje przy użyciu {attackerWeapon.Name}. Rzut na WW: {rollResult} Wartość cechy: {attackerStats.WW} Modyfikator: {_attackModifier - targetUnit.DefensiveBonus}");
            }
            else
            {
                Debug.Log($"{attackerStats.Name} atakuje przy użyciu {attackerWeapon.Name}. Rzut na WW: {rollResult} Wartość cechy: {attackerStats.WW}");
            }
        }

        return isSuccessful;
    }
    #endregion

    #region Calculating damage
    private int DamageRoll(Stats attackerStats, Weapon attackerWeapon)
    {
        //Rzut na obrażenia
        int damageRollResult;

        //Uwzględnienie broni druzgoczącej
        if (attackerWeapon.Impact == true)
        {
            int roll1 = Random.Range(1, 11);
            int roll2 = Random.Range(1, 11);
            damageRollResult = roll1 >= roll2 ? roll1 : roll2;
            Debug.Log($"Atak druzgoczącą bronią. Rzut na obrażenia nr 1: {roll1} Rzut nr 2: {roll2}");
        }
        else
        {
            damageRollResult = Random.Range(1, 11);
            Debug.Log($"Rzut na obrażenia: {damageRollResult}");
        }

        // Mechanika Furii Ulryka
        if (damageRollResult == 10)
        {
            int confirmRoll = Random.Range(1, 101); //rzut na potwierdzenie Furii
            int additionalDamage = 0; //obrażenia, które dodajemy do wyniku rzutu

            if (_attackDistance <= 1.5f)
            {
                if (attackerStats.WW >= confirmRoll)
                {
                    additionalDamage = Random.Range(1, 11);
                    damageRollResult += additionalDamage;
                    Debug.Log($"Rzut na potwierdzenie {confirmRoll}. FURIA ULRYKA!");
                }
                else
                {
                    Debug.Log($"Rzut na potwierdzenie {confirmRoll}. Nie udało się potwierdzić Furii Ulryka.");
                }
            }
            else if (_attackDistance > 1.5f)
            {
                if (attackerStats.US >= confirmRoll)
                {
                    additionalDamage = Random.Range(1, 11);
                    damageRollResult += additionalDamage;
                    Debug.Log($"Rzut na potwierdzenie {confirmRoll}. FURIA ULRYKA!");
                }
                else
                {
                    Debug.Log($"Rzut na potwierdzenie {confirmRoll}. Nie udało się potwierdzić Furii Ulryka.");
                }
            }

            while (additionalDamage == 10)
            {
                additionalDamage = Random.Range(1, 11);
                damageRollResult += additionalDamage;
                Debug.Log($"KOLEJNA FURIA ULRYKA!");
            }
        }

        return damageRollResult;
    }

    int CalculateDamage(int damageRollResult, Stats attackerStats, Weapon attackerWeapon)
    {
        int damage;

        if (_attackDistance <= 1.5f) //Oblicza łączne obrażenia dla ataku w zwarciu
        {
            damage = attackerStats.StrikeMightyBlow || (attackerWeapon.Id == 0 && attackerStats.StreetFighting == true) ? damageRollResult + attackerStats.S + attackerWeapon.S + 1 : damageRollResult + attackerStats.S + attackerWeapon.S;
        }
        else //Oblicza łączne obrażenia dla ataku dystansowego
        {
            damage = attackerStats.MightyShot ? damageRollResult + attackerWeapon.S + 1 : damageRollResult + attackerWeapon.S;             
        }

        if (damage < 0) damage = 0;

        return damage;
    }
    #endregion

    #region Check for attack localization and return armor value
    private int CalculateArmor(Stats targetStats, Weapon attackerWeapon)
    {
        int attackLocalization = Random.Range(1, 101);
        int armor = 0;

        switch (attackLocalization)
        {
            case int n when (n >= 1 && n <= 15):
                Debug.Log("Trafienie w głowę");
                armor = targetStats.Armor_head;
                break;
            case int n when (n >= 16 && n <= 35):
                Debug.Log("Trafienie w prawą rękę");
                armor = targetStats.Armor_arms;
                break;
            case int n when (n >= 36 && n <= 55):
                Debug.Log("Trafienie w lewą rękę");
                armor = targetStats.Armor_arms;
                break;
            case int n when (n >= 56 && n <= 80):
                Debug.Log("Trafienie w korpus");
                armor = targetStats.Armor_torso;
                break;
            case int n when (n >= 81 && n <= 90):
                Debug.Log("Trafienie w prawą nogę");
                armor = targetStats.Armor_legs;
                break;
            case int n when (n >= 91 && n <= 100):
                Debug.Log("Trafienie w lewą nogę");
                armor = targetStats.Armor_legs;
                break;
        }

        //Podwaja wartość zbroi w przypadku walki przy użyciu pięści
        if(attackerWeapon.Id == 0) armor *= 2;

        //Uwzględnienie broni przebijających zbroję
        if (attackerWeapon.ArmourPiercing == true) armor --;

        return armor;
    }
    #endregion

    #region Aiming
    public void SetAim()
    {
        if (Unit.SelectedUnit == null) return;

        Unit unit = Unit.SelectedUnit.GetComponent<Unit>();

        //Sprawdza, czy postać już celuje i chce przestać, czy chce dopiero przycelować
        if(unit.AimingBonus != 0)
        {
            unit.AimingBonus = 0;        
        }
        else
        {
            //Wykonuje akcję
            bool canDoAction;
            canDoAction = RoundsManager.Instance.DoHalfAction(Unit.SelectedUnit.GetComponent<Unit>());
            if(!canDoAction) return;

            Weapon attackerWeapon = InventoryManager.Instance.ChooseWeaponToAttack(unit.gameObject);
            if (attackerWeapon == null)
            {
                attackerWeapon = unit.GetComponent<Weapon>();
            }

            //Dodaje modyfikator do trafienia uzwględniając strzał mierzony w przypadku ataków dystansowych
            unit.AimingBonus += Unit.SelectedUnit.GetComponent<Stats>().Sharpshooter && attackerWeapon.Type.Contains("ranged") ? 20 : 10; 

            Debug.Log("Przycelowanie");
        }

        UpdateAimButtonColor();
    }
    public void UpdateAimButtonColor()
    {
        if(Unit.SelectedUnit.GetComponent<Unit>().AimingBonus != 0)
        {
            _aimButton.GetComponent<UnityEngine.UI.Image>().color = Color.green;
        }
        else
        {
            _aimButton.GetComponent<UnityEngine.UI.Image>().color = Color.white;
        }
    }
    #endregion

    #region Charge
    public void Charge(GameObject attacker, GameObject target)
    {
        //Sprawdza pole, w którym atakujący zatrzyma się po wykonaniu szarży
        GameObject targetTile = GetTileAdjacentToTarget(attacker, target);

        Vector3 targetTilePosition = Vector3.zero;

        if(targetTile != null)
        {
            targetTilePosition = new Vector3(targetTile.transform.position.x, targetTile.transform.position.y, 0);
        }
        else
        {
            Debug.Log($"Cel ataku stoi poza zasięgiem szarży.");
            return;
        }

        //Ścieżka ruchu szarżującego
        List<Vector3> path = MovementManager.Instance.FindPath(attacker.transform.position, targetTilePosition, attacker.GetComponent<Stats>().TempSz);

        //Sprawdza, czy postać jest wystarczająco daleko do wykonania szarży
        if (path.Count >= 3 && path.Count <= attacker.GetComponent<Stats>().TempSz)
        {
            _attackModifier += 10;

            MovementManager.Instance.MoveSelectedUnit(targetTile, attacker);

            // Wywołanie funkcji z wyczekaniem na koniec animacji ruchu postaci
            StartCoroutine(DelayedAttack(attacker, target, path.Count * 0.2f));

            IEnumerator DelayedAttack(GameObject attacker, GameObject target, float delay)
            {
                yield return new WaitForSeconds(delay);

                Attack(attacker.GetComponent<Unit>(), target.GetComponent<Unit>(), false);

                ChangeAttackType(); // Resetuje szarżę
            }
        }
        else
        {
            ChangeAttackType(); // Resetuje szarżę

            Debug.Log("Zbyt mała odległość na wykonanie szarży");
        }
    }

    // Szuka wolnej pozycji obok celu szarży, do której droga postaci jest najkrótsza
    public GameObject GetTileAdjacentToTarget(GameObject attacker, GameObject target)
    {
        Vector3 targetPos = target.transform.position;

        //Wszystkie przylegające pozycje do atakowanego
        Vector3[] positions = { targetPos + Vector3.right,
            targetPos + Vector3.left,
            targetPos + Vector3.up,
            targetPos + Vector3.down,
            targetPos + new Vector3(1, 1, 0),
            targetPos + new Vector3(-1, -1, 0),
            targetPos + new Vector3(-1, 1, 0),
            targetPos + new Vector3(1, -1, 0)
        };

        GameObject targetTile = null;

        //Długość najkrótszej ścieżki do pola docelowego
        int shortestPathLength = int.MaxValue;

        //Lista przechowująca ścieżkę ruchu szarżującego
        List<Vector3> path = new List<Vector3>();

        foreach (Vector3 pos in positions)
        {
            GameObject tile = GameObject.Find($"Tile {pos.x - GridManager.Instance.transform.position.x} {pos.y - GridManager.Instance.transform.position.y}");

            //Jeżeli pole jest zajęte to szukamy innego
            if (tile == null || tile.GetComponent<Tile>().IsOccupied) continue;

            path = MovementManager.Instance.FindPath(attacker.transform.position, pos, attacker.GetComponent<Stats>().TempSz);

            // Aktualizuje najkrótszą drogę
            if (path.Count < shortestPathLength)
            {
                shortestPathLength = path.Count;
                targetTile = tile;
            }  
        }

        return targetTile;
    }
    #endregion

    #region Defensive position
    public void DefensivePosition()
    {
        if (Unit.SelectedUnit == null) return;

        Unit unit = Unit.SelectedUnit.GetComponent<Unit>();

        if (unit.DefensiveBonus == 0)
        {
            //Wykonuje akcję
            bool canDoAction = RoundsManager.Instance.DoFullAction(unit);
            if(!canDoAction) return;   

            Debug.Log("Pozycja obronna.");

            unit.DefensiveBonus = 20;
        }
        else
        {
            unit.DefensiveBonus = 0;
        }

        UpdateDefensivePositionButtonColor();
    }
    public void UpdateDefensivePositionButtonColor()
    {
        if(Unit.SelectedUnit.GetComponent<Unit>().DefensiveBonus > 0)
        {
            _defensivePositionButton.GetComponent<UnityEngine.UI.Image>().color = Color.green;
        }
        else
        {
            _defensivePositionButton.GetComponent<UnityEngine.UI.Image>().color = Color.white;
        }
    }
    #endregion

    #region Parry and dodge
    private bool CheckForParryAndDodge(Weapon attackerWeapon, Weapon targetWeapon, Stats targetStats, Unit targetUnit)
    {
        bool targetIsDefended = false;

        //Sprawdzenie, czy jest aktywny tryb automatycznej obrony
        if(GameManager.IsAutoDefenseMode)
        {
            //Sprawdza, czy atakowany ma jakieś modifykatory do parowania
            int parryModifier = 0;
            if (targetWeapon.Defensive) parryModifier += 10;
            if (attackerWeapon.Slow) parryModifier += 10;
            if (attackerWeapon.Fast) parryModifier -= 10;
            if (Unit.SelectedUnit.GetComponent<Stats>().PowerfulBlow) parryModifier -= 30;
            if (targetUnit.GuardedAttackBonus != 0) parryModifier += targetUnit.GuardedAttackBonus;

            if (targetUnit.CanParry && targetUnit.CanDodge)
            {
                /* Sprawdza, czy atakowana postać ma większą szansę na unik, czy na parowanie i na tej podstawie ustala kolejność tych akcji.
                Warunek sprawdza też, czy obrońca broni się Pięściami (Id=0). Parowanie pięściami jest możliwe tylko, gdy przeciwnik również atakuje Pięściami */
                if (targetStats.WW + parryModifier > (targetStats.Zr + (targetStats.Dodge * 10) - 10) && (targetWeapon.Id != 0 || targetWeapon.Id == attackerWeapon.Id))
                {
                    targetIsDefended = Parry(attackerWeapon, targetWeapon, targetStats, parryModifier);
                }
                else
                {
                    targetIsDefended = Dodge(targetStats);
                }
            }
            else if (targetUnit.CanParry && (targetWeapon.Id != 0 || targetWeapon.Id == attackerWeapon.Id))
            {
                targetIsDefended = Parry(attackerWeapon, targetWeapon, targetStats, parryModifier);
            }
            else if (targetUnit.CanDodge)
            {
                targetIsDefended = Dodge(targetStats);
            }
        }
        else
        {
            //POKAZANIE POP-UPA Z ZAPYTANIEM, CZY CHCEMY WYKONAĆ UNIK LUB PAROWANIE
        }

        return !targetIsDefended; //Zwracana wartość definiuje, czy atak się powiódł. Zwracamy odwrotność, bo gdy obrona się powiodła, oznacza to, że atak nie.
    }
            
    private bool Parry(Weapon attackerWeapon, Weapon targetWeapon, Stats targetStats, int parryModifier)
    {
        //Wykonuje akcję, jeżeli postać nie posiada błyskawicznego bloku lub dwóch broni/tarczy (sprawdza, czy broń trzymana w drugiej ręce jest jednoręczna, jeśli tak to znaczy, że nie zużywa akcji)
        var equippedWeapons = targetStats.GetComponent<Inventory>().EquippedWeapons;
        bool isFirstWeaponShield = equippedWeapons[0] != null && equippedWeapons[0].Type.Contains("shield");
        bool hasTwoOneHandedWeaponsOrShield = (equippedWeapons[0] != null && equippedWeapons[1] != null && equippedWeapons[0].Name != equippedWeapons[1].Name) || isFirstWeaponShield;

        if(targetStats.LightningParry != true && hasTwoOneHandedWeaponsOrShield != true)
        {
            Unit unit = targetStats.GetComponent<Unit>();
            if (RoundsManager.Instance.UnitsWithActionsLeft.ContainsKey(unit) && RoundsManager.Instance.UnitsWithActionsLeft[unit] >= 1)
            {
                RoundsManager.Instance.DoHalfAction(targetStats.GetComponent<Unit>());
            }
            else return false;
        }

        //Sprawia, że atakowany nie będzie mógł więcej parować w tej rundzie
        targetStats.GetComponent<Unit>().CanParry = false;

        int rollResult = Random.Range(1, 101);

        if (parryModifier != 0)
        {
            Debug.Log($"Rzut {targetStats.Name} na parowanie: {rollResult} Wartość cechy: {targetStats.WW} Modyfikator do parowania: {parryModifier}");
        }
        else
        {
            Debug.Log($"Rzut {targetStats.Name} na parowanie: {rollResult} Wartość cechy: {targetStats.WW}");
        }

        if (rollResult <= targetStats.WW + parryModifier && rollResult < 96)
        {
            return true;
        }      
        else
        {
            return false;
        }        
    }

    private bool Dodge(Stats targetStats)
    {
        //Sprawia, że atakowany nie będzie mógł więcej unikać w tej rundzie   
        targetStats.GetComponent<Unit>().CanDodge = false;
        
        int rollResult = Random.Range(1, 101);

        Debug.Log($"Rzut {targetStats.Name} na unik: {rollResult} Wartość cechy: {targetStats.Zr}");

        if (rollResult <= targetStats.Zr + (targetStats.Dodge * 10) - 10 + targetStats.GetComponent<Unit>().GuardedAttackBonus && rollResult < 96)
        {
            return true;
        }      
        else
        {
            return false;
        }  
    }
    #endregion

    #region Reloading
    public void Reload()
    {
        if(Unit.SelectedUnit == null) return;

        Weapon weapon = Unit.SelectedUnit.GetComponent<Inventory>().EquippedWeapons[0];

        if(weapon == null || !weapon.Type.Contains("ranged")) return;

        if(weapon.ReloadLeft > 0)
        {
            //Wykonuje akcję
            bool canDoAction;
            canDoAction = RoundsManager.Instance.DoHalfAction(Unit.SelectedUnit.GetComponent<Unit>());
            if(!canDoAction) return;  

            weapon.ReloadLeft --;       
        }
        
        if(weapon.ReloadLeft == 0)
        {
            Debug.Log($"Broń {Unit.SelectedUnit.GetComponent<Stats>().Name} załadowana.");
        }
        else
        {
            Debug.Log($"Ładowanie broni {Unit.SelectedUnit.GetComponent<Stats>().Name}. Pozostała/y {weapon.ReloadLeft} akcja/e do końca.");
        }      
    }

    private void ResetWeaponLoad(Weapon attackerWeapon, Stats attackerStats)
    {
        //Sprawia, że po ataku należy przeładować broń
        attackerWeapon.ReloadLeft = attackerWeapon.ReloadTime;
        attackerWeapon.WeaponsWithReloadLeft[attackerWeapon.Id] = attackerWeapon.ReloadLeft;

        //Uwzględnia zdolność Błyskawicznego Przeładowania
        if (attackerStats.RapidReload == true)
        {
            attackerWeapon.ReloadLeft--;   
        }
    }
    #endregion
}
