using System;
using System.Linq;
using System.Text.Json;
using ChromeDino.Core;

internal static class Program
{
    private static int passed;
    private static readonly JsonSerializerOptions Options = new JsonSerializerOptions { IncludeFields = true };
    private static string Json<T>(T value) { return JsonSerializer.Serialize(value, Options); }
    private static T Copy<T>(T value) { return JsonSerializer.Deserialize<T>(Json(value), Options); }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Run(string name, Action test) { test(); Console.WriteLine("PASS " + name); passed++; }
    private static EvolutionSettings Small() { return new EvolutionSettings { populationSize = 24, eliteCount = 2 }; }
    private static DinoAction Jump(bool held = true) { return new DinoAction { jump = held }; }
    private static DinoWorld CollisionWorld(ObstacleKind kind, double bottom, int count = 1)
    {
        var snapshot = new DinoWorld(count, 1).Capture();
        snapshot.nextObstacleId = 1;
        snapshot.obstacles = new[] { new ObstacleState { id = 0, kind = kind, x = 80, bottom = bottom } };
        return new DinoWorld(snapshot);
    }

    private static void Main(string[] args)
    {
        Run("Independent neural biases and feed-forward state", () =>
        {
            var brain = BrainGenome.Create(new DeterministicRandom(10));
            Array.Clear(brain.genes, 0, brain.genes.Length);
            brain.genes[brain.genes.Length - 1] = Math.Log(3);
            var network = new NeuralNetwork(brain);
            var result = network.Evaluate(new double[10]);
            Check(result.Length == 2 && Math.Abs(result[0] - 0.5) < 1e-12 && Math.Abs(result[1] - 0.75) < 1e-12, "Output biases are wrong.");
            string expected = Json(result);
            Check(expected == Json(network.Evaluate(new double[10])), "Signals accumulate.");
            brain.genes[brain.genes.Length - 1] = -20;
            Check(expected == Json(network.Evaluate(new double[10])), "Network shares source genes.");
        });
        Run("Reject invalid neural topology and NaN", () =>
        {
            var brain = BrainGenome.Create(new DeterministicRandom(2));
            brain.genes[0] = double.NaN;
            bool rejected = false;
            try { brain.Validate(); } catch (ArgumentException) { rejected = true; }
            Check(rejected, "NaN accepted.");
        });
        Run("Same physics for both participants", () =>
        {
            var world = new DinoWorld(2, 45);
            for (int tick = 0; tick < 4000 && world.AliveCount > 0; tick++)
            {
                var action = new DinoAction { jump = tick % 65 < 25, duck = tick % 101 > 93 };
                world.Tick(new[] { action, action });
                Check(Json(world.Dinos[0]) == Json(world.Dinos[1]), "Physics differs by player index.");
            }
        });
        Run("Variable jump height, no double jump or auto hop", () =>
        {
            var shortJump = new DinoWorld(1, 9);
            var longJump = new DinoWorld(1, 9);
            double shortPeak = 0, longPeak = 0;
            for (int i = 0; i < 65; i++)
            {
                shortJump.Tick(new[] { Jump(i == 0) });
                longJump.Tick(new[] { Jump() });
                shortPeak = Math.Max(shortPeak, shortJump.Dinos[0].y);
                longPeak = Math.Max(longPeak, longJump.Dinos[0].y);
            }
            Check(longPeak > shortPeak + 10, "Holding jump does not increase height.");
            Check(longJump.Dinos[0].y == 0, "Holding jump automatically hops on landing.");
            longJump.Tick(new[] { Jump(false) });
            longJump.Tick(new[] { Jump() });
            Check(longJump.Dinos[0].y > 0, "A second press cannot jump.");
        });
        Run("Ducking and speed drop", () =>
        {
            var normal = new DinoWorld(1, 10);
            var drop = new DinoWorld(1, 10);
            for (int i = 0; i < 8; i++) { normal.Tick(new[] { Jump() }); drop.Tick(new[] { Jump() }); }
            drop.Tick(new[] { new DinoAction { duck = true } });
            normal.Tick(new[] { Jump() });
            Check(drop.Dinos[0].velocity < 0 && drop.Dinos[0].y < normal.Dinos[0].y, "Down does not cancel jump.");
            for (int i = 0; i < 20; i++) drop.Tick(new[] { new DinoAction { duck = true } });
            Check(drop.Dinos[0].y == 0 && drop.Dinos[0].ducking, "Speed drop does not finish in duck.");
        });
        Run("Cactus collision counts death once", () =>
        {
            var world = CollisionWorld(ObstacleKind.LargeCactus, 0);
            while (world.AliveCount > 0) world.Tick(new DinoAction[1]);
            int ticks = world.Ticks;
            for (int i = 0; i < 10; i++) world.Tick(new[] { Jump() });
            Check(world.AliveCount == 0 && world.Ticks == ticks, "Dead world continues or dies twice.");
        });
        Run("Middle bird requires duck, high bird can pass standing", () =>
        {
            var world = CollisionWorld(ObstacleKind.Bird, 25, 2);
            for (int i = 0; i < 20; i++) world.Tick(new[] { new DinoAction(), new DinoAction { duck = true } });
            Check(!world.Dinos[0].alive && world.Dinos[1].alive, "Bird hitboxes do not distinguish ducking.");
            var high = CollisionWorld(ObstacleKind.Bird, 50);
            for (int i = 0; i < 20; i++) high.Tick(new DinoAction[1]);
            Check(high.Dinos[0].alive, "High flying bird kills standing dino.");
        });
        Run("Three seconds clear, bounded acceleration and score", () =>
        {
            var world = new DinoWorld(1, 4);
            for (int i = 0; i < GameRules.ClearTicks - 1; i++) world.Tick(new DinoAction[1]);
            Check(world.Obstacles.Count == 0 && world.Speed > 6, "Initial clear time or acceleration wrong.");
            world.Tick(new DinoAction[1]);
            Check(world.Obstacles.Count == 1 && world.Score > 0, "First obstacle or score missing.");
            var snapshot = world.Capture();
            snapshot.ticks = 100000;
            snapshot.dinos[0].survivalTicks = snapshot.ticks;
            Check(new DinoWorld(snapshot).Speed == 13, "Speed is not capped.");
        });
        Run("No shared obstacle mutation or empty list crash", () =>
        {
            var single = new DinoWorld(1, 32);
            var multi = new DinoWorld(24, 32);
            for (int i = 0; i < 250; i++)
            {
                single.Tick(new DinoAction[1]); multi.Tick(new DinoAction[24]);
                Check(Json(single.Capture().obstacles) == Json(multi.Capture().obstacles), "Course depends on population.");
            }
        });
        Run("World snapshot restores controls and random stream exactly", () =>
        {
            var original = new DinoWorld(1, 123);
            for (int i = 0; i < 200; i++) original.Tick(new[] { Jump(i % 65 < 30) });
            var restored = new DinoWorld(Copy(original.Capture()));
            for (int i = 0; i < 3000; i++)
            {
                var action = new DinoAction { jump = i % 59 < 25, duck = i % 110 > 100 };
                original.Tick(new[] { action }); restored.Tick(new[] { action });
                Check(Json(original.Capture()) == Json(restored.Capture()), "World continuation diverges.");
            }
        });
        Run("Exact continuation through multiple generations", () =>
        {
            var original = new TrainingSession("deterministic", Small(), 12345);
            for (int i = 0; i < 283; i++) original.Tick();
            var restored = new TrainingSession(Copy(original.Capture()));
            for (int i = 0; i < 3500; i++)
            {
                Check(Json(original.Tick()) == Json(restored.Tick()), "Champion or generation diverges.");
                if (i % 239 == 0) Check(Json(original.Capture()) == Json(restored.Capture()), "Training continuation diverges.");
            }
            Check(original.Generation > 2, "Training did not evolve.");
        });
        Run("Elites stay unchanged, children never share weights", () =>
        {
            var random = new DeterministicRandom(445);
            var settings = Small();
            var population = Enumerable.Range(0, settings.populationSize).Select(_ => BrainGenome.Create(random)).ToArray();
            var dinos = Enumerable.Range(0, population.Length).Select(i => new DinoState { survivalTicks = i, score = i }).ToArray();
            string before = Json(population);
            var offspring = new GeneticAlgorithm(settings, random).Evolve(population, dinos);
            Check(before == Json(population), "Evolution mutates parents.");
            Check(Json(offspring[0]) == Json(population[population.Length - 1]), "Champion is not preserved.");
            offspring[0].genes[0] = 999;
            Check(before == Json(population), "Elite shares parent array.");
            for (int i = 1; i < offspring.Length; i++) Check(!ReferenceEquals(offspring[0].genes, offspring[i].genes), "Children share arrays.");
        });
        Run("Archived champion reproduces exact result", () =>
        {
            var session = new TrainingSession("replay", Small(), 999);
            GenerationChampion champion = null;
            for (int i = 0; i < 10000 && champion == null; i++) champion = session.Tick();
            Check(champion != null, "No generation winner.");
            var world = new DinoWorld(1, champion.courseSeed);
            var pilot = new NeuralPilot(champion.brain);
            while (world.AliveCount > 0 && world.Ticks <= champion.survivalTicks) world.Tick(new[] { pilot.Decide(world, 0) });
            Check(world.AliveCount == 0 && world.Dinos[0].survivalTicks == champion.survivalTicks && world.Dinos[0].score == champion.score, "Replay differs.");
        });
        Run("Living survivor remains in current generation", () =>
        {
            var session = new TrainingSession("survivor", Small(), 19);
            var snapshot = session.Capture();
            for (int i = 1; i < snapshot.world.dinos.Length; i++) snapshot.world.dinos[i].alive = false;
            session = new TrainingSession(snapshot);
            var champion = session.CaptureLivingSurvivor();
            Check(champion != null && !champion.generationCompleted && session.Generation == 1, "Living survivor ends generation.");
        });
        Run("Invalid checkpoints rejected", () =>
        {
            var snapshot = new DinoWorld(1, 2).Capture();
            snapshot.distance = double.NaN;
            bool rejected = false;
            try { new DinoWorld(snapshot); } catch (ArgumentException) { rejected = true; }
            Check(rejected, "Non-finite distance accepted.");
        });
        Run("Evaluation modes always own matching runners", () =>
        {
            var brain = BrainGenome.Create(new DeterministicRandom(512));
            var watch = new EvaluationSession(brain, 12, EvaluationMode.WatchBot);
            var versus = new EvaluationSession(brain, 12, EvaluationMode.HumanVsBot);
            Check(watch.World.Dinos.Length == 1 && watch.Human == null && !watch.HasHuman, "Bot-only population mismatch.");
            Check(versus.World.Dinos.Length == 2 && versus.Human == versus.World.Dinos[1] && versus.HasHuman, "Competition population mismatch.");
            versus.SetHumanInput(true, true);
            versus.Stop();
            int ticks = versus.World.Ticks;
            versus.RequestJump(); versus.Tick();
            Check(versus.State == EvaluationState.Stopped && versus.World.Ticks == ticks, "Stopped round accepts controls.");
        });
        Run("Competition waits and consumes a queued tap once", () =>
        {
            var round = new EvaluationSession(BrainGenome.Create(new DeterministicRandom(13)), 14, EvaluationMode.HumanVsBot);
            for (int i = 0; i < 60; i++) round.Tick();
            Check(round.World.Ticks == 0 && round.State == EvaluationState.WaitingForHuman, "Round moved before first jump.");
            round.RequestJump(); round.Tick();
            Check(round.Human.y > 0 && round.State == EvaluationState.Running, "First press did not jump/start.");
            for (int i = 0; i < 65; i++) round.Tick();
            Check(round.Human.y == 0 && !round.Human.jumpHeld, "Queued jump repeats after landing.");
        });
        Run("Pause clears held input and accepts a fresh jump", () =>
        {
            var round = new EvaluationSession(BrainGenome.Create(new DeterministicRandom(15)), 16, EvaluationMode.HumanVsBot);
            round.RequestJump(); round.SetHumanInput(true, false);
            for (int i = 0; i < 65; i++) round.Tick();
            Check(round.Human.y == 0 && round.Human.jumpHeld, "Held jump setup failed.");
            round.Pause();
            int ticks = round.World.Ticks;
            round.Tick();
            Check(round.World.Ticks == ticks && round.State == EvaluationState.Paused, "Paused round advances.");
            round.RequestJump(); round.Tick();
            Check(round.Human.y > 0 && round.State == EvaluationState.Running, "Pause retained an old jump latch.");
            round.SetHumanInput(false, true); round.Pause(); round.Resume();
            for (int i = 0; i < 65; i++) round.Tick();
            Check(!round.Human.ducking, "Paused round retained held duck input.");
        });
        Run("Human controls do not affect bot decisions", () =>
        {
            var brain = BrainGenome.Create(new DeterministicRandom(17));
            var watch = new EvaluationSession(brain, 18, EvaluationMode.WatchBot);
            var versus = new EvaluationSession(brain, 18, EvaluationMode.HumanVsBot);
            versus.RequestJump();
            for (int i = 0; i < 200; i++)
            {
                watch.SetHumanInput(true, true); watch.RequestJump();
                watch.Tick(); versus.Tick();
                Check(Json(watch.Bot) == Json(versus.Bot), "Bot gets different physics or decisions by mode.");
            }
            watch.Pause();
            int ticks = watch.World.Ticks;
            watch.Tick();
            Check(watch.World.Ticks == ticks, "Paused bot advances.");
            watch.Resume(); watch.Tick();
            Check(watch.World.Ticks > ticks, "Bot cannot resume.");
        });
        Console.WriteLine(passed + " core checks passed.");
        if (args.Contains("--training-smoke")) TrainingSmoke();
    }

