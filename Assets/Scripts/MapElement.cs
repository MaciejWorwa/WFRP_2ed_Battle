using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class MapElement : MonoBehaviour
{
    public bool IsHighObstacle;
    public bool IsLowObstacle;
    public bool IsCollider;

    void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    public void SetColliderState(bool state)
    {
        IsCollider = state;
        if (GetComponent<BoxCollider2D>() != null)
        {
            // Ustawienie kolidera w zależności od wartości IsCollider
            GetComponent<BoxCollider2D>().enabled = IsCollider;
        }
    }

    private void OnMouseOver()
    {
        // Jeżeli nie jesteśmy w kreatorze pola bitwy to funkcja usuwania przeszkód jest wyłączona. 
        // Tak samo nie wywołujemy jej, gdy lewy przycisk myszy nie jest wciśnięty
        if (SceneManager.GetActiveScene().buildIndex != 0) return;

        if (Input.GetMouseButtonDown(1)) // Sprawdza, czy prawy przycisk myszy jest wciśnięty
        {
            RotateElement();
        }

        if (GameManager.IsMousePressed)
        {
            DestroyElement();
        }
    }

    private void RotateElement()
    {
        // Obraca element o 90 stopni wokół osi Z
        transform.Rotate(0, 0, 90);
    }

    private void DestroyElement()
    {
        if (MapEditor.IsElementRemoving)
        {
            MapEditor.Instance.AllElements.Remove(gameObject);
            Destroy(gameObject);

            Collider2D collider = Physics2D.OverlapPoint(transform.position);

            if (collider != null && collider.gameObject.CompareTag("Tile"))
            {
                collider.GetComponent<Tile>().IsOccupied = false;
            }
        }
    }
}
