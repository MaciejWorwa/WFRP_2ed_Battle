using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AttackManager : MonoBehaviour
{
    // Prywatne statyczne pole przechowujące instancję
    private static AttackManager instance;

    // Publiczny dostęp do instancji
    public static AttackManager Instance
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

    public void Attack(Unit attacker, Unit target) 
    {
        Debug.Log(attacker);
        Debug.Log(target);
    }
}
