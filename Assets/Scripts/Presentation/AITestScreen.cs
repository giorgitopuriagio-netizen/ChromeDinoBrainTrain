using System;
using System.Collections.Generic;
using System.Linq;
using ChromeDino.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ChromeDino.Presentation
{
    public sealed class AITestScreen : MonoBehaviour
    {
        private enum Mode { Watch, Versus, Human }
        private BoardView board;
        private Dropdown runs, generations;
        private Button watchButton, versusButton, humanButton, startButton, courseButton;
        private Text courseLabel, stats, message;
        private IReadOnlyList<GenerationChampion> archive;
        private List<string> runIds;
        private List<GenerationChampion> choices = new List<GenerationChampion>();
        private DinoWorld world;
        private NeuralPilot pilot;
        private DinoAction[] actions;
        private Mode mode;
        private bool recordedCourse, running, waiting, jumpQueued, pointerJump, pointerDuck;
        private double accumulator;
        private GenerationChampion selected;
        public DinoWorld World { get { return world; } }

        private void Start()
        {
            var ui = new UIFactory();
            ui.Header("Игра / проверка ИИ");
            board = BoardView.Create(ui);
            board.Pressed = () => { pointerJump = true; QueueJump(); };
            board.Released = () => pointerJump = false;
            ui.Label(ui.Root, "Запуск обучения", 60, 430, 540, 30, 20, UIFactory.Muted);
            runs = ui.Dropdown(ui.Root, 60, 468, 540, SelectRun);
            ui.Label(ui.Root, "Мозг поколения", 60, 535, 540, 30, 20, UIFactory.Muted);
            generations = ui.Dropdown(ui.Root, 60, 573, 540, SelectGeneration);
            watchButton = ui.Button(ui.Root, "Смотреть бота", 650, 430, 170, 55, () => SetMode(Mode.Watch), false, 18);
            versusButton = ui.Button(ui.Root, "Человек против бота", 830, 430, 195, 55, () => SetMode(Mode.Versus), false, 17);
            humanButton = ui.Button(ui.Root, "Играть самому", 1035, 430, 175, 55, () => SetMode(Mode.Human), false, 18);
            courseButton = ui.Button(ui.Root, "Трасса: новая", 650, 505, 560, 48, () =>
            {
                recordedCourse = !recordedCourse;
                courseLabel.text = recordedCourse ? "Трасса: из выбранного поколения" : "Трасса: новая";
            }, false, 19);
            courseLabel = courseButton.GetComponentInChildren<Text>();
            startButton = ui.Button(ui.Root, "Начать / повторить", 650, 573, 560, 58, BeginRound, true);
            HoldInput jumpHold = null;
            var jumpButton = ui.Button(ui.Root, "Прыжок / пробел", 650, 650, 270, 54, () =>
            {
                if (jumpHold == null || jumpHold.LastPointerFrame != Time.frameCount) QueueJump();
            }, false, 20);
            jumpHold = jumpButton.gameObject.AddComponent<HoldInput>();
            jumpHold.Changed = held => { pointerJump = held; if (held) QueueJump(); };
            var duckButton = ui.Button(ui.Root, "Пригнуться / ↓", 940, 650, 270, 54, () => { }, false, 20);
            duckButton.gameObject.AddComponent<HoldInput>().Changed = held => pointerDuck = held;
            ui.Button(ui.Root, "Остановить заезд", 650, 724, 270, 50, () => FinishRound(true), false, 19);
            ui.Button(ui.Root, "В меню", 940, 724, 270, 50, AppServices.MainMenu);
            stats = ui.Label(ui.Root, "", 60, 650, 540, 65, 23);
            message = ui.Label(ui.Root, "", 60, 735, 540, 108, 19, UIFactory.Muted);
            ui.Label(ui.Root, "Пробел / ↑ / касание — прыжок. Удержание — выше. ↓ — пригнуться или ускорить падение.", 60, 856, 1150, 30, 18, UIFactory.Muted);
            SetMode(AppServices.HumanGame ? Mode.Human : Mode.Watch);
            LoadArchive();
        }

        private void LoadArchive()
        {
            try
            {
                archive = AppServices.Repository.ListChampions(out int unreadable);
                runIds = archive.Select(champion => champion.runId).Distinct().ToList();
                runs.ClearOptions();
                runs.AddOptions(runIds.Count == 0 ? new List<string> { "Пока нет сохранённых поколений" } : runIds);
                SelectRun(0);
                if (unreadable > 0) message.text = "Пропущены повреждённые записи: " + unreadable + ".";
            }
            catch (Exception error)
            {
                archive = new List<GenerationChampion>();
                runIds = new List<string>();
                SelectRun(0);
                message.text = "Не удалось прочитать архив: " + error.Message + "\nОбычная игра доступна.";
                Debug.LogException(error);
            }
            SetSelectors(true);
        }

        private void SelectRun(int index)
        {
            choices = runIds != null && index >= 0 && index < runIds.Count
                ? archive.Where(champion => champion.runId == runIds[index]).ToList() : new List<GenerationChampion>();
            generations.ClearOptions();
            generations.AddOptions(choices.Count == 0 ? new List<string> { "Сначала пройди обучение или играй сам" }
                : choices.Select(champion => "Поколение " + champion.generation + " · " + champion.score + " очк. · " +
                    (champion.generationCompleted ? (champion.survivalTicks * GameRules.Step).ToString("F1") + " с" : "последний живой")).ToList());
            generations.SetValueWithoutNotify(0);
            SelectGeneration(0);
        }

        private void SelectGeneration(int index)
        {
            selected = index >= 0 && index < choices.Count ? choices[index] : null;
            RefreshSelection();
        }

        private void RefreshSelection()
        {
            if (startButton == null) return;
            startButton.interactable = !running && (mode == Mode.Human || selected != null);
            if (mode == Mode.Human)
            {
                stats.text = "Обычная игра";
                message.text = "Нажми «Начать», затем пробел или поле.\nСерый Dino — оригинальные спрайты; твой — синий.";
            }
            else if (selected == null)
            {
                stats.text = "Нет сохранённых мозгов";
                message.text = "Запусти обучение. Победители поколений и последний выживший сохраняются в архив.";
            }
            else
            {
                stats.text = (selected.generationCompleted ? "Победитель поколения " : "Последний выживший поколения ") + selected.generation;
                message.text = "Выбери режим и начни забег. Физика и препятствия одинаковы для всех." +
                    (!selected.generationCompleted ? "\nЭто снимок незавершённого поколения." : "") +
                    (selected.tiedSurvivors > 1 ? "\nПри равном времени использованы очки, препятствия и индекс." : "");
            }
        }

        private void SetMode(Mode value)
        {
            if (running) return;
            mode = value;
            if (watchButton != null)
            {
                Button[] buttons = { watchButton, versusButton, humanButton };
                for (int i = 0; i < buttons.Length; i++)
                {
                    buttons[i].GetComponent<Image>().color = (int)mode == i ? UIFactory.Green : UIFactory.Cream;
                    buttons[i].GetComponentInChildren<Text>().color = (int)mode == i ? Color.white : UIFactory.Ink;
                }
            }
            SetSelectors(true);
            RefreshSelection();
        }

        private void BeginRound()
        {
            if (running || mode != Mode.Human && selected == null) return;
            uint seed = recordedCourse && selected != null ? selected.courseSeed : unchecked((uint)DateTime.UtcNow.Ticks);
            world = new DinoWorld(mode == Mode.Versus ? 2 : 1, seed);
            pilot = mode == Mode.Human ? null : new NeuralPilot(selected.brain);
            actions = new DinoAction[world.Dinos.Length];
            accumulator = 0;
            jumpQueued = pointerJump = pointerDuck = false;
            running = true;
            waiting = mode != Mode.Watch;
            board.SetReady(waiting);
            board.SetGameOver(false);
            SetSelectors(false);
            message.text = waiting ? "Пробел / ↑ / касание поля — старт." : "Бот играет самостоятельно.";
        }

        private void SetSelectors(bool enabled)
        {
            if (runs == null || startButton == null) return;
            runs.interactable = enabled && mode != Mode.Human && runIds != null && runIds.Count > 0;
            generations.interactable = enabled && mode != Mode.Human && choices.Count > 0;
            watchButton.interactable = versusButton.interactable = humanButton.interactable = enabled;
            courseButton.interactable = enabled && selected != null;
            startButton.interactable = enabled && (mode == Mode.Human || selected != null);
        }

        private void QueueJump()
        {
            if (!running) return;
            if (waiting)
            {
                waiting = false;
                accumulator = 0;
                board.SetReady(false);
                message.text = "Удерживай прыжок для высоты. ↓ — пригнуться.";
            }
            if (mode != Mode.Watch) jumpQueued = true;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) QueueJump();
            if (Input.GetKeyDown(KeyCode.Escape) && running) FinishRound(true);
            if (running && !waiting)
            {
                accumulator += Math.Min(0.25, Time.unscaledDeltaTime);
                while (running && accumulator >= GameRules.Step)
                {
                    accumulator -= GameRules.Step;
                    if (pilot != null) actions[0] = world.Dinos[0].alive ? pilot.Decide(world, 0) : default(DinoAction);
                    if (mode != Mode.Watch)
                    {
                        int human = mode == Mode.Human ? 0 : 1;
                        actions[human] = new DinoAction { jump = jumpQueued || pointerJump || Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W),
                            duck = pointerDuck || Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S) };
                    }
                    jumpQueued = false;
                    world.Tick(actions);
                    if (world.AliveCount == 0) FinishRound(false);
                }
            }
            if (board == null) return;
            board.Render(world, mode == Mode.Versus, mode == Mode.Human);
            if (world == null) return;
            stats.text = mode == Mode.Human ? "Человек: " + world.Dinos[0].score + (world.Dinos[0].alive ? "" : " (выбыл)")
                : "Бот: " + world.Dinos[0].score + (world.Dinos[0].alive ? "" : " (выбыл)") +
                    (mode == Mode.Versus ? "\nЧеловек: " + world.Dinos[1].score + (world.Dinos[1].alive ? "" : " (выбыл)") : "");
            if (running && mode == Mode.Versus && !world.Dinos[1].alive) message.text = "Человек выбыл. Бот продолжает забег.";
            else if (running && mode == Mode.Versus && !world.Dinos[0].alive) message.text = "Бот выбыл. Продолжай!";
        }

        private void FinishRound(bool interrupted)
        {
            if (!running) return;
            running = waiting = jumpQueued = pointerJump = pointerDuck = false;
            board.SetReady(false);
            board.SetGameOver(!interrupted);
            SetSelectors(true);
            if (!interrupted && mode != Mode.Watch)
            {
                int humanScore = world.Dinos[mode == Mode.Human ? 0 : 1].score;
                if (humanScore > PlayerPrefs.GetInt("DinoHighScore", 0))
                {
                    PlayerPrefs.SetInt("DinoHighScore", humanScore);
                    PlayerPrefs.Save();
                }
            }
            if (interrupted) message.text = "Забег остановлен. Можно выбрать режим и повторить.";
            else if (mode != Mode.Versus) message.text = "Забег завершён. Очки: " + world.Dinos[0].score + ".";
            else
            {
                int difference = world.Dinos[1].survivalTicks.CompareTo(world.Dinos[0].survivalTicks);
                message.text = difference > 0 ? "Ты продержался дольше бота!" : difference < 0 ? "Бот продержался дольше. Попробуй ещё!" : "Ничья: оба выбыли на одном такте.";
            }
        }

        private void Suspend()
        {
            pointerJump = pointerDuck = jumpQueued = false;
            if (!running) return;
            waiting = true;
            accumulator = 0;
            board.SetReady(true);
            message.text = "Пауза. Пробел / касание — продолжить.";
        }
        private void OnApplicationPause(bool value) { if (value) Suspend(); }
        private void OnApplicationFocus(bool value) { if (!value) Suspend(); }
    }
}
