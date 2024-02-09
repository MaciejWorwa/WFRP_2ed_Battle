using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Tile : MonoBehaviour
{
    [SerializeField] private Color _baseColor, _firstColor, _secondColor, _highlightColor, _rangeColor, _highlightRangeColor;
    private Renderer _renderer;
    public bool IsOccupied;

    [SerializeField] private MovementManager _movementManager;
    //private GridManager _gridManager;

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        _movementManager = GameObject.Find("MovementManager").GetComponent<MovementManager>();
        //_gridManager = GameObject.Find("GridManager").GetComponent<GridManager>();
    }

    public void Init(bool isOffset)
    {
        _renderer.material.color = isOffset ? _secondColor : _firstColor;
        _baseColor = _renderer.material.color;
        _rangeColor = _baseColor * 0.9f;
    }

    private void OnMouseEnter()
    {
        HighlightTile();      
    }

    private void OnMouseExit()
    {
        ResetTileHighlight();
    }

    private void OnMouseUp()
    {
        if(Unit.SelectedUnit != null)
        {
            _movementManager.MoveSelectedUnit(this.gameObject, Unit.SelectedUnit);     
        }
    }

    private void HighlightTile()
    {
        if(_renderer.material.color == _baseColor)
            _renderer.material.color = _highlightColor;
        else if (_renderer.material.color == _rangeColor)
            _renderer.material.color = _highlightRangeColor;
    }
    private void ResetTileHighlight()
    {
        if(_renderer.material.color == _highlightColor)
            _renderer.material.color = _baseColor;
        else if (_renderer.material.color == _highlightRangeColor)
            _renderer.material.color = _rangeColor;
    }

    public void SetRangeColor()
    {
        ResetRangeColor();

        if (Unit.SelectedUnit != null)
            _renderer.material.color = _rangeColor;
    }

    public void ResetRangeColor()
    {      
        _renderer.material.color = _baseColor;
    }
}
