using System;
using System.IO;
using ChromeDino.Core;
using ChromeDino.Persistence;
using NUnit.Framework;
using UnityEngine;

namespace ChromeDino.Tests
{
    public sealed class PersistenceTests
    {
        private string directory;

        [SetUp]
        public void SetUp() { directory = Path.Combine(Path.GetTempPath(), "ChromeDinoTests-" + Guid.NewGuid().ToString("N")); }

        [TearDown]
        public void TearDown() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }

        private static TrainingSession Create(string id = "test-run")
        {
            return new TrainingSession(id, new EvolutionSettings { populationSize = 24, eliteCount = 2 }, 12345);
        }

        [Test]
        public void JsonUtilityCheckpointResumesAllFutureDecisions()
        {
            var repository = new JsonTrainingRepository(directory);
            var original = Create();
            for (int i = 0; i < 137; i++) original.Tick();
            repository.Save(original);
            var resumed = repository.LoadLatest();
            for (int i = 0; i < 4000; i++)
            {
                Assert.AreEqual(JsonUtility.ToJson(original.Tick()), JsonUtility.ToJson(resumed.Tick()));
                if (i % 499 == 0) Assert.AreEqual(JsonUtility.ToJson(original.Capture()), JsonUtility.ToJson(resumed.Capture()));
            }
        }

        [Test]
        public void CorruptedCheckpointFallsBackToPreviousAtomicSave()
        {
            var repository = new JsonTrainingRepository(directory);
            var session = Create();
            session.Tick();
            repository.Save(session);
            string expected = JsonUtility.ToJson(session.Capture());
            session.Tick();
            repository.Save(session);
            File.WriteAllText(Path.Combine(directory, session.RunId, "checkpoint.json"), "{ broken json");
            Assert.AreEqual(expected, JsonUtility.ToJson(repository.LoadLatest().Capture()));
        }

        [Test]
        public void ValidJsonWithInvalidSchemaAlsoFallsBackToCheckpointBackup()
        {
            var repository = new JsonTrainingRepository(directory);
            var session = Create();
            session.Tick(); repository.Save(session);
            string expected = JsonUtility.ToJson(session.Capture());
            session.Tick(); repository.Save(session);
            File.WriteAllText(Path.Combine(directory, session.RunId, "checkpoint.json"), "{}");
            Assert.AreEqual(expected, JsonUtility.ToJson(repository.LoadLatest().Capture()));
        }

        [Test]
        public void ChampionArchiveKeepsSeparateRunsAndSkipsBadRecords()
        {
            var repository = new JsonTrainingRepository(directory);
            foreach (string id in new[] { "first-run", "second-run" })
            {
                var session = Create(id);
                GenerationChampion champion = null;
                for (int i = 0; i < 5000 && champion == null; i++) champion = session.Tick();
                Assert.IsNotNull(champion);
                repository.Save(session, champion);
            }
            File.WriteAllText(Path.Combine(directory, "first-run", "generation-99999999.json"), "invalid");
            var archive = repository.ListChampions(out int broken);
            Assert.AreEqual(2, archive.Count);
            Assert.AreEqual(1, broken);
            Assert.AreEqual("second-run", repository.LoadLatest().RunId);
        }

        [Test]
        public void SavedChampionReplaysTheSameCourseAndResult()
        {
            var repository = new JsonTrainingRepository(directory);
            var session = Create();
            GenerationChampion champion = null;
            for (int i = 0; i < 5000 && champion == null; i++) champion = session.Tick();
            Assert.IsNotNull(champion);
            repository.Save(session, champion);
            var loaded = repository.ListChampions(out int broken)[0];
            Assert.AreEqual(JsonUtility.ToJson(champion), JsonUtility.ToJson(loaded));
            Assert.AreEqual(0, broken);
            var world = new DinoWorld(1, loaded.courseSeed);
            var pilot = new NeuralPilot(loaded.brain);
            while (world.AliveCount > 0 && world.Ticks <= champion.survivalTicks)
                world.Tick(new[] { pilot.Decide(world, 0) });
            Assert.AreEqual(0, world.AliveCount);
            Assert.AreEqual(champion.survivalTicks, world.Dinos[0].survivalTicks);
            Assert.AreEqual(champion.score, world.Dinos[0].score);
        }

