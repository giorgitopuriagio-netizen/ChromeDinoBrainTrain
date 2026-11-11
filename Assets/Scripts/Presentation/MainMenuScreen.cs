using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace ChromeDino.Presentation
{
    public sealed class MainMenuScreen : MonoBehaviour
    {
        private BoardView board;
        private Text message;

        private void Start()
        {
            var ui = new UIFactory();
            ui.Header("Main menu");
            board = BoardView.Create(ui);
            ui.Label(ui.Root, "PLAY AND EVOLVE", 60, 430, 550, 50, 34);
            ui.Label(ui.Root, "Train runners to avoid cacti and birds, choose a generation's brain, and put it to the test.",
                60, 505, 530, 125, 25, UIFactory.Muted);
            ui.Label(ui.Root, "Training runs until you stop it.\nContinue restores the last saved state.\nNew training keeps earlier generations in the archive.",
                60, 675, 550, 120, 21, UIFactory.Muted);
            ui.Button(ui.Root, "New training", 660, 430, 540, 68, () => Try(AppServices.StartNewTraining), true);
            var resume = ui.Button(ui.Root, "Continue training", 660, 516, 540, 68, () => Try(AppServices.ContinueTraining));
            string latest = Path.Combine(AppServices.Repository.RootPath, "latest.txt");
            resume.interactable = File.Exists(latest) || File.Exists(latest + ".bak");
            ui.Button(ui.Root, "AI evaluation / Human vs bot", 660, 602, 540, 68, AppServices.OpenTest);
            message = ui.Label(ui.Root, "", 660, 725, 540, 100, 20, new Color32(158, 60, 41, 255));
            ui.Label(ui.Root, "Classic runner. One jump. One more try.", 60, 855, 1140, 30, 19, UIFactory.Muted);
        }

        private void Try(Action action)
        {
            try { action(); }
            catch (Exception error) { message.text = "Could not open training: " + error.Message; Debug.LogException(error); }
        }

        private void Update() { if (board != null) board.Render(null); }
    }
}
