using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CreateUnitButton : MonoBehaviour, IPointerClickHandler
{
    public static Image SelectedUnitButtonImage;

    public void OnPointerClick(PointerEventData eventData)
    {
        if(UnitsManager.IsUnitEditing == false)
        {
            UnitsManager.Instance.CreateUnitMode();
        }

        //Zresetuj kolor poprzednio wybranego przycisku
        if (SelectedUnitButtonImage != null)
        {
            ResetColor(SelectedUnitButtonImage);
        }

        // Odniesienie do wybranego przycisku w panelu
        SelectedUnitButtonImage = this.GetComponent<Image>();

        //Zmień kolor nowo wybranego przycisku
        HighlightElement(SelectedUnitButtonImage);
    }

    public void ResetColor(Image image)
    {
        image.color = new Color(0.55f, 0.66f, 0.66f, 0.05f);
    }

    public void HighlightElement(Image image)
    {
        image.color = new Color(0f, 0.8f, 0f, 0.35f);
    }
}
