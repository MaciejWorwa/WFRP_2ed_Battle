using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MapElementUI : MonoBehaviour, IPointerClickHandler
{
    public static Image SelectedElementImage;
    public static GameObject SelectedElement;

    public void OnPointerClick(PointerEventData eventData)
    {
        MapEditor.Instance.RemoveElementsMode(false);

        //Zresetuj kolor poprzednio wybranego elementu
        if (SelectedElementImage != null)
        {
            ResetColor(SelectedElementImage);
        }

        // Odniesienie do wybranego elementu w panelu
        SelectedElementImage = this.GetComponent<Image>();

        // Odniesienie do prefabu wybranego elementu
        SelectedElement = Resources.Load<GameObject>(this.gameObject.name);

        //Zmie� kolor nowo wybranego elementu
        HighlightElement(SelectedElementImage);

        GameManager.IsMapElementPlacing = true;

        Debug.Log("Wybierz pole, na kt�rym chcesz umie�ci� wybrany element otoczenia. Przytrzymuj�c lewy przycisk myszy i przesuwaj�c po mapie, mo�esz umieszcza� wiele element�w naraz.");
    }

    public void ResetColor(Image image)
    {
        image.color = Color.white;
    }

    public void HighlightElement(Image image)
    {
        image.color = new Color(0f, 0.8f, 0f, 0.5f);
    }
}
