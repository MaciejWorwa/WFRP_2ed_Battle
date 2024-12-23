using UnityEngine;
using System.Collections.Generic;

public class UnitGenome
{
    public int[] Genes = new int[30];
    public float Fitness = 0;
    public string UnitName; // Nazwa jednostki
    public int GenerationNumber; // Numer generacji, z której jednostka pochodzi

    private int _currentGeneIndex = 0; // Indeks aktualnie wykorzystywanego genu

    // Zamiast losowego wyboru, teraz wybieramy geny sekwencyjnie
    public int GetDecision()
    {
        // Pobieramy decyzję na podstawie aktualnego indeksu genu
        int decision = Genes[_currentGeneIndex];

        // Zwiększamy licznik genu. Jeśli dojdziemy do końca tablicy genów, wracamy do początku.
        _currentGeneIndex = (_currentGeneIndex + 1) % Genes.Length;

        return decision;
    }

    // Inicjalizacja losowych genów
    public void RandomizeGenes(string name, int generationNumber)
    {
        UnitName = name;
        this.GenerationNumber = generationNumber;

        for (int i = 0; i < Genes.Length; i++)
        {
            Genes[i] = UnityEngine.Random.Range(0, 11); // Zakres od 0 do 10
        }

        _currentGeneIndex = 0; // Resetujemy licznik przy randomizacji
    }

    // Krzyżowanie genomów
    public UnitGenome Crossover(UnitGenome otherParent)
    {
        UnitGenome offspring = new UnitGenome();

        for (int i = 0; i < Genes.Length; i++)
        {
            offspring.Genes[i] = (UnityEngine.Random.Range(0, 2) == 0) ? this.Genes[i] : otherParent.Genes[i];
        }

        offspring.UnitName = "Unit " + (GeneticAlgorithmManager.GenerationNumber) + " " + Random.Range(0, 1000);
        offspring.GenerationNumber = GeneticAlgorithmManager.GenerationNumber;

        return offspring;
    }

    // Mutacja genów
    public void Mutate(float mutationRate)
    {
        for (int i = 0; i < Genes.Length; i++)
        {
            if (UnityEngine.Random.Range(0f, 1f) < mutationRate)
            {
                Genes[i] = UnityEngine.Random.Range(0, 11); // Mutujemy na nową wartość
            }
        }
    }
}

[System.Serializable]
public class UnitGenomeData
{
    public int[] Genes;
    public float Fitness;
    public string UnitName;
    public int GenerationNumber;
}

// Struktura do serializacji genomów
[System.Serializable]
public class UnitGenomeSerializable
{
    public int[] Genes;
    public float Fitness;
}

// Kontener na listę genomów do serializacji
[System.Serializable]
public class UnitGenomesContainer
{
    public List<UnitGenomeData> Genomes; // Lista genomów jednostek
    public int GenerationNumber; // Numer generacji do zapisania w pliku JSON
}
