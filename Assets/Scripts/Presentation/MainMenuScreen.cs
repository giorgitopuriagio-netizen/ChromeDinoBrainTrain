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
            ui.Header("Игра и обучение нейросети");
            board = BoardView.Create(ui);
            ui.Label(ui.Root, "БЕГИ. ПРЫГАЙ. УЧИСЬ.", 60, 430, 570, 50, 32);
            ui.Label(ui.Root, "Классический Dino: кактусы, птицы и всё более быстрый бег. Играй сам или обучи свою популяцию динозавров.", 60, 500, 520, 120, 24, UIFactory.Muted);
            ui.Label(ui.Root, "Пробел / ↑ — прыжок. Удержание — выше.\n↓ — пригнуться или быстрее приземлиться.\nКасание поля и экранные кнопки тоже работают.", 60, 650, 540, 110, 21, UIFactory.Muted);
            ui.Button(ui.Root, "Играть самому", 660, 430, 540, 60, AppServices.OpenHumanGame, true);
            ui.Button(ui.Root, "Новое обучение", 660, 508, 540, 60, () => Try(AppServices.StartNewTraining));
            var resume = ui.Button(ui.Root, "Продолжить обучение", 660, 586, 540, 60, () => Try(AppServices.ContinueTraining));
            resume.interactable = File.Exists(Path.Combine(AppServices.Repository.RootPath, "latest.txt"));
            ui.Button(ui.Root, "Проверка ИИ / человек против бота", 660, 664, 540, 60, AppServices.OpenTest, false, 21);
            message = ui.Label(ui.Root, "", 660, 750, 540, 95, 19, new Color32(175, 65, 55, 255));
            ui.Label(ui.Root, "200 особей · архив поколений · точное продолжение обучения", 60, 850, 1140, 30, 19, UIFactory.Muted);
        }
        private void Try(Action action)
        {
            try { action(); }
            catch (Exception error) { message.text = "Не удалось открыть обучение: " + error.Message; Debug.LogException(error); }
        }
        private void Update() { if (board != null) board.Render(null); }
    }
}
