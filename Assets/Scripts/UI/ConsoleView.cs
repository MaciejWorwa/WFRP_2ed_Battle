using UnityEngine;
using UnityEngine.UI;

namespace DebugStuff
{
    public class ConsoleView : MonoBehaviour
    {
        //#if !UNITY_EDITOR
        static string _myLog = "";
        private string _output;

        private bool _doShow;

        [SerializeField] private Slider _slider;

        void Start()
        {
            _doShow = false;
        }

        void OnEnable()
        {
            Application.logMessageReceived += Log;
        }

        void OnDisable()
        {
            Application.logMessageReceived -= Log;
        }

        public void Log(string logString, string stackTrace, LogType type)
        {
            _output = logString;
            _myLog = _output + "\n" + _myLog;
            if (_myLog.Length > 5000)
            {
                _myLog = _myLog.Substring(0, 4000);
            }
        }

        void OnGUI()
        {
            if (!_doShow) { return; }

            float consoleWidth = Screen.width * 0.42f; // szerokość okna konsoli
            float consoleHeight = Screen.height * (_slider.value); // wysokość okna konsoli rozwijająca się z góry na dół
            float consolePosX = Screen.width - consoleWidth; // pozycja X okna konsoli, w prawym górnym rogu
            float consolePosY = 0; // pozycja Y okna konsoli, zaczyna się od góry ekranu

            Rect consoleRect = new Rect(consolePosX, consolePosY, consoleWidth, consoleHeight);

            GUIStyle style = GUI.skin.GetStyle("Box"); // Wybranie stylu okna konsoli
            style.fontSize = (int)(Screen.width * 0.008f); // Wielkość czcionki
            style.alignment = TextAnchor.UpperLeft; // Wyrównanie tekstu do lewej górnej krawędzi okna konsoli

            _myLog = GUI.TextArea(consoleRect, _myLog, style);
        }

        public void ShowOrHideConsole()
        {
            _doShow = !_doShow;
        }
    }
}