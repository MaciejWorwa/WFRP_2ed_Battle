using UnityEngine;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;

public class ConsoleView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _consoleText;    // TextMeshPro do wyświetlania logów
    [SerializeField] private RectTransform _contentRect;      // RectTransform dla Content ScrollView
    [SerializeField] private ScrollRect _scrollRect;          // ScrollRect do przewijania
    [SerializeField] private RectTransform _scrollRectTransform; // RectTransform dla ScrollRect
    [SerializeField] private Animator _animator;
 

    private List<string> _myLogs = new List<string>();        // Lista do przechowywania logów
    private bool _doShow = true;                              // Flaga do pokazywania/ukrywania konsoli

    void Start()
    {
        if (_consoleText != null)
        {
            _consoleText.text = ""; // Początkowe wyczyszczenie tekstu konsoli
        }
    }

    private void OnEnable()
    {
        // Zarejestruj metodę do odbierania logów aplikacji
        Application.logMessageReceived += HandleLog;
    }

    private void OnDisable()
    {
        // Wyrejestruj metodę
        Application.logMessageReceived -= HandleLog;
    }

    private void HandleLog(string logString, string stackTrace, LogType type)
    {
        // Dodaj log do listy
        _myLogs.Add(logString);

        // Ogranicz do 100 wiadomości, usuń najstarsze
        if (_myLogs.Count > 100)
        {
            _myLogs.RemoveAt(0);
        }

        // Zaktualizuj zawartość TextMeshPro z logami
        UpdateConsoleText();
    }

    private void UpdateConsoleText()
    {
        if (_consoleText != null && _doShow)
        {
            _consoleText.text = string.Join("\n", _myLogs);
            
            // Dostosuj wysokość Content do ilości tekstu
            AdjustContentHeight();

            // Przewiń do dołu po dodaniu nowej wiadomości
            Canvas.ForceUpdateCanvases(); // Upewnia się, że interfejs UI został zaktualizowany przed przewijaniem
            _scrollRect.verticalNormalizedPosition = 0f; // Przewiń na dół
        }
    }

    private void AdjustContentHeight()
    {
        if (_consoleText != null && _contentRect != null)
        {
            // Wyznacz wysokość na podstawie preferowanej wysokości tekstu
            float preferredHeight = _consoleText.preferredHeight;
            _contentRect.sizeDelta = new Vector2(_contentRect.sizeDelta.x, preferredHeight);
        }
    }

    public void ShowOrHideConsole()
    {
        _doShow = !_doShow;

        // Pokazywanie lub ukrywanie konsoli
        if (_consoleText != null)
        {
            _consoleText.gameObject.SetActive(_doShow);
        }
    }
}
