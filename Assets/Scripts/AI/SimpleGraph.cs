using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SimpleGraph : MonoBehaviour
{
    public RectTransform graphContainer;
    public Sprite dotSprite;

    private List<float> valueList = new List<float>();

    public void AddValue(float value)
    {
        valueList.Add(value);
        DrawGraph();
    }

    private void DrawGraph()
    {
        // Usuwanie poprzednich punktów
        foreach (Transform child in graphContainer)
        {
            Destroy(child.gameObject);
        }

        float graphHeight = graphContainer.sizeDelta.y;
        float graphWidth = graphContainer.sizeDelta.x;
        float yMax = Mathf.Max(valueList.ToArray()) + 1f;
        float yMin = Mathf.Min(valueList.ToArray()) - 1f;

        for (int i = 0; i < valueList.Count; i++)
        {
            float xPosition = (graphWidth / (valueList.Count - 1)) * i;
            float yPosition = ((valueList[i] - yMin) / (yMax - yMin)) * graphHeight;

            CreateDot(new Vector2(xPosition, yPosition));
        }
    }

    private void CreateDot(Vector2 anchoredPosition)
    {
        GameObject dot = new GameObject("dot", typeof(Image));
        dot.transform.SetParent(graphContainer, false);
        dot.GetComponent<Image>().sprite = dotSprite;
        RectTransform rectTransform = dot.GetComponent<RectTransform>();
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = new Vector2(5, 5);
    }
}
