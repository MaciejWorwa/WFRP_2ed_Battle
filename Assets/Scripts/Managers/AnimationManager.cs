using System.Collections.Generic;
using UnityEngine;

public class AnimationManager : MonoBehaviour
{
     // Prywatne statyczne pole przechowujące instancję
    private static AnimationManager instance;

    // Publiczny dostęp do instancji
    public static AnimationManager Instance
    {
        get { return instance; }
    }

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            // Jeśli instancja już istnieje, a próbujemy utworzyć kolejną, niszczymy nadmiarową
            Destroy(gameObject);
        }
    }
    
    public Dictionary<Animator, bool> PanelStates = new Dictionary<Animator, bool>(); // Mapowanie Animator -> stan panelu

    public void TogglePanel(Animator panelAnimator)
    {
        // Sprawdza, czy animator ma już zapisany stan; jeśli nie, ustaw domyślny na "schowany"
        if (!PanelStates.ContainsKey(panelAnimator))
        {
            PanelStates[panelAnimator] = false; // Domyślnie panel jest schowany
        }

        // Zmiana stanu panelu
        bool isPanelOpen = PanelStates[panelAnimator];
        isPanelOpen = !isPanelOpen;
        PanelStates[panelAnimator] = isPanelOpen;

        // Sprawdza, czy animator ma parametr "IsExpanded"
        if (HasParameter(panelAnimator, "IsExpanded"))
        {
            bool isExpanded = panelAnimator.GetBool("IsExpanded");

            if (isExpanded)
            {
                if (isPanelOpen)
                {
                    panelAnimator.Play("ShowExpandedPanel"); // Nazwa animacji otwierającej rozszerzony panel
                }
                else
                {
                    panelAnimator.Play("HideExpandedPanel"); // Nazwa animacji zamykającej rozszerzony panel
                }
            }
            else
            {
                PlayDefaultPanelAnimation(panelAnimator, isPanelOpen);
            }
        }
        else
        {
            PlayDefaultPanelAnimation(panelAnimator, isPanelOpen);
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

    // Funkcja pomocnicza do sprawdzania, czy animator posiada parametr o podanej nazwie
    private bool HasParameter(Animator animator, string paramName)
    {
        foreach (AnimatorControllerParameter param in animator.parameters)
        {
            if (param.name == paramName)
            {
                return true;
            }
        }
        return false;
    }

    // Funkcja do odtwarzania domyślnej animacji
    private void PlayDefaultPanelAnimation(Animator panelAnimator, bool isPanelOpen)
    {
        if (isPanelOpen)
        {
            panelAnimator.Play("ShowPanel"); // Nazwa animacji otwierającej panel
        }
        else
        {
            panelAnimator.Play("HidePanel"); // Nazwa animacji zamykającej panel
        }
    }

    public void ExpandPanel(Animator panelAnimator)
    {
        panelAnimator.Play("ExpandPanel");
        panelAnimator.SetBool("IsExpanded", true);
    }

    public void CollapsePanel(Animator panelAnimator)
    {
        panelAnimator.Play("CollapsePanel");
        panelAnimator.SetBool("IsExpanded", false);
    }
}
