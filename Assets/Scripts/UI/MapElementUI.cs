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

        //Zmieñ kolor nowo wybranego elementu
        HighlightElement(SelectedElementImage);

        GameManager.IsMapElementPlacing = true;

        Debug.Log("Wybierz pole, na którym chcesz umieœciæ wybrany element otoczenia. Przytrzymuj¹c lewy przycisk myszy i przesuwaj¹c po mapie, mo¿esz umieszczaæ wiele elementów naraz.");
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
