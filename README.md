# Chrome Dino Brain Train

[Русская версия](README_RU.md)

A Unity endless runner with an evolutionary neural network training system. You can play the game yourself, train a population of AI runners, replay saved champions, and compete against a bot on the same course.

This guide describes the current implementation and provides a practical review sequence. The interface currently uses Russian labels; their English meanings are listed below.

## 1. Open and run the project

- Editor version: **Unity 6000.3.8f1**.
- Open the project folder in Unity Hub and allow Unity to import its packages and assets.
- Open [Assets/Scenes/MainMenu.unity](Assets/Scenes/MainMenu.unity), then press **Play**.
- Alternatively, use **Chrome Dino > Open Main Menu** in the editor menu.
- A local Windows build, if present, is available at `Builds/Windows/ChromeDino.exe` in the project root.

The three main scenes are `MainMenu`, `Training`, and `AITest`. They are already included in Build Settings. The retained `SampleScene` also opens the new main menu.

## 2. Find the main actions

| English action | Current interface label |
| --- | --- |
| Play yourself | Играть самому |
| New training | Новое обучение |
| Continue training | Продолжить обучение |
| AI evaluation / human versus bot | Проверка ИИ / человек против бота |
| Pause / Resume | Пауза / Продолжить |
| Stop and save | Остановить и сохранить |
| Evaluate AI | Проверить ИИ |
| Watch the bot | Смотреть бота |
| Human versus bot | Человек против бота |
| Start / Retry | Начать / повторить |
| Stop the run | Остановить заезд |
| Main menu | В меню |

The training statistics show **Generation** (Поколение), **Alive** (Живы), **Score** (Очки), and **Best score** (Рекорд).

## 3. Suggested review sequence

### A. Play the game without training

1. Select **Play yourself**, then **Start / Retry**.
2. Press **Space**, **Up Arrow**, or **W** to start and jump. Clicking or touching the playing field also works.
3. Hold the jump input for a higher jump. Release it and press again after landing to jump again.
4. Hold **Down Arrow** or **S** to duck on the ground or descend faster in the air. The on-screen duck button also supports holding.
5. Avoid the cacti and birds. Running speed increases over time, and score is based on distance.
6. After a collision, use **Start / Retry** for another attempt. **Esc** or **Stop the run** interrupts the attempt.

Expected behavior: the game waits for your first jump before starting. No trained network or saved checkpoint is required. The first three seconds of simulation are clear of newly spawned obstacles. Your runner is blue; the personal high score is stored in PlayerPrefs.

### B. Start and inspect training

1. Return to the main menu and select **New training**.
2. Observe a population of **200 runners** on one shared course.
3. Try the **x1, x5, x10, and x30** speed buttons. These request more fixed simulation steps; the physics step stays the same. Achieved training speed depends on the machine.
4. Watch the alive count decrease. When everyone dies, the best runner is archived and the next generation starts.
5. Wait for a completed generation, or for just one runner to remain alive, then select **Stop and save**.
6. Select **Evaluate AI** to inspect an archived brain.

Expected behavior: training has no automatic generation or survivor time limit. A last living runner is saved for evaluation without ending its generation. New training creates a separate run and retains previous runs in the archive.

**When switching to another window, training pauses and saves. Select Resume after returning.** A stopped session is continued through the main menu's **Continue training** action.

### C. Verify saving and continuation

1. During training, note the generation, alive count, score, and runner positions.
2. Select **Stop and save**, then **Main menu**.
3. Select **Continue training**.
4. Confirm that the saved generation and world continue rather than starting a fresh population.

The checkpoint includes all genomes, the current world, runner control states, and both random generator states. Automated tests check exact continuation over subsequent decisions and generations; the visible counters can advance immediately after resuming.

### D. Evaluate a champion and compete against it

