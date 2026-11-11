using System;
using System.IO;
using ChromeDino.Core;
using ChromeDino.Persistence;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChromeDino.Presentation
{
    public static class AppServices
    {
        private static ITrainingRepository repository;
        public static TrainingSession Training { get; set; }
        public static ITrainingRepository Repository
        {
            get { return repository ?? (repository = new JsonTrainingRepository(Path.Combine(Application.persistentDataPath, "Training"))); }
            set { repository = value ?? throw new ArgumentNullException(nameof(value)); }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { repository = null; Training = null; }

        public static void StartNewTraining()
        {
            string id = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            var session = new TrainingSession(id, new EvolutionSettings(), unchecked((uint)DateTime.UtcNow.Ticks));
            Repository.Save(session);
            Training = session;
            SceneManager.LoadScene("Training");
        }

        public static void ContinueTraining()
        {
            Training = Repository.LoadLatest();
            SceneManager.LoadScene("Training");
        }

        public static void MainMenu() { Training = null; SceneManager.LoadScene("MainMenu"); }
        public static void OpenTest() { Training = null; SceneManager.LoadScene("AITest"); }
    }
}
