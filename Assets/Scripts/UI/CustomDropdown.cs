using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CustomDropdown : MonoBehaviour
{
    public List<Button> Buttons = new List<Button>();
    public int SelectedIndex = 0;
    public Button SelectedButton;
    private Color _defaultColor = new Color(0.55f, 0.66f, 0.66f, 0.05f); // Domyślny kolor przycisku
    private Color _selectedColor = new Color(1f, 1f, 1f, 0.2f); // Kolor wybranego przycisku
    private Color _activeColor = new Color(0.15f, 1f, 0.45f, 0.2f); // Kolor aktywnego przycisku

    private void Awake()
    {
        InitializeButtons();
    }

    public void InitializeButtons()
    {
        for (int i = 0; i < Buttons.Count; i++)
        {
            int capturedIndex = i;
            Buttons[capturedIndex].onClick.RemoveAllListeners();
            Buttons[capturedIndex].onClick.AddListener(() => SelectOption(capturedIndex + 1)); // Zakładamy, że indeksy zaczynają się od 1
        }
    }

    void SelectOption(int index)
    {
        if (SelectedIndex >= 1 && SelectedIndex <= Buttons.Count && Buttons[SelectedIndex - 1].GetComponent<Image>().color != _activeColor)
        {
            ResetColor(SelectedIndex);
        }
        
        SelectedButton = null;
        SelectedIndex = index;
        SelectedButton = Buttons[SelectedIndex - 1];

        if(Buttons[SelectedIndex - 1].GetComponent<Image>().color != _activeColor)
        {
            Buttons[SelectedIndex - 1].GetComponent<Image>().color = _selectedColor;
        }
    }

    public void MakeOptionActive(int index)
    {
        Buttons[index - 1].GetComponent<Image>().color = _activeColor;

        //W przypadku aktywnych (trzymanych) broni z ekwipunku pokazuje informację o ręce, w której dana broń jest trzymana
        InventoryManager.Instance.DisplayHandInfo(Buttons[index - 1]);
    }

    public void ResetColor(int index)
    {
        Buttons[index - 1].GetComponent<Image>().color = _defaultColor;

        //Ukrywa widok informacji dodatkowej
        Buttons[index - 1].transform.Find("hand_text").gameObject.SetActive(false);
    }

    public int GetSelectedIndex()
    {
        return SelectedIndex;
    }

    // Opcjonalnie: metoda do zaznaczenia przycisku programowo
    public void SetSelectedIndex(int index)
    {
        if (index >= 0 && index <= Buttons.Count)
        {
            SelectOption(index);
        }
    }
}