1. Open **AI evaluation / human versus bot**.
2. Choose a training run in the first dropdown and a generation in the second dropdown.
3. Select **Watch the bot**, then **Start / Retry**.
4. Stop the run and switch **Трасса: новая** (New course) to **Трасса: из выбранного поколения** (Selected generation's course) to replay the recorded course.
5. Select **Human versus bot**, then **Start / Retry**. Your first jump starts both participants together.

Expected behavior: both runners use the same physics and obstacles and do not collide with each other. Gray is the bot and blue is the human. If one dies, the other continues. Survival time determines the result; death on the same simulation tick is a draw. Manual interruption does not declare a winner.

A completed champion should reproduce its original score and survival time on its recorded course. An entry marked **последний живой** is a snapshot of a living survivor from an unfinished generation. With an empty archive, train first; ordinary human play remains available.

## 4. How the AI learns

The controller is a feed-forward network with topology **10 -> 16 -> 10 -> 2**. Hidden layers use `tanh`, output neurons use `sigmoid`, and every neuron has an evolvable bias.

Its ten inputs describe the nearest obstacle's distance, width, height, and elevation; running speed; the runner's height and vertical velocity; whether it is grounded; the gap to the following obstacle; and its ducking state. The two outputs control jumping and ducking.

Training uses a **genetic algorithm**. It evaluates survival, ranks runners, selects parents through tournaments, crosses their weights and biases, and applies additive Gaussian mutations. Four elite genomes are retained unchanged, and 5% of the population consists of new random genomes. Parent and child arrays are independent.

All individuals in a generation face the same course. Subsequent generations receive different courses. Ranking prioritizes survival ticks, followed by score, passed obstacles, and a stable index when all other values tie. Scores can fluctuate between generations because their courses differ.

## 5. Code to inspect

| Location from the project root | Responsibility |
| --- | --- |
| [Assets/Scripts/Core/DinoWorld.cs](Assets/Scripts/Core/DinoWorld.cs) | Deterministic 1/60-second simulation, movement, obstacles, collisions, and sensors. |
| [Assets/Scripts/Core/NeuralNetwork.cs](Assets/Scripts/Core/NeuralNetwork.cs) | Genome validation, initialization, biases, and network evaluation. |
| [Assets/Scripts/Core/GeneticAlgorithm.cs](Assets/Scripts/Core/GeneticAlgorithm.cs) | Fitness ranking, selection, crossover, mutation, and elitism. |
| [Assets/Scripts/Core/TrainingSession.cs](Assets/Scripts/Core/TrainingSession.cs) | Population lifecycle, champions, and complete training snapshots. |
| [Assets/Scripts/Persistence/TrainingRepository.cs](Assets/Scripts/Persistence/TrainingRepository.cs) | Checkpoints, archive access, atomic writes, and backup recovery. |
| [Assets/Scripts/Persistence/TrainingJson.cs](Assets/Scripts/Persistence/TrainingJson.cs) | JSON plus exact binary double values for reproducible continuation. |
| `Assets/Scripts/Presentation/` | Scene flow, UI, held input, rendering, and application lifecycle handling. |
| `Assets/Tests/EditMode/` and `Assets/Tests/PlayMode/` | Persistence and interactive workflow checks. |

The core assembly has no Unity engine references. Human play, training, and bot evaluation all use the same `DinoWorld`. Training acceleration does not change collision or movement rules. UI objects are created in code when a scene starts, so the scene hierarchy is intentionally small before entering Play Mode.

## 6. Save files and failure handling

Saves are stored under `Application.persistentDataPath/Training/`, outside the project Assets folder:

- `<runId>/checkpoint.json`: full training state.
- `<runId>/checkpoint.json.bak`: previous checkpoint.
- `<runId>/generation-00000001.json` and later files: archived champions.
- `latest.txt`: the run selected for continuation.

Autosaves occur every 30 seconds, after generations, on pause or focus loss, and on application exit. A write failure pauses training and prevents navigation away from unsaved state. Loading falls back to the previous checkpoint if the current checkpoint is damaged. Invalid archive entries are skipped.

## 7. Run the checks and build

Open Unity's **Test Runner** and run both **Edit Mode** and **Play Mode** tests. The Play Mode checks cover menu navigation, training, saving, bot evaluation, competition, human play without a brain, held input, restart, and pause behavior. They use temporary save directories.

For the independent core checks, run these commands from the **project root**:

```powershell
dotnet run --project Tests/CoreChecks/CoreChecks.csproj --configuration Release
dotnet run --project Tests/CoreChecks/CoreChecks.csproj --configuration Release -- --training-smoke
```

These command-line checks require **.NET SDK 10**. The Unity game itself does not depend on that SDK. The second command trains three populations for up to 40 generations and also compares champions on ten unseen courses.

Recorded project validation: **15 core checks, 6 Edit Mode tests, and 2 Play Mode tests passed**. In the three 40-generation trials, initial champion scores of 58, 58, and 72 improved to best training scores of 878, 1731, and 1644. Mean scores on ten unseen courses improved from 40.4/50.3/56.2 to 606.7/763.0/900.6. These are recorded experimental results; new training runs can differ.

Local reports and UI previews, when generated, are in `TestResults/` at the project root. The Windows build has also passed a startup check.

Use **Chrome Dino > Build Windows** to create `Builds/Windows/ChromeDino.exe`. Install the Windows build support module for the selected Unity editor if it is missing.

## 8. Scope and project history

The runner recreates familiar Chrome Dino behavior in Unity. It includes variable jump height, ducking, faster descent, cactus groups, birds at three heights, acceleration, distance scoring, and alternating day/night lighting.

The earlier implementation is retained in `Legacy~/Assets/` outside Unity's imported assets. The active game uses the original project's sprite atlas in `Assets/Resources/DinoAtlas.png`. See [README_RU.md](README_RU.md) and [Docs/LOGIC_AUDIT.md](Docs/LOGIC_AUDIT.md) for additional implementation and audit notes.
