using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    // Prywatne statyczne pole przechowujące instancję
    private static GameManager instance;

    // Publiczny dostęp do instancji
    public static GameManager Instance
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

    public bool IsAutoKillMode = false;
    [SerializeField] private Button _autoKillButton;
    public bool IsFriendlyFire = false;
    [SerializeField] private Button _friendlyFireButton;

    public void SetAutoKillMode()
    {
        IsAutoKillMode = !IsAutoKillMode;
        
        if(IsAutoKillMode)
        {
            _autoKillButton.GetComponent<Image>().color = Color.green;
            Debug.Log("Tryb automatycznej śmierci (gdy żywotność spanie poniżej zera) został włączony.");
        }
        else
        {
            _autoKillButton.GetComponent<Image>().color = Color.white;
            Debug.Log("Tryb automatycznej śmierci (gdy żywotność spanie poniżej zera) został wyłączony.");
        }
    }

    public void SetFriendlyFireMode()
    {
        IsFriendlyFire = !IsFriendlyFire;

        if (IsFriendlyFire)
        {
            _friendlyFireButton.GetComponent<Image>().color = Color.green;
            Debug.Log("Friendly fire został włączony.");
        }
        else
        {
            _friendlyFireButton.GetComponent<Image>().color = Color.white;
            Debug.Log("Friendly fire został wyłączony.");
        }
    }
}
