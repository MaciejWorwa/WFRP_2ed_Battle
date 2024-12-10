using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CameraManager : MonoBehaviour
{
    [SerializeField] private Camera _camera;
    [SerializeField] private float _moveSpeed = 5f;
    public static float MinZoom = 4f;
    public static float MaxZoom = 30f;
    public static Vector2 MaxXRange;
    public static Vector2 MaxYRange;
    public static Color BackgroundColor;

    private Vector3 _dragOrigin;

    void Start()
    {
        if(BackgroundColor != null)
        {
            _camera.backgroundColor = BackgroundColor;
        }
        else
        {
            _camera.backgroundColor = new Color(0.29f, 0.52f, 0.6f);
        }
    }

    void Update()
    {
        if(GameManager.IsGamePaused || GameManager.Instance.IsPointerOverUI() || GameManager.Instance.IsAnyInputFieldFocused()) return;

        float scrollInput = Input.GetAxis("Mouse ScrollWheel");

        // Oblicza nowy rozmiar pola widzenia kamery
        float zoomSize = _camera.orthographicSize - scrollInput * 5f;

        // Ogranicza rozmiar pola widzenia kamery do ustalonych granic
        zoomSize = Mathf.Clamp(zoomSize, MinZoom, MaxZoom);

        // Aktualizuje rozmiar pola widzenia kamery
        _camera.orthographicSize = zoomSize;

        // Dynamicznie dostosowuje prędkość ruchu w zależności od zoomu
        _moveSpeed = _camera.orthographicSize / MinZoom;

        float horizontalInput = 0f;
        float verticalInput = 0f;

        // Pobiera wejście z klawiszy strzałek
        horizontalInput = Input.GetAxis("Horizontal");
        verticalInput = Input.GetAxis("Vertical");

        //Gdy przytrzymujemy Ctrl lub Cmd do zapobiegamy przesuwaniu się kamery (bo np. Ctrl + A to włączenie tryby automatycznej walki)
        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) || Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand))
        {
            horizontalInput = 0;
            verticalInput = 0;
        }

        // Oblicza wektor ruchu dla przesunięcia kamery
        Vector3 moveDirection = new Vector3(horizontalInput, verticalInput, 0f) * _moveSpeed * Time.deltaTime;

        // Sprawdza, czy ruch przekracza zakresy i ogranicza go do zakresów
        float newX = Mathf.Clamp(transform.position.x + moveDirection.x, MaxXRange.x, MaxXRange.y);
        float newY = Mathf.Clamp(transform.position.y + moveDirection.y, MaxYRange.x, MaxYRange.y);
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

    public static void ChangeCameraRange(int width, int height)
    {
        MaxZoom = Math.Max(width, height) / 1.7f;
        MaxXRange = new Vector2(-width * 1.25f, width * 1.25f);
        MaxYRange = new Vector2(-height * 0.85f, height * 0.85f);
    }

    public void ChangeBackgroundColor(Color color)
    {
        _camera.backgroundColor = color;
        BackgroundColor = color;
    }
}