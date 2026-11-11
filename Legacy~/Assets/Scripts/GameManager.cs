using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    static public Neurons_Network[] neuronNetworks;

    static public int dinoPopulation = 10;
    static public int generation = 0;
    
    static public double[][][] newGenerationWeights;

    private GeneticAlgorithm dinoEvolution;

    public GameObject dinoPreefab;    
    
    // Start is called before the first frame update
    void Start()
    {
        dinoEvolution = new GeneticAlgorithm();
        generation++;
        neuronNetworks = new Neurons_Network[dinoPopulation];

        DinoCreate(dinoPopulation, -6.8f, -4.03f);
    }

    public void DinoCreate(int quantity, float x, float y)
    {
        for(int i = 0; i < quantity; i++)
        {
            Instantiate(dinoPreefab, new Vector3(x, y, 0), Quaternion.identity);
            if(i % 10 == 0 && i != 0) x++;
        }
    }

    public void GeneticSelection()
    {
        dinoEvolution.EvolutionStart(neuronNetworks);
        
        newGenerationWeights = dinoEvolution.newGeneration;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
