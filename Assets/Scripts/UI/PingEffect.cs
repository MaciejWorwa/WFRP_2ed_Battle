using UnityEngine;
using System.Collections;

public class PingEffect : MonoBehaviour
{
    [SerializeField] private GameObject _circlePrefab; // Prefab animowanego okręgu
    [SerializeField] private float _holdTime = 0.5f;     // Czas przytrzymania w sekundach
    [SerializeField] private float _animationDuration = 0.7f; // Czas trwania animacji

    private float _holdTimer = 0f;
    private bool _isHolding = false;
    private Vector3 _mousePosition;

    void Update()
    {
        // Sprawdzenie, czy prawy przycisk myszy jest wciśnięty
        if (Input.GetMouseButton(1))
        {
            if (!_isHolding)
            {
                // Rozpoczęcie liczenia czasu przytrzymania
                _isHolding = true;
                _holdTimer = 0f;
                _mousePosition = Input.mousePosition;
            }

            // Aktualizacja licznika czasu
            _holdTimer += Time.deltaTime;

            if (_holdTimer >= _holdTime)
            {
                // Tworzenie efektu w miejscu kliknięcia
                CreateCircleEffect(_mousePosition);
                _isHolding = false; // Zresetowanie flagi
            }
        }
        else
        {
            // Resetowanie, jeśli przycisk został zwolniony przed osiągnięciem czasu przytrzymania
            _isHolding = false;
            _holdTimer = 0f;
        }
    }

    private void CreateCircleEffect(Vector3 screenPosition)
    {
        // Konwersja pozycji ekranu na pozycję świata
        Vector3 worldPosition = Camera.main.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, Camera.main.nearClipPlane));

        // Tworzenie instancji okręgu
        GameObject circle = Instantiate(_circlePrefab, new Vector3(worldPosition.x, worldPosition.y, 0), Quaternion.identity);

        // Skalowanie okręgu w czasie
        StartCoroutine(AnimateCircle(circle));
    }

    private IEnumerator AnimateCircle(GameObject circle)
    {
        float elapsedTime = 0f;

        while (elapsedTime < _animationDuration)
        {
            // Stopniowe zwiększanie skali
            float scale = Mathf.Lerp(0, 1, elapsedTime / _animationDuration);
            circle.transform.localScale = new Vector3(scale, scale, 1);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // Usunięcie okręgu po zakończeniu animacji
        Destroy(circle);
    }
}
