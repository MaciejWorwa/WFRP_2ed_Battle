using UnityEngine;

public class GameSpeedController : MonoBehaviour
{
    [Range(1f, 100f)]
    public float gameSpeed = 1f;  // Prędkość gry kontrolowana z inspektora

    void Update()
    {
        // Aktualizacja prędkości gry na podstawie wartości z inspektora
        Time.timeScale = gameSpeed;
        // Ważne, aby upewnić się, że prędkość czasu nie jest mniejsza niż 0
        if (Time.timeScale < 0.1f)
        {
            Time.timeScale = 0.1f;
        }
    }
}
