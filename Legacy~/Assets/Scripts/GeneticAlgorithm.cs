using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;

using Random = UnityEngine.Random;

public class GeneticAlgorithm : MonoBehaviour
{
    private Neurons_Network[] individuals;

    private int genesNum;
    private int generation;
    private int parentsNum;
    private int chromosomeLength;
    
    private double[][][] chromosomes;
    private double[][][] fromSelection;

    public double[][][] newGeneration { get; protected set; }
    
    public GeneticAlgorithm()
    {
        generation = 1;
        parentsNum = 4;
    
        chromosomes = null;
        fromSelection = null;
        newGeneration = null;
    }
    
    public void EvolutionStart(Neurons_Network[] individuals)
    {
        generation++;
        this.individuals = individuals;
        this.newGeneration = new double[this.individuals.Length][][];
        
        this.chromosomes = GetChromosomes(this.individuals);

        Grouping();
        Crosovering();
        Mutation();
    }

    private double[][][] GetChromosomes(Neurons_Network[] individuals)
    {
        double[][][] chromosomes = new double[individuals.Length][][];
        int lengthCounter = 0;

        for(int i = 0; i < individuals[0].neurons.Length; i++)
            for(int j = 0; j < individuals[0].neurons[i].Length; j++)
                if(individuals[0].neurons[i][j].weights != null) lengthCounter++;

        for(int i = 0; i < chromosomes.Length; i++)
            chromosomes[i] = new double[lengthCounter][];

        for(int i = 0; i < individuals.Length; i++)
            for(int j = 0, l = 0; j < individuals[i].neurons.Length; j++)
                for(int k = 0; k < individuals[i].neurons[j].Length; k++)
                    if(individuals[i].neurons[j][k].weights != null)
                    {
                        chromosomes[i][l] = individuals[i].neurons[j][k].weights;
                        l++;
                    }


        return chromosomes;
    }

    private void Grouping()
    {
        fromSelection = new double[parentsNum][][];
        
        fromSelection[0] = chromosomes[chromosomes.Length - 1];
    
        for (int i = 0; i < fromSelection.Length; i++)
        {
            fromSelection[i] = chromosomes[chromosomes.Length - (i + 1)];
        }
    }
    
    private void Crosovering()
    {
        for (int i = 0; i < chromosomes.Length; i++)
        {
            double[][] chromosome1 = null;
            double[][] chromosome2 = null;
    
            newGeneration[i] = new double[this.chromosomes[i].Length][];
    
            do
            {
                chromosome1 = fromSelection[Random.Range(0, parentsNum)];
                chromosome2 = fromSelection[Random.Range(0, parentsNum)];
            }
            while (chromosome1.Equals(chromosome2));

            int point = Random.Range(1, chromosome1.Length);
            
            for (int j = 0; j < point; j++)
                newGeneration[i][j] = chromosome1[j];

            for (int j = point; j < chromosome2.Length; j++)
                newGeneration[i][j] = chromosome2[j];
        }
    }
    
    private void Mutation()
    {
        for (int i = 0; i < newGeneration.Length; i++)
            if (Random.Range(0, 100) <= 50)
            {
                int mutable = Random.Range(0, newGeneration[i].Length);
                double[] newGene = new double[newGeneration[i][mutable].Length];

                for(int j = 0; j < newGeneration[i][mutable].Length; j++)
                    newGene[j] += Random.Range(-0.2f, 0.2f);

                newGeneration[i][mutable] = newGene;
            }
    }
}