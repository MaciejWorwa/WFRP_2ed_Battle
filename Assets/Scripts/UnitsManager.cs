using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class UnitsManager : MonoBehaviour
{
    [SerializeField] private GameObject _unitPrefab;
    [SerializeField] private GridManager _unitPrefab2;
    public static bool isUnitPlacing;

    public void CreateUnit(Vector2? position = null)
    {
        // Sprawdza, czy pozycja zosta³a przekazana, jeœli nie, generuje losow¹
        Vector2 newPosition = position ?? new Vector2(Random.Range(-8, 9), Random.Range(-4, 5));

        GameObject newUnit = Instantiate(_unitPrefab, newPosition, Quaternion.identity);
        newUnit.AddComponent<Unit>();
    }
}
