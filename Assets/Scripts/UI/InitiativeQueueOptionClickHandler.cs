using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;

public class InitiativeQueueOptionClickHandler : MonoBehaviour, IPointerClickHandler
{
    public GameObject SelectedOptionBackground;

    public void OnPointerClick(PointerEventData eventData)
    {
        string unitName = transform.Find("Name_Text").GetComponent<TMP_Text>().text;

        //Wybranie zaznaczonej jednostki
        GameObject.Find(unitName).GetComponent<Unit>().SelectUnit();

        Transform initiativeQueue = RoundsManager.Instance.InitiativeScrollViewContent;

        //Zaznaczenie lub odznaczenie klikniêtej opcji
        SelectedOptionBackground.SetActive(!SelectedOptionBackground.activeSelf);

        //Odznaczenie wszystkich pozosta³ych opcji
        for (int i = 0; i < initiativeQueue.childCount; i++)
        {
            Transform child = initiativeQueue.GetChild(i).transform.Find("selected_option_background");

            if (child == SelectedOptionBackground.transform) continue;

            child.gameObject.SetActive(false);
        }
    }
}
