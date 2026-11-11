using System;
using System.Collections.Generic;

namespace ChromeDino.Core
{
    public static class GameRules
    {
        public const int Version = 1;
        public const double Step = 1.0 / 60;
        public const double Width = 600;
        public const double Height = 150;
        public const double Ground = 10;
        public const double DinoX = 50;
        public const double DinoWidth = 44;
        public const double DinoHeight = 47;
        public const double InitialSpeed = 6;
        public const double MaxSpeed = 13;
        public const double Acceleration = 0.001;
        public const double Gravity = 0.6;
        public const double JumpVelocity = 10;
        public const double MinJumpHeight = 30;
        public const double MaxJumpHeight = 63;
        public const int ClearTicks = 180;
    }

    [Serializable]
    public struct DinoAction
    {
        public bool jump;
        public bool duck;
    }

    [Serializable]
    public sealed class DinoState
    {
        // Height above the running surface, and velocity in pixels per fixed tick.
        public double y;
        public double velocity;
        public bool alive = true;
        public bool ducking;
        public bool jumpHeld;
        public bool reachedMinHeight;
        public bool speedDrop;
        public int score;
        public int passedObstacles;
        public int lastScoredObstacle = -1;
        public int survivalTicks;
        public double deathClearance;
        public DinoState Clone() { return (DinoState)MemberwiseClone(); }
    }

    public enum ObstacleKind { SmallCactus, LargeCactus, Bird }

    [Serializable]
    public sealed class ObstacleState
    {
        public int id;
        public double x;
        public double bottom;
        public double speedOffset;
        public int size = 1;
        public ObstacleKind kind;
        public double Width { get { return kind == ObstacleKind.Bird ? 46 : (kind == ObstacleKind.SmallCactus ? 17 : 25) * size; } }
        public double Height { get { return kind == ObstacleKind.Bird ? 40 : kind == ObstacleKind.SmallCactus ? 35 : 50; } }
        public ObstacleState Clone() { return (ObstacleState)MemberwiseClone(); }
    }

    [Serializable]
    public sealed class WorldSnapshot
    {
        public int ticks;
        public int nextObstacleId;
        public uint randomState;
        public double distance;
        public DinoState[] dinos;
        public ObstacleState[] obstacles;
    }

    public sealed class DinoWorld
    {
        private readonly DeterministicRandom random;
        private readonly List<ObstacleState> obstacles = new List<ObstacleState>();
        private int nextObstacleId;
        public DinoState[] Dinos { get; private set; }
        public IReadOnlyList<ObstacleState> Obstacles { get { return obstacles; } }
        public int Ticks { get; private set; }
        public int AliveCount { get; private set; }
        public double Distance { get; private set; }
        public double Speed { get { return Math.Min(GameRules.MaxSpeed, GameRules.InitialSpeed + Ticks * GameRules.Acceleration); } }
        public int Score { get { return (int)Math.Round(Distance * 0.025, MidpointRounding.AwayFromZero); } }

        public DinoWorld(int dinoCount, uint courseSeed)
        {
            if (dinoCount < 1 || dinoCount > 1000) throw new ArgumentOutOfRangeException(nameof(dinoCount));
            random = new DeterministicRandom(courseSeed);
            Dinos = new DinoState[dinoCount];
            for (int i = 0; i < dinoCount; i++) Dinos[i] = new DinoState();
            AliveCount = dinoCount;
        }

        public DinoWorld(WorldSnapshot snapshot)
        {
            if (snapshot == null || snapshot.ticks < 0 || snapshot.randomState == 0 || !Finite(snapshot.distance) ||
                snapshot.distance < 0 || snapshot.dinos == null || snapshot.dinos.Length < 1 || snapshot.dinos.Length > 1000 ||
                snapshot.obstacles == null || snapshot.obstacles.Length > 10 || snapshot.nextObstacleId < 0)
                throw new ArgumentException("Invalid world checkpoint.");
            random = new DeterministicRandom(snapshot.randomState);
            Ticks = snapshot.ticks;
            Distance = snapshot.distance;
            nextObstacleId = snapshot.nextObstacleId;
            Dinos = new DinoState[snapshot.dinos.Length];
            for (int i = 0; i < Dinos.Length; i++)
            {
                var dino = snapshot.dinos[i];
                if (dino == null || !Finite(dino.y) || !Finite(dino.velocity) || !Finite(dino.deathClearance) ||
                    dino.y < 0 || dino.y > 200 || Math.Abs(dino.velocity) > 100 || dino.score < 0 ||
                    dino.passedObstacles < 0 || dino.lastScoredObstacle >= nextObstacleId ||
                    dino.lastScoredObstacle < -1 || dino.survivalTicks < 0 || dino.survivalTicks > Ticks ||
                    (dino.alive && dino.survivalTicks != Ticks)) throw new ArgumentException("Invalid dino checkpoint.");
                Dinos[i] = dino.Clone();
                if (dino.alive) AliveCount++;
            }
            int previousId = -1;
            foreach (var obstacle in snapshot.obstacles)
            {
                if (obstacle == null || !Finite(obstacle.x) || !Finite(obstacle.bottom) || !Finite(obstacle.speedOffset) ||
                    Math.Abs(obstacle.x) > 10000 || obstacle.bottom < 0 || obstacle.bottom > 60 ||
                    obstacle.id <= previousId || obstacle.id >= nextObstacleId || obstacle.size < 1 || obstacle.size > 3 ||
                    !Enum.IsDefined(typeof(ObstacleKind), obstacle.kind) || Math.Abs(obstacle.speedOffset) > 0.8 ||
                    (obstacle.kind == ObstacleKind.Bird && obstacle.size != 1) ||
                    (obstacle.kind != ObstacleKind.Bird && (obstacle.bottom != 0 || obstacle.speedOffset != 0)))
                    throw new ArgumentException("Invalid obstacle checkpoint.");
                obstacles.Add(obstacle.Clone());
                previousId = obstacle.id;
            }
        }

