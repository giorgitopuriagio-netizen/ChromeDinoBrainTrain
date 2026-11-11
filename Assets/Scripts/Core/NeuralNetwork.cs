using System;

namespace ChromeDino.Core
{
    [Serializable]
    public sealed class BrainGenome
    {
        public int[] layers;
        public double[] genes;

        public static int[] DefaultLayers { get { return new[] { 10, 16, 10, 2 }; } }

        public static int GeneCount(int[] topology)
        {
            if (topology == null || topology.Length < 2 || topology.Length > 8)
                throw new ArgumentException("Invalid network topology.");
            int count = 0;
            for (int layer = 0; layer < topology.Length; layer++)
            {
                if (topology[layer] < 1 || topology[layer] > 256) throw new ArgumentException("Invalid layer size.");
                if (layer > 0) count += (topology[layer - 1] + 1) * topology[layer];
            }
            return count;
        }

        public void Validate()
        {
            int count = GeneCount(layers);
            if (layers[0] != 10 || layers[layers.Length - 1] != 2 || genes == null || genes.Length != count)
                throw new ArgumentException("The brain does not match the ten sensors and jump/duck outputs.");
            foreach (double gene in genes)
                if (double.IsNaN(gene) || double.IsInfinity(gene) || Math.Abs(gene) > 20)
                    throw new ArgumentException("Invalid neural weight.");
        }

        public BrainGenome Clone()
        {
            return new BrainGenome { layers = (int[])layers.Clone(), genes = (double[])genes.Clone() };
        }

        public static BrainGenome Create(DeterministicRandom random)
        {
            var result = new BrainGenome { layers = DefaultLayers };
            result.genes = new double[GeneCount(result.layers)];
            int offset = 0;
            for (int layer = 1; layer < result.layers.Length; layer++)
            {
                double scale = Math.Sqrt(6.0 / (result.layers[layer - 1] + result.layers[layer]));
                for (int neuron = 0; neuron < result.layers[layer]; neuron++)
                {
                    for (int input = 0; input < result.layers[layer - 1]; input++)
                        result.genes[offset++] = (random.NextDouble() * 2 - 1) * scale;
                    result.genes[offset++] = (random.NextDouble() * 2 - 1) * 0.25; // Evolvable bias.
                }
            }
            return result;
        }
    }

    // A feed-forward brain is a plain C# object, never a MonoBehaviour.
    public sealed class NeuralNetwork
    {
        private readonly BrainGenome genome;
        private readonly double[][] activations;

        public NeuralNetwork(BrainGenome genome)
        {
            genome.Validate();
            this.genome = genome.Clone();
            activations = new double[genome.layers.Length][];
            for (int i = 0; i < activations.Length; i++) activations[i] = new double[genome.layers[i]];
        }

        public double[] Evaluate(double[] inputs)
        {
            if (inputs == null || inputs.Length != activations[0].Length)
                throw new ArgumentException("Incorrect sensor count.");
            Array.Copy(inputs, activations[0], inputs.Length);
            int offset = 0;
            for (int layer = 1; layer < activations.Length; layer++)
            {
                for (int neuron = 0; neuron < activations[layer].Length; neuron++)
                {
                    double sum = 0;
                    for (int input = 0; input < activations[layer - 1].Length; input++)
                        sum += activations[layer - 1][input] * genome.genes[offset++];
                    sum += genome.genes[offset++];
                    activations[layer][neuron] = layer == activations.Length - 1
                        ? 1 / (1 + Math.Exp(-Math.Max(-60, Math.Min(60, sum)))) : Math.Tanh(sum);
                }
            }
            return activations[activations.Length - 1];
        }
    }
}
