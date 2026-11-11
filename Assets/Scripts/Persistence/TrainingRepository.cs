using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ChromeDino.Core;

namespace ChromeDino.Persistence
{
    public interface ITrainingRepository
    {
        string RootPath { get; }
        void Save(TrainingSession session, GenerationChampion champion = null);
        TrainingSession LoadLatest();
        IReadOnlyList<GenerationChampion> ListChampions(out int unreadableFiles);
    }

    public sealed class JsonTrainingRepository : ITrainingRepository
    {
        public string RootPath { get; private set; }

        public JsonTrainingRepository(string rootPath) { RootPath = Path.GetFullPath(rootPath); }

        public void Save(TrainingSession session, GenerationChampion champion = null)
        {
            ValidateRunId(session.RunId);
            string directory = Path.Combine(RootPath, session.RunId);
            Directory.CreateDirectory(directory);
            if (champion == null) champion = session.CaptureLivingSurvivor();
            if (champion != null)
            {
                ValidateChampion(champion);
                if (champion.runId != session.RunId) throw new InvalidDataException("Champion belongs to another run.");
                AtomicWrite(Path.Combine(directory, "generation-" + champion.generation.ToString("D8", CultureInfo.InvariantCulture) + ".json"),
                    TrainingJson.WriteChampion(champion));
            }
            AtomicWrite(Path.Combine(directory, "checkpoint.json"), TrainingJson.WriteCheckpoint(session.Capture()));
            AtomicWrite(Path.Combine(RootPath, "latest.txt"), session.RunId);
        }

        public TrainingSession LoadLatest()
        {
            string latestPath = Path.Combine(RootPath, "latest.txt");
            if (!File.Exists(latestPath)) throw new FileNotFoundException("No saved training session was found.");
            string runId = File.ReadAllText(latestPath).Trim();
            ValidateRunId(runId);
            string path = Path.Combine(RootPath, runId, "checkpoint.json");
            Exception primaryError;
            try { return ReadSession(path, runId); }
            catch (Exception error) when (IsDataError(error)) { primaryError = error; }
            try { return ReadSession(path + ".bak", runId); }
            catch (Exception error) when (IsDataError(error))
            {
                throw new InvalidDataException("Could not read the checkpoint or its backup. " + primaryError.Message, error);
            }
        }

        private static TrainingSession ReadSession(string path, string runId)
        {
            var snapshot = TrainingJson.ReadCheckpoint(ReadLimited(path, 32 * 1024 * 1024));
            if (snapshot == null || snapshot.runId != runId) throw new InvalidDataException("Checkpoint run ID mismatch.");
            return new TrainingSession(snapshot);
        }

        public IReadOnlyList<GenerationChampion> ListChampions(out int unreadableFiles)
        {
            unreadableFiles = 0;
            var champions = new List<GenerationChampion>();
            if (!Directory.Exists(RootPath)) return champions;
            foreach (string directory in Directory.GetDirectories(RootPath))
            {
                foreach (string path in Directory.GetFiles(directory, "generation-*.json"))
                {
                    try
                    {
                        var champion = TrainingJson.ReadChampion(ReadLimited(path, 2 * 1024 * 1024));
                        ValidateChampion(champion);
                        if (champion.runId != Path.GetFileName(directory)) throw new InvalidDataException("Archive run ID mismatch.");
                        champions.Add(champion);
                    }
                    catch (Exception error) when (IsDataError(error)) { unreadableFiles++; }
                }
            }
            return champions.OrderByDescending(champion => champion.runId, StringComparer.Ordinal)
                .ThenByDescending(champion => champion.generation).ToList();
        }

        public static void ValidateChampion(GenerationChampion champion)
        {
            if (champion == null || champion.formatVersion != 1 || champion.rulesVersion != GameRules.Version ||
                champion.generation < 1 || champion.score < 0 || champion.survivalTicks < 1 || champion.brain == null ||
                champion.courseSeed == 0 || champion.dinoIndex < 0 || champion.tiedSurvivors < 1)
                throw new InvalidDataException("Incompatible generation archive.");
            ValidateRunId(champion.runId);
            champion.brain.Validate();
        }

        private static void ValidateRunId(string runId)
        {
            if (string.IsNullOrEmpty(runId) || runId.Length > 80 ||
                runId.Any(character => !char.IsLetterOrDigit(character) && character != '-' && character != '_'))
                throw new InvalidDataException("Invalid training run ID.");
        }

        private static bool IsDataError(Exception error)
        {
            return error is IOException || error is ArgumentException || error is UnauthorizedAccessException;
        }

        private static string ReadLimited(string path, long maximumBytes)
        {
            if (new FileInfo(path).Length > maximumBytes) throw new InvalidDataException("Save file is too large.");
            return File.ReadAllText(path);
        }

        private static void AtomicWrite(string path, string data)
        {
            string temporary = path + ".tmp";
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false)))
            {
                writer.Write(data);
                writer.Flush();
                stream.Flush(true);
            }
            if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
            else File.Move(temporary, path);
        }
    }
}
