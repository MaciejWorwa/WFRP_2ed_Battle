using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InputFieldFilter : MonoBehaviour
{
    [SerializeField] private TMP_InputField _inputField;
    [SerializeField] private bool _isAttributeInput;

    private void Start()
    {
        _inputField = GetComponent<TMP_InputField>();
        _inputField.onValidateInput += ValidateInput;
    }

    private char ValidateInput(string text, int charIndex, char addedChar)
    {
        if (_isAttributeInput)
        {
            // Sprawdź, czy dodany znak jest cyfrą i czy wartość nie przekracza 99
            if (char.IsDigit(addedChar) && (text.Length < 2 || text == "9" && addedChar <= '9'))
            {
                return addedChar;
            }
            else
            {
                return '\0'; // Blokuj dodanie nieprawidłowego znaku
            }
        }
        else
        {
            // Dozwolone znaki: cyfry, litery i spacje
            if (char.IsLetterOrDigit(addedChar) || char.IsWhiteSpace(addedChar))
            {
                return addedChar; // Zwróć dodany znak
            }
            else
            {
                return '\0'; // Zablokuj dodanie nieprawidłowego znaku
            }
        }
    }
}
