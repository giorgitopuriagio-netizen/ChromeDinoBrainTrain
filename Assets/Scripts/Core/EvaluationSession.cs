using System;

namespace ChromeDino.Core
{
    public enum EvaluationMode { WatchBot, HumanVsBot }
    public enum EvaluationState { WaitingForHuman, Running, Paused, Completed, Stopped }

    // A round owns its immutable mode, matching population, controller and input.
    public sealed class EvaluationSession
    {
        private readonly NeuralPilot pilot;
        private readonly DinoAction[] actions;
        private bool queuedJump;
        private bool heldJump;
        private bool heldDuck;

        public EvaluationMode Mode { get; private set; }
        public EvaluationState State { get; private set; }
        public DinoWorld World { get; private set; }
        public bool HasHuman { get { return Mode == EvaluationMode.HumanVsBot; } }
        public bool IsActive { get { return State == EvaluationState.Running || State == EvaluationState.WaitingForHuman || State == EvaluationState.Paused; } }
        public DinoState Bot { get { return World.Dinos[0]; } }
        public DinoState Human { get { return HasHuman ? World.Dinos[1] : null; } }

        public EvaluationSession(BrainGenome brain, uint courseSeed, EvaluationMode mode)
        {
            if (brain == null) throw new ArgumentNullException(nameof(brain));
            if (mode != EvaluationMode.WatchBot && mode != EvaluationMode.HumanVsBot)
                throw new ArgumentOutOfRangeException(nameof(mode));
            pilot = new NeuralPilot(brain);
            Mode = mode;
            World = new DinoWorld(HasHuman ? 2 : 1, courseSeed);
            actions = new DinoAction[World.Dinos.Length];
            State = HasHuman ? EvaluationState.WaitingForHuman : EvaluationState.Running;
        }

        public void RequestJump()
        {
            if (!IsActive || !HasHuman) return;
            // A new press can resume competition even after the human has crashed.
            State = EvaluationState.Running;
            queuedJump |= Human.alive;
        }

        public void SetHumanInput(bool jump, bool duck)
        {
            heldJump = IsActive && HasHuman && jump;
            heldDuck = IsActive && HasHuman && duck;
        }

        public void Tick()
        {
            if (State != EvaluationState.Running) return;
            actions[0] = Bot.alive ? pilot.Decide(World, 0) : default(DinoAction);
            if (HasHuman) actions[1] = new DinoAction { jump = queuedJump || heldJump, duck = heldDuck };
            queuedJump = false;
            World.Tick(actions);
            if (World.AliveCount == 0) State = EvaluationState.Completed;
        }

        private void ClearInput()
        {
            queuedJump = heldJump = heldDuck = false;
            if (HasHuman) Human.jumpHeld = false;
        }

        public void Pause()
        {
            if (!IsActive) return;
            if (State == EvaluationState.Running) State = EvaluationState.Paused;
            ClearInput();
        }

        public void Resume() { if (State == EvaluationState.Paused) State = EvaluationState.Running; }

        public void Stop()
        {
            if (!IsActive) return;
            State = EvaluationState.Stopped;
            ClearInput();
        }
    }
}
