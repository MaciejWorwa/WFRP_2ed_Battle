using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class ConsoleView : MonoBehaviour
{
    private List<string> _myLogs = new List<string>();
    private Vector2 _scrollPosition = Vector2.zero;
    private bool _doShow = true;
    //[SerializeField] private Slider _slider;
    private float _consoleWidth;
    private float _consoleHeight;

    void Start()
    {
        _consoleHeight = Screen.height * 0.33f;
        _consoleWidth = Screen.width * 0.4f;
    }

    private void OnEnable()
    {
        Application.logMessageReceived += HandleLog;
    }

    private void OnDisable()
    {
        Application.logMessageReceived -= HandleLog;
    }

    private void HandleLog(string logString, string stackTrace, LogType type)
    {
        _myLogs.Add(logString);
        // Ogranicz do 100 wiadomości i usuń najstarsze
        if (_myLogs.Count > 100)
        {
            _myLogs.RemoveAt(0);
        }
        // Zapewnij automatyczne przewijanie do najnowszej wiadomości
        _scrollPosition.y = Mathf.Infinity;
    }

    void OnGUI()
    {
        if (!_doShow) return;
        
        float consolePosX = Screen.width - _consoleWidth - 10;
        float consolePosY = Screen.height / 13;

        Rect consoleRect = new Rect(consolePosX, consolePosY, _consoleWidth, _consoleHeight);

        GUIStyle style = new GUIStyle(GUI.skin.box)
        {
            fontSize = (int)(Screen.width * 0.008f),
            alignment = TextAnchor.UpperLeft,
            wordWrap = true,
            padding = new RectOffset(5, 5, 5, 5)
        };
        
        // Usuń tło dla różnych stanów
        style.normal.background = null;
        style.hover.background = null;
        style.active.background = null;
        style.focused.background = null;

        // Ustaw ten sam kolor tekstu dla różnych stanów, aby zapobiec zmianie koloru
        Color textColor = Color.white;
        style.normal.textColor = textColor;
        style.hover.textColor = textColor;
        style.active.textColor = textColor;
        style.focused.textColor = textColor;

        GUILayout.BeginArea(consoleRect);
        _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, false, true);

        foreach (string log in _myLogs)
        {
            GUILayout.Label(log, style);
        }

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    public void ShowOrHideConsole()
    {
        _doShow = !_doShow;
    }
}
