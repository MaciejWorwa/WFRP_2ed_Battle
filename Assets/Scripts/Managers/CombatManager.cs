using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UI;
using UnityEngine.UIElements;
using System.Linq;
using static SimpleFileBrowser.FileBrowser;
using UnityEditor;
using System.Drawing;
using TMPro;
using System;
using Unity.VisualScripting;
using static UnityEngine.GraphicsBuffer;
using UnityEditor.Experimental.GraphView;

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
    public int AvailableAttacks;

    [Header("Przyciski wszystkich typów ataku")]
    [SerializeField] private UnityEngine.UI.Button _aimButton;
    [SerializeField] private UnityEngine.UI.Button _defensiveStanceButton;
    [SerializeField] private UnityEngine.UI.Button _standardAttackButton;
    [SerializeField] private UnityEngine.UI.Button _chargeButton;
    [SerializeField] private UnityEngine.UI.Button _allOutAttackButton;
    [SerializeField] private UnityEngine.UI.Button _guardedAttackButton;
    [SerializeField] private UnityEngine.UI.Button _swiftAttackButton;
    [SerializeField] private UnityEngine.UI.Button _feintButton;
    [SerializeField] private UnityEngine.UI.Button _stunButton;
    [SerializeField] private UnityEngine.UI.Button _disarmButton;
    [SerializeField] private UnityEngine.UI.Button _grapplingButton;
    public Dictionary<string, bool> AttackTypes = new Dictionary<string, bool>();

    private bool _isSuccessful;  //Skuteczność ataku

    [Header("Panele do manualnego zarządzania")]
    [SerializeField] private GameObject _parryAndDodgePanel;
    [SerializeField] private UnityEngine.UI.Button _dodgeButton;
    [SerializeField] private UnityEngine.UI.Button _parryButton;
    [SerializeField] private UnityEngine.UI.Button _getDamageButton;
    [SerializeField] private UnityEngine.UI.Button _cancelButton;
    private string _parryOrDodge;
    [SerializeField] private GameObject _applyDamagePanel;
    [SerializeField] private GameObject _rollResultPanel;
    [SerializeField] private TMP_InputField _parryOrDodgeInputField;
    [SerializeField] private TMP_InputField _damageInputField;
    // Zmienne do przechowywania wyniku
    private int _manualRollResult;
    private bool _isWaitingForRoll;
    public bool TargetIsDefended;
    public bool IsManualPlayerAttack;

    private List<string> _greenskinsList = new List<string> {"Goblin", "Hobgoblin", "Ork zwyczajny", "Czarny ork", "Dziki ork"}; //Lista string wszystkich zielonoskórych
    private List<string> _poisoningUnitsList = new List<string> {"Ghul", "Hobgoblin", "Olbrzymi pająk", "Wywern"}; //Lista string wszystkich jednostek, których atak jest trujący

    // Metoda inicjalizująca słownik ataków
    void Start()
    {
        InitializeAttackTypes();
        UpdateAttackTypeButtonsColor();

        _dodgeButton.onClick.AddListener(() => ParryOrDodgeButtonClick("dodge"));
        _parryButton.onClick.AddListener(() => ParryOrDodgeButtonClick("parry"));
        _getDamageButton.onClick.AddListener(() => ParryOrDodgeButtonClick(""));
        _cancelButton.onClick.AddListener(() => ParryOrDodgeButtonClick("cancel"));
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) && _parryAndDodgePanel.activeSelf)
        {
            ParryOrDodgeButtonClick("cancel");
        }
    }

    #region Attack types
    private void InitializeAttackTypes()
    {
        // Dodajemy typy ataków do słownika
        AttackTypes.Add("StandardAttack", true);
        AttackTypes.Add("Charge", false);
        AttackTypes.Add("AllOutAttack", false);  // Szaleńczy atak
        AttackTypes.Add("GuardedAttack", false);  // Ostrożny atak
        AttackTypes.Add("SwiftAttack", false);  // Atak wielokrotny
        AttackTypes.Add("Feint", false);  // Finta
        AttackTypes.Add("Stun", false);  // Ogłuszanie
        AttackTypes.Add("Disarm", false);  // Rozbrajanie
        AttackTypes.Add("Grappling", false);  // Chwyt
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

            // Zmieniamy wartość bool dla danego typu ataku na true, a jeśli już był true to zmieniamy na standardowy atak.
            if(!AttackTypes[attackTypeName] && RoundsManager.Instance.UnitsWithActionsLeft.ContainsKey(unit))
            {
                AttackTypes[attackTypeName] = true;
            }
            else
            {
                AttackTypes[attackTypeName] = false;
                AttackTypes["StandardAttack"] = true;
                if(unit.GetComponent<Stats>().TempSz == unit.GetComponent<Stats>().Sz * 2)
                {
                    MovementManager.Instance.UpdateMovementRange(1);
                }
            }

            if(AttackTypes["SwiftAttack"] == true)
            {
                //Atak wielokrotny jest dostępny tylko dla jednostek z ilością ataków większą niż 1
                if(unit.GetComponent<Stats>().A > 1)
                {
                    //Ustala dostępne ataki
                    AvailableAttacks = unit.GetComponent<Stats>().A;
                }
                else
                {
                    AttackTypes[attackTypeName] = false;
                    AttackTypes["StandardAttack"] = true;
                    Debug.Log("Atak wielokrotny jest dostępny tylko dla jednostek z ilością ataków większą niż jeden.");
                }
            }
            else
            {
                AvailableAttacks = 0;
            }

            //Ogłuszanie jest dostępne tylko dla jednostek ze zdolnością ogłuszania
            if (AttackTypes["Stun"] == true && unit.GetComponent<Stats>().StrikeToStun == false)
            {
                    AttackTypes[attackTypeName] = false;
                    AttackTypes["StandardAttack"] = true;
                    Debug.Log("Ogłuszanie mogą wykonywać tylko jednostki posiadające tą zdolność.");
            }

            //Rozbrajanie jest dostępne tylko dla jednostek ze zdolnością rozbrajania
            if (AttackTypes["Disarm"] == true && unit.GetComponent<Stats>().Disarm == false)
            {
                AttackTypes[attackTypeName] = false;
                AttackTypes["StandardAttack"] = true;
                Debug.Log("Rozbrajanie mogą wykonywać tylko jednostki posiadające tą zdolność.");
            }

            //Ograniczenie finty, ogłuszania, zapasów i rozbrajania do ataków w zwarciu
            if ((AttackTypes["Feint"] || AttackTypes["Stun"] || AttackTypes["Grappling"] || AttackTypes["Disarm"] || AttackTypes["Charge"]) == true && unit.GetComponent<Inventory>().EquippedWeapons[0] != null && unit.GetComponent<Inventory>().EquippedWeapons[0].Type.Contains("ranged"))
            {
                AttackTypes[attackTypeName] = false;
                AttackTypes["StandardAttack"] = true;
                Debug.Log("Jednostka walcząca bronią dystansową nie może wykonać tej akcji.");
            }
            else if ((AttackTypes["AllOutAttack"] || AttackTypes["GuardedAttack"] || AttackTypes["Charge"]) == true && RoundsManager.Instance.UnitsWithActionsLeft[unit] < 2)
            {
                AttackTypes[attackTypeName] = false;
                AttackTypes["StandardAttack"] = true;
                Debug.Log("Ta jednostka nie może w tej rundzie wykonać akcji podwójnej.");
            }
            else if (AttackTypes["Charge"] == true && !unit.IsCharging)
            {
                MovementManager.Instance.UpdateMovementRange(2);
            }
        }

        UpdateAttackTypeButtonsColor();
    }

    public void UpdateAttackTypeButtonsColor()
    {
        _standardAttackButton.GetComponent<UnityEngine.UI.Image>().color = AttackTypes["StandardAttack"] ? new UnityEngine.Color(0.15f, 1f, 0.45f) : UnityEngine.Color.white;
        _chargeButton.GetComponent<UnityEngine.UI.Image>().color = AttackTypes["Charge"] ? new UnityEngine.Color(0.15f, 1f, 0.45f) : UnityEngine.Color.white;
        _allOutAttackButton.GetComponent<UnityEngine.UI.Image>().color = AttackTypes["AllOutAttack"] ? new UnityEngine.Color(0.15f, 1f, 0.45f) : UnityEngine.Color.white;
        _guardedAttackButton.GetComponent<UnityEngine.UI.Image>().color = AttackTypes["GuardedAttack"] ? new UnityEngine.Color(0.15f, 1f, 0.45f) : UnityEngine.Color.white;
        _swiftAttackButton.GetComponent<UnityEngine.UI.Image>().color = AttackTypes["SwiftAttack"] ? new UnityEngine.Color(0.15f, 1f, 0.45f) : UnityEngine.Color.white;
        _feintButton.GetComponent<UnityEngine.UI.Image>().color = AttackTypes["Feint"] ? new UnityEngine.Color(0.15f, 1f, 0.45f) : UnityEngine.Color.white;
        _stunButton.GetComponent<UnityEngine.UI.Image>().color = AttackTypes["Stun"] ? new UnityEngine.Color(0.15f, 1f, 0.45f) : UnityEngine.Color.white;
        _disarmButton.GetComponent<UnityEngine.UI.Image>().color = AttackTypes["Disarm"] ? new UnityEngine.Color(0.15f, 1f, 0.45f) : UnityEngine.Color.white;
        _grapplingButton.GetComponent<UnityEngine.UI.Image>().color = AttackTypes["Grappling"] ? new UnityEngine.Color(0.15f, 1f, 0.45f) : UnityEngine.Color.white;
    }

    #endregion

    #region Attack function
    public void Attack(Unit attacker, Unit target, bool opportunityAttack)
    {
        //Sprawdza, czy gra nie jest wstrzymana (np. poprzez otwarcie dodatkowych paneli)
        if (GameManager.IsGamePaused)
        {
            Debug.Log("Gra została wstrzymana. Aby ją wznowić musisz wyłączyć okno znajdujące się na polu gry.");
            return;
        }

        if (attacker.CanAttack == false && opportunityAttack == false)
        {
            Debug.Log("Wybrana jednostka nie może wykonać kolejnego ataku w tej rundzie.");
            return;
        }

        if(opportunityAttack)
        {
            ChangeAttackType("StandardAttack");
        }

        //Jeśli jednostka atakująca jest w stanie zapasów z inną jednostkę to Grappling jest jedynym możliwym atakiem
        if(attacker.GrappledUnitId != 0)
        {
            if (attacker.GrappledUnitId != target.UnitId)
            {
                Debug.Log("Wybrana jednostka może atakować jedynie przeciwnika, z którym jest w trakcie zapasów.");
                return;
            }

            ChangeAttackType("Grappling");
        }

        Stats attackerStats = attacker.Stats;
        Stats targetStats = target.Stats;

        Weapon attackerWeapon = InventoryManager.Instance.ChooseWeaponToAttack(attacker.gameObject);
        Weapon targetWeapon = InventoryManager.Instance.ChooseWeaponToAttack(target.gameObject);

        //Liczy dystans pomiedzy walczącymi
        _attackDistance = CalculateDistance(attacker.gameObject, target.gameObject);

        //Wykonuje atak, jeśli cel jest w zasięgu
        if (_attackDistance <= attackerWeapon.AttackRange || _attackDistance <= attackerWeapon.AttackRange * 2 && attackerWeapon.Type.Contains("ranged") && !attackerWeapon.Type.Contains("short-range-only"))
        {

            if (attackerWeapon.Type.Contains("ranged"))
            {
                //Sprawdza konieczne warunki do wykonania ataku dystansowego
                if(ValidateRangedAttack(attacker, target, attackerWeapon, _attackDistance) == false) return;

                // Sprawdza, czy na linii strzału znajduje się przeszkoda
                RaycastHit2D[] raycastHits = Physics2D.RaycastAll(attacker.transform.position, target.transform.position - attacker.transform.position, _attackDistance);

                foreach (var raycastHit in raycastHits)
                {
                    if (raycastHit.collider == null) continue;

                    var mapElement = raycastHit.collider.GetComponent<MapElement>();
                    var unit = raycastHit.collider.GetComponent<Unit>();

                    if (mapElement != null && mapElement.IsLowObstacle)
                    {
                        _attackModifier -= 20;
                        Debug.Log("Strzał jest wykonywany w jednostkę znajdującą się za przeszkodą. Zastosowano ujemny modyfikator do trafienia.");
                        break; // Żeby modyfikator nie kumulował się za każdą przeszkodę
                    }

                    if (unit != null && unit != target && unit != attacker)
                    {
                        _attackModifier -= 20;
                        Debug.Log("Na linii strzału znajduje się inna jednostka. Zastosowano ujemny modyfikator do trafienia.");
                        break; // Żeby modyfikator nie kumulował się za każdą postać
                    }
                }
            }

            //Sprawdza, czy jest to atak wykonywany przez gracza, po którym będzie trzeba wpisać obrażenia w odpowiednim panelu. Ta zmienna jest konieczna, aby nowa jednostka nie była wybierana dopóki nie wpiszemy obrażeń.
            IsManualPlayerAttack = attacker.CompareTag("PlayerUnit") && GameManager.IsAutoDiceRollingMode == false;

            //Wykonuje akcję (pomija tak okazyjny)
            bool canDoAction = true;
            if (attacker.IsCharging && _attackModifier != 0) //Szarża
            {
                canDoAction = RoundsManager.Instance.DoFullAction(attacker);
            }
            else if (AttackTypes["AllOutAttack"] || AttackTypes["GuardedAttack"] || (AttackTypes["SwiftAttack"] && AvailableAttacks == attackerStats.A)) //Specjalne ataki (szaleńczy, ostrożny i wielokrotny)
            {
                canDoAction = RoundsManager.Instance.DoFullAction(attacker);
            }
            else if ((AttackTypes["StandardAttack"] && opportunityAttack == false) || AttackTypes["Feint"] || AttackTypes["Stun"]|| AttackTypes["Disarm"] || AttackTypes["Grappling"]) //Zwykły atak, ogłuszanie, rozbrajanie, zapasy lub finta
            {
                canDoAction = RoundsManager.Instance.DoHalfAction(attacker);
            }

            if (!canDoAction)
            {
                //Zresetowanie bonusu do trafienia
                _attackModifier = 0;
                return;
            }

            //Zaznacza, że jednostka wykonała już akcję ataku w tej rundzie. Uniemożliwia to wykonanie kolejnej. Nie dotyczy ataku okazyjnego i finty, a w wielokrotnym sprawdza ilość dostępnych ataków
            if ((!opportunityAttack && !AttackTypes["SwiftAttack"] && !AttackTypes["Feint"]) || (AttackTypes["SwiftAttack"] && AvailableAttacks == 0))
            {
                attacker.CanAttack = false;
            }

            //Resetuje pozycję obronną, jeśli była aktywna
            if (attacker.DefensiveBonus != 0)
            {
                DefensiveStance();
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
            else if(AttackTypes["SwiftAttack"] == true)
            {
                if(AvailableAttacks <= 0)
                {
                    Debug.Log("Wybrana jednostka nie może wykonać kolejnego ataku w tej rundzie.");
                    return;
                }

                AvailableAttacks --; 

                //Zmienia jednostkę wg kolejności inicjatywy
                if(AvailableAttacks <= 0) InitiativeQueueManager.Instance.SelectUnitByQueue();
            }

            //Aktualizuje modyfikator ataku o celowanie
            _attackModifier += attacker.AimingBonus;

            //Jeśli walczące jednostki są już w trakcie zapasów to dalsza mechanika ataku się różni
            if(AttackTypes["Grappling"] && attacker.GrappledUnitId == target.UnitId)
            {
                StartCoroutine(Grappling(attackerStats, targetStats, true));
                return;
            }

            //Rzut na trafienie
            int rollResult = UnityEngine.Random.Range(1, 101);

            //W przypadku, gdy atak następuje po udanym rzucie na trafienie rzeczywistymi kośćmi to nie sprawdzamy rzutu na trafienie. W przeciwnym razie sprawdzamy
            if(IsManualPlayerAttack)
            {
                if(attackerWeapon.Quality != "Magiczna" && target.GetComponent<Stats>().Ethereal == true)
                {
                    Debug.Log("Przeciwnik jest odporny na niemagiczne ataki. Aby go zranić konieczne jest użycie magicznej broni lub zaklęcia.");
                    _isSuccessful = false;
                }
                else if (AttackTypes["Feint"] == true)
                {
                    attacker.Feinted = Feint(rollResult, attackerStats, targetStats);

                    ChangeAttackType("StandardAttack");

                    return; //Kończy akcję ataku, żeby nie przechodzić do dalszych etapów jak np. zadanie obrażeń
                }
                else
                {
                    _isSuccessful = true;
                    
                    //Obliczenie modyfikatora do ataku
                    string attackType = AttackTypes["AllOutAttack"] ? "AllOutAttack" : AttackTypes["GuardedAttack"] ? "GuardedAttack" : "";
                    int modifier = CalculateAttackModifier(attackerStats, attackerWeapon, target, attackType, _attackDistance);
                    
                    // Sprawdzenie ataku dystansowego i modyfikatora za tarczę
                    if (attackerWeapon.Type.Contains("ranged"))
                    {
                        int shieldModifier = target.GetComponent<Inventory>().EquippedWeapons
                            .Where(weapon => weapon != null && weapon.Name == "Tarcza")
                            .Select(_ => 10).FirstOrDefault();

                        modifier -= shieldModifier;
                    }
                    modifier -= target.DefensiveBonus;

                    string message = $"Wykonaj rzut kośćmi na trafienie.";
                    if(modifier > 0)
                    {      
                        message += $" Modyfikator: <color=green>{modifier}</color>.";
                    }
                    else if (modifier < 0)
                    {
                        message += $" Modyfikator: <color=red>{modifier}</color>.";
                    }
                    message += $" Jeśli atak chybi, wyłącz okno znajdujące się na środku ekranu.";
                    Debug.Log(message);
                }
                
                //Sprawia, że po ataku należy przeładować broń. Uwzględnia błyskawiczne przeładowanie
                if (attackerWeapon.Type.Contains("ranged"))
                {
                    ResetWeaponLoad(attackerWeapon, attackerStats);
                }
            }
            else
            {
                //Wykonuje fintę. Po niej następuje zwykły atak (inaczej bez sensu jest robienie finty).
                if (AttackTypes["Feint"] == true)
                {
                    attacker.Feinted = Feint(rollResult, attackerStats, targetStats);

                    ChangeAttackType("StandardAttack");

                    return; //Kończy akcję ataku, żeby nie przechodzić do dalszych etapów jak np. zadanie obrażeń
                }

                //Sprawdza, czy atak jest atakiem dystansowym, czy atakiem w zwarciu i ustala jego skuteczność
                _isSuccessful = CheckAttackEffectiveness(rollResult, attackerStats, attackerWeapon, target);

                //Niepowodzenie przy pechu lub powodzenie przy szczęściu
                if (rollResult >= 96)
                {
                    Debug.Log($"{attackerStats.Name} wyrzucił <color=red>PECHA</color> na trafienie!");
                    _isSuccessful = false;

                    //Aktualizuje osiągnięcia
                    attackerStats.UnfortunateEvents ++;
                }
                else if(rollResult <= 5 && !(attackerWeapon.Quality != "Magiczna" && attackerStats.DaemonicAura != true && targetStats.Ethereal))
                {
                    Debug.Log($"{attackerStats.Name} wyrzucił <color=green>SZCZĘŚCIE</color> na trafienie!");
                    _isSuccessful = true;
   
                    //Aktualizuje osiągnięcia
                    attackerStats.FortunateEvents ++;
                }
            }

            //Zresetowanie bonusu do trafienia
            _attackModifier = 0;

            //Zresetowanie celowania, jeżeli było aktywne
            if(attacker.AimingBonus != 0)
            {
                attacker.AimingBonus = 0;
                UpdateAimButtonColor();
            }

            if(AttackTypes["Grappling"])
            {
                StartCoroutine(Grappling(attackerStats, targetStats, _isSuccessful));
                return;
            }

            //Atakowany próbuje parować lub unikać.
            if (_isSuccessful && rollResult > 5 && attackerWeapon.Type.Contains("melee") && target.GrappledUnitId == 0 && !target.Grappled && (attacker.Feinted != true || AttackTypes["StandardAttack"] != true))
            {
                //Sprawdza, czy parowanie wymaga akcji
                var equippedWeapons = targetStats.GetComponent<Inventory>().EquippedWeapons;
                bool isFirstWeaponShield = equippedWeapons[0] != null && equippedWeapons[0].Type.Contains("shield");
                bool hasTwoOneHandedWeaponsOrShield = (equippedWeapons[0] != null && equippedWeapons[1] != null && equippedWeapons[0].Name != equippedWeapons[1].Name) || isFirstWeaponShield;

                bool canParry = target.CanParry && ((RoundsManager.Instance.UnitsWithActionsLeft.ContainsKey(target) && RoundsManager.Instance.UnitsWithActionsLeft[target] >= 1) || targetStats.LightningParry || hasTwoOneHandedWeaponsOrShield);

                //Sprawdzenie, czy jest aktywny tryb automatycznej obrony
                if (!GameManager.IsAutoDefenseMode && (canParry || target.CanDodge))
                {
                    _parryAndDodgePanel.SetActive(true);

                    _dodgeButton.gameObject.SetActive(target.CanDodge);
                    _parryButton.gameObject.SetActive(canParry);

                    StartCoroutine(WaitForDefenseReaction());

                    // Korutyna obsługująca reakcję obronną
                    IEnumerator WaitForDefenseReaction()
                    {
                        // Czekaj na kliknięcie przycisku
                        Debug.Log("Wybierz reakcję atakowanej postaci.");
                        yield return new WaitUntil(() => _parryAndDodgePanel.activeSelf == false);

                        StartCoroutine(CheckForParryAndDodge(attackerWeapon, targetWeapon, targetStats, target, false));
                        
                        if(!GameManager.IsAutoDiceRollingMode)
                        {
                            yield return new WaitUntil(() => _rollResultPanel.activeSelf == false);
                        }

                        _isSuccessful = !TargetIsDefended;

                        ExecuteAttack(attacker, target, attackerWeapon);

                        //Uwzględnienie zdolności Grad Ciosów, czyli wykonanie drugiego ataku
                        if(AttackTypes["StandardAttack"] == true && (attackerStats.Race == "Smok" || attackerStats.Race == "Hydra") && targetStats.TempHealth >= 0)
                        {
                            SpeedOfAttack(attackerStats, targetStats, attackerWeapon, targetWeapon, attacker, target); // Grad Ciosów
                        }

                        //Zmienia jednostkę wg kolejności inicjatywy
                        if(RoundsManager.Instance.UnitsWithActionsLeft[attacker] == 0 && AvailableAttacks <= 0)
                        {
                            InitiativeQueueManager.Instance.SelectUnitByQueue();
                        }
                    }
                    _parryOrDodge = "";

                    return;
                }

                if(canParry || target.CanDodge)
                {
                    StartCoroutine(CheckForParryAndDodge(attackerWeapon, targetWeapon, targetStats, target, false));
                    _isSuccessful = !TargetIsDefended;
                }
            }

            //Zresetowanie finty
            attacker.Feinted = false;

            ExecuteAttack(attacker, target, attackerWeapon);

            //Uwzględnienie zdolności Grad Ciosów, czyli wykonanie drugiego ataku
            if(AttackTypes["StandardAttack"] == true && (attackerStats.Race == "Smok" || attackerStats.Race == "Hydra") && targetStats.TempHealth >= 0)
            {
                SpeedOfAttack(attackerStats, targetStats, attackerWeapon, targetWeapon, attacker, target); // Grad Ciosów
            }

            StartCoroutine(AnimationManager.Instance.PlayAnimation("attack", attacker.gameObject, target.gameObject));

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

    public void ExecuteAttack(Unit attacker, Unit target, Weapon attackerWeapon)
    {
        Stats attackerStats = attacker.GetComponent<Stats>();
        Stats targetStats = target.GetComponent<Stats>();

        if (_isSuccessful)
        {
            //Dodaje nagrodę za trafienie przeciwnika (uczenie AI)
            ReinforcementLearningManager.Instance.HasHitTarget = true;

            //Ogłuszanie
            if (AttackTypes["Stun"] == true)
            {
                Stun(attackerWeapon, attackerStats, targetStats);
                return; //Kończy akcję ataku, żeby nie przechodzić do dalszych etapów jak np. zadanie obrażeń
            }

            //Rozbrajanie
            if (AttackTypes["Disarm"] == true)
            {
                Disarm(attackerStats, targetStats, InventoryManager.Instance.ChooseWeaponToAttack(target.gameObject));
                return; //Kończy akcję ataku, żeby nie przechodzić do dalszych etapów jak np. zadanie obrażeń
            }

            //Unieruchomienie, jeżeli broń atakującego posiada cechę "unieruchamiający"
            if (attackerWeapon.Snare)
            {
                if (target.Trapped == false)
                {
                    target.Trapped = true;
                    RoundsManager.Instance.UnitsWithActionsLeft[target] = 0;
                  
                    Debug.Log($"{attackerStats.Name} unieruchomił {targetStats.Name} przy pomocy {attackerWeapon.Name}");
                    UnitsManager.Instance.UpdateUnitPanel(attacker.gameObject);

                    if (!attackerWeapon.Type.Contains("throwing")) //Związanie atakującego z celem (np. pochwycenie przez bicz). Wtedy atakujący również nie może wykonywać innych akcji
                    {
                        attacker.TrappedUnitId = target.UnitId;
                        RoundsManager.Instance.UnitsWithActionsLeft[attacker] = 0;
                    }
                }

                if (attackerWeapon.Type.Contains("no-damage")) return; //Jeśli broń nie powoduje obrażeń, np. sieć, to pomijamy dalszą część kodu
            }

            int armor = CalculateArmor(targetStats, attackerWeapon);

            //W przypadku, gdy atak następuje w trybie ręcznego rzucania kośćmi to nie sprawdzamy rzutu na obrażenia. W przeciwnym razie sprawdzamy
            if (IsManualPlayerAttack)
            {
                Debug.Log($"{attackerStats.Name} trafia w {targetStats.Name}. Zadaj obrażenia ręcznie wpisując wynik rzutu i kliknij \"Zatwierdź\".");

                _applyDamagePanel.SetActive(true);

                StartCoroutine(WaitForDamageValue());

                IEnumerator WaitForDamageValue()
                {
                    // Czekaj na kliknięcie przycisku
                    yield return new WaitUntil(() => _applyDamagePanel.activeSelf == false);
                    int damage = 0;
                    if (int.TryParse(_damageInputField.text, out int inputDamage))
                    {
                        damage = CalculateDamage(inputDamage, attackerStats, attackerWeapon);
                        _damageInputField.text = null;
                    }

                    //Uwzględnienie strzału przebijającego zbroję (zdolność)
                    if (attackerStats.SureShot && _attackDistance <= 1.5f && attackerWeapon.Type.Contains("ranged") && armor > 0) armor--;

                    Debug.Log($"{attackerStats.Name} zadał {damage} obrażeń.");

                    //Aktualizuje osiągnięcia
                    attackerStats.TotalDamageDealt += damage;
                    if(damage > attackerStats.HighestDamageDealt)
                    {
                        attackerStats.HighestDamageDealt = damage;
                    }

                    //Zadaje obrażenia
                    ApplyDamage(damage, attackerStats, targetStats, armor, target);

                    //Sprawdza, czy atak spowodował śmierć
                    if (targetStats.TempHealth < 0 && GameManager.IsAutoKillMode)
                    {
                        HandleDeath(targetStats, target.gameObject, attackerStats);
                    }

                    //Aktualizuje aktywną postać na kolejce inicjatywy, jeśli atakujący nie ma już dostępnych akcji. Ta funkcja jest tu wywołana, dlatego że chcemy zastosować opóźnienie i poczekać ze zmianą jednostki do momentu wpisania wartości obrażeń
                    if(RoundsManager.Instance.UnitsWithActionsLeft[attacker] == 0 && AvailableAttacks <= 0)
                    {
                        //Zmienia jednostkę wg kolejności inicjatywy
                        InitiativeQueueManager.Instance.SelectUnitByQueue();
                    }

                    IsManualPlayerAttack = false;
                }
                return;
            }
            else
            {
                int damageRollResult = DamageRoll(attackerStats, attackerWeapon);
                int damage = CalculateDamage(damageRollResult, attackerStats, attackerWeapon);

                //Bonus do obrażeń w przypadku atakowania postaci bezbronnej
                if (target.HelplessDuration > 0)
                {
                    int extraRoll = UnityEngine.Random.Range(1, 11);
                    damage += extraRoll;
                    Debug.Log($"Rzut na dodatkowe obrażenia z powodu atakowania bezbronnej jednostki: {extraRoll}");
                }

                //Uwzględnienie strzału przebijającego zbroję (zdolność)
                if (attackerStats.SureShot && _attackDistance <= 1.5f && attackerWeapon.Type.Contains("ranged") && armor > 0) armor--;

                Debug.Log($"{attackerStats.Name} zadał {damage} obrażeń.");

                //Aktualizuje osiągnięcia
                attackerStats.TotalDamageDealt += damage;
                if(damage > attackerStats.HighestDamageDealt)
                {
                    attackerStats.HighestDamageDealt = damage;
                }

                //Zadaje obrażenia
                ApplyDamage(damage, attackerStats, targetStats, armor, target);
            }

            //Sprawdza, czy atak spowodował śmierć
            if (targetStats.TempHealth < 0 && GameManager.IsAutoKillMode)
            {
                HandleDeath(targetStats, target.gameObject, attackerStats);
            }
        }
        else
        {
            Debug.Log($"Atak {attackerStats.Name} chybił.");
            StartCoroutine(AnimationManager.Instance.PlayAnimation("miss", null, target.gameObject));

            //Nie dodaje nagrody za trafienie przeciwnika (uczenie AI)
            ReinforcementLearningManager.Instance.HasHitTarget = false;
        }
    }

    // Obliczanie i stosowanie obrażeń
    private void ApplyDamage(int damage, Stats attackerStats, Stats targetStats, int armor, Unit targetUnit)
    {
        //Aktualizuje osiągnięcia
        targetStats.TotalDamageTaken += damage;
        if(damage > targetStats.HighestDamageTaken)
        {
            targetStats.HighestDamageTaken = damage;
        }

        int targetWt = targetStats.Wt;

        //Uwzględnienie zdolności Demoniczna Aura
        if(targetStats.DaemonicAura == true && attackerStats.GetComponent<Weapon>().Quality != "Magiczna")
        {
            targetWt += 2;
        }

        if (damage > targetWt + armor)
        {
            // Zatrucie
            if(_poisoningUnitsList.Contains(attackerStats.Race))
            {
                int rollOdp = UnityEngine.Random.Range(1, 101);
                int modifier = attackerStats.Race == "Olbrzymi pająk" ? 0 : -10;
                int extraDamage = attackerStats.Race == "Wywern" ? 3 : 2;

                Debug.Log($"{targetStats.Name} wykonał rzut na Odporność i wyrzucił {rollOdp}. Wartość cechy: {targetStats.Odp}. Modyfikator: {modifier}");

                if((rollOdp > targetStats.Odp + modifier && rollOdp > 5) || rollOdp >= 96)
                {
                    if (rollOdp >= 96)
                    {
                        Debug.Log($"{targetStats.Name} wyrzucił <color=red>PECHA!</color>");
                                    
                        //Aktualizuje osiągnięcia
                        targetStats.UnfortunateEvents ++;
                    }

                    if(attackerStats.Race == "Olbrzymi pająk")
                    {
                        int duration = UnityEngine.Random.Range(1, 11);
                        if(targetStats.GetComponent<Unit>().HelplessDuration < duration)
                        {
                            targetStats.GetComponent<Unit>().HelplessDuration = duration;
                            RoundsManager.Instance.UnitsWithActionsLeft[targetStats.GetComponent<Unit>()] = 0;
                            Debug.Log($"{targetStats.Name} nie zdał testu i został sparaliżowany przez {attackerStats.Name} na {duration} rund/y.");
                        }     
                    }
                    else
                    {
                        damage += extraDamage;
                        Debug.Log($"{targetStats.Name} nie zdał testu i traci dodatkowe {extraDamage} punkty Żywotności w wyniku zatrucia przez {attackerStats.Name}.");
                    }
                }
                else if (rollOdp < 96)
                {
                    if(rollOdp <= 5)
                    {
                        Debug.Log($"{targetStats.Name} wyrzucił <color=green>SZCZĘŚCIE!</color>");
            
                        //Aktualizuje osiągnięcia
                        targetStats.FortunateEvents ++;
                    }
                    Debug.Log($"{targetStats.Name} zdał test Odporności.");
                }
            }

            targetStats.TempHealth -= damage - (targetWt + armor);

            if(!GameManager.IsStatsHidingMode || targetUnit.gameObject.CompareTag("PlayerUnit"))
            {
                Debug.Log($"{targetStats.Name} znegował {targetWt + armor} obrażeń.");
            }
            else if(targetStats.TempHealth >= 0)
            {
                Debug.Log($"{targetStats.Name} został zraniony.");
            }

                   
            if ((targetStats.TempHealth < 0 && GameManager.IsHealthPointsHidingMode) || (targetStats.TempHealth < 0 && targetUnit.gameObject.CompareTag("EnemyUnit") && GameManager.IsStatsHidingMode))
            {
                Debug.Log($"Żywotność {targetStats.Name} spadła poniżej zera i wynosi <color=red>{targetStats.TempHealth}</color>.");
            }

            // Aktualizacja punktów żywotności
            if (!GameManager.IsHealthPointsHidingMode && !(GameManager.IsStatsHidingMode && targetUnit.gameObject.CompareTag("EnemyUnit")))
            {
                targetUnit.DisplayUnitHealthPoints();
                if (targetStats.TempHealth < 0)
                {
                    Debug.Log($"Punkty żywotności {targetStats.Name}: <color=red>{targetStats.TempHealth}</color><color=#4dd2ff>/{targetStats.MaxHealth}</color>");
                }
                else
                {
                    Debug.Log($"Punkty żywotności {targetStats.Name}: <color=#4dd2ff>{targetStats.TempHealth}/{targetStats.MaxHealth}</color>");
                }
            }
            else
            {
                targetUnit.HideUnitHealthPoints();
            }

            targetUnit.LastAttackerStats = attackerStats;

            //Aktualizuje żywotność w panelu jednostki, jeśli dostała obrażenia np. w wyniku ataku okazyjnego
            if (Unit.SelectedUnit == targetUnit.gameObject)
            {
                UnitsManager.Instance.UpdateUnitPanel(targetUnit.gameObject);
            }

            StartCoroutine(AnimationManager.Instance.PlayAnimation("damage", null, targetUnit.gameObject, damage - (targetWt + armor)));
        }
        else
        {
            Debug.Log($"Atak nie przebił się przez pancerz.");
        }
    }

    public void HandleDeath(Stats targetStats, GameObject target, Stats attackerStats)
    {
        // Zapobiega usunięciu postaci graczy, gdy statystyki przeciwników są ukryte
        if (GameManager.IsStatsHidingMode && targetStats.gameObject.CompareTag("PlayerUnit"))
        {
            return;
        }

        StartCoroutine(AnimationManager.Instance.PlayAnimation("kill", attackerStats.gameObject, target));

        //Aktualizuje osiągnięcia
        attackerStats.OpponentsKilled ++;
        if(attackerStats.StrongestDefeatedOpponentOverall < targetStats.Overall)
        {
            attackerStats.StrongestDefeatedOpponentOverall = targetStats.Overall;
            attackerStats.StrongestDefeatedOpponent = targetStats.Name;
        }

        // Usuwanie jednostki
        UnitsManager.Instance.DestroyUnit(target);

        // Aktualizacja podświetlenia pól w zasięgu ruchu atakującego
        GridManager.Instance.HighlightTilesInMovementRange();
    }
    #endregion

    #region Special unit abilities
    private void SpeedOfAttack(Stats attackerStats, Stats targetStats, Weapon attackerWeapon, Weapon targetWeapon, Unit attacker, Unit target)
    {
        //Rzut na trafienie
        int rollResult = UnityEngine.Random.Range(1, 101);

        if(!IsManualPlayerAttack)
        {
            //Sprawdza, czy atak jest atakiem dystansowym, czy atakiem w zwarciu i ustala jego skuteczność
            _isSuccessful = CheckAttackEffectiveness(rollResult, attackerStats, attackerWeapon, target);

            //Niepowodzenie przy pechu lub powodzenie przy szczęściu
            if (rollResult >= 96)
            {
                Debug.Log($"{attackerStats.Name} wyrzucił <color=red>PECHA</color> na trafienie!");
                _isSuccessful = false;

                //Aktualizuje osiągnięcia
                attackerStats.UnfortunateEvents ++;
            }
            else if(rollResult <= 5 && !(attackerWeapon.Quality != "Magiczna" && attackerStats.DaemonicAura != true && targetStats.Ethereal))
            {
                Debug.Log($"{attackerStats.Name} wyrzucił <color=green>SZCZĘŚCIE</color> na trafienie!");
                _isSuccessful = true;

                //Aktualizuje osiągnięcia
                attackerStats.FortunateEvents ++;
            }
        }

        //Atakowany próbuje parować lub unikać.
        if (_isSuccessful && rollResult > 5 && attackerWeapon.Type.Contains("melee") && (attacker.Feinted != true || AttackTypes["StandardAttack"] != true))
        {
            //Sprawdza, czy parowanie wymaga akcji
            var equippedWeapons = targetStats.GetComponent<Inventory>().EquippedWeapons;
            bool isFirstWeaponShield = equippedWeapons[0] != null && equippedWeapons[0].Type.Contains("shield");
            bool hasTwoOneHandedWeaponsOrShield = (equippedWeapons[0] != null && equippedWeapons[1] != null && equippedWeapons[0].Name != equippedWeapons[1].Name) || isFirstWeaponShield;

            bool canParry = target.CanParry && ((RoundsManager.Instance.UnitsWithActionsLeft.ContainsKey(target) && RoundsManager.Instance.UnitsWithActionsLeft[target] >= 1) || targetStats.LightningParry || hasTwoOneHandedWeaponsOrShield);

            //Sprawdzenie, czy jest aktywny tryb automatycznej obrony
            if (!GameManager.IsAutoDefenseMode && (canParry || target.CanDodge))
            {
                _parryAndDodgePanel.SetActive(true);

                _dodgeButton.gameObject.SetActive(target.CanDodge);
                _parryButton.gameObject.SetActive(canParry);

                StartCoroutine(WaitForDefenseReaction());

                // Korutyna obsługująca reakcję obronną
                IEnumerator WaitForDefenseReaction()
                {
                    // Czekaj na kliknięcie przycisku
                    Debug.Log("Wybierz reakcję atakowanej postaci.");
                    yield return new WaitUntil(() => _parryAndDodgePanel.activeSelf == false);

                    StartCoroutine(CheckForParryAndDodge(attackerWeapon, targetWeapon, targetStats, target, false));
                    
                    if(!GameManager.IsAutoDiceRollingMode)
                    {
                        yield return new WaitUntil(() => _rollResultPanel.activeSelf == false);
                    }

                    _isSuccessful = !TargetIsDefended;

                    ExecuteAttack(attacker, target, attackerWeapon);

                    //Zmienia jednostkę wg kolejności inicjatywy
                    if(RoundsManager.Instance.UnitsWithActionsLeft[attacker] == 0 && AvailableAttacks <= 0)
                    {
                        InitiativeQueueManager.Instance.SelectUnitByQueue();
                    }
                }
                _parryOrDodge = "";

                return;
            }

            if(canParry || target.CanDodge)
            {
                StartCoroutine(CheckForParryAndDodge(attackerWeapon, targetWeapon, targetStats, target, false));
                _isSuccessful = !TargetIsDefended;
            }
        }

        ExecuteAttack(attacker, target, attackerWeapon);
    }
    #endregion

    #region Calculating distance and validating distance attack
    public float CalculateDistance(GameObject attacker, GameObject target)
    {
        if (attacker != null && target != null)
        {
            _attackDistance = Vector2.Distance(attacker.transform.position, target.transform.position);
            return _attackDistance;
        }
        else
        {
            Debug.LogError("Nie udało się ustalić odległości pomiędzy walczącymi.");
            return 0;
        }
    }

    public bool ValidateRangedAttack(Unit attacker, Unit target, Weapon attackerWeapon, float attackDistance)
    {
        // Sprawdza, czy broń jest naładowana
        if (attackerWeapon.ReloadLeft != 0)
        {            
            if(!ReinforcementLearningManager.Instance.IsLearning)
            {
                Debug.Log($"Broń wymaga przeładowania.");
            }
            return false;
        }

        // Sprawdza, czy cel nie znajduje się zbyt blisko
        if (attackDistance <= 1.5f)
        {
            if(!ReinforcementLearningManager.Instance.IsLearning)
            {
                Debug.Log($"Jednostka stoi zbyt blisko celu, aby wykonać atak dystansowy.");
            }

            return false;
        }

        // Sprawdza, czy na linii strzału znajduje się przeszkoda
        RaycastHit2D[] raycastHits = Physics2D.RaycastAll(attacker.transform.position, target.transform.position - attacker.transform.position, attackDistance);

        foreach (var raycastHit in raycastHits)
        {
            if (raycastHit.collider == null) continue;

            var mapElement = raycastHit.collider.GetComponent<MapElement>();
            var unit = raycastHit.collider.GetComponent<Unit>();

            if (mapElement != null)
            {
                if (mapElement.IsHighObstacle)
                {
                    Debug.Log("Na linii strzału znajduje się przeszkoda, przez którą strzał jest niemożliwy.");
                    return false;
                }
            }
        }

        return true;
    }

    #endregion

    #region Check attack effectiveness
    private bool CheckAttackEffectiveness(int rollResult, Stats attackerStats, Weapon attackerWeapon, Unit targetUnit)
    {
        bool _isSuccessful = false;

        // Określenie typu ataku
        string attackType = AttackTypes["AllOutAttack"] ? "AllOutAttack" : AttackTypes["GuardedAttack"] ? "GuardedAttack" : "";

        // Obliczanie modyfikatora ataku
        _attackModifier = CalculateAttackModifier(attackerStats, attackerWeapon, targetUnit, attackType, _attackDistance);

        // Automatyczne trafienie, jeśli cel jest bezbronny
        if (targetUnit.HelplessDuration > 0)
        {
            return true;
        }

        // Sprawdzenie ataku dystansowego
        if (attackerWeapon.Type.Contains("ranged"))
        {
            int shieldModifier = targetUnit.GetComponent<Inventory>().EquippedWeapons
                .Where(weapon => weapon != null && weapon.Name == "Tarcza")
                .Select(_ => 10).FirstOrDefault();

            int totalModifier = _attackModifier - targetUnit.DefensiveBonus - shieldModifier;

            _isSuccessful = rollResult <= (attackerStats.US + totalModifier);

            Debug.Log($"{attackerStats.Name} atakuje {targetUnit.GetComponent<Stats>().Name} przy użyciu {attackerWeapon.Name}. Rzut na US: {rollResult} Wartość cechy: {attackerStats.US}" + $"{(totalModifier != 0 ? $" Modyfikator: {totalModifier}" : "")}");
            ResetWeaponLoad(attackerWeapon, attackerStats);
        }

        // Sprawdzenie ataku w zwarciu
        if (attackerWeapon.Type.Contains("melee"))
        {
            int totalModifier = _attackModifier - targetUnit.DefensiveBonus;

            _isSuccessful = rollResult <= (attackerStats.WW + totalModifier);

            Debug.Log($"{attackerStats.Name} atakuje {targetUnit.GetComponent<Stats>().Name} przy użyciu {attackerWeapon.Name}. Rzut na WW: {rollResult} Wartość cechy: {attackerStats.WW}" + $"{(totalModifier != 0 ? $" Modyfikator: {totalModifier}" : "")}");
        }

        // Odporność przeciwnika na niemagiczne ataki
        if (attackerWeapon.Quality != "Magiczna" && attackerStats.DaemonicAura != true && targetUnit.GetComponent<Stats>().Ethereal)
        {
            Debug.Log("Przeciwnik jest odporny na niemagiczne ataki. Aby go zranić, konieczne jest użycie magicznej broni lub zaklęcia.");
            return false;
        }

        return _isSuccessful;
    }


    //Oblicza modyfikator do trafienia
    private int CalculateAttackModifier(Stats attackerStats, Weapon attackerWeapon, Unit targetUnit, string attackType, float attackDistance = 0)
    {
        int attackModifier = _attackModifier;

        // Utrudnienie za atak słabszą ręką
        if (attackerStats.GetComponent<Inventory>().EquippedWeapons[0] == null || attackerWeapon.Name != attackerStats.GetComponent<Inventory>().EquippedWeapons[0].Name)
        {
            if (!attackerStats.Ambidextrous && attackerWeapon.Id != 0 && !attackerWeapon.Balanced)
            {
                attackModifier -= 20;
            }
        }

        // Modyfikatory za typ ataku
        if (attackType == "AllOutAttack") attackModifier += 20;
        else if (attackType == "GuardedAttack") attackModifier -= 10;

        // Modyfikatory za jakość broni
        if (attackerWeapon.Quality == "Kiepska") attackModifier -= 5;
        else if (attackerWeapon.Quality == "Najlepsza" || attackerWeapon.Quality == "Magiczna") attackModifier += 5;

        // Stany celu
        if (targetUnit.StunDuration > 0) attackModifier += 20;
        if (targetUnit.Trapped || targetUnit.Grappled || targetUnit.GrappledUnitId != 0) attackModifier += 20;

        // Modyfikator za dystans
        if (attackerWeapon.Type.Contains("ranged"))
        {
            attackModifier -= attackDistance > attackerWeapon.AttackRange ? 20 : 0;
        }
        else
        {
            // Przewaga liczebna
            attackModifier += CountOutnumber(attackerStats.GetComponent<Unit>(), targetUnit);
        }

        // Bijatyka
        if (attackerWeapon.Type.Contains("melee") && 
            (attackerWeapon.Id == 0 || attackerWeapon.Id == 11) && 
            attackerStats.StreetFighting)
        {
            attackModifier += 10;
        }

        //Zapiekła nienawiść
        if(attackerStats.GrudgeBornFury == true && _greenskinsList.Contains(targetUnit.GetComponent<Stats>().Race))
        {
            attackModifier += 5;
        }

        return attackModifier;
    }

    //Modyfikator za przewagę liczebną
    private int CountOutnumber(Unit attacker, Unit target)
    {
        if (attacker.CompareTag(target.tag)) return 0; // Jeśli atakujemy sojusznika to pomijamy przewagę liczebną

        int adjacentOpponents = 0; // Przeciwnicy atakującego stojący obok celu ataku
        int adjacentAllies = 0;    // Sojusznicy atakującego stojący obok celu ataku
        int adjacentOpponentsNearAttacker = 0; // Przeciwnicy atakującego stojący obok atakującego
        int modifier = 0;

        // Zbiór do przechowywania już policzonych przeciwników
        HashSet<Collider2D> countedOpponents = new HashSet<Collider2D>();

        // Funkcja pomocnicza do zliczania jednostek w sąsiedztwie danej pozycji
        void CountAdjacentUnits(Vector2 center, string allyTag, string opponentTag, ref int allies, ref int opponents)
        {
            Vector2[] positions = {
                center,
                center + Vector2.right,
                center + Vector2.left,
                center + Vector2.up,
                center + Vector2.down,
                center + new Vector2(1, 1),
                center + new Vector2(-1, -1),
                center + new Vector2(-1, 1),
                center + new Vector2(1, -1)
            };

            foreach (var pos in positions)
            {
                Collider2D collider = Physics2D.OverlapPoint(pos);
                if (collider == null) continue;

                if (collider.CompareTag(allyTag) && InventoryManager.Instance.ChooseWeaponToAttack(collider.gameObject).Type.Contains("melee"))
                {
                    allies++;
                }
                else if (collider.CompareTag(opponentTag) && !countedOpponents.Contains(collider) && InventoryManager.Instance.ChooseWeaponToAttack(collider.gameObject).Type.Contains("melee"))
                {
                    opponents++;
                    countedOpponents.Add(collider); // Dodajemy do zestawu zliczonych przeciwników
                }
            }
        }

        // Zlicza sojuszników i przeciwników atakującego w sąsiedztwie celu ataku
        CountAdjacentUnits(target.transform.position, attacker.tag, target.tag, ref adjacentAllies, ref adjacentOpponents);

        // Zlicza przeciwników atakujacego w sąsiedztwie atakującego (bez liczenia jego sojuszników, bo oni nie mają wpływu na przewagę)
        int ignoredAllies = 0; // Tymczasowy licznik, ignorowany
        CountAdjacentUnits(attacker.transform.position, attacker.tag, target.tag, ref ignoredAllies /* ignorujemy sojuszników */, ref adjacentOpponentsNearAttacker);

        // Dodaje przeciwników w sąsiedztwie atakującego do całkowitej liczby jego przeciwników
        adjacentOpponents += adjacentOpponentsNearAttacker;

        // Wylicza modyfikator na podstawie stosunku przeciwników do sojuszników atakującego
        if (adjacentAllies >= adjacentOpponents * 4)
        {
            modifier = 30;
        }
        else if (adjacentAllies >= adjacentOpponents * 3)
        {
            modifier = 20;
        }
        else if (adjacentAllies >= adjacentOpponents * 2)
        {
            modifier = 10;
        }

        return modifier;
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
            int roll1 = UnityEngine.Random.Range(1, 11);
            int roll2 = UnityEngine.Random.Range(1, 11);
            damageRollResult = roll1 >= roll2 ? roll1 : roll2;
            Debug.Log($"Atak druzgoczącą bronią. Rzut na obrażenia nr 1: {roll1} Rzut nr 2: {roll2}");

            //Uwzględnienie broni ciężkiej
            if(attackerWeapon.Tiring) attackerWeapon.Impact = false;
        }
        else
        {
            damageRollResult = UnityEngine.Random.Range(1, 11);
            Debug.Log($"Rzut na obrażenia: {damageRollResult}");
        }

        // Mechanika Furii Ulryka
        if (damageRollResult == 10)
        {
            int confirmRoll = UnityEngine.Random.Range(1, 101); //rzut na potwierdzenie Furii
            int additionalDamage = 0; //obrażenia, które dodajemy do wyniku rzutu

            if (_attackDistance <= 1.5f)
            {
                if (attackerStats.WW >= confirmRoll)
                {
                    additionalDamage = UnityEngine.Random.Range(1, 11);
                    damageRollResult += additionalDamage;
                    Debug.Log($"Rzut na potwierdzenie {confirmRoll}. <color=green>FURIA ULRYKA!</color>");
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
                    additionalDamage = UnityEngine.Random.Range(1, 11);
                    damageRollResult += additionalDamage;
                    Debug.Log($"Rzut na potwierdzenie {confirmRoll}. <color=green>FURIA ULRYKA!</color>");
                }
                else
                {
                    Debug.Log($"Rzut na potwierdzenie {confirmRoll}. Nie udało się potwierdzić Furii Ulryka.");
                }
            }

            while (additionalDamage == 10)
            {
                additionalDamage = UnityEngine.Random.Range(1, 11);
                damageRollResult += additionalDamage;
                Debug.Log($"KOLEJNA <color=green>FURIA ULRYKA!</color>");
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

            //Dodaje siłę do broni dystansowych opierających na niej swoje obrażenia (np. bicz)
            if(attackerWeapon.Type.Contains("strength-based")) damage += attackerStats.S;         
        }

        if (damage < 0) damage = 0;

        return damage;
    }
    #endregion

    #region Check for attack localization and return armor value
    private int CalculateArmor(Stats targetStats, Weapon attackerWeapon)
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

        //Podwaja wartość zbroi w przypadku walki przy użyciu pięści
        if(attackerWeapon.Id == 0) armor *= 2;

        //Uwzględnienie broni przebijających zbroję
        if (attackerWeapon.ArmourPiercing == true) armor --;

        //Uwzględnienie broni ignorujących zbroję
        if (attackerWeapon.ArmourIgnoring == true) armor = 0;

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
            unit.AimingBonus += unit.GetComponent<Stats>().Sharpshooter && attackerWeapon.Type.Contains("ranged") ? 20 : 10; 

            Debug.Log($"{unit.GetComponent<Stats>().Name} przycelowuje.");

            StartCoroutine(AnimationManager.Instance.PlayAnimation("aim", Unit.SelectedUnit));
        }

        UpdateAimButtonColor();
    }
    public void UpdateAimButtonColor()
    {
        if(Unit.SelectedUnit != null && Unit.SelectedUnit.GetComponent<Unit>().AimingBonus != 0)
        {
            _aimButton.GetComponent<UnityEngine.UI.Image>().color = UnityEngine.Color.green;
        }
        else
        {
            _aimButton.GetComponent<UnityEngine.UI.Image>().color = UnityEngine.Color.white;
        }
    }
    #endregion

    #region Charge
    public void Charge(GameObject attacker, GameObject target)
    {
        //Sprawdza pole, w którym atakujący zatrzyma się po wykonaniu szarży
        GameObject targetTile = GetTileAdjacentToTarget(attacker, target);

        Vector2 targetTilePosition = Vector2.zero;

        if(targetTile != null)
        {
            targetTilePosition = new Vector2(targetTile.transform.position.x, targetTile.transform.position.y);
        }
        else
        {
            Debug.Log($"Cel ataku stoi poza zasięgiem szarży.");
            return;
        }

        //Ścieżka ruchu szarżującego
        List<Vector2> path = MovementManager.Instance.FindPath(attacker.transform.position, targetTilePosition);

        //Sprawdza, czy postać jest wystarczająco daleko do wykonania szarży
        if (path.Count >= 2 && path.Count <= attacker.GetComponent<Stats>().TempSz)
        {
            //Zapisuje grę przed wykonaniem ruchu, aby użycie punktu szczęścia wczytywało pozycję przed wykonaniem szarży i można było wykonać ją ponownie
            SaveAndLoadManager.Instance.SaveUnits(UnitsManager.Instance.AllUnits, "autosave");

            _attackModifier += 10;

            MovementManager.Instance.MoveSelectedUnit(targetTile, attacker);

            // Wywołanie funkcji z wyczekaniem na koniec animacji ruchu postaci
            float delay = 0.25f;
            StartCoroutine(DelayedAttack(attacker, target, path.Count * delay));

            IEnumerator DelayedAttack(GameObject attacker, GameObject target, float delay)
            {
                yield return new WaitForSeconds(delay);

                if(attacker == null || target == null) yield break;

                Attack(attacker.GetComponent<Unit>(), target.GetComponent<Unit>(), false);
                     
                ChangeAttackType(); // Resetuje szarżę
            }
        }
        else
        {
            ChangeAttackType(); // Resetuje szarżę

            Debug.Log("Zbyt mała odległość na wykonanie szarży.");
        }
    }

    // Szuka wolnej pozycji obok celu szarży, do której droga postaci jest najkrótsza
    public GameObject GetTileAdjacentToTarget(GameObject attacker, GameObject target)
    {
        if(target == null) return null;

        Vector2 targetPos = target.transform.position;

        //Wszystkie przylegające pozycje do atakowanego
        Vector2[] positions = { targetPos + Vector2.right,
            targetPos + Vector2.left,
            targetPos + Vector2.up,
            targetPos + Vector2.down,
            targetPos + new Vector2(1, 1),
            targetPos + new Vector2(-1, -1),
            targetPos + new Vector2(-1, 1),
            targetPos + new Vector2(1, -1)
        };

        GameObject targetTile = null;

        //Długość najkrótszej ścieżki do pola docelowego
        int shortestPathLength = int.MaxValue;

        //Lista przechowująca ścieżkę ruchu szarżującego
        List<Vector2> path = new List<Vector2>();

        foreach (Vector2 pos in positions)
        {
            GameObject tile = GameObject.Find($"Tile {pos.x - GridManager.Instance.transform.position.x} {pos.y - GridManager.Instance.transform.position.y}");

            //Jeżeli pole jest zajęte to szukamy innego
            if (tile == null || tile.GetComponent<Tile>().IsOccupied) continue;

            path = MovementManager.Instance.FindPath(attacker.transform.position, pos);

            if(path.Count == 0) continue;

            // Aktualizuje najkrótszą drogę
            if (path.Count < shortestPathLength)
            {
                shortestPathLength = path.Count;
                targetTile = tile;
            }  
        }

        if(shortestPathLength > attacker.GetComponent<Stats>().TempSz && !GameManager.IsAutoCombatMode && !ReinforcementLearningManager.Instance.IsLearning)
        {
            return null;
        }
        else
        {
            return targetTile;
        }      
    }
    #endregion

    #region Defensive stance
    public void DefensiveStance()
    {
        if (Unit.SelectedUnit == null) return;

        Unit unit = Unit.SelectedUnit.GetComponent<Unit>();

        if (unit.DefensiveBonus == 0)
        {
            //Wykonuje akcję
            bool canDoAction = RoundsManager.Instance.DoFullAction(unit);
            if(!canDoAction) return;   

            Debug.Log($"{unit.GetComponent<Stats>().Name} przyjmuje pozycja obronną.");

            unit.DefensiveBonus = 20;
        }
        else
        {
            unit.DefensiveBonus = 0;
        }

        UpdateDefensiveStanceButtonColor();
    }
    public void UpdateDefensiveStanceButtonColor()
    {
        if(Unit.SelectedUnit.GetComponent<Unit>().DefensiveBonus > 0)
        {
            _defensiveStanceButton.GetComponent<UnityEngine.UI.Image>().color = UnityEngine.Color.green;

            //Wyświetla ikonkę pozycji obronnej przy tokenie jednostki
            Unit.SelectedUnit.transform.Find("Canvas/Defensive_stance_image").gameObject.SetActive(true);
        }
        else
        {
            _defensiveStanceButton.GetComponent<UnityEngine.UI.Image>().color = UnityEngine.Color.white;

            //Ukrywa ikonkę pozycji obronnej przy tokenie jednostki
            Unit.SelectedUnit.transform.Find("Canvas/Defensive_stance_image").gameObject.SetActive(false);
        }
    }
    #endregion

    #region Parry and dodge
    public IEnumerator CheckForParryAndDodge(Weapon attackerWeapon, Weapon targetWeapon, Stats targetStats, Unit targetUnit, bool isTouchSpellAttack)
    {
        TargetIsDefended = false;

        //Sprawdza, czy atakowany ma jakieś modifykatory do parowania
        int parryModifier = 0;
        if(isTouchSpellAttack)
        {
            parryModifier -= 20;
        }
        else
        {
            if (targetWeapon.Defensive) parryModifier += 10;
            if (attackerWeapon.Slow) parryModifier += 10;
            if (attackerWeapon.Fast) parryModifier -= 10;
            if (Unit.SelectedUnit != null && Unit.SelectedUnit.GetComponent<Stats>().PowerfulBlow) parryModifier -= 30;
        }

        //Uwzględnia karę do uników za ciężką zbroję
        //Sprawdza, czy zbroja nie jest wynikiem zaklęcia Pancerz Eteru
        bool etherArmor = false;
        if(MagicManager.Instance.UnitsStatsAffectedBySpell != null && MagicManager.Instance.UnitsStatsAffectedBySpell.Count > 0)
        {
            //Przeszukanie statystyk jednostek, na które działają zaklęcia czasowe
            for (int i = 0; i < MagicManager.Instance.UnitsStatsAffectedBySpell.Count; i++)
            {
                //Jeżeli wcześniejsza wartość zbroi (w tym przypadku na głowie, ale to może być dowolna lokalizacja) jest inna niż obecna, świadczy to o użyciu Pancerzu Eteru
                if (MagicManager.Instance.UnitsStatsAffectedBySpell[i].Name == targetStats.Name && MagicManager.Instance.UnitsStatsAffectedBySpell[i].Armor_head != targetStats.Armor_head)
                {
                    etherArmor = true;
                }
            }
        }
        int dodgeModifier = 0;
        if((targetStats.Armor_head >= 3 || targetStats.Armor_torso >= 3 || targetStats.Armor_arms >= 3 || targetStats.Armor_legs >= 3) && !etherArmor)
        {
            dodgeModifier = -10;
        }

        if (targetUnit.GuardedAttackBonus != 0) parryModifier += targetUnit.GuardedAttackBonus;

        int manualRollResult = 0;
        if(GameManager.IsAutoDefenseMode == false)
        {
            if (_parryOrDodge == "parry")
            {
                // Ręczne wpisanie wyniku na parowanie lub unik
                if (!GameManager.IsAutoDiceRollingMode && targetStats.CompareTag("PlayerUnit"))
                {
                    // Wywołaj korutynę i poczekaj na wynik
                    yield return StartCoroutine(WaitForRollValue(targetStats));
                    manualRollResult = _manualRollResult;
                }
                TargetIsDefended = Parry(targetStats, parryModifier, manualRollResult);
            }
            else if (_parryOrDodge == "dodge")
            {
                // Ręczne wpisanie wyniku na parowanie lub unik
                if (!GameManager.IsAutoDiceRollingMode && targetStats.CompareTag("PlayerUnit"))
                {
                    // Wywołaj korutynę i poczekaj na wynik
                    yield return StartCoroutine(WaitForRollValue(targetStats));
                    manualRollResult = _manualRollResult;
                }
                TargetIsDefended = Dodge(targetStats, dodgeModifier, manualRollResult);
            }
            else if(_parryOrDodge == "cancel")
            {
                yield break;
            }

            if(TargetIsDefended)
            {
                StartCoroutine(AnimationManager.Instance.PlayAnimation("parry", null, targetUnit.gameObject));
            }
            yield break;
        }

        if (targetUnit.CanParry && targetUnit.CanDodge)
        {
            /* Sprawdza, czy atakowana postać ma większą szansę na unik, czy na parowanie i na tej podstawie ustala kolejność tych akcji.
            Warunek sprawdza też, czy obrońca broni się Pięściami (Id=0). Parowanie pięściami jest możliwe tylko, gdy przeciwnik również atakuje Pięściami. */
            if (targetStats.WW + parryModifier > (targetStats.Zr + (targetStats.Dodge * 10) - 10 + dodgeModifier) && (targetWeapon.Id != 0 || targetWeapon.Id == attackerWeapon.Id))
            {
                TargetIsDefended = Parry(targetStats, parryModifier, manualRollResult);
            }
            else
            {
                TargetIsDefended = Dodge(targetStats, dodgeModifier, manualRollResult);
            }
        }
        else if (targetUnit.CanParry && (targetWeapon.Id != 0 || targetWeapon.Id == attackerWeapon.Id))
        {
            TargetIsDefended = Parry(targetStats, parryModifier, manualRollResult);
        }
        else if (targetUnit.CanDodge)
        {
            TargetIsDefended = Dodge(targetStats, dodgeModifier, manualRollResult);
        }

        if(TargetIsDefended)
        {
            StartCoroutine(AnimationManager.Instance.PlayAnimation("parry", null, targetUnit.gameObject));
        }
    }

    // Korutyna czekająca na wpisanie wyniku przez użytkownika
    private IEnumerator WaitForRollValue(Stats stats)
    {
        _isWaitingForRoll = true;
        _manualRollResult = 0;

        // Wyświetl panel do wpisania wyniku
        if (_rollResultPanel != null)
        {
            _rollResultPanel.SetActive(true);
            _rollResultPanel.GetComponentInChildren<TMP_Text>().text = "Wpisz wynik rzutu " + stats.Name;
        }

        // Wyzeruj pole tekstowe
        if (_parryOrDodgeInputField != null)
        {
            _parryOrDodgeInputField.text = "";
        }

        // Czekaj aż użytkownik wpisze wartość i kliknie Submit
        while (_isWaitingForRoll)
        {
            yield return null; // Czekaj na następną ramkę
        }

        // Ukryj panel po wpisaniu
        if (_rollResultPanel != null)
        {
            _rollResultPanel.SetActive(false);
        }
    }

    public void OnSubmitRoll()
    {
        if (_parryOrDodgeInputField != null && int.TryParse(_parryOrDodgeInputField.text, out int result))
        {
            _manualRollResult = result;
            _isWaitingForRoll = false; // Przerywamy oczekiwanie
            _parryOrDodgeInputField.text = ""; // Czyścimy pole
        }
    }
            
    private bool Parry(Stats targetStats, int parryModifier, int manualRollResult = 0)
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

        int rollResult = manualRollResult == 0 ? UnityEngine.Random.Range(1, 101) : manualRollResult;

        if (parryModifier != 0)
        {
            Debug.Log($"Rzut {targetStats.Name} na parowanie: {rollResult} Wartość cechy: {targetStats.WW} Modyfikator do parowania: {parryModifier}");
        }
        else
        {
            Debug.Log($"Rzut {targetStats.Name} na parowanie: {rollResult} Wartość cechy: {targetStats.WW}");
        }

        if(rollResult <= 5)
        {
            Debug.Log($"{targetStats.Name} wyrzucił <color=green>SZCZĘŚCIE</color> na parowanie!</color>");
 
            //Aktualizuje osiągnięcia
            targetStats.FortunateEvents ++;
            return true;
        }
        else if (rollResult <= targetStats.WW + parryModifier && rollResult < 96)
        {
            return true;
        }
        else if (rollResult >= 96)
        {
            Debug.Log($"{targetStats.Name} wyrzucił <color=red>PECHA</color> na parowanie!");
                        
            //Aktualizuje osiągnięcia
            targetStats.UnfortunateEvents ++;
            return false;
        }
        else
        {
            return false;
        }
    }

    private bool Dodge(Stats targetStats, int modifier, int manualRollResult = 0)
    {
        //Sprawia, że atakowany nie będzie mógł więcej unikać w tej rundzie   
        targetStats.GetComponent<Unit>().CanDodge = false;
        
        int rollResult = manualRollResult == 0 ? UnityEngine.Random.Range(1, 101) : manualRollResult;

        modifier += (targetStats.Dodge * 10) - 10;

        if (modifier != 0)
        {
            Debug.Log($"Rzut {targetStats.Name} na unik: {rollResult} Wartość cechy: {targetStats.Zr} Modyfikator: {modifier}");
        }
        else
        {
            Debug.Log($"Rzut {targetStats.Name} na unik: {rollResult} Wartość cechy: {targetStats.Zr}");
        }

        if(rollResult <= 5)
        {
            Debug.Log($"{targetStats.Name} wyrzucił <color=green>SZCZĘŚCIE</color> na unik!</color>");

            //Aktualizuje osiągnięcia
            targetStats.FortunateEvents ++;
            return true;
        }
        else if (rollResult <= targetStats.Zr + (targetStats.Dodge * 10) - 10 + modifier + targetStats.GetComponent<Unit>().GuardedAttackBonus && rollResult < 96)
        {
            return true;
        }      
        else if (rollResult >= 96)
        {
            Debug.Log($"{targetStats.Name} wyrzucił <color=red>PECHA</color> na unik!");

            //Aktualizuje osiągnięcia
            targetStats.UnfortunateEvents ++;
            return false;
        }
        else
        {
            return false;
        }  
    }

    void ParryOrDodgeButtonClick(string parryOrDodge)
    {
        _parryOrDodge = parryOrDodge;
    }
    #endregion

    #region Reloading
    public void Reload()
    {
        if(Unit.SelectedUnit == null) return;

        Weapon weapon = Unit.SelectedUnit.GetComponent<Inventory>().EquippedWeapons[0];

        if(weapon == null || !weapon.Type.Contains("ranged")) 
        {
            Debug.Log($"Wybrana broń nie wymaga ładowania.");
            return;
        }

        if(weapon.ReloadLeft > 0)
        {
            //Wykonuje akcję
            bool canDoAction;
            canDoAction = RoundsManager.Instance.DoHalfAction(Unit.SelectedUnit.GetComponent<Unit>());
            if(!canDoAction) return;  

            weapon.ReloadLeft --;

            StartCoroutine(AnimationManager.Instance.PlayAnimation("reload", Unit.SelectedUnit));       
        }
        
        if(weapon.ReloadLeft == 0)
        {
            Debug.Log($"Broń {Unit.SelectedUnit.GetComponent<Stats>().Name} załadowana.");
        }
        else
        {
            Debug.Log($"Ładowanie broni {Unit.SelectedUnit.GetComponent<Stats>().Name}. Pozostała/y {weapon.ReloadLeft} akcja/e do końca.");
        }  

        InventoryManager.Instance.DisplayReloadTime();    
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

        //Uwzględnia zdolność Artylerzysta
        if (attackerStats.MasterGunner == true && attackerWeapon.Type.Contains("gunpowder"))
        {
            attackerWeapon.ReloadLeft--;
        }

        //Zapobiega ujemnej wartości czasu przeładowania
        if(attackerWeapon.ReloadLeft < 0)
        {
            attackerWeapon.ReloadLeft = 0;
        }

        InventoryManager.Instance.DisplayReloadTime();
    }
    #endregion

    #region Feint
    private bool Feint(int rollResult, Stats attackerStats, Stats targetStats)
    {
        //Przeciwstawny rzut na WW
        int targetRollResult = UnityEngine.Random.Range(1, 101);

        int attackerSuccessLevel = attackerStats.WW - rollResult;
        int targetSuccessLevel = targetStats.WW - targetRollResult;

        Debug.Log($"{attackerStats.Name} wykonuje fintę. Następuje przeciwstawny rzut na WW. Atakujący rzuca: {rollResult} Wartość WW: {attackerStats.WW}. Atakowany rzuca: {targetRollResult} Wartość WW: {targetStats.WW}. Wynik: {attackerSuccessLevel} do {targetSuccessLevel}");

        if (attackerSuccessLevel > targetSuccessLevel)
        {
            return true;
        }
        else return false;
    }
    #endregion
 
    #region Grappling
    private IEnumerator Grappling(Stats attackerStats, Stats targetStats, bool isSuccessfull)
    { 
        if(Unit.SelectedUnit == null) yield break;

        // Stats attackerStats = Unit.SelectedUnit.GetComponent<Stats>();
        Unit attackerUnit = Unit.SelectedUnit.GetComponent<Unit>();
        Unit targetUnit = targetStats.GetComponent<Unit>();

        // Zmiana broni na pięści
        attackerUnit.GetComponent<Weapon>().ResetWeapon();
        Weapon attackerWeapon = attackerUnit.GetComponent<Weapon>();

        //Rzut obronny na Zr
        int targetRollResult;

        if(isSuccessfull == false)
        {
            Debug.Log($"Atak chybił. Próba pochwycenia nie powiodła się.");
            yield break;
        }
        
        // Jeżeli atakujący już wcześniej pochwycił cel, to próbuje zadać mu obrażenia
        if(attackerUnit.GrappledUnitId == targetUnit.UnitId)
        {
            int attackerRollResult;
            
            if (!GameManager.IsAutoDiceRollingMode && targetStats.CompareTag("PlayerUnit"))
            {
                // Wywołaj korutynę i poczekaj na wynik
                yield return StartCoroutine(WaitForRollValue(targetStats));
                targetRollResult = _manualRollResult;
            }
            else
            {
                targetRollResult = UnityEngine.Random.Range(1, 101);
            }

            if (!GameManager.IsAutoDiceRollingMode && attackerStats.CompareTag("PlayerUnit"))
            {
                // Wywołaj korutynę i poczekaj na wynik
                yield return StartCoroutine(WaitForRollValue(attackerStats));
                attackerRollResult = _manualRollResult;
            }
            else
            {
                attackerRollResult = UnityEngine.Random.Range(1, 101);
            }

            int attackerSuccessLevel = attackerStats.K - attackerRollResult;
            int targetSuccessLevel = targetStats.K - targetRollResult;

            Debug.Log($"{attackerStats.Name} próbuje zadać obrażenia {targetStats.Name}. Następuje przeciwstawny rzut na Krzepę. Pochwytujący rzuca: {attackerRollResult} Wartość cechy: {attackerStats.K}. Pochwycony rzuca: {targetRollResult} Wartość cechy: {targetStats.K}. Wynik: {attackerSuccessLevel} do {targetSuccessLevel}");

            if (attackerSuccessLevel > targetSuccessLevel)
            {
                int damage = 0;
                int armor = CalculateArmor(targetStats, attackerWeapon);

                if (!GameManager.IsAutoDiceRollingMode && attackerStats.CompareTag("PlayerUnit"))
                {
                    Debug.Log($"{attackerStats.Name} trafia w {targetStats.Name}. Zadaj obrażenia ręcznie wpisując wynik rzutu i kliknij \"Zatwierdź\".");  
                    _applyDamagePanel.SetActive(true);

                    StartCoroutine(WaitForDamageValue());

                    IEnumerator WaitForDamageValue()
                    {
                        // Czekaj na kliknięcie przycisku
                        yield return new WaitUntil(() => _applyDamagePanel.activeSelf == false);
                        if (int.TryParse(_damageInputField.text, out int inputDamage))
                        {
                            damage = CalculateDamage(inputDamage, attackerStats, attackerWeapon);
                            _damageInputField.text = null;
                        }

                        Debug.Log($"{attackerStats.Name} zadał {damage} obrażeń.");

                        //Aktualizuje aktywną postać na kolejce inicjatywy, jeśli atakujący nie ma już dostępnych akcji. Ta funkcja jest tu wywołana, dlatego że chcemy zastosować opóźnienie i poczekać ze zmianą jednostki do momentu wpisania wartości obrażeń
                        if(RoundsManager.Instance.UnitsWithActionsLeft[attackerUnit] == 0 && AvailableAttacks <= 0)
                        {
                            //Zmienia jednostkę wg kolejności inicjatywy
                            InitiativeQueueManager.Instance.SelectUnitByQueue();
                        }
                    }
                }
                else
                {
                    int damageRollResult = DamageRoll(attackerStats, attackerWeapon);
                    damage = CalculateDamage(damageRollResult, attackerStats, attackerWeapon);

                    Debug.Log($"{attackerStats.Name} zadał {damage} obrażeń.");
                }

                //Aktualizuje osiągnięcia
                attackerStats.TotalDamageDealt += damage;
                if(damage > attackerStats.HighestDamageDealt)
                {
                    attackerStats.HighestDamageDealt = damage;
                }

                //Zadaje obrażenia
                ApplyDamage(damage, attackerStats, targetStats, armor, targetUnit);

                //Sprawdza, czy atak spowodował śmierć
                if (targetStats.TempHealth < 0 && GameManager.IsAutoKillMode)
                {
                    HandleDeath(targetStats, targetUnit.gameObject, attackerStats);
                }
            }
            else
            {
                Debug.Log($"Atak {attackerStats.Name} chybił.");
            }
         
            yield break;
        }

        if (!GameManager.IsAutoDiceRollingMode && targetStats.CompareTag("PlayerUnit"))
        {
            // Wywołaj korutynę i poczekaj na wynik
            yield return StartCoroutine(WaitForRollValue(targetStats));
            targetRollResult = _manualRollResult;
        }
        else
        {
            targetRollResult = UnityEngine.Random.Range(1, 101);
        }

        if(targetRollResult <= targetStats.Zr)
        {
            Debug.Log($"{targetStats.Name} wykonuje rzut na Zr i rzuca: {targetRollResult} Wartość cechy: {targetStats.Zr}. Próba pochwycenia nie powiodła się.");
        }
        else
        {
            attackerUnit.GrappledUnitId = targetUnit.UnitId;
            targetUnit.Grappled = true;
            RoundsManager.Instance.UnitsWithActionsLeft[targetUnit] = 0;
            UnitsManager.Instance.UpdateUnitPanel(attackerUnit.gameObject);

            Debug.Log($"{targetStats.Name} wykonuje rzut na Zr i rzuca: {targetRollResult} Wartość cechy: {targetStats.Zr}. {targetStats.Name} został pochwycony przez {attackerStats.Name}.");
        }
    }

    public IEnumerator EscapeFromTheGrappling(Unit unit)
    {
        Stats targetStats = unit.GetComponent<Stats>();
        Unit attackerUnit = null;
        Stats attackerStats = null;

        foreach (Unit u in UnitsManager.Instance.AllUnits)
        {
            if(u.GrappledUnitId == unit.UnitId)
            {
                attackerUnit = u;
                attackerStats = u.GetComponent<Stats>();
            }
        }
        if(attackerUnit == null) yield break;

        //Przeciwstawny rzut na Krzepę
        int targetRollResult;
        int attackerRollResult;

        if (!GameManager.IsAutoDiceRollingMode && targetStats.CompareTag("PlayerUnit"))
        {
            // Wywołaj korutynę i poczekaj na wynik
            yield return StartCoroutine(WaitForRollValue(targetStats));
            targetRollResult = _manualRollResult;
        }
        else
        {
            targetRollResult = UnityEngine.Random.Range(1, 101);
        }

        if (!GameManager.IsAutoDiceRollingMode && attackerStats.CompareTag("PlayerUnit"))
        {
            // Wywołaj korutynę i poczekaj na wynik
            yield return StartCoroutine(WaitForRollValue(attackerStats));
            attackerRollResult = _manualRollResult;
        }
        else
        {
            attackerRollResult = UnityEngine.Random.Range(1, 101);
        }

        int attackerSuccessLevel = attackerStats.K - attackerRollResult;
        int targetSuccessLevel = targetStats.K - targetRollResult;

        Debug.Log($"{targetStats.Name} próbuje się uwolnić z pochwycenia. Następuje przeciwstawny rzut na Krzepę. Pochwycony rzuca: {targetRollResult} Wartość cechy: {targetStats.K}. Pochwytujący rzuca: {attackerRollResult} Wartość cechy: {attackerStats.K}. Wynik: {targetSuccessLevel} do {attackerSuccessLevel}");

        if (targetSuccessLevel > attackerSuccessLevel)
        {
            unit.Grappled = false;
            attackerUnit.GrappledUnitId = 0;

            UnitsManager.Instance.UpdateUnitPanel(targetStats.gameObject);
            Debug.Log($"{targetStats.Name} uwolnił/a się.");
        }
        else
        {
            Debug.Log($"Próba uwolnienia {targetStats.Name} nie powiodła się.");
        }  
    }
    #endregion

    #region Stun
    public void Stun(Weapon attackerWeapon, Stats attackerStats, Stats targetStats)
    {
        //Przeciwstawny rzut na Krzepę
        int attackerRollResult = UnityEngine.Random.Range(1, 101);
        int modifier = attackerWeapon.Pummelling ? 10 : 0; // bonus za broń z cechą "ogłuszający"
        int targetRollResult = UnityEngine.Random.Range(1, 101);

        int attackerSuccessLevel = attackerStats.K - attackerRollResult + modifier;
        int targetSuccessLevel = targetStats.K - targetRollResult;

        Debug.Log($"{attackerStats.Name} wykonuje ogłuszanie. Następuje przeciwstawny rzut na krzepę. Atakujący rzuca: {attackerRollResult} Wartość cechy: {attackerStats.K}. Atakowany rzuca: {targetRollResult} Wartość cechy: {targetStats.K}. Wynik: {attackerSuccessLevel} do {targetSuccessLevel}");

        if (attackerSuccessLevel > targetSuccessLevel)
        {
            // Rzut na odporność
            int odpRoll = UnityEngine.Random.Range(1, 101);
            int armorModifier = targetStats.Armor_head * 10; //Mofydikator do rzutu na odporność za zbroję na głowie

            Debug.Log($"Rzut na odporność {targetStats.Name}. Wynik rzutu: {odpRoll} Wartość cechy: {targetStats.Odp}. Modyfikator za hełm: {armorModifier}");

            if (odpRoll >= 96)
            {
                Debug.Log($"{targetStats.Name} wyrzucił <color=red>PECHA!</color>");                
                //Aktualizuje osiągnięcia
                targetStats.UnfortunateEvents ++;
            }
            else if (odpRoll <= 5)
            {
                Debug.Log($"{targetStats.Name} wyrzucił <color=green>SZCZĘŚCIE!</color>");                
                //Aktualizuje osiągnięcia
                targetStats.FortunateEvents ++;
            }

            if (odpRoll > (targetStats.Odp + modifier) && odpRoll > 5 || odpRoll >= 96)
            {
                int roundsNumber = UnityEngine.Random.Range(1, 11);

                if(targetStats.GetComponent<Unit>().StunDuration < roundsNumber)
                {
                    targetStats.GetComponent<Unit>().StunDuration = roundsNumber;
                    RoundsManager.Instance.UnitsWithActionsLeft[targetStats.GetComponent<Unit>()] = 0;
                    Debug.Log($"{targetStats.Name} zostaje ogłuszony na {roundsNumber} rund/y.");
                }
            }
            else
            {
                Debug.Log($"Ogłuszanie nie powiodło się.");
            }
        }
        else
        {
            Debug.Log($"Ogłuszanie nie powiodło się.");
        }
    }
    #endregion

    #region Trap
    //Próba uwolnienia się z unieruchomienia
    public IEnumerator EscapeFromTheSnare(Unit unit)
    {
        Stats unitStats = unit.GetComponent<Stats>();

        int rollResult;
        if (!GameManager.IsAutoDiceRollingMode && unitStats.CompareTag("PlayerUnit"))
        {
            // Wywołaj korutynę i poczekaj na wynik
            yield return StartCoroutine(WaitForRollValue(unitStats));
            rollResult = _manualRollResult;
        }
        else
        {
            rollResult = UnityEngine.Random.Range(1, 101);
        }

        string attributeName = unitStats.K > unitStats.Zr ? "Krzepę" : "Zręczność";
        int attributeValue = unitStats.K > unitStats.Zr ? unitStats.K : unitStats.Zr;

        Debug.Log($"{unitStats.Name} próbuje się uwolnić. Rzut na {attributeName}: {rollResult} Wartość cechy: {attributeValue}");

        if (rollResult >= 96)
        {
            Debug.Log($"{unitStats.Name} wyrzucił <color=red>PECHA!</color>");                
            //Aktualizuje osiągnięcia
            unitStats.UnfortunateEvents ++;
        }
        else if (rollResult <= 5)
        {
            Debug.Log($"{unitStats.Name} wyrzucił <color=green>SZCZĘŚCIE!</color>");                
            //Aktualizuje osiągnięcia
            unitStats.FortunateEvents ++;
        }

        if (rollResult < attributeValue && rollResult < 96 || rollResult <= 5)
        {
            unit.Trapped = false;

            foreach (var u in UnitsManager.Instance.AllUnits)
            {
                if(unit.UnitId == u.TrappedUnitId)
                {
                    u.TrappedUnitId = 0;
                }
            }

            UnitsManager.Instance.UpdateUnitPanel(unit.gameObject);
            Debug.Log($"{unitStats.Name} uwolnił/a się.");
        }
        else
        {
            Debug.Log($"Próba uwolnienia {unitStats.Name} nie powiodła się.");
        }    
    }
    #endregion

    #region Disarm
    private void Disarm(Stats attackerStats, Stats targetStats, Weapon targetWeapon)
    {
        if (targetWeapon.NaturalWeapon == true)
        {
            Debug.Log("Próba rozbrojenia nie powiodła się. Nie można rozbrajać jednostek walczących bronią naturalną.");
            return;
        }

        //Przeciwstawny rzut na Zręczność
        int attackerRollResult = UnityEngine.Random.Range(1, 101);
        int targetRollResult = UnityEngine.Random.Range(1, 101);

        int attackerSuccessLevel = attackerStats.Zr - attackerRollResult;
        int targetSuccessLevel = targetStats.Zr - targetRollResult;

        Debug.Log($"{attackerStats.Name} próbuje rozbroić {targetStats.Name}. Następuje przeciwstawny rzut na zręczność. Atakujący rzuca: {attackerRollResult} Wartość cechy: {attackerStats.Zr}. Atakowany rzuca: {targetRollResult} Wartość cechy: {targetStats.Zr}. Wynik: {attackerSuccessLevel} do {targetSuccessLevel}");

        if (attackerSuccessLevel > targetSuccessLevel)
        {
            targetStats.GetComponent<Weapon>().ResetWeapon();

            //Aktualizujemy tablicę dobytych broni
            Weapon[] equippedWeapons = targetStats.GetComponent<Inventory>().EquippedWeapons;
            for (int i = 0; i < equippedWeapons.Length; i++)
            {
                equippedWeapons[i] = null;
            }

            Debug.Log($"{targetStats.Name} został rozbrojony.");
        }
        else
        {
            Debug.Log($"Rozbrajanie nie powiodło się.");
        }
    }
    #endregion
}
