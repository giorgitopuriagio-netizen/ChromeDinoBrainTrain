using System;
using System.IO;
using TMPro;
using UnityEngine;

namespace ChromeDino.Presentation
{
    public sealed class MainMenuScreen : MonoBehaviour
    {
        private BoardView board;
        private TMP_Text message;

        private void Start()
        {
            var ui = new UIFactory();
            ui.Header("Main menu");
            board = BoardView.Create(ui);
            ui.Label(ui.Root, "Train a brain.\nRace its champion.", 40, 455, 580, 116, 43,
                UIFactory.Ink, TextAlignmentOptions.TopLeft, true);
            ui.Label(ui.Root, "Watch a population learn to jump and duck.\nChoose a saved champion, then race it yourself.",
                42, 593, 560, 80, 22, UIFactory.Muted);
            ui.Label(ui.Root, "200 runners per generation", 42, 715, 550, 30, 19, UIFactory.Ink,
                TextAlignmentOptions.TopLeft, true);
            ui.Label(ui.Root, "One shared course. One set of rules.", 42, 751, 550, 28, 18, UIFactory.Muted);
            ui.MenuButton("New training", "Start a new population. Keep your saved champions.", 452,
                () => Try(AppServices.StartNewTraining), true);
            var resume = ui.MenuButton("Continue training", "Resume your latest saved session.", 556,
                () => Try(AppServices.ContinueTraining));
            string latest = Path.Combine(AppServices.Repository.RootPath, "latest.txt");
            resume.interactable = File.Exists(latest) || File.Exists(latest + ".bak");
            ui.MenuButton("AI evaluation / Human vs bot", "Watch a champion or challenge it on the same course.", 660, AppServices.OpenTest);
            message = ui.Label(ui.Root, resume.interactable ? "Your latest training session is ready to continue."
                : "First time here? Start with New training.", 672, 775, 568, 56, 17, UIFactory.Muted);
            ui.Footer("SPACE / UP  Jump     DOWN  Duck     Hold jump to go higher");
        }

        private void Try(Action action)
        {
            try { action(); }
            catch (Exception error)
            {
                message.color = new Color32(166, 49, 49, 255);
                message.text = "Training could not be opened. " + error.Message;
                Debug.LogException(error);
            }
        }

        private void Update() { if (board != null) board.Render(null); }
    }
}