        private static bool Finite(double value) { return !double.IsNaN(value) && !double.IsInfinity(value); }

        private void SpawnObstacle(double x)
        {
            var kind = (ObstacleKind)random.NextInt(Speed >= 8.5 ? 3 : 2);
            // Avoid long chains of one obstacle type while keeping each course reproducible.
            if (obstacles.Count >= 2 && obstacles[obstacles.Count - 1].kind == kind && obstacles[obstacles.Count - 2].kind == kind)
                kind = kind == ObstacleKind.SmallCactus ? ObstacleKind.LargeCactus : ObstacleKind.SmallCactus;
            bool bird = kind == ObstacleKind.Bird;
            obstacles.Add(new ObstacleState { id = nextObstacleId++, x = x, kind = kind,
                size = bird || (kind == ObstacleKind.LargeCactus && Speed < 7) ? 1 : 1 + random.NextInt(3),
                bottom = bird ? new[] { 0.0, 25.0, 50.0 }[random.NextInt(3)] : 0,
                speedOffset = bird ? (random.NextInt(2) == 0 ? -0.8 : 0.8) : 0 });
        }

        public ObstacleState NextObstacle()
        {
            ObstacleState closest = null;
            foreach (var obstacle in obstacles)
                if (obstacle.x + obstacle.Width >= GameRules.DinoX && (closest == null || obstacle.x < closest.x)) closest = obstacle;
            return closest;
        }

        public void ReadSensors(int dinoIndex, double[] inputs)
        {
            if (inputs == null || inputs.Length != 10) throw new ArgumentException("Ten sensors are required.");
            var dino = Dinos[dinoIndex];
            var obstacle = NextObstacle();
            double gap = GameRules.Width;
            if (obstacle != null)
                foreach (var next in obstacles)
                    if (next.id > obstacle.id) { gap = next.x - obstacle.x - obstacle.Width; break; }
            inputs[0] = obstacle == null ? 1 : Math.Max(-1, Math.Min(1, (obstacle.x - GameRules.DinoX - GameRules.DinoWidth) / GameRules.Width));
            inputs[1] = obstacle == null ? 0 : obstacle.Width / 75;
            inputs[2] = obstacle == null ? 0 : obstacle.Height / 50;
            inputs[3] = obstacle == null ? 0 : obstacle.bottom / 50;
            inputs[4] = Speed / GameRules.MaxSpeed;
            inputs[5] = dino.y / 100;
            inputs[6] = dino.velocity / 12;
            inputs[7] = dino.y == 0 ? 1 : 0;
            inputs[8] = Math.Max(0, Math.Min(1, gap / GameRules.Width));
            inputs[9] = dino.ducking ? 1 : 0;
        }

