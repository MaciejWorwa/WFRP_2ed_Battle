using System.Collections.Generic;
using UnityEngine;

public class AnimationManager : MonoBehaviour
{
    private Dictionary<Animator, bool> _panelStates = new Dictionary<Animator, bool>(); // Mapowanie Animator -> stan panelu

    public void TogglePanel(Animator panelAnimator)
    {
        // Sprawdza, czy animator ma już zapisany stan; jeśli nie, ustaw domyślny na "schowany"
        if (!_panelStates.ContainsKey(panelAnimator))
        {
            _panelStates[panelAnimator] = false; // Domyślnie panel jest schowany
        }

        // Zmiana stanu panelu
        bool isPanelOpen = _panelStates[panelAnimator];
        isPanelOpen = !isPanelOpen;
        _panelStates[panelAnimator] = isPanelOpen;

        // Odtwarzanie odpowiedniej animacji
        if (isPanelOpen)
        {
            panelAnimator.Play("ShowPanel"); // Nazwa animacji otwierającej panel
        }
        else
        {
            panelAnimator.Play("HidePanel"); // Nazwa animacji zamykającej panel
        }

        // Rotacja przycisku strzałki
        Transform arrowButtonTransform = panelAnimator.gameObject.transform.Find("arrow_button");
        if (arrowButtonTransform != null)
        {
            float currentZRotation = arrowButtonTransform.localRotation.eulerAngles.z; // Obecny kąt Z w stopniach
            float rotationAngle = isPanelOpen ? currentZRotation - 180 : currentZRotation + 180; // Oblicza nowy kąt
            arrowButtonTransform.localRotation = Quaternion.Euler(0, 0, rotationAngle); // Ustawia nową rotację
        }
    }
}
