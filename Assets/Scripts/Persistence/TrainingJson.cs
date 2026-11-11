using System;
using System.IO;
using ChromeDino.Core;
using UnityEngine;

namespace ChromeDino.Persistence
{
    // Unity's native JSON parser can round doubles by one ULP. Keep readable JSON
    // plus an exact IEEE-754 payload so resuming cannot change a threshold decision.
    internal static class TrainingJson
    {
        [Serializable]
        private sealed class CheckpointDocument
        {
            public int formatVersion = 1;
            public TrainingSnapshot snapshot;
            public string exactNumbers;
        }

        [Serializable]
        private sealed class ChampionDocument
        {
            public int formatVersion = 1;
            public GenerationChampion champion;
            public string exactNumbers;
        }

        public static string WriteCheckpoint(TrainingSnapshot snapshot)
        {
            string numbers = Encode(writer => VisitSnapshot(snapshot, value => { writer.Write(value); return value; }));
            return JsonUtility.ToJson(new CheckpointDocument { snapshot = snapshot, exactNumbers = numbers });
        }

        public static TrainingSnapshot ReadCheckpoint(string json)
        {
            var document = JsonUtility.FromJson<CheckpointDocument>(json);
            if (document == null || document.formatVersion != 1 || document.snapshot == null)
                throw new InvalidDataException("Invalid checkpoint document.");
            Decode(document.exactNumbers, reader => VisitSnapshot(document.snapshot, _ => reader.ReadDouble()));
            return document.snapshot;
        }

        public static string WriteChampion(GenerationChampion champion)
        {
            string numbers = Encode(writer => VisitGenome(champion.brain, value => { writer.Write(value); return value; }));
            return JsonUtility.ToJson(new ChampionDocument { champion = champion, exactNumbers = numbers }, true);
        }

        public static GenerationChampion ReadChampion(string json)
        {
            var document = JsonUtility.FromJson<ChampionDocument>(json);
            if (document == null || document.formatVersion != 1 || document.champion == null)
                throw new InvalidDataException("Invalid champion document.");
            Decode(document.exactNumbers, reader => VisitGenome(document.champion.brain, _ => reader.ReadDouble()));
            return document.champion;
        }

        private static string Encode(Action<BinaryWriter> write)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true)) write(writer);
                return Convert.ToBase64String(stream.ToArray());
            }
        }

        private static void Decode(string payload, Action<BinaryReader> read)
        {
            if (string.IsNullOrEmpty(payload)) throw new InvalidDataException("Missing exact numeric payload.");
            byte[] data;
            try { data = Convert.FromBase64String(payload); }
            catch (FormatException error) { throw new InvalidDataException("Invalid exact numeric payload.", error); }
            using (var stream = new MemoryStream(data, false))
            using (var reader = new BinaryReader(stream))
            {
                read(reader);
                if (stream.Position != stream.Length) throw new InvalidDataException("Extra data in numeric payload.");
            }
        }

        private static void VisitGenome(BrainGenome genome, Func<double, double> number)
        {
            if (genome == null || genome.genes == null || genome.genes.Length != BrainGenome.GeneCount(genome.layers))
                throw new InvalidDataException("Invalid genome shape.");
            for (int i = 0; i < genome.genes.Length; i++) genome.genes[i] = number(genome.genes[i]);
        }

        private static void VisitSnapshot(TrainingSnapshot snapshot, Func<double, double> number)
        {
            if (snapshot.settings == null || snapshot.population == null || snapshot.population.Length < 2 ||
                snapshot.population.Length > 1000 || snapshot.world == null || snapshot.world.dinos == null ||
                snapshot.world.dinos.Length != snapshot.population.Length || snapshot.world.obstacles == null ||
                snapshot.world.obstacles.Length > 10)
                throw new InvalidDataException("Incomplete checkpoint document.");
            snapshot.settings.mutationRate = number(snapshot.settings.mutationRate);
            snapshot.settings.mutationStrength = number(snapshot.settings.mutationStrength);
            snapshot.settings.immigrantFraction = number(snapshot.settings.immigrantFraction);
            foreach (var genome in snapshot.population) VisitGenome(genome, number);
            foreach (var dino in snapshot.world.dinos)
            {
                if (dino == null) throw new InvalidDataException("Missing dino.");
                dino.y = number(dino.y);
                dino.velocity = number(dino.velocity);
                dino.deathClearance = number(dino.deathClearance);
            }
            snapshot.world.distance = number(snapshot.world.distance);
            foreach (var pipe in snapshot.world.obstacles)
            {
                if (pipe == null) throw new InvalidDataException("Missing obstacle.");
                pipe.x = number(pipe.x);
                pipe.bottom = number(pipe.bottom);
                pipe.speedOffset = number(pipe.speedOffset);
            }
        }
    }
}
