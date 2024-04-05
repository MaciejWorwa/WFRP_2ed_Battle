using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class IncrementButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    private Button _button;
    [SerializeField] private int _incrementValue;
    private bool _isHeld = false;

    void Start()
    {
        _button = GetComponent<Button>();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (_button.interactable) // Sprawdzanie, czy przycisk jest aktywny
        {
            _isHeld = true;
            StartCoroutine(RepeatAction());
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _isHeld = false;
    }

    private IEnumerator RepeatAction()
    {
        while (_isHeld)
        {
            //Modyfikuje liczbę punktów żywotności jednostki
            UnitsManager.Instance.ChangeTemporaryHealthPoints(_incrementValue);
            
            yield return new WaitForSeconds(0.2f); // Czeka przed kolejnym wywołaniem
        }
    }
}
