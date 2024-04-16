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
        // Je¿eli nie jesteœmy w kreatorze pola bitwy to funkcja usuwania przeszkód jest wy³¹czona. Tak samo nie wywo³ujemy jej, gdy lewy przycisk myszy nie jest wciœniêty
        if (SceneManager.GetActiveScene().buildIndex != 0 || GameManager.IsMousePressed == false) return;

        DestroyElement();
    }

    private void DestroyElement()
    {
        if (MapEditor.IsElementRemoving)
        {
            Destroy(gameObject);

            Collider2D collider = Physics2D.OverlapCircle(transform.position, 0.1f);

            if (collider != null && collider.gameObject.CompareTag("Tile"))
            {
                collider.GetComponent<Tile>().IsOccupied = false;
            }

        }
    }
}
