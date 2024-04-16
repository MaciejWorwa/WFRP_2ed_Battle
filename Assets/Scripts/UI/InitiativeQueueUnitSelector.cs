using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;

public class InitiativeQueueUnitSelector : MonoBehaviour, IPointerClickHandler
{
    public void OnPointerClick(PointerEventData eventData)
    {
        string unitName = transform.Find("Name_Text").GetComponent<TMP_Text>().text;
        
        Unit unit = null;
        foreach (KeyValuePair<Unit, int> pair in InitiativeQueueManager.Instance.InitiativeQueue)
        {
            if (pair.Key.GetComponent<Stats>().Name == unitName)
            {
                unit = pair.Key;
            }
        }

        if(unit != null)
        {
            unit.SelectUnit();
        }
    }
}
