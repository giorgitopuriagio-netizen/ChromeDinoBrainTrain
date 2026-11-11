using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ChromeDino.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ChromeDino.Presentation
{
    public sealed class AITestScreen : MonoBehaviour
    {
        private BoardView board;
        private Dropdown generations;
        private Button watchButton, versusButton, courseButton, jumpButton, duckButton, stopButton;
        private Text courseLabel, stats, message;
        private IReadOnlyList<GenerationChampion> choices = Array.Empty<GenerationChampion>();
        private bool recordedCourse;
        private bool pointerJump, pointerDuck;
        private double accumulator;
        private GenerationChampion selected;
        public EvaluationSession CurrentRound { get; private set; }

        private void Start()
        {
            var ui = new UIFactory();
            ui.Header("AI evaluation");
            board = BoardView.Create(ui);
            board.Pressed = () => { pointerJump = CanControlHuman(); QueueJump(); };
            board.Released = () => pointerJump = false;
            ui.Label(ui.Root, "Generation brain", 60, 430, 540, 32, 21, UIFactory.Muted);
            generations = ui.Dropdown(ui.Root, 60, 475, 540, SelectGeneration);
            watchButton = ui.Button(ui.Root, "Watch bot", 650, 430, 270, 65, () => BeginRound(EvaluationMode.WatchBot));
            versusButton = ui.Button(ui.Root, "Human vs bot", 940, 430, 270, 65, () => BeginRound(EvaluationMode.HumanVsBot));
            courseButton = ui.Button(ui.Root, "Course: new", 650, 525, 560, 54, () =>
            {
                if (CurrentRound != null && CurrentRound.IsActive) return;
                recordedCourse = !recordedCourse;
                courseLabel.text = recordedCourse ? "Course: selected generation" : "Course: new";
            }, false, 21);
            courseLabel = courseButton.GetComponentInChildren<Text>();
            HoldInput jumpHold = null;
            jumpButton = ui.Button(ui.Root, "Jump / Space", 650, 610, 175, 56, () =>
            {
                if (jumpHold == null || jumpHold.LastPointerFrame != Time.frameCount) QueueJump();
            }, false, 19);
            jumpHold = jumpButton.gameObject.AddComponent<HoldInput>();
            jumpHold.Changed = held =>
            {
                pointerJump = held && CanControlHuman();
                if (held) QueueJump();
            };
            duckButton = ui.Button(ui.Root, "Duck / Down", 843, 610, 175, 56, () => { }, false, 19);
            duckButton.gameObject.AddComponent<HoldInput>().Changed = held => pointerDuck = held && CanControlHuman();
            stopButton = ui.Button(ui.Root, "Stop round", 1035, 610, 175, 56, StopRound, false, 19);
            stats = ui.Label(ui.Root, "", 60, 565, 540, 90, 27);
            message = ui.Label(ui.Root, "", 60, 690, 540, 140, 21, UIFactory.Muted);
            ui.Button(ui.Root, "Main menu", 650, 815, 560, 48, AppServices.MainMenu, false, 21);
            ui.Label(ui.Root, "Gray: bot. Blue: human. Hold Space / Up to jump higher; Down to duck or drop faster.",
                60, 855, 1150, 35, 18, UIFactory.Muted);
            LoadArchive();
            RefreshControls();
        }

        private void LoadArchive()
        {
            try
            {
                choices = AppServices.Repository.ListChampions(out int unreadable);
                var runIds = choices.Select(champion => champion.runId).Distinct().ToList();
                generations.ClearOptions();
                generations.AddOptions(choices.Count == 0 ? new List<string> { "No saved brains yet" }
                    : choices.Select(champion => "Generation " + champion.generation +
                        (runIds.Count > 1 ? " (run " + (runIds.IndexOf(champion.runId) + 1) + ")" : "") +
                        "  ·  " + champion.score + " points  ·  " + (champion.generationCompleted
                            ? (champion.survivalTicks * GameRules.Step).ToString("F1", CultureInfo.InvariantCulture) + " s"
                            : "last survivor")).ToList());
                generations.SetValueWithoutNotify(0);
                SelectGeneration(0);
                if (unreadable > 0) message.text += "\nUnreadable records skipped: " + unreadable + ".";
            }
            catch (Exception error)
            {
                choices = Array.Empty<GenerationChampion>();
                selected = null;
                stats.text = "Archive unavailable";
                message.text = "Could not read the archive: " + error.Message;
                Debug.LogException(error);
            }
        }

        private void SelectGeneration(int index)
        {
            if (CurrentRound != null && CurrentRound.IsActive) return;
            selected = index >= 0 && index < choices.Count ? choices[index] : null;
            CurrentRound = null;
            ClearInput();
            board.SetReady(false);
            board.SetGameOver(false);
            stats.text = selected == null ? "No saved brains" :
                (selected.generationCompleted ? "Generation winner: " : "Last survivor: generation ") + selected.generation;
            message.text = selected == null
                ? "Train runners from the main menu first. A brain is saved when one runner remains and when its generation ends."
                : "Click Watch bot or Human vs bot to start the selected mode." +
                    (!selected.generationCompleted ? "\nThis is a living survivor; its training generation is still in progress." : "") +
                    (selected.tiedSurvivors > 1 ? "\nSeveral runners crashed on the final tick; the recorded winner was selected by the tie rule." : "");
            RefreshControls();
        }

        private void BeginRound(EvaluationMode mode)
        {
            if (selected == null) return;
            try
            {
                uint seed = recordedCourse ? selected.courseSeed : unchecked((uint)DateTime.UtcNow.Ticks);
                var replacement = new EvaluationSession(selected.brain, seed, mode);
                CurrentRound?.Stop();
                CurrentRound = replacement;
                ClearInput();
                board.SetReady(replacement.State == EvaluationState.WaitingForHuman);
                board.SetGameOver(false);
                message.text = replacement.HasHuman ? "Press Space or tap the field to start together with the bot."
                    : "The bot is playing. Click a mode button to restart or switch modes.";
                RefreshControls();
            }
            catch (Exception error) { message.text = "Could not start the round: " + error.Message; Debug.LogException(error); }
        }

        private bool CanControlHuman()
        {
            return CurrentRound != null && CurrentRound.IsActive && CurrentRound.HasHuman && CurrentRound.Human.alive;
        }

        private void RefreshControls()
        {
            bool active = CurrentRound != null && CurrentRound.IsActive;
            generations.interactable = !active && choices.Count > 0;
            courseButton.interactable = !active && selected != null;
            watchButton.interactable = versusButton.interactable = selected != null;
            stopButton.interactable = active;
            jumpButton.interactable = active && CurrentRound.HasHuman &&
                (CurrentRound.Human.alive || CurrentRound.State == EvaluationState.Paused);
            duckButton.interactable = CanControlHuman();
            SetModeColor(watchButton, CurrentRound != null && !CurrentRound.HasHuman);
            SetModeColor(versusButton, CurrentRound != null && CurrentRound.HasHuman);
        }

        private static void SetModeColor(Button button, bool selectedMode)
        {
            button.GetComponent<Image>().color = selectedMode ? UIFactory.Green : UIFactory.Cream;
            button.GetComponentInChildren<Text>().color = selectedMode ? Color.white : UIFactory.Ink;
        }

        private void ClearInput() { pointerJump = pointerDuck = false; accumulator = 0; }

        private void QueueJump()
        {
            if (CurrentRound == null || !CurrentRound.IsActive || !CurrentRound.HasHuman) return;
            bool resuming = CurrentRound.State != EvaluationState.Running;
            CurrentRound.RequestJump();
            if (resuming)
            {
                accumulator = 0;
                board.SetReady(false);
                message.text = "Hold jump for a higher jump. Hold Down to duck or descend faster.";
            }
        }

        private void Update()
        {
            if (board == null) return;
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) QueueJump();
            if (Input.GetKeyDown(KeyCode.Escape)) StopRound();
            var round = CurrentRound;
            if (round != null && round.State == EvaluationState.Running)
            {
                round.SetHumanInput(pointerJump || Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W),
                    pointerDuck || Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S));
                accumulator += Math.Min(0.25, Time.unscaledDeltaTime);
                while (round.State == EvaluationState.Running && accumulator >= GameRules.Step)
                {
                    accumulator -= GameRules.Step;
                    round.Tick();
                }
                if (round.State == EvaluationState.Completed) FinishRound();
            }
            board.Render(round?.World, round != null && round.HasHuman);
            if (round != null)
            {
                stats.text = "Bot: " + round.Bot.score + (round.Bot.alive ? "" : " (out)") +
                    (round.HasHuman ? "\nHuman: " + round.Human.score + (round.Human.alive ? "" : " (out)") : "");
                if (round.State == EvaluationState.Running && round.HasHuman)
                {
                    if (!round.Human.alive) message.text = "You are out. The bot is still running.\nWait for the result or stop the round.";
                    else if (!round.Bot.alive) message.text = "The bot is out. Keep going!";
                }
            }
            RefreshControls();
        }

        private void StopRound()
        {
            if (CurrentRound == null || !CurrentRound.IsActive) return;
            CurrentRound.Stop();
            FinishRound();
        }

        private void FinishRound()
        {
            ClearInput();
            board.SetReady(false);
            board.SetGameOver(CurrentRound.State == EvaluationState.Completed);
            if (CurrentRound.State == EvaluationState.Completed && CurrentRound.HasHuman &&
                CurrentRound.Human.score > PlayerPrefs.GetInt("DinoHighScore", 0))
            {
                PlayerPrefs.SetInt("DinoHighScore", CurrentRound.Human.score);
                PlayerPrefs.Save();
            }
            if (CurrentRound.State == EvaluationState.Stopped)
                message.text = "Round stopped. Select another brain or click a mode button to play again.";
            else if (!CurrentRound.HasHuman) message.text = "Round complete. The bot scored " + CurrentRound.Bot.score + " points.";
            else
            {
                int difference = CurrentRound.Human.survivalTicks.CompareTo(CurrentRound.Bot.survivalTicks);
                message.text = difference > 0 ? "You survived longer than the bot!" : difference < 0
                    ? "The bot survived longer. Try again!" : "Draw: both runners crashed on the same tick.";
            }
            RefreshControls();
        }

        private void SetSuspended(bool value)
        {
            if (CurrentRound == null || !CurrentRound.IsActive) return;
            if (value)
            {
                CurrentRound.Pause();
                ClearInput();
                board.SetReady(CurrentRound.HasHuman);
                message.text = CurrentRound.HasHuman ? "Round paused. Press Space or tap the field to resume." : "Round paused.";
            }
            else if (!CurrentRound.HasHuman)
            {
                CurrentRound.Resume();
                message.text = "The bot is playing.";
            }
            RefreshControls();
        }
        private void OnApplicationPause(bool value) { SetSuspended(value); }
        private void OnApplicationFocus(bool value) { SetSuspended(!value); }
    }
}
