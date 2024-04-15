using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MapElement : MonoBehaviour, IPointerClickHandler
{
    public void OnPointerClick(PointerEventData eventData)
    {
        // Za�aduj prefab
        GameObject elementPrefab = Resources.Load<GameObject>(this.gameObject.name);

        //TO JEST TYLKO TYMCZASOWO. DOCELOWO MA TO WYWOLYWAC FUNKCJE W MAP EDITOR, KT�RA W ZALE�NO�CI OD LOSOWEJ POZYCJI LUB KLIKNI�CIA MYSZY TWORZY OBIEKT SPRAWDZAJ�C, CZY POLE JEST WOLNE. JEDNOCZE�NIE USTAWIA WYBRANYTILE NA ZAJ�TY
        if (elementPrefab != null)
        {
            int x = Random.Range(0, GridManager.Instance.Width);
            int y = Random.Range(0, GridManager.Instance.Height);

            Vector3 randomPosition = new Vector3(x + GridManager.Instance.gameObject.transform.position.x, y + GridManager.Instance.gameObject.transform.position.y, 1);

            Instantiate(elementPrefab, randomPosition, Quaternion.identity);
        }
        else
        {
            Debug.LogError($"Nie znaleziono elementu {this.gameObject.name}");
        }
    }
}
