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
    [SerializeField] private UnityEngine.UI.Button _aimButton;
    [SerializeField] private UnityEngine.UI.Button _defensivePositionButton;

    #region Attack function
    public void Attack(Unit attacker, Unit target, bool opportunityAttack) 
    {
        //Sprawdza też, czy gra nie jest wstrzymana (np. poprzez otwarcie dodatkowych paneli)
        if(GameManager.Instance.IsGamePaused)
        {
            Debug.Log("Gra została wstrzymana. Aby ją wznowić musisz wyłączyć okno znajdujące się na polu gry.");
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
            //Sprawdza, czy broń jest naładowana (w przypadku ataku dystansowego)
            if (_attackDistance > 1.5f && attackerWeapon.ReloadLeft != 0)
            {
                Debug.Log($"Broń wymaga przeładowania.");
                return;
            }

            //Sprawdza, czy cel nie znajduje się zbyt blisko (w przypadku ataku dystansowego)
            if (_attackDistance <= 1.5f && attackerWeapon.Type[0] == "ranged")
            {
                Debug.Log($"Stoisz zbyt blisko celu aby wykonać atak dystansowy.");
                return;
            }

            //Wykonuje akcję (pomija szarżę, bo akcja została wykonaa na początku ruchu postaci, pomija też atak okazyjny)
            bool canDoAction = true;
            if(attacker.IsCharging !=true && opportunityAttack == false)
            {
                canDoAction = RoundsManager.Instance.DoHalfAction(attacker);
            }
            else if (attacker.IsCharging)
            {
                //Zresetowanie szarży
                MovementManager.Instance.UpdateMovementRange(1);

                canDoAction = RoundsManager.Instance.DoFullAction(attacker);
            }
            //DODAĆ OPCJE WIELOKROTNEGO, OSTROŻNEGO I SZALEŃCZEGO ATAKU

            if(!canDoAction) return;

            //Resetuje pozycję obronną, jeśli była aktywna
            if (attacker.DefensiveBonus != 0)
            {
                DefensivePosition();
            }  

            //Aktualizuje modyfikator ataku o celowanie
            _attackModifier += attacker.AimingBonus;

            //Rzut na trafienie
            int rollResult = Random.Range(1, 101);

            //Sprawdza, czy atak jest atakiem dystansowym, czy atakiem w zwarciu i ustala jego skuteczność
            bool isSuccessful = CheckAttackEffectiveness(rollResult, attackerStats, attackerWeapon, target);

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
                if (targetStats.TempHealth < 0 && GameManager.Instance.IsAutoKillMode)
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

            //Sprawia, że po ataku należy przeładować broń
            attackerWeapon.ReloadLeft = attackerWeapon.ReloadTime;
            attackerWeapon.WeaponsWithReloadLeft[attackerWeapon.Id] = attackerWeapon.ReloadLeft;

            //Uwzględnia zdolność Błyskawicznego Przeładowania
            if (attackerStats.RapidReload == true)
            {
                attackerWeapon.ReloadLeft--;   
            }
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

            //Dodaje modyfikator do trafienia uzwględniając
            unit.AimingBonus += Unit.SelectedUnit.GetComponent<Stats>().SureShot ? 20 : 10; 

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
            }
        }
        else
        {
            //Zresetowanie szarży
            MovementManager.Instance.UpdateMovementRange(1);

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
        if(GameManager.Instance.IsAutoDefenseMode)
        {
            //Sprawdza, czy atakowany ma jakieś modifykatory do parowania
            int parryModifier = 0;
            if (targetWeapon.Defensive) parryModifier += 10;
            if (attackerWeapon.Slow) parryModifier += 10;
            if (attackerWeapon.Fast) parryModifier -= 10;

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

        Debug.Log("isFirstWeaponShield " + isFirstWeaponShield);
        Debug.Log("hasTwoOneHandedWeaponsOrShield " + hasTwoOneHandedWeaponsOrShield);
        if(targetStats.LightningParry != true && hasTwoOneHandedWeaponsOrShield != true)
        {
            RoundsManager.Instance.DoHalfAction(targetStats.GetComponent<Unit>());
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

        if (rollResult <= targetStats.WW + parryModifier)
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

        if (rollResult <= targetStats.Zr + (targetStats.Dodge * 10) - 10)
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
    #endregion


}
