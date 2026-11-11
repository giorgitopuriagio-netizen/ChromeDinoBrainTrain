using System;
using System.Collections;
using System.IO;
using System.Linq;
using ChromeDino.Core;
using ChromeDino.Persistence;
using ChromeDino.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ChromeDino.Tests
{
    public sealed class SceneFlowTests
    {
        private string directory;

        private static Button FindButton(string caption)
        {
            return UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                .Single(button => button.GetComponentInChildren<Text>()?.text == caption);
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
            yield return null;
        }

        [UnityTest]
        public IEnumerator MenuTrainingArchiveAndBothGameModesWork()
        {
            directory = Path.Combine(Path.GetTempPath(), "ChromeDinoSceneTests-" + Guid.NewGuid().ToString("N"));
            AppServices.Repository = new JsonTrainingRepository(directory);
            yield return SceneManager.LoadSceneAsync("MainMenu");
            yield return null;
            yield return null;
            Assert.IsNotNull(FindButton("Новое обучение"));
            Assert.IsNotNull(FindButton("Проверка ИИ / человек против бота"));
            Assert.IsNotNull(UnityEngine.Object.FindFirstObjectByType<BoardView>());
            yield return Capture("01-main-menu");

            var session = new TrainingSession("scene-smoke", new EvolutionSettings(), 314159);
            AppServices.Repository.Save(session);
            AppServices.Training = session;
            yield return SceneManager.LoadSceneAsync("Training");
            yield return null;
            yield return null;
            FindButton("×30").onClick.Invoke();
            float deadline = Time.realtimeSinceStartup + 12;
            while (session.Generation < 2 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.GreaterOrEqual(session.Generation, 2, "Training did not advance.");
            FindButton("Остановить и сохранить").onClick.Invoke();
            string stopped = JsonUtility.ToJson(session.Capture());
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.AreEqual(stopped, JsonUtility.ToJson(session.Capture()), "Stopped training still runs.");
            Assert.AreEqual(stopped, JsonUtility.ToJson(AppServices.Repository.LoadLatest().Capture()));
            yield return Capture("02-training");

            FindButton("Проверить ИИ").onClick.Invoke();
            yield return null;
            yield return null;
            yield return null;
            Assert.AreEqual("AITest", SceneManager.GetActiveScene().name);
            var dropdowns = UnityEngine.Object.FindObjectsByType<Dropdown>(FindObjectsSortMode.None);
            Assert.AreEqual(2, dropdowns.Length);
            Assert.IsTrue(dropdowns.All(dropdown => dropdown.options.Count > 0));
            dropdowns[0].Show();
            yield return null;
            dropdowns[0].Hide();
            FindButton("Начать / повторить").onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.IsFalse(FindButton("Человек против бота").interactable);
            FindButton("Остановить заезд").onClick.Invoke();
            FindButton("Человек против бота").onClick.Invoke();
            FindButton("Начать / повторить").onClick.Invoke();
            FindButton("Прыжок / пробел").onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.IsTrue(UnityEngine.Object.FindObjectsByType<Text>(FindObjectsSortMode.None).Any(text => text.text.Contains("Человек:")));
            yield return new WaitForSecondsRealtime(0.4f);
            FindButton("Прыжок / пробел").onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.6f);
            FindButton("Прыжок / пробел").onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Capture("03-human-versus-bot");
            FindButton("Остановить заезд").onClick.Invoke();
            FindButton("В меню").onClick.Invoke();
            yield return null;
            yield return null;
            yield return null;
            Assert.AreEqual("MainMenu", SceneManager.GetActiveScene().name);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator HumanGameWorksWithoutTrainingAndHeldControlsRelease()
        {
            directory = Path.Combine(Path.GetTempPath(), "ChromeDinoHumanTests-" + Guid.NewGuid().ToString("N"));
            AppServices.Repository = new JsonTrainingRepository(directory);
            yield return SceneManager.LoadSceneAsync("MainMenu");
            yield return null;
            yield return null;
            FindButton("Играть самому").onClick.Invoke();
            yield return null;
            yield return null;
            yield return null;
            Assert.AreEqual("AITest", SceneManager.GetActiveScene().name);
            var screen = UnityEngine.Object.FindFirstObjectByType<AITestScreen>();
            var start = FindButton("Начать / повторить");
            Assert.IsTrue(start.interactable, "Human game requires a trained brain.");
            start.onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.AreEqual(0, screen.World.Ticks, "Game should wait for the first press.");
            var data = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current);
            var jump = FindButton("Прыжок / пробел").GetComponent<HoldInput>();
            jump.OnPointerDown(data);
            yield return new WaitForSecondsRealtime(0.16f);
            Assert.Greater(screen.World.Dinos[0].y, 30);
            jump.OnPointerUp(data);
            yield return new WaitForSecondsRealtime(0.7f);
            Assert.AreEqual(0, screen.World.Dinos[0].y);
            var duck = FindButton("Пригнуться / ↓").GetComponent<HoldInput>();
            duck.OnPointerDown(data);
            yield return new WaitForSecondsRealtime(0.08f);
            Assert.IsTrue(screen.World.Dinos[0].ducking);
            duck.OnPointerExit(data);
            yield return new WaitForSecondsRealtime(0.08f);
            Assert.IsFalse(screen.World.Dinos[0].ducking, "Pointer exit leaves duck held.");
            screen.SendMessage("OnApplicationFocus", false);
            int paused = screen.World.Ticks;
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.AreEqual(paused, screen.World.Ticks);
            FindButton("Прыжок / пробел").onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.Greater(screen.World.Ticks, paused);
            yield return new WaitForSecondsRealtime(2.4f);
            Assert.Greater(screen.World.Obstacles.Count, 0, "No obstacles appeared after the clear time.");
            yield return Capture("04-human-game");
            FindButton("Остановить заезд").onClick.Invoke();
            start.onClick.Invoke();
            Assert.AreEqual(0, screen.World.Ticks, "Restart retained old simulation.");
            FindButton("Остановить заезд").onClick.Invoke();
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
