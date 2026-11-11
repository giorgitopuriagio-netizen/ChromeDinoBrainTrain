using System;

namespace ChromeDino.Core
{
    [Serializable]
    public sealed class EvolutionSettings
    {
        public int populationSize = 200;
        public int eliteCount = 4;
        public int tournamentSize = 4;
        public double mutationRate = 0.08;
        public double mutationStrength = 0.3;
        public double immigrantFraction = 0.05;

        public EvolutionSettings Clone() { return (EvolutionSettings)MemberwiseClone(); }

        public void Validate()
        {
            if (populationSize < 2 || populationSize > 1000 || eliteCount < 1 || eliteCount >= populationSize ||
                tournamentSize < 1 || tournamentSize > populationSize || !(mutationRate >= 0 && mutationRate <= 1) ||
                !(mutationStrength > 0 && mutationStrength <= 3) || !(immigrantFraction >= 0 && immigrantFraction < 0.5))
                throw new ArgumentException("Invalid evolution settings.");
        }
    }

    public sealed class GeneticAlgorithm
    {
        private readonly EvolutionSettings settings;
        private readonly DeterministicRandom random;

        public GeneticAlgorithm(EvolutionSettings settings, DeterministicRandom random)
        {
            settings.Validate();
            this.settings = settings.Clone();
            this.random = random;
        }

        // Survival is the primary fitness. Ties from the same simulation tick are explicit.
        public static int CompareFitness(DinoState first, DinoState second)
        {
            int comparison = first.survivalTicks.CompareTo(second.survivalTicks);
            if (comparison == 0) comparison = first.score.CompareTo(second.score);
            if (comparison == 0) comparison = first.passedObstacles.CompareTo(second.passedObstacles);
            return comparison;
        }

        public static int[] Rank(DinoState[] dinos)
        {
            var indices = new int[dinos.Length];
            for (int i = 0; i < indices.Length; i++) indices[i] = i;
            Array.Sort(indices, (first, second) =>
            {
                int comparison = CompareFitness(dinos[second], dinos[first]);
                return comparison == 0 ? first.CompareTo(second) : comparison;
            });
            return indices;
        }

        public BrainGenome[] Evolve(BrainGenome[] population, DinoState[] dinos)
        {
            if (population.Length != settings.populationSize || dinos.Length != population.Length)
                throw new ArgumentException("Population and fitness sizes must match.");
            var ranked = Rank(dinos);
            var offspring = new BrainGenome[population.Length];
            for (int i = 0; i < settings.eliteCount; i++) offspring[i] = population[ranked[i]].Clone();
            int immigrants = Math.Min(population.Length - settings.eliteCount,
                (int)(population.Length * settings.immigrantFraction));
            for (int i = settings.eliteCount; i < offspring.Length - immigrants; i++)
            {
                var first = population[Tournament(ranked)];
                var second = population[Tournament(ranked)];
                var child = first.Clone();
                for (int gene = 0; gene < child.genes.Length; gene++)
                {
                    child.genes[gene] = random.NextDouble() < 0.5 ? first.genes[gene] : second.genes[gene];
                    if (random.NextDouble() < settings.mutationRate)
                        child.genes[gene] += random.Gaussian() * settings.mutationStrength;
                    child.genes[gene] = Math.Max(-10, Math.Min(10, child.genes[gene]));
                }
                offspring[i] = child;
            }
            for (int i = offspring.Length - immigrants; i < offspring.Length; i++)
                offspring[i] = BrainGenome.Create(random);
            return offspring;
        }

        private int Tournament(int[] ranked)
        {
            int bestRank = ranked.Length - 1;
            for (int i = 0; i < settings.tournamentSize; i++) bestRank = Math.Min(bestRank, random.NextInt(ranked.Length));
            return ranked[bestRank];
        }
    }
}
