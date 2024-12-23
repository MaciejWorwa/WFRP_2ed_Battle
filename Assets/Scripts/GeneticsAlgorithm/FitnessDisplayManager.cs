using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class FitnessDisplayManager : MonoBehaviour
{
    [SerializeField] private TMP_Text _fitnessDisplayText;  // Referencja do pola tekstowego, które wyświetli średnią fitnessu
    [SerializeField] private TMP_Text _highestFitnessText;  // Referencja do pola tekstowego, które wyświetli najwyższy fitness
    [SerializeField] private TMP_Text _geneDistributionText; // Referencja do pola tekstowego, które wyświetli średnią ilość każdego genu

    private List<float> _allFitnessValues = new List<float>(); // Lista przechowująca wszystkie wartości fitness
    private float _totalFitness = 0f;   // Całkowita suma fitnessów
    private int _totalUnits = 0;        // Liczba jednostek
    private float _highestFitness = 0f; // Zmienna przechowująca najwyższy fitness

    private int[] _totalGeneCount = new int[11];  // Tablica przechowująca liczbę wystąpień każdego genu
    private int _totalGenerationsCounted = 0;     // Licznik generacji, w których była najlepsza jednostka

    public void ShowAverageFitness()
    {
        if (_totalUnits > 0) // Zapobiega dzieleniu przez 0
        {
            // Posortowanie listy fitnessów w porządku malejącym
            List<float> sortedFitnessValues = new List<float>(_allFitnessValues);
            sortedFitnessValues.Sort((a, b) => b.CompareTo(a)); // Sortowanie w porządku malejącym

            // Obliczamy średnią tylko dla 50% najlepszych fitnessów
            int halfUnits = Mathf.Max(1, sortedFitnessValues.Count / 2); // Zapewnia, że nie będzie dzielenia przez 0
            float topHalfFitnessSum = 0f;

            for (int i = 0; i < halfUnits; i++)
            {
                topHalfFitnessSum += sortedFitnessValues[i];
            }

            float averageFitness = topHalfFitnessSum / halfUnits;
            _fitnessDisplayText.text = "Średnia fitnessu najlepszych: " + averageFitness.ToString("F2");
        }

        // Wyświetlamy najwyższy fitness
        _highestFitnessText.text = "Najwyższy fitness: " + _highestFitness.ToString("F0");

        // Wyświetlamy średnią ilość każdego genu tylko dla najlepszej jednostki
        if (_totalGenerationsCounted > 0)
        {
            string geneDistribution = "";
            for (int i = 0; i < 11; i++)
            {
                float averageGeneCount = (float)_totalGeneCount[i] / _totalGenerationsCounted;
                geneDistribution += $"<color=green>Gen {i}:</color> {averageGeneCount:F2}<br>"; // Używamy <br> dla nowej linii w TMP_Text
            }
            _geneDistributionText.text = geneDistribution;
        }
    }

    // Metoda do dodawania fitnessu do listy i aktualizowania sumy oraz najwyższego fitnessu
    public void AddFitness(float fitness)
    {
        _allFitnessValues.Add(fitness);
        _totalFitness += fitness;
        _totalUnits++;

        // Aktualizujemy najwyższy fitness, jeśli nowy fitness jest większy
        if (fitness > _highestFitness)
        {
            _highestFitness = fitness;
        }
    }

    // Metoda resetowania wartości fitnessu (np. na początku nowej generacji)
    public void ResetFitnessData()
    {
        _allFitnessValues.Clear();
        _totalFitness = 0;
        _totalUnits = 0;
        _highestFitness = 0; // Resetujemy najwyższy fitness
        _totalGeneCount = new int[11]; // Resetujemy liczniki genów
        _totalGenerationsCounted = 0;
    }

    // Metoda do aktualizacji licznika genów tylko dla najlepszej jednostki
    public void UpdateGeneCount(int[] genes)
    {
        for (int i = 0; i < genes.Length; i++)
        {
            _totalGeneCount[genes[i]]++;  // Zwiększamy licznik dla każdego genu
        }
        _totalGenerationsCounted++;
    }
}
