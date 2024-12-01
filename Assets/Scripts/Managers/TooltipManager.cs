using UnityEngine;
using TMPro;

public class TooltipManager : MonoBehaviour
{
    public static TooltipManager Instance;

    [SerializeField] private RectTransform _tooltipTransform; // Kontener tooltipa
    [SerializeField] private RectTransform _textRectTransform; // Pole tekstowe
    [SerializeField] private TMP_Text _tooltipText; // TMP_Text w polu tekstowym
    [SerializeField] private float _paddingX = 20f; // Margines w poziomie
    [SerializeField] private float _paddingY = 34f; // Margines w pionie
    [SerializeField] private float _maxWidth = 400f; // Maksymalna szerokość tooltipa
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
            
            _tooltipTransform.position = new Vector2(mousePosition.x, mousePosition.y + offsetY);
        }
    }

    public void ShowTooltip(string text)
    {
        if (_isTooltipActive) return;

        _tooltipText.text = text;

        // Obliczanie preferowanej szerokości z ograniczeniami
        Vector2 textSize = _tooltipText.GetPreferredValues(text);
        float width = Mathf.Clamp(textSize.x, _minWidth, _maxWidth); // Ogranicz szerokość do zakresu
        float height = _tooltipText.preferredHeight;   // Automatyczna wysokość

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
