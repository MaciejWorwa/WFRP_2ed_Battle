using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class TileCover : MonoBehaviour
{
    public int Number;

    // Start is called before the first frame update
    void Start()
    {
        // Ustawiamy obiekt, aby nie był niszczony przy ładowaniu nowej sceny
        DontDestroyOnLoad(gameObject);

        //Wyświetla numer pola, jeśli takowy posiada
        if(Number != 0)
        {
            transform.GetChild(0).gameObject.SetActive(true);
            GetComponentInChildren<TMP_Text>().text = Number.ToString();
        }
    }

    private void OnMouseOver()
    {   
        //Numerowanie zakrytych pól cyframi od 0 do 9 po wciśnięciu PPM
        if (Input.GetMouseButtonDown(1))
        {
            // Sprawdzenie, czy przytrzymany jest Ctrl lub Command
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) ||
                Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand) || Number >= 9)
            {
                // Resetowanie numeru
                Number = 0;
                transform.GetChild(0).gameObject.SetActive(false);
            }
            else
            {
                // Zwiększanie numeru
                Number++;
                transform.GetChild(0).gameObject.SetActive(true);
                GetComponentInChildren<TMP_Text>().text = Number.ToString();
            }
        }
    }
}
