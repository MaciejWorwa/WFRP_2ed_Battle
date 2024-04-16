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
        // Konfiguracja SimpleFileBrowser po opóźnieniu
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
        if(unitObject == null || filePath.Length < 1) yield break;

        // Sprawdza, czy plik istnieje
        if (!File.Exists(filePath))
        {
            Debug.LogError($"<color=red>Plik graficzny z tokenem nie został znaleziony: {filePath}</color>");
            yield break;
        }

        //Aktywuje token
        unitObject.transform.Find("Token").gameObject.SetActive(true);

        SpriteRenderer imageRenderer = unitObject.transform.Find("Token").GetComponent<SpriteRenderer>();

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
                //Ustawienie koloru na biały, żeby nie było overlaya koloru na tokenie
                imageRenderer.material.color = Color.white;

                // Obliczanie nowego Rect, aby zachować proporcje 1:1
                int minSize = Mathf.Min(texture.width, texture.height);
                float offsetX = (texture.width - minSize) / 2f;
                float offsetY = (texture.height - minSize) / 2f;
                Rect rect = new Rect(offsetX, offsetY, minSize, minSize);

                // Obliczenie pixelsPerUnit, zachowując dopasowanie do wielkości sprite'a
                float spriteSize = Mathf.Min(imageRenderer.size.x, imageRenderer.size.y);
                float pixelsPerUnit = minSize / spriteSize;

                // Używanie nowego Rect i pixelsPerUnit do stworzenia sprite'a
                Sprite newSprite = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), pixelsPerUnit);
                imageRenderer.sprite = newSprite;

                // Aktualizacja ścieżki do tokena jednostki
                unitObject.GetComponent<Unit>().TokenFilePath = filePath;

                UnitsManager.Instance.UpdateUnitPanel(unitObject);
            }
        }
        else
        {
            //Dezatywuje token
            unitObject.transform.Find("Token").gameObject.SetActive(false);
            Debug.LogError("Nie udało się załadować obrazu.");
        }

        yield return null;
    }
}


