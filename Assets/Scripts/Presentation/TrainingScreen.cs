using System;
using System.Diagnostics;
using ChromeDino.Core;
using UnityEngine;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

namespace ChromeDino.Presentation
{
    public sealed class TrainingScreen : MonoBehaviour
    {
        private TrainingSession session;
        private BoardView board;
        private Text statistics;
        private Text message;
        private Text pauseLabel;
        private Button pauseButton;
        private Button stopButton;
        private Button testButton;
        private GenerationChampion pendingChampion;
        private int lastChampionGeneration;
        private int lastChampionScore;
        private int savedLivingGeneration;
        private int speed = 1;
        private bool paused;
        private bool stopped;
        private double accumulator;
        private float nextAutosave;
        private readonly Stopwatch budget = new Stopwatch();

        private void Start()
        {
            var ui = new UIFactory();
            ui.Header("Training / Genetic algorithm");
            board = BoardView.Create(ui);
            statistics = ui.Label(ui.Root, "", 60, 430, 560, 260, 26);
            ui.Label(ui.Root, "Training speed", 680, 430, 520, 35, 21, UIFactory.Muted);
            int[] speeds = { 1, 5, 10, 30 };
            for (int i = 0; i < speeds.Length; i++)
            {
                int value = speeds[i];
                ui.Button(ui.Root, "×" + value, 680 + i * 133, 475, 120, 50, () => speed = value);
            }
            pauseButton = ui.Button(ui.Root, "Pause", 680, 548, 520, 54, TogglePause);
            pauseLabel = pauseButton.GetComponentInChildren<Text>();
            stopButton = ui.Button(ui.Root, "Stop and save", 680, 620, 520, 60, StopTraining, true);
            ui.Button(ui.Root, "Main menu", 680, 702, 245, 52, () => { if (StopAndSave()) AppServices.MainMenu(); });
            testButton = ui.Button(ui.Root, "Evaluate AI", 945, 702, 255, 52, () => { if (StopAndSave()) AppServices.OpenTest(); });
            message = ui.Label(ui.Root, "", 60, 730, 570, 110, 19, UIFactory.Muted);
            ui.Label(ui.Root, "Each generation's last survivor is saved to the archive.", 60, 855, 1140, 30, 19, UIFactory.Muted);

            session = AppServices.Training;
            if (session == null)
            {
                stopped = true;
                statistics.text = "Start training from the main menu.";
                ui.Button(ui.Root, "Train from scratch", 60, 500, 540, 58, () => TryOpen(AppServices.StartNewTraining), true);
                ui.Button(ui.Root, "Continue latest training", 60, 580, 540, 58, () => TryOpen(AppServices.ContinueTraining));
            }
            else message.text = "Training continues until you press Stop and save.\nAutosave: every 30 seconds and after each generation.";
            nextAutosave = Time.unscaledTime + 30;
            Refresh();
        }

        private void TryOpen(Action action)
        {
            try { action(); }
            catch (Exception error) { message.text = error.Message; Debug.LogException(error); }
        }

        private void TogglePause()
        {
            if (paused)
            {
                if (!Save()) return;
                paused = false;
            }
            else
            {
                paused = true;
                Save();
            }
            accumulator = 0;
            Refresh();
        }

        private void Update()
        {
            if (session == null) return;
            if (Input.GetKeyDown(KeyCode.Escape)) StopTraining();
            if (!stopped && !paused)
            {
                accumulator = Math.Min(2, accumulator + Math.Min(0.25, Time.unscaledDeltaTime) * speed);
                budget.Restart();
                while (accumulator >= GameRules.Step && budget.ElapsedMilliseconds < 14)
                {
                    accumulator -= GameRules.Step;
                    pendingChampion = session.Tick();
                    if (pendingChampion != null)
                    {
                        lastChampionGeneration = pendingChampion.generation;
                        lastChampionScore = pendingChampion.score;
                        if (!Save()) break;
                    }
                    else if (session.World.AliveCount == 1 && savedLivingGeneration != session.Generation)
                    {
                        if (!Save()) break;
                        savedLivingGeneration = session.Generation;
                    }
                }
                if (Time.unscaledTime >= nextAutosave)
                {
                    Save();
                    nextAutosave = Time.unscaledTime + 30;
                }
            }
            board.Render(session.World);
            Refresh();
        }

        private bool Save()
        {
            if (session == null) return true;
            try
            {
                AppServices.Repository.Save(session, pendingChampion);
                pendingChampion = null;
                return true;
            }
            catch (Exception error)
            {
                paused = true;
                message.text = "Could not save: " + error.Message + "\nResolve the issue and press Stop and save again.";
                Debug.LogException(error);
                return false;
            }
        }

        private void StopTraining() { StopAndSave(); }

        private bool StopAndSave()
        {
            if (!Save()) return false;
            stopped = true;
            accumulator = 0;
            if (session != null) message.text = "Training stopped and saved.\nThe unfinished generation will resume from here.\nArchived winners and the last survivor can be evaluated.";
            Refresh();
            return true;
        }

        private void Refresh()
        {
            if (session != null)
            {
                int currentScore = 0;
                foreach (var bird in session.World.Dinos) currentScore = Math.Max(currentScore, bird.score);
                statistics.text = "Generation  " + session.Generation + "\nAlive  " + session.World.AliveCount + " / " + session.PopulationSize +
                    "\nScore  " + currentScore + "    Best  " + session.BestScore +
                    "\nAttempt time  " + (session.World.Ticks * GameRules.Step).ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + " s" +
                    "\nSpeed  ×" + speed + "    " + (stopped ? "STOPPED" : paused ? "PAUSED" : "TRAINING") +
                    (lastChampionGeneration > 0 ? "\nSaved: generation " + lastChampionGeneration + ", points " + lastChampionScore : "");
            }
            pauseButton.interactable = session != null && !stopped;
            stopButton.interactable = session != null && !stopped;
            testButton.interactable = session != null && (session.Generation > 1 || session.World.AliveCount == 1);
            pauseLabel.text = paused ? "Resume" : "Pause";
        }

        private void OnApplicationPause(bool value)
        {
            if (!value || session == null || stopped) return;
            paused = true;
            accumulator = 0;
            Save();
        }

        private void OnApplicationFocus(bool value) { if (!value) OnApplicationPause(true); }

        private void OnApplicationQuit() { if (session != null) Save(); }
    }
}
