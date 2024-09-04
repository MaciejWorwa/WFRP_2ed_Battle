using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class MapElement : MonoBehaviour
{
    public bool IsHighObstacle;
    public bool IsLowObstacle;

    void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }
    private void OnMouseOver()
    {
        // Jeżeli nie jesteśmy w kreatorze pola bitwy to funkcja usuwania przeszkód jest wyłączona. Tak samo nie wywołujemy jej, gdy lewy przycisk myszy nie jest wciśnięty
        if (SceneManager.GetActiveScene().buildIndex != 0 || GameManager.IsMousePressed == false) return;

        DestroyElement();
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
