# ChromeDinoBrainTrain

[Русская версия](README_RU.md)

A Unity **6000.3.8f1** endless runner with a neural network and a genetic algorithm. Train a population of Dino agents, evaluate a saved generation, and compete against its champion on the same course.

The menu and scene-selection workflow match [FlappyBirdBrainTrain](https://github.com/Foxter931/FlappyBirdBrainTrain): **New training**, **Continue training**, and **AI evaluation / Human vs bot**. Evaluation has one combined generation-brain list and mode buttons that start or restart a round immediately. The runner retains its own jump, duck, obstacle, and scoring rules.

## Getting started

1. Open this project in Unity Hub with **Unity 6000.3.8f1** and let Unity import its dependencies.
2. Open [MainMenu.unity](Assets/Scenes/MainMenu.unity), then press **Play**. Alternatively, select **Chrome Dino > Open Main Menu**.
3. Start training or continue a saved session. On a clean checkout, run **New training** before evaluating a brain.

Scenes: **MainMenu**, **Training**, and **AITest**. MainMenu is first in Build Settings. The interface is in English.

For a Windows build, use **Chrome Dino > Build Windows**. Output: `Builds/Windows/ChromeDino.exe`. Share the entire build directory, including the executable's data and runtime files. Windows build support must be installed for this Unity editor.

## Reviewer walkthrough

1. Click **New training** to create a separate run with **200 agents**. Earlier runs remain available in the archive.
2. Select **x10** or **x30** to accelerate training. Observe the generation, alive count, score, and best score. Acceleration performs more fixed simulation steps without changing physics; achieved speed depends on the machine.
3. Wait for a generation to end or for just one runner to remain. Its brain becomes available for evaluation. Training has no automatic overall generation limit or survivor timeout.
4. Click **Stop and save**. The simulation stops and writes a complete checkpoint. Click **Evaluate AI** to open the saved-brain screen.
5. Choose **Generation brain**. This single list contains champions from all runs. When generation numbers repeat, a run number distinguishes them; run 1 is the newest archived run.
6. Choose **Course: new** to evaluate generalization, or **Course: selected generation** to replay the selected champion's recorded course.
7. Click **Watch bot** to start a one-runner round immediately. There is no separate Start button.
8. Click **Human vs bot** to create a fresh two-runner competition. Both participants wait for the first human jump. Gray is the bot; blue is the human.
9. Press **Space**, **Up Arrow**, **W**, the **Jump / Space** button, or the field to start and jump. Hold for a higher jump. Use **Down Arrow**, **S**, or **Duck / Down** to duck on the ground or descend faster in the air.
10. Switch between the mode buttons or click the current mode again. Each click creates a fresh round and clears old input. Generation and course selectors unlock after **Stop round** or natural completion.
11. Return to **Main menu**, then select **Continue training**. The saved generation, population, world, and random states resume rather than creating a new population.

Switching windows pauses training and saves it; click **Resume** after returning. Competition also pauses on focus loss and waits for a new human jump to resume. Bot-only evaluation resumes automatically when focus returns. **Esc** stops the current round or training session.

Early agents can crash quickly. Since generations face different courses, performance need not improve in every generation.

## Game rules

- The first three seconds are clear of newly spawned obstacles. Speed increases from 6 to 13 pixels per simulation tick.
- Small and large cacti appear in groups. Birds appear at higher speeds and fly at three elevations.
- Jump height depends on holding or releasing jump. A new jump requires landing and a new press; holding the key does not cause automatic hopping.
- Both competitors use the same physics and obstacles. They do not collide with one another.
- If one participant dies, the other keeps running. Longer survival wins; death on the same tick is a draw. Manual interruption does not declare a winner.
- Score is based on distance. The human high score is stored in PlayerPrefs. Day/night lighting alternates as distance increases.

## Architecture and learning

| Source | Responsibility |
| --- | --- |
| [DinoWorld.cs](Assets/Scripts/Core/DinoWorld.cs) | Deterministic 1/60-second simulation, controls, obstacles, sensors, scoring, and collision boxes. |
| [NeuralNetwork.cs](Assets/Scripts/Core/NeuralNetwork.cs) | Genome validation, weights and biases, and feed-forward evaluation. |
| [GeneticAlgorithm.cs](Assets/Scripts/Core/GeneticAlgorithm.cs) | Fitness, tournament selection, crossover, mutation, and elitism. |
| [TrainingSession.cs](Assets/Scripts/Core/TrainingSession.cs) | Population lifecycle, champions, and complete training snapshots. |
| [EvaluationSession.cs](Assets/Scripts/Core/EvaluationSession.cs) | Immutable round mode, matching runner/action arrays, and pause/input state. |
| [TrainingRepository.cs](Assets/Scripts/Persistence/TrainingRepository.cs) | Atomic saves, checkpoints, champion archive, and backup recovery. |
| [TrainingJson.cs](Assets/Scripts/Persistence/TrainingJson.cs) | JSON with an exact binary representation of double values. |
| [Presentation](Assets/Scripts/Presentation) | Scene navigation, UI, rendering, held input, and application events. |

The core assembly has no Unity engine references. Training, bot evaluation, and human competition use the same simulation. Scene interfaces are created in code at runtime, so the scene hierarchy is intentionally small before Play Mode.

The network topology is **10 -> 16 -> 10 -> 2**, with `tanh` hidden layers, `sigmoid` outputs, and evolvable biases. Inputs describe the nearest obstacle's distance, width, height, and elevation; running speed; runner height and vertical velocity; grounded state; the following gap; and ducking state. Outputs control jump and duck.

The genetic algorithm retains four unchanged elites, selects parents through tournaments, crosses every weight and bias, applies additive Gaussian mutations, and introduces 5% random immigrants. Genomes do not share mutable arrays. Fitness prioritizes survival ticks, then score, passed obstacles, and a stable index for exact ties. All agents within one generation face the same course; later generations get different courses.

## Persistence

Saves live outside the repository at `Application.persistentDataPath/Training/`:

- `<runId>/checkpoint.json`: complete training state; `.bak` contains the previous checkpoint.
- `<runId>/generation-00000001.json` and subsequent files: generation champions; records can also have backup copies.
- `latest.txt`: the run used for continuation; its backup is tried if this pointer is missing or invalid.

Autosaves occur every 30 seconds, after generations, on pause/focus loss, and on exit. Write failures pause training and prevent navigation away from unsaved state. Damaged primary checkpoints and champion records can recover from their backups. Invalid archive entries are skipped.

Exact numeric payloads preserve double values through Unity JSON parsing, so continuation can reproduce future decisions and generation transitions. A completed champion replays its recorded result on the same course. **Last survivor** entries belong to unfinished generations and can keep improving while training continues.

## Tests

Run **Edit Mode** and **Play Mode** in Unity Test Runner. Checks cover checkpoint continuation, backups, archive integrity, menu navigation, combined generation selection, immediate mode launch, repeated mode switching, held controls, pause/resume, and stopping/restarting.

Independent core checks, from the project root:

```powershell
dotnet run --project Tests/CoreChecks/CoreChecks.csproj --configuration Release
dotnet run --project Tests/CoreChecks/CoreChecks.csproj --configuration Release -- --training-smoke
```

These standalone commands require **.NET SDK 10**; the Unity game does not. The smoke command trains three populations for up to 40 generations, then evaluates champions on ten unseen courses. Recorded earlier trials improved best training scores from 58/58/72 to 878/1731/1644, and unseen-course means from 40.4/50.3/56.2 to 606.7/763.0/900.6. Results from a new run can differ.

Generated reports and UI previews are written to `TestResults/`; Unity logs are in `Logs/`. These generated files are excluded from version control. See [the audit](Docs/LOGIC_AUDIT.md) for the latest implementation notes.

## Repository cleanup

The repository contains the active three scenes, scripts, tests, project settings, package declarations, and the sprite atlas in `Assets/Resources/DinoAtlas.png`. Old scripts/prefabs, the duplicate SampleScene, generated IDE projects, editor layouts, and cache/log files have been removed from the tracked project. Unity regenerates its local working files when needed. Unused 2D, navigation, timeline, visual scripting, multiplayer, and obsolete IDE packages have been removed.

Original versions remain in Git history. The runner recreates familiar Chrome Dino behavior in Unity; it does not claim bit-for-bit equivalence with the browser implementation.