    private static void TrainingSmoke()
    {
        foreach (uint seed in new uint[] { 314159, 271828, 161803 })
        {
            var session = new TrainingSession("smoke-" + seed, new EvolutionSettings(), seed);
            int firstBest = 0, champions = 0;
            GenerationChampion firstChampion = null, bestChampion = null;
            while (session.Generation <= 40 && session.World.Ticks < 24000)
            {
                var champion = session.Tick();
                if (champion != null)
                {
                    champions++;
                    if (champions == 1) { firstBest = champion.score; firstChampion = champion; }
                    if (bestChampion == null || champion.score > bestChampion.score) bestChampion = champion;
                    Console.WriteLine("seed=" + seed + " generation=" + champion.generation + " score=" + champion.score);
                }
            }
            Console.WriteLine("RESULT seed=" + seed + " initial=" + firstBest + " best=" + session.BestScore + " generations=" + champions);
            Check(session.BestScore >= 250 && session.BestScore > firstBest, "Evolution did not improve enough.");
            double before = EvaluateUnseen(firstChampion.brain), after = EvaluateUnseen(bestChampion.brain);
            Console.WriteLine("UNSEEN seed=" + seed + " initialMean=" + before + " trainedMean=" + after);
            Check(after > before * 1.5, "Champion did not generalize to unseen courses.");
        }
    }

    private static double EvaluateUnseen(BrainGenome brain)
    {
        int sum = 0;
        for (uint seed = 10001; seed <= 10010; seed++)
        {
            var world = new DinoWorld(1, seed);
            var pilot = new NeuralPilot(brain);
            var actions = new DinoAction[1];
            while (world.AliveCount > 0 && world.Ticks < 24000)
            {
                actions[0] = pilot.Decide(world, 0);
                world.Tick(actions);
            }
            sum += world.Dinos[0].score;
        }
        return sum / 10.0;
    }
}
