using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InputFieldFilter : MonoBehaviour
{
    [SerializeField] private TMP_InputField _inputField;

    private void Start()
    {
        _inputField = GetComponent<TMP_InputField>();
        _inputField.onValidateInput += ValidateInput;
    }

    private char ValidateInput(string text, int charIndex, char addedChar)
    {
        // Dozwolone znaki: cyfry, litery i spacje
        if (char.IsLetterOrDigit(addedChar) || char.IsWhiteSpace(addedChar))
        {
            return addedChar; // Zwróæ dodany znak
        }
        else
        {
            return '\0'; // Zablokuj dodanie nieprawid³owego znaku
        }
    }
}
