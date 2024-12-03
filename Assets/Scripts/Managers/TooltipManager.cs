using UnityEngine;
using TMPro;

public class TooltipManager : MonoBehaviour
{
    public static TooltipManager Instance;

    [SerializeField] private RectTransform _tooltipTransform; // Kontener tooltipa
    [SerializeField] private RectTransform _textRectTransform; // Pole tekstowe
    [SerializeField] private TMP_Text _tooltipText; // TMP_Text w polu tekstowym
    [SerializeField] private float _paddingX = 35f; // Margines w poziomie
    [SerializeField] private float _paddingY = 34f; // Margines w pionie
    [SerializeField] private float _maxWidth = 300f; // Maksymalna szerokość tooltipa
    [SerializeField] private float _minWidth = 150f; // Minimalna szerokość tooltipa
    private bool _isTooltipActive = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }

        HideTooltip();
    }

    private void Update()
    {
        if (_tooltipTransform.gameObject.activeSelf)
        {
            Vector2 mousePosition = Input.mousePosition;

            // Dynamiczny offset w pionie
            float offsetY = _tooltipTransform.rect.height / 2.5f;

            // Przesunięcie tooltipa w górę jeśli nachodzi na kursor
            Vector2 newPosition = new Vector2(mousePosition.x, mousePosition.y + offsetY);
            float tooltipHeight = _tooltipTransform.rect.height;
            float tooltipWidth = _tooltipTransform.rect.width;

            if (newPosition.y - (tooltipHeight / 2) < mousePosition.y)
            {
                newPosition.y = mousePosition.y + (tooltipHeight / 2) + (0.035f * Screen.height);
            }

            // Zapobieganie wychodzeniu tooltipa poza krawędzie ekranu
            float screenWidth = Screen.width;
            float screenHeight = Screen.height;

            if (newPosition.x + (tooltipWidth / 2) > screenWidth)
            {
                newPosition.x = screenWidth - (tooltipWidth / 2) - 10f; // Dodaje odstęp od prawej krawędzi
            }
            else if (newPosition.x - (tooltipWidth / 2) < 0)
            {
                newPosition.x = (tooltipWidth / 2) + 10f; // Dodaje odstęp od lewej krawędzi
            }

            if (newPosition.y + (tooltipHeight / 2) > screenHeight)
            {
                newPosition.y = screenHeight - (tooltipHeight / 2) - 10f; // Dodaje odstęp od górnej krawędzi
            }
            else if (newPosition.y - (tooltipHeight / 2) < 0)
            {
                newPosition.y = (tooltipHeight / 2) + 10f; // Dodaje odstęp od dolnej krawędzi
            }

            // Przypisuje obliczoną pozycję
            _tooltipTransform.position = newPosition;
        }
    }

    public void ShowTooltip(string text)
    {
        if (_isTooltipActive) return;

        _tooltipText.text = text;

        // Obliczanie preferowanej szerokości i wysokości z ograniczeniami
        Vector2 textSize = _tooltipText.GetPreferredValues(text, _maxWidth, 0);
        float width = Mathf.Clamp(textSize.x, _minWidth, _maxWidth); // Ogranicz szerokość do zakresu
        float height = textSize.y; // Automatyczna wysokość uwzględniająca łamanie linii

        // Ustawienie szerokości i wysokości pola tekstowego
        _textRectTransform.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Horizontal,
            width
        );
        _textRectTransform.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            height
        );

        // Ustawienie rozmiaru kontenera tooltipa
        _tooltipTransform.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Horizontal,
            width + _paddingX
        );
        _tooltipTransform.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            height + _paddingY
        );

        _tooltipTransform.gameObject.SetActive(true);
        _isTooltipActive = true;
    }

    public void HideTooltip()
    {
        if (!_isTooltipActive) return;

        _tooltipTransform.gameObject.SetActive(false);
        _isTooltipActive = false;
    }
}
