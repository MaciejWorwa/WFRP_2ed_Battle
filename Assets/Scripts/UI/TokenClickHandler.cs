using UnityEngine;
using UnityEngine.UI;

public class TokenClickHandler : MonoBehaviour
{
    public void OnTokenClick(Image tokenImage)
    {
        // Przekazuje sprite tokena do wyświetlenia w panelu
        if (tokenImage != null && tokenImage.sprite != null)
        {
            TokensManager.Instance.ShowTokenDisplayPanel(tokenImage.sprite);
        }
    }
}
