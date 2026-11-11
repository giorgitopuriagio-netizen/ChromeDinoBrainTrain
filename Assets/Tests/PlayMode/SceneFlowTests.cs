using System;
using System.Collections;
using System.IO;
using System.Linq;
using TMPro;
using ChromeDino.Core;
using ChromeDino.Persistence;
using ChromeDino.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ChromeDino.Tests
{
    public sealed class SceneFlowTests
    {
        private string directory;
        private static Button Button(string text)
        {
            return UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                .Single(button => button.GetComponentInChildren<TMP_Text>()?.text == text);
        }
        private static AITestScreen Screen { get { return UnityEngine.Object.FindFirstObjectByType<AITestScreen>(); } }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "ChromeDinoSceneTests-" + Guid.NewGuid().ToString("N"));
            AppServices.Repository = new JsonTrainingRepository(directory);
            yield return SceneManager.LoadSceneAsync("MainMenu");
            yield return null;
            yield return null;
        }

        private static IEnumerator OpenEvaluation()
        {
            Button("AI evaluation / Human vs bot").onClick.Invoke();
            yield return null;
            yield return null;
            yield return null;
        }

        private static void AddChampion(string runId, uint seed)
        {
            var session = new TrainingSession(runId, new EvolutionSettings { populationSize = 24, eliteCount = 2 }, seed);
            GenerationChampion champion = null;
            for (int i = 0; i < 12000 && champion == null; i++) champion = session.Tick();
            Assert.IsNotNull(champion);
            AppServices.Repository.Save(session, champion);
        }

        private static IEnumerator Capture(string name)
        {
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../TestResults/Previews"));
            Directory.CreateDirectory(output);
            yield return null;
            var canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
            var camera = Camera.main;
            Assert.IsNotNull(canvas);
            Assert.IsNotNull(camera);
            var target = new RenderTexture(1280, 900, 24);
            var pixels = new Texture2D(1280, 900, TextureFormat.RGBA32, false);
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var previousMode = canvas.renderMode;
            try
            {
                camera.targetTexture = target;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1280, 900), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(Path.Combine(output, name + ".png"), pixels.EncodeToPNG());
            }
            finally
            {
                canvas.renderMode = previousMode;
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                UnityEngine.Object.Destroy(pixels);
                target.Release();
                UnityEngine.Object.Destroy(target);
            }
        }

        [UnityTest]
        public IEnumerator MenuTrainingSaveContinueAndEvaluationFollowTheSharedFlow()
        {
            Assert.AreEqual(3, UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Length);
            Assert.IsNotNull(Button("New training"));
            Assert.IsFalse(Button("Continue training").interactable);
            yield return Capture("01-main-menu");
            var session = new TrainingSession("training-flow", new EvolutionSettings(), 314159);
            AppServices.Repository.Save(session);
            AppServices.Training = session;
            yield return SceneManager.LoadSceneAsync("Training");
            yield return null;
            yield return null;
            Button("×30").onClick.Invoke();
            float deadline = Time.realtimeSinceStartup + 12;
            while (session.Generation < 2 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.GreaterOrEqual(session.Generation, 2);
            Button("Stop and save").onClick.Invoke();
            string stopped = JsonUtility.ToJson(session.Capture());
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.AreEqual(stopped, JsonUtility.ToJson(session.Capture()));
            Assert.AreEqual(stopped, JsonUtility.ToJson(AppServices.Repository.LoadLatest().Capture()));
            yield return Capture("02-training");
            Button("Main menu").onClick.Invoke();
            yield return null;
            yield return null;
            yield return null;
            Assert.IsTrue(Button("Continue training").interactable);
            Button("Continue training").onClick.Invoke();
            yield return null;
            yield return null;
            yield return null;
            Assert.AreEqual("training-flow", AppServices.Training.RunId);
            Assert.AreEqual(session.Generation, AppServices.Training.Generation);
            Button("Stop and save").onClick.Invoke();
            Button("Evaluate AI").onClick.Invoke();
            yield return null;
            yield return null;
            yield return null;
            Assert.AreEqual("AITest", SceneManager.GetActiveScene().name);
            Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<TMP_Dropdown>(FindObjectsSortMode.None).Length);
            Button("Watch bot").onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.Greater(Screen.CurrentRound.World.Ticks, 0, "Mode button did not immediately start the bot.");
            Assert.IsTrue(Button("Human vs bot").interactable);
            Button("Stop round").onClick.Invoke();
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator OneArchiveListAndRepeatedModeSwitchesKeepWorldAndControlsConsistent()
        {
            AddChampion("first-run", 12345);
            AddChampion("second-run", 999);
            yield return OpenEvaluation();
            var dropdown = UnityEngine.Object.FindFirstObjectByType<TMP_Dropdown>();
            Assert.AreEqual(2, dropdown.options.Count);
            Assert.IsTrue(dropdown.options.Any(option => option.text.Contains("run 1")));
            Assert.IsTrue(dropdown.options.Any(option => option.text.Contains("run 2")));
            dropdown.Show(); yield return null; dropdown.Hide();
            Button("Course: new").onClick.Invoke();
            for (int i = 0; i < 4; i++)
            {
                Button("Watch bot").onClick.Invoke();
                yield return null;
                Assert.AreEqual(1, Screen.CurrentRound.World.Dinos.Length);
                Assert.IsNull(Screen.CurrentRound.Human);
                Assert.IsFalse(dropdown.interactable);
                Assert.IsTrue(Button("Human vs bot").interactable);
                Button("Human vs bot").onClick.Invoke();
                yield return null;
                Assert.AreEqual(2, Screen.CurrentRound.World.Dinos.Length);
                Assert.AreEqual(EvaluationState.WaitingForHuman, Screen.CurrentRound.State);
                Assert.AreEqual(0, Screen.CurrentRound.World.Ticks);
                Button("Jump / Space").onClick.Invoke();
                yield return new WaitForSecondsRealtime(0.05f);
                Assert.Greater(Screen.CurrentRound.World.Ticks, 0);
            }
            yield return Capture("03-human-versus-bot");
            Button("Stop round").onClick.Invoke();
            Assert.IsTrue(dropdown.interactable);
            dropdown.value = 1;
            yield return null;
            Assert.IsNull(Screen.CurrentRound, "Changing brain retained a stale world.");
            Button("Watch bot").onClick.Invoke();
            Screen.SendMessage("OnApplicationFocus", false);
            int paused = Screen.CurrentRound.World.Ticks;
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.AreEqual(paused, Screen.CurrentRound.World.Ticks);
            Screen.SendMessage("OnApplicationFocus", true);
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.Greater(Screen.CurrentRound.World.Ticks, paused, "Bot did not resume on focus return.");
            Button("Stop round").onClick.Invoke();
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator EmptyArchiveAndHeldHumanInputPauseAndRestartAreSafe()
        {
            yield return OpenEvaluation();
            Assert.IsFalse(Button("Watch bot").interactable);
            Assert.IsFalse(Button("Human vs bot").interactable);
            Assert.IsFalse(Button("Stop round").interactable);
            Assert.IsNull(Screen.CurrentRound);
            Button("Main menu").onClick.Invoke();
            yield return null; yield return null; yield return null;
            AddChampion("controls", 12345);
            yield return OpenEvaluation();
            Button("Human vs bot").onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.AreEqual(0, Screen.CurrentRound.World.Ticks);
            var data = new PointerEventData(EventSystem.current);
            var jump = Button("Jump / Space").GetComponent<HoldInput>();
            jump.OnPointerDown(data);
            yield return new WaitForSecondsRealtime(0.16f);
            Assert.Greater(Screen.CurrentRound.Human.y, 30);
            data.button = PointerEventData.InputButton.Right;
            jump.OnPointerUp(data);
            yield return null;
            Assert.IsTrue(Screen.CurrentRound.Human.jumpHeld, "Unrelated mouse button released a held jump.");
            data.button = PointerEventData.InputButton.Left;
            jump.OnPointerUp(data);
            yield return new WaitForSecondsRealtime(0.7f);
            Assert.AreEqual(0, Screen.CurrentRound.Human.y);
            var duck = Button("Duck / Down").GetComponent<HoldInput>();
            duck.OnPointerDown(data);
            yield return new WaitForSecondsRealtime(0.08f);
            Assert.IsTrue(Screen.CurrentRound.Human.ducking);
            // Losing focus while the pointer is held may never deliver PointerUp.
            duck.SendMessage("OnApplicationFocus", false);
            Screen.SendMessage("OnApplicationFocus", false);
            Screen.SendMessage("OnApplicationFocus", true);
            Button("Jump / Space").onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.7f);
            duck.OnPointerDown(data);
            yield return new WaitForSecondsRealtime(0.08f);
            Assert.IsTrue(Screen.CurrentRound.Human.ducking, "Focus loss left the hold component latched.");
            duck.OnPointerExit(data);
            yield return new WaitForSecondsRealtime(0.08f);
            Assert.IsFalse(Screen.CurrentRound.Human.ducking);
            Screen.SendMessage("OnApplicationFocus", false);
            int paused = Screen.CurrentRound.World.Ticks;
            Screen.SendMessage("OnApplicationFocus", true);
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.AreEqual(paused, Screen.CurrentRound.World.Ticks, "Competition resumed without a new human press.");
            Button("Jump / Space").onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.Greater(Screen.CurrentRound.World.Ticks, paused);
            yield return new WaitForSecondsRealtime(1.6f);
            Assert.Greater(Screen.CurrentRound.World.Obstacles.Count, 0);
            yield return Capture("04-running-course");
            Button("Human vs bot").onClick.Invoke();
            yield return null;
            Assert.AreEqual(0, Screen.CurrentRound.World.Ticks);
            Assert.AreEqual(EvaluationState.WaitingForHuman, Screen.CurrentRound.State);
            Assert.IsFalse(Screen.CurrentRound.Human.jumpHeld);
            Button("Stop round").onClick.Invoke();
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return SceneManager.LoadSceneAsync("MainMenu");
            yield return null;
            AppServices.Training = null;
            if (directory != null && Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
