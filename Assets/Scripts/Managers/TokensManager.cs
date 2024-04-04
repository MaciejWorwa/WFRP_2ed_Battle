using UnityEngine;
using SimpleFileBrowser;
using System.Collections;
using UnityEngine.UI;
using System.IO;
using System.Collections.Generic;
using System;

public class TokensManager : MonoBehaviour
{
    // Prywatne statyczne pole przechowujące instancję
    private static TokensManager instance;

    // Publiczny dostęp do instancji
    public static TokensManager Instance
    {
        get { return instance; }
    }

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            //DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            // Jeśli instancja już istnieje, a próbujemy utworzyć kolejną, niszczymy nadmiarową
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // Konfiguracja SimpleFileBrowser
        FileBrowser.SetFilters(true, new FileBrowser.Filter("Images", ".jpg", ".png"));
        FileBrowser.SetDefaultFilter(".jpg");
    }

    public void OpenFileBrowser()
    {
        if(Unit.SelectedUnit == null) return;

        StartCoroutine(ShowLoadDialogCoroutine());
    }
    
    IEnumerator ShowLoadDialogCoroutine()
    {
        yield return FileBrowser.WaitForLoadDialog(FileBrowser.PickMode.Files, false, null, null, "Wybierz obraz", "Zatwierdź");

        if (FileBrowser.Success)
        {
            string filePath = FileBrowser.Result[0];
            StartCoroutine(LoadTokenImage(filePath, Unit.SelectedUnit));
        }
    }

    public IEnumerator LoadTokenImage(string filePath, GameObject unitObject)
    {
        if(unitObject == null) yield break;

        SpriteRenderer imageRenderer = unitObject.transform.Find("Token").GetComponent<SpriteRenderer>();

        //Ustawienie koloru na biały, żeby nie było overlaya koloru na tokenie
        imageRenderer.material.color = Color.white;

        byte[] byteTexture = File.ReadAllBytes(filePath);
        Texture2D texture = new Texture2D(2, 2);
        if (texture.LoadImage(byteTexture))
        {
            // Sprawdź rozdzielczość obrazu
            if (texture.width > 1024 || texture.height > 1024) // Ograniczenia rozdzielczości
            {
                Debug.LogError("Obraz jest za duży.");
            }
            else
            {
                float spriteWidth = imageRenderer.size.x; // Szerokość SpriteRenderer w jednostkach Unity
                float spriteHeight = imageRenderer.size.y; // Wysokość SpriteRenderer w jednostkach Unity

                // Oblicza pixelsPerUnit dla nowego sprite'a, biorąc pod uwagę rozmiar jednostki
                float pixelsPerUnitX = texture.width / spriteWidth;
                float pixelsPerUnitY = texture.height / spriteHeight;
                float pixelsPerUnit = Mathf.Max(pixelsPerUnitX, pixelsPerUnitY); // Wybierz większą wartość, aby zapewnić pełne pokrycie

                Sprite newSprite = Sprite.Create(texture, new Rect(0.0f, 0.0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), pixelsPerUnit);
                imageRenderer.sprite = newSprite;

                //Aktualizacja ścieżki do tokena jednostki
                Unit.SelectedUnit.GetComponent<Unit>().TokenFilePath = filePath;
            }
        }
        else
        {
            Debug.LogError("Nie udało się załadować obrazu.");
        }

        yield return null;
    }
}


