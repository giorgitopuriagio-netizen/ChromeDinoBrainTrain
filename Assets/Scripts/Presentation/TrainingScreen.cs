using System;
using System.Diagnostics;
using TMPro;
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
        private TMP_Text generationStat, aliveStat, scoreStat, bestStat, status, timing;
        private readonly Button[] speedButtons = new Button[4];
        private TMP_Text message;
        private TMP_Text pauseLabel;
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
        private static readonly int[] Speeds = { 1, 5, 10, 30 };

        private void Start()
        {
            var ui = new UIFactory();
            ui.Header("Training");
            board = BoardView.Create(ui);
            var card = ui.Card(40, 452, 576, 292);
            status = ui.Label(card, "TRAINING", 24, 18, 520, 26, 15, UIFactory.Accent,
                TextAlignmentOptions.TopLeft, true);
            generationStat = ui.Stat(card, "GENERATION", 24, 65, 240);
            aliveStat = ui.Stat(card, "RUNNERS ALIVE", 300, 65, 250);
            scoreStat = ui.Stat(card, "CURRENT SCORE", 24, 177, 240);
            bestStat = ui.Stat(card, "BEST SCORE", 300, 177, 250);
            timing = ui.Label(ui.Root, "", 42, 762, 572, 66, 17, UIFactory.Muted);
            ui.Label(ui.Root, "TRAINING SPEED", 672, 452, 568, 26, 15, UIFactory.Muted);
            for (int i = 0; i < Speeds.Length; i++)
            {
                int value = Speeds[i];
                speedButtons[i] = ui.Button(ui.Root, "×" + value, 672 + i * 146, 492, 130, 50, () => speed = value);
            }
            pauseButton = ui.Button(ui.Root, "Pause", 672, 564, 568, 54, TogglePause);
            pauseLabel = pauseButton.GetComponentInChildren<TMP_Text>();
            stopButton = ui.Button(ui.Root, "Stop and save", 672, 636, 568, 60, StopTraining, true);
            ui.Button(ui.Root, "Main menu", 672, 716, 272, 52, () => { if (StopAndSave()) AppServices.MainMenu(); });
            testButton = ui.Button(ui.Root, "Evaluate AI", 960, 716, 280, 52, () => { if (StopAndSave()) AppServices.OpenTest(); });
            message = ui.Label(ui.Root, "", 672, 788, 568, 52, 16, UIFactory.Muted);
            ui.Footer("Champions are saved to the archive. Pause or stop at any time; your progress is kept.");

            session = AppServices.Training;
            if (session == null)
            {
                stopped = true;
                foreach (var label in card.GetComponentsInChildren<TMP_Text>())
                    if (label != status) label.gameObject.SetActive(false);
                status.text = "NO ACTIVE SESSION";
                ui.Button(ui.Root, "Train from scratch", 64, 530, 528, 58, () => TryOpen(AppServices.StartNewTraining), true);
                ui.Button(ui.Root, "Continue latest training", 64, 610, 528, 58, () => TryOpen(AppServices.ContinueTraining));
            }
            else message.text = "Progress saves every 30 seconds and after each generation.";
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
                message.text = "Save failed: " + error.Message + "\nCheck the save folder, then try Stop and save again.";
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
            if (session != null) message.text = "Progress saved. Continue this generation from the main menu, or evaluate a saved champion.";
            Refresh();
            return true;
        }

        private void Refresh()
        {
            if (session != null)
            {
                int currentScore = 0;
                foreach (var dino in session.World.Dinos) currentScore = Math.Max(currentScore, dino.score);
                generationStat.text = session.Generation.ToString();
                aliveStat.text = session.World.AliveCount + "<size=21><color=#586677> / " + session.PopulationSize + "</color></size>";
                scoreStat.text = currentScore.ToString();
                bestStat.text = session.BestScore.ToString();
                status.text = stopped ? "SAVED / STOPPED" : paused ? "PAUSED" : "TRAINING IN PROGRESS";
                timing.text = "Attempt: " + (session.World.Ticks * GameRules.Step).ToString("F1", System.Globalization.CultureInfo.InvariantCulture) +
                    " s   ·   Speed: ×" + speed + (lastChampionGeneration > 0 ? "   ·   Saved gen " + lastChampionGeneration + ": " + lastChampionScore + " pts" : "");
            }
            pauseButton.interactable = session != null && !stopped;
            stopButton.interactable = session != null && !stopped;
            testButton.interactable = session != null && (session.Generation > 1 || session.World.AliveCount == 1);
            pauseLabel.text = paused ? "Resume" : "Pause";
            for (int i = 0; i < speedButtons.Length; i++)
            {
                speedButtons[i].interactable = session != null && !stopped;
                UIFactory.SetSelected(speedButtons[i], speed == Speeds[i]);
            }
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
