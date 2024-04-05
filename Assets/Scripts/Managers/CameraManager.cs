using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraManager : MonoBehaviour
{
    private Camera _camera;
    [SerializeField] private float _moveSpeed = 5f;
    [SerializeField] private float _minZoom = 4f;
    [SerializeField] private float _maxZoom = 15f;
    [SerializeField] private Vector2 _maxXRange = new Vector2(-40f, 40f);
    [SerializeField] private Vector2 _maxYRange = new Vector2(-20f, 20f);

    private Vector3 _dragOrigin;

    void Start()
    {
        _camera = Camera.main;
    }

    void Update()
    {
        if(GameManager.IsGamePaused || GameManager.Instance.IsAnyInputFieldFocused()) return;

        float scrollInput = Input.GetAxis("Mouse ScrollWheel");

        // Oblicza nowy rozmiar pola widzenia kamery
        float zoomSize = _camera.orthographicSize - scrollInput * _moveSpeed;

        // Ogranicza rozmiar pola widzenia kamery do ustalonych granic
        zoomSize = Mathf.Clamp(zoomSize, _minZoom, _maxZoom);

        // Aktualizuje rozmiar pola widzenia kamery
        _camera.orthographicSize = zoomSize;

        float horizontalInput = 0f;
        float verticalInput = 0f;

        // Pobiera wejście z klawiszy strzałek
        horizontalInput = Input.GetAxis("Horizontal");
        verticalInput = Input.GetAxis("Vertical");

        // Oblicza wektor ruchu dla przesunięcia kamery
        Vector3 moveDirection = new Vector3(horizontalInput, verticalInput, 0f) * _moveSpeed * Time.deltaTime;

        // Sprawdza, czy ruch przekracza zakresy i ogranicza go do zakresów
        float newX = Mathf.Clamp(transform.position.x + moveDirection.x, _maxXRange.x, _maxXRange.y);
        float newY = Mathf.Clamp(transform.position.y + moveDirection.y, _maxYRange.x, _maxYRange.y);
        moveDirection = new Vector3(newX, newY, -10f) - transform.position;

        // Przesuwa kamerę
        transform.Translate(moveDirection);
    }
    void LateUpdate()
    {
        if (Input.GetMouseButtonDown(2)) // Sprawdza, czy środkowy przycisk myszy został wciśnięty
        {
            _dragOrigin = Input.mousePosition; // Pobiera pozycję myszy w momencie wciśnięcia środkowego przycisku
            return;
        }

        if (Input.GetMouseButton(2)) // Sprawdza, czy środkowy przycisk myszy jest przytrzymany
        {
            Vector3 currentPosition = Input.mousePosition; // Aktualna pozycja myszy
            Vector3 dragDirection = (currentPosition - _dragOrigin) * (600f * _moveSpeed / Screen.width) * Time.deltaTime; // Kierunek przesunięcia

            transform.Translate(-dragDirection); // Przesuwa kamerę przeciwnie do kierunku przeciągnięcia

            _dragOrigin = currentPosition; // Aktualizuje pozycję początkową dla kolejnego kroku
        }
    }
}