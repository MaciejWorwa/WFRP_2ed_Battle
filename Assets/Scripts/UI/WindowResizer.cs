using UnityEngine;

public class WindowResizer : MonoBehaviour
{
    private const float _aspectRatio = 16f / 9f; // Docelowe proporcje ekranu

    void Update()
    {
        // Sprawdź, czy tryb pełnoekranowy jest wyłączony
        if (!Screen.fullScreen)
        {
            // Uzyskaj aktualny rozmiar okna
            int currentWidth = Screen.width;
            int currentHeight = Screen.height;

            // Oblicz nową wysokość na podstawie szerokości i proporcji
            int newHeight = Mathf.RoundToInt(currentWidth / _aspectRatio);

            // Jeśli wysokość się zmienia, ustaw nowy rozmiar okna
            if (currentHeight != newHeight)
            {
                Screen.SetResolution(currentWidth, newHeight, false);
            }
        }
    }
}
