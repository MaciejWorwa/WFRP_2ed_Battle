using UnityEngine;
using SimpleFileBrowser;
using System.Collections;
using UnityEngine.UI;
using System.IO;

public class ImageLoader : MonoBehaviour
{
    public SpriteRenderer imageRenderer; // Miejsce, gdzie wy�wietlany jest token

    void Start()
    {
        // Konfiguracja SimpleFileBrowser
        FileBrowser.SetFilters(true, new FileBrowser.Filter("Images", ".jpg", ".png"));
        FileBrowser.SetDefaultFilter(".jpg");

        if (PlayerPrefs.HasKey(gameObject.name))
        {
            string path = PlayerPrefs.GetString(gameObject.name);
            StartCoroutine(LoadImage(path));
        }
    }

    public void OpenFileBrowser()
    {
        StartCoroutine(ShowLoadDialogCoroutine());
    }

    IEnumerator ShowLoadDialogCoroutine()
    {
        yield return FileBrowser.WaitForLoadDialog(FileBrowser.PickMode.Files, false, null, null, "Wybierz obraz", "Zatwierd�");

        if (FileBrowser.Success)
        {
            string filePath = FileBrowser.Result[0];
            StartCoroutine(LoadImage(filePath));
        }
    }

    IEnumerator LoadImage(string filePath)
    {
        byte[] byteTexture = File.ReadAllBytes(filePath);
        Texture2D texture = new Texture2D(2, 2);
        if (texture.LoadImage(byteTexture))
        {
            // Sprawd� rozdzielczo�� obrazu
            if (texture.width > 1024 || texture.height > 1024) // Ograniczenia rozdzielczo�ci
            {
                Debug.LogError("Obraz jest za du�y.");
            }
            else
            {
                float spriteWidth = imageRenderer.size.x; // Szeroko�� SpriteRenderer w jednostkach Unity
                float spriteHeight = imageRenderer.size.y; // Wysoko�� SpriteRenderer w jednostkach Unity

                // Oblicza pixelsPerUnit dla nowego sprite'a, bior�c pod uwag� rozmiar jednostki
                float pixelsPerUnitX = texture.width / spriteWidth;
                float pixelsPerUnitY = texture.height / spriteHeight;
                float pixelsPerUnit = Mathf.Max(pixelsPerUnitX, pixelsPerUnitY); // Wybierz wi�ksz� warto��, aby zapewni� pe�ne pokrycie

                Sprite newSprite = Sprite.Create(texture, new Rect(0.0f, 0.0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), pixelsPerUnit);
                imageRenderer.sprite = newSprite;

                SaveImagePath(filePath);
            }
        }
        else
        {
            Debug.LogError("Nie uda�o si� za�adowa� obrazu.");
        }

        yield return null;
    }

    public void SaveImagePath(string path)
    {
        PlayerPrefs.SetString(gameObject.name, path);
        PlayerPrefs.Save();
    }
}
