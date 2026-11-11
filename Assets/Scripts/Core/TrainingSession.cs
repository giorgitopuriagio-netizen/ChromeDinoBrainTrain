using System;

namespace ChromeDino.Core
{
    [Serializable]
    public sealed class GenerationChampion
    {
        public int formatVersion = 1;
        public int rulesVersion = GameRules.Version;
        public string runId;
        public int generation;
        public int dinoIndex;
        public int score;
        public int survivalTicks;
        public int tiedSurvivors;
        public bool generationCompleted;
        public uint courseSeed;
        public BrainGenome brain;
    }

    [Serializable]
    public sealed class TrainingSnapshot
    {
        public int formatVersion = 1;
        public int rulesVersion = GameRules.Version;
        public string runId;
        public string createdUtc;
        public int generation;
        public int bestScore;
        public uint geneticRandomState;
        public uint courseSeed;
        public EvolutionSettings settings;
        public BrainGenome[] population;
        public WorldSnapshot world;
    }

    public sealed class NeuralPilot
    {
        private readonly NeuralNetwork network;
        private readonly double[] inputs = new double[10];

        public NeuralPilot(BrainGenome genome) { network = new NeuralNetwork(genome); }

        public DinoAction Decide(DinoWorld world, int dinoIndex)
        {
            world.ReadSensors(dinoIndex, inputs);
            var output = network.Evaluate(inputs);
            return new DinoAction { jump = output[0] > 0.5, duck = output[1] > 0.5 && output[1] > output[0] };
        }
    }

    public sealed class TrainingSession
    {
        private readonly EvolutionSettings settings;
        private readonly DeterministicRandom random;
        private readonly GeneticAlgorithm evolution;
        private BrainGenome[] population;
        private NeuralPilot[] pilots;
        private DinoAction[] actions;
        private uint courseSeed;

        public string RunId { get; private set; }
        public string CreatedUtc { get; private set; }
        public int Generation { get; private set; }
        public int BestScore { get; private set; }
        public DinoWorld World { get; private set; }
        public int PopulationSize { get { return population.Length; } }

        public TrainingSession(string runId, EvolutionSettings settings, uint seed)
        {
            settings.Validate();
            RunId = runId;
            CreatedUtc = DateTime.UtcNow.ToString("O");
            this.settings = settings.Clone();
            random = new DeterministicRandom(seed);
            evolution = new GeneticAlgorithm(this.settings, random);
            population = new BrainGenome[settings.populationSize];
            for (int i = 0; i < population.Length; i++) population[i] = BrainGenome.Create(random);
            Generation = 1;
            StartGeneration();
        }

        public TrainingSession(TrainingSnapshot snapshot)
        {
            if (snapshot == null || snapshot.formatVersion != 1 || snapshot.rulesVersion != GameRules.Version ||
                string.IsNullOrEmpty(snapshot.runId) || snapshot.generation < 1 || snapshot.bestScore < 0 ||
                snapshot.settings == null || snapshot.population == null || snapshot.geneticRandomState == 0 ||
                snapshot.courseSeed == 0) throw new ArgumentException("Incompatible training checkpoint.");
            snapshot.settings.Validate();
            if (snapshot.population.Length != snapshot.settings.populationSize)
                throw new ArgumentException("Incomplete checkpoint population.");
            settings = snapshot.settings.Clone();
            random = new DeterministicRandom(snapshot.geneticRandomState);
            evolution = new GeneticAlgorithm(settings, random);
            population = new BrainGenome[snapshot.population.Length];
            for (int i = 0; i < population.Length; i++)
            {
                if (snapshot.population[i] == null) throw new ArgumentException("Missing brain.");
                snapshot.population[i].Validate();
                if (i > 0)
                {
                    var topology = snapshot.population[0].layers;
                    var candidate = snapshot.population[i].layers;
                    if (candidate.Length != topology.Length) throw new ArgumentException("Population topologies do not match.");
                    for (int layer = 0; layer < topology.Length; layer++)
                        if (candidate[layer] != topology[layer]) throw new ArgumentException("Population topologies do not match.");
                }
                population[i] = snapshot.population[i].Clone();
            }
            World = new DinoWorld(snapshot.world);
            if (World.Dinos.Length != population.Length || World.AliveCount == 0)
                throw new ArgumentException("Checkpoint world and population do not match.");
            RunId = snapshot.runId;
            CreatedUtc = snapshot.createdUtc;
            Generation = snapshot.generation;
            BestScore = snapshot.bestScore;
            courseSeed = snapshot.courseSeed;
            CreatePilots();
        }

        private void CreatePilots()
        {
            pilots = new NeuralPilot[population.Length];
            actions = new DinoAction[population.Length];
            for (int i = 0; i < pilots.Length; i++) pilots[i] = new NeuralPilot(population[i]);
        }

        private void StartGeneration()
        {
            courseSeed = random.NextUInt();
            World = new DinoWorld(population.Length, courseSeed);
            CreatePilots();
        }

        // Null means the current generation is still running. No timer stops a survivor.
        public GenerationChampion Tick()
        {
            for (int i = 0; i < pilots.Length; i++)
                actions[i] = World.Dinos[i].alive ? pilots[i].Decide(World, i) : default(DinoAction);
            World.Tick(actions);
            foreach (var dino in World.Dinos) BestScore = Math.Max(BestScore, dino.score);
            if (World.AliveCount > 0) return null;

            int winnerIndex = GeneticAlgorithm.Rank(World.Dinos)[0];
            var winner = World.Dinos[winnerIndex];
            int ties = 0;
            foreach (var dino in World.Dinos) if (dino.survivalTicks == winner.survivalTicks) ties++;
            var champion = new GenerationChampion { runId = RunId, generation = Generation,
                dinoIndex = winnerIndex, score = winner.score, survivalTicks = winner.survivalTicks,
                tiedSurvivors = ties, generationCompleted = true, courseSeed = courseSeed, brain = population[winnerIndex].Clone() };
            population = evolution.Evolve(population, World.Dinos);
            Generation++;
            StartGeneration();
            return champion;
        }

        public GenerationChampion CaptureLivingSurvivor()
        {
            if (World.AliveCount != 1) return null;
            for (int i = 0; i < World.Dinos.Length; i++)
            {
                var dino = World.Dinos[i];
                if (!dino.alive) continue;
                return new GenerationChampion { runId = RunId, generation = Generation, dinoIndex = i,
                    score = dino.score, survivalTicks = dino.survivalTicks, tiedSurvivors = 1,
                    generationCompleted = false, courseSeed = courseSeed, brain = population[i].Clone() };
            }
            return null;
        }

        public TrainingSnapshot Capture()
        {
            var copy = new BrainGenome[population.Length];
            for (int i = 0; i < copy.Length; i++) copy[i] = population[i].Clone();
            return new TrainingSnapshot { runId = RunId, createdUtc = CreatedUtc, generation = Generation,
                bestScore = BestScore, settings = settings.Clone(), population = copy,
                geneticRandomState = random.State, courseSeed = courseSeed, world = World.Capture() };
        }
    }
}
