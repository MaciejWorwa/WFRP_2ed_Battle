using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TileCover : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        // Ustawiamy obiekt, aby nie był niszczony przy ładowaniu nowej sceny
        DontDestroyOnLoad(gameObject);
    }

    // private void OnMouseDown()
    // {    
    //     Destroy(gameObject);
    // }
}