        [Test]
        public void LivingLastSurvivorIsAvailableWithoutEndingTheGeneration()
        {
            var repository = new JsonTrainingRepository(directory);
            var session = Create();
            var snapshot = session.Capture();
            for (int i = 1; i < snapshot.world.dinos.Length; i++) snapshot.world.dinos[i].alive = false;
            snapshot.world.ticks = 10;
            snapshot.world.dinos[0].survivalTicks = 10;
            session = new TrainingSession(snapshot);
            repository.Save(session);
            var archive = repository.ListChampions(out int broken);
            Assert.AreEqual(1, archive.Count);
            Assert.IsFalse(archive[0].generationCompleted);
            Assert.AreEqual(1, archive[0].generation);
            Assert.AreEqual(1, repository.LoadLatest().World.AliveCount);
            Assert.AreEqual(1, repository.LoadLatest().Generation);
            Assert.AreEqual(0, broken);
        }

        [Test]
        public void MissingAndCorruptLatestPointerRecoverFromItsBackup()
        {
            var repository = new JsonTrainingRepository(directory);
            var session = Create();
            session.Tick(); repository.Save(session);
            session.Tick(); repository.Save(session);
            string expected = JsonUtility.ToJson(session.Capture());
            string latest = Path.Combine(directory, "latest.txt");
            File.WriteAllText(latest, "../invalid-run");
            Assert.AreEqual(expected, JsonUtility.ToJson(repository.LoadLatest().Capture()));
            File.Delete(latest);
            Assert.AreEqual(expected, JsonUtility.ToJson(repository.LoadLatest().Capture()));
        }

        [Test]
        public void ChampionBackupRecoversCorruptAndMissingPrimaryWithoutDuplicates()
        {
            var repository = new JsonTrainingRepository(directory);
            var session = Create();
            GenerationChampion champion = null;
            for (int i = 0; i < 5000 && champion == null; i++) champion = session.Tick();
            Assert.IsNotNull(champion);
            repository.Save(session, champion);
            repository.Save(session, champion);
            string path = Path.Combine(directory, session.RunId, "generation-" + champion.generation.ToString("D8") + ".json");
            File.WriteAllText(path, "invalid");
            var archive = repository.ListChampions(out int broken);
            Assert.AreEqual(1, archive.Count);
            Assert.AreEqual(0, broken);
            Assert.AreEqual(JsonUtility.ToJson(champion), JsonUtility.ToJson(archive[0]));
            File.Delete(path);
            Assert.AreEqual(1, repository.ListChampions(out broken).Count);
            Assert.AreEqual(0, broken);
        }

        [Test]
        public void ArchiveRejectsAGenerationStoredUnderTheWrongFileName()
        {
            var repository = new JsonTrainingRepository(directory);
            var session = Create();
            GenerationChampion champion = null;
            for (int i = 0; i < 5000 && champion == null; i++) champion = session.Tick();
            Assert.IsNotNull(champion);
            repository.Save(session, champion);
            string path = Path.Combine(directory, session.RunId, "generation-" + champion.generation.ToString("D8") + ".json");
            File.Move(path, Path.Combine(directory, session.RunId, "generation-99999999.json"));
            Assert.AreEqual(0, repository.ListChampions(out int broken).Count);
            Assert.AreEqual(1, broken);
        }

        [Test]
        public void LoadingNeverAcceptsPathTraversalOrAnotherRulesVersion()
        {
            var repository = new JsonTrainingRepository(directory);
            var session = Create();
            repository.Save(session);
            File.WriteAllText(Path.Combine(directory, "latest.txt"), "../another-folder");
            Assert.Throws<InvalidDataException>(() => repository.LoadLatest());
            var snapshot = session.Capture();
            snapshot.rulesVersion++;
            Assert.Throws<ArgumentException>(() => new TrainingSession(snapshot));
        }
    }
}