        public void Tick(DinoAction[] actions)
        {
            if (actions == null || actions.Length != Dinos.Length) throw new ArgumentException("One action per dino is required.");
            if (AliveCount == 0) return;
            double speed = Speed;
            Ticks++;
            Distance += speed;
            foreach (var obstacle in obstacles) obstacle.x -= speed + obstacle.speedOffset;
            obstacles.RemoveAll(obstacle => obstacle.x + obstacle.Width < -20);
            if (Ticks >= GameRules.ClearTicks)
            {
                if (obstacles.Count == 0) SpawnObstacle(GameRules.Width);
                var last = obstacles[obstacles.Count - 1];
                // One random gap per spawn: no per-frame reroll and no impossible overlaps.
                if (last.x + last.Width < GameRules.Width)
                {
                    double minimum = Math.Round(last.Width * speed * 0.6 + (last.kind == ObstacleKind.Bird ? 150 : 120));
                    double gap = minimum * (1 + random.NextDouble() * 0.5);
                    SpawnObstacle(last.x + last.Width + gap);
                }
            }
            for (int i = 0; i < Dinos.Length; i++)
            {
                var dino = Dinos[i];
                if (!dino.alive) continue;
                var action = actions[i];
                bool startJump = action.jump && !dino.jumpHeld && !action.duck && dino.y == 0;
                dino.jumpHeld = action.jump;
                if (startJump)
                {
                    dino.velocity = GameRules.JumpVelocity + speed / 10;
                    dino.reachedMinHeight = dino.speedDrop = false;
                }
                dino.ducking = action.duck && dino.y == 0 && !startJump;
                if (dino.y > 0 || startJump)
                {
                    if (action.duck && !dino.speedDrop) { dino.speedDrop = true; dino.velocity = -1; }
                    if ((!action.jump && dino.reachedMinHeight) || dino.y >= GameRules.MaxJumpHeight)
                        dino.velocity = Math.Min(5, dino.velocity);
                    dino.y = Math.Max(0, dino.y + dino.velocity * (dino.speedDrop ? 3 : 1));
                    dino.velocity -= GameRules.Gravity;
                    if (dino.y >= GameRules.MinJumpHeight) dino.reachedMinHeight = true;
                    if (dino.y == 0)
                    {
                        dino.velocity = 0;
                        dino.speedDrop = false;
                        dino.ducking = action.duck;
                    }
                }
                dino.survivalTicks++;
                foreach (var obstacle in obstacles)
                {
                    if (Collides(dino, obstacle))
                    {
                        dino.alive = false;
                        dino.deathClearance = dino.y;
                        AliveCount--;
                        break;
                    }
                    if (obstacle.id > dino.lastScoredObstacle && obstacle.x + obstacle.Width < GameRules.DinoX)
                    {
                        dino.lastScoredObstacle = obstacle.id;
                        dino.passedObstacles++;
                    }
                }
                if (dino.alive) dino.score = Score;
            }
        }

        private struct Box
        {
            public double x, y, width, height;
            public Box(double x, double y, double width, double height) { this.x = x; this.y = y; this.width = width; this.height = height; }
        }
        // Several small boxes follow the silhouette; transparent corners do not kill a runner.
        private static readonly Box[] Running = { new Box(22,31,17,16), new Box(1,20,30,9), new Box(10,4,14,8),
            new Box(1,18,29,5), new Box(5,13,21,4), new Box(9,9,15,4) };
        private static readonly Box[] Ducking = { new Box(1,4,55,25) };
        private static readonly Box[] Small = { new Box(0,1,5,27), new Box(4,1,6,34), new Box(10,17,7,14) };
        private static readonly Box[] Large = { new Box(0,0,7,38), new Box(8,1,7,49), new Box(13,2,10,38) };
        private static readonly Box[] Flying = { new Box(15,20,16,5), new Box(18,13,24,6), new Box(2,23,4,3), new Box(6,23,4,7), new Box(10,23,6,9) };

        private static bool Collides(DinoState dino, ObstacleState obstacle)
        {
            if (obstacle.x > GameRules.DinoX + 59 || obstacle.x + obstacle.Width < GameRules.DinoX ||
                obstacle.bottom > dino.y + GameRules.DinoHeight || obstacle.bottom + obstacle.Height < dino.y) return false;
            var runner = dino.ducking ? Ducking : Running;
            var target = obstacle.kind == ObstacleKind.Bird ? Flying : obstacle.kind == ObstacleKind.SmallCactus ? Small : Large;
            foreach (var a in runner)
            {
                for (int part = 0; part < target.Length; part++)
                {
                    var b = target[part];
                    if (obstacle.size > 1)
                    {
                        if (part == 1) b.width = obstacle.Width - target[0].width - target[2].width;
                        if (part == 2) b.x = obstacle.Width - target[2].width;
                    }
                    if (GameRules.DinoX + a.x < obstacle.x + b.x + b.width && GameRules.DinoX + a.x + a.width > obstacle.x + b.x &&
                        dino.y + a.y < obstacle.bottom + b.y + b.height && dino.y + a.y + a.height > obstacle.bottom + b.y) return true;
                }
            }
            return false;
        }

        public WorldSnapshot Capture()
        {
            var snapshot = new WorldSnapshot { ticks = Ticks, nextObstacleId = nextObstacleId, randomState = random.State,
                distance = Distance, dinos = new DinoState[Dinos.Length], obstacles = new ObstacleState[obstacles.Count] };
            for (int i = 0; i < Dinos.Length; i++) snapshot.dinos[i] = Dinos[i].Clone();
            for (int i = 0; i < obstacles.Count; i++) snapshot.obstacles[i] = obstacles[i].Clone();
            return snapshot;
        }
    }
}
