using UnityEngine;

public class MultiScreenDisplay : MonoBehaviour
{
     // Prywatne statyczne pole przechowujące instancję
    private static MultiScreenDisplay instance;

    // Publiczny dostęp do instancji
    public static MultiScreenDisplay Instance
    {
        get { return instance; }
    }

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            // Jeśli instancja już istnieje, a próbujemy utworzyć kolejną, niszczymy nadmiarową
            Destroy(gameObject);
        }
    }
    public Camera GamemasterCamera; // Główna kamera przypisana do pierwszego ekranu
    public Camera PlayersCamera; // Druga kamera przypisana do drugiego ekranu

    private int _originalCullingMask; // Zmienna do przechowywania oryginalnej maski warstw


    void Start()
    {
        // Zachowaj oryginalną maskę culling dla głównej kamery
        _originalCullingMask = GamemasterCamera.cullingMask;

        // Upewnia się, że masz podłączone dwa monitory
        if (Display.displays.Length > 1)
        {
            // Aktywuje drugi wyświetlacz
            Display.displays[1].Activate();
            
            // Przypisuje kamery do odpowiednich ekranów
            GamemasterCamera.targetDisplay = 0;  // Wyświetlacz 1
            PlayersCamera.gameObject.SetActive(true);
            PlayersCamera.targetDisplay = 1;  // Wyświetlacz 2

            // Jeśli druga kamera jest aktywna, ustawia oryginalną maskę culling dla głównej kamery
            GamemasterCamera.cullingMask = _originalCullingMask;
        }
        else
        {
            GamemasterCamera.cullingMask = ~0; // Sprawia, że renderowane są wszystkie warstwy, czyli TileCovers będą nieprzeźroczyste
            PlayersCamera.gameObject.SetActive(false);
            Debug.Log("Możesz podłączyć drugi monitor, aby wyświetlić na nim osobny widok dla graczy.");
        }
    }
}
