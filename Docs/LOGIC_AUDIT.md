# Project audit

Reviewed on 11 November 2026. Target: **ChromeDinoBrainTrain**. Reference workflow: **FlappyBirdBrainTrain**.

## Menu and scene flow

The active scenes are MainMenu, Training, and AITest. MainMenu offers the same three actions as the reference: New training, Continue training, and AI evaluation / Human vs bot.

Evaluation now uses one generation-brain dropdown containing champions from every saved run. Run numbers distinguish repeated generation numbers. Watch bot and Human vs bot immediately construct a fresh round. Mode buttons stay available during a round; the generation and course selectors remain locked until it finishes or is stopped. The separate run selector, Start/Retry button, and standalone-human mode were removed to match the requested reference workflow. Human play remains available through competition.

Interface text is in English. The horizontal Dino board, jumping, holding, ducking, and runner-specific obstacles are retained.

## Fixed behavior

| Issue | Change and verification |
| --- | --- |
| Round mode, world size, pilot, input array, and running flags were managed separately in the screen. | EvaluationSession owns the immutable mode and matching runner/action counts. Mode changes replace the complete round. Core and Play Mode checks cover repeated switches in both directions. |
| Switching modes required stopping and pressing a separate Start button. | A mode button launches or restarts immediately, matching FlappyBirdBrainTrain. |
| Choosing a different generation after a round could retain the previous world and overwrite the selection's statistics. | A new selection clears the old round, overlays, accumulated time, and input. |
| A held jump could remain latched in the world after pausing, preventing a fresh press from jumping. | Pause clears both queued/held input and the human runner's jump latch. Competition resumes on a fresh human press. |
| A right mouse-button release could cancel an active left-button hold. | HoldInput tracks the controlling pointer and respects mouse button and button-interactable state. |
| Bot-only evaluation waited for manual input after focus returned. | Bot playback resumes automatically; competition waits for the human. Both clear accumulated time and pointer input on suspension. |
| The final board score could exceed the recorded runner score by one point on the collision tick. | The ended board displays the maximum recorded participant score. Simulation rules and saved-brain compatibility are unchanged. |
| A damaged/missing latest.txt prevented continuation even when latest.txt.bak was valid. | The pointer is size-limited, validated, and restored from its backup when necessary. Checkpoint loading continues to use the selected run rather than silently switching to another run if that run's checkpoint is damaged. |
| A damaged or missing primary champion record made an intact backup unavailable. | The archive considers primary and backup records together, loads each generation once, and validates both run identity and generation filename. |
| InvalidDataException was not classified as a recoverable data error. | Schema-invalid JSON now reaches checkpoint/pointer/archive backup handling. A dedicated test covers a syntactically valid but structurally invalid checkpoint. |

## Training and gameplay review

The existing deterministic fixed-step world, 10-16-10-2 network, biases, normalized sensors, genetic selection, crossover, additive mutation, elitism, and random immigrants were retained. No game-rules version or checkpoint schema change was needed.

The checks verify identical bot behavior between evaluation modes, equal physics for participants, variable jump height, ducking and speed drop, cactus/bird hitboxes, clear time and acceleration, independent genomes, exact continuation, survivor handling, and champion replay. Completed generations still archive a winner and start the next population. A living last survivor remains playable without a forced timeout.

Earlier 40-generation trials on seeds 314159, 271828, and 161803 improved best training scores from 58/58/72 to 878/1731/1644. Mean scores on unseen seeds 10001-10010 improved from 40.4/50.3/56.2 to 606.7/763.0/900.6. These historical experiments are reproducible with --training-smoke; scores on different courses need not improve monotonically.

## Cleanup

Removed items are listed in [CLEANUP.md](CLEANUP.md). The old implementation is already in Git history. A checked ZIP of the original tracked project was also created outside the repository before cleanup. Application.persistentDataPath and user training saves were not touched.

FlappyBirdBrainTrain was read as a reference; its project files were not modified by this review.

## Validation outputs

Core, Edit Mode, Play Mode, build, and startup results are recorded in the generated TestResults/ and Logs/ directories. Play Mode captures menu, training, competition, and the running course. The root README documents the current workflow and commands.

Verified results: **19 core checks, 10 Edit Mode tests, and 3 Play Mode scenarios passed (32 total)**. Three training smoke runs passed. The cleaned Windows build succeeded and passed a 10-second headless startup check without observed errors or exceptions. Rendered UI previews were inspected separately.
