# Interface and rendering

ChromeDinoBrainTrain uses a clean English interface and keeps the familiar pixel silhouettes of the Dino runner. All three screens share typography, spacing, button states, and control hints.

## Text

TextMesh Pro renders signed-distance-field text, including labels inside the scaled game board. The font and its license are included in `Assets/TextMesh Pro/`; no system font or additional download is needed. The bundled Liberation Sans font uses the SIL Open Font License; see `Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt`.

The main menu explains each action. Training separates generation, living runners, current score, and best score into four large values. The selected training speed and evaluation mode use the same blue highlight. Messages explain how to start, resume, save, and recover from an error. The saved-brain list uses ellipsis for unusually long entries rather than drawing beyond its field.

## Pixel graphics

The original `DinoAtlas.png` remains uncompressed, with point filtering and no mipmaps. At the 1280 × 900 reference resolution, the 600 × 150 simulation board is displayed at exactly 2×. Canvas pixel alignment and the `ChromeDino/UI/Pixel Ink` shader keep sprite edges sharp.

The shader extracts the dark silhouettes from the original atlas and applies a consistent ink color. This removes pale edge halos and makes runners, birds, cacti, and ground visible during the night phase. The bot is gray; the human is blue, with a brighter blue at night. UI clipping is supported so obstacles stay inside the board.

The canvas fits the complete layout into the available viewport and fills the surrounding area with the same background. Signed-distance-field text remains smooth at different window sizes; pixel art deliberately retains hard edges.

## Review the result

1. Open `Assets/Scenes/MainMenu.unity` and enter Play Mode. Check the action descriptions and keyboard hints.
2. Start training and try each speed. Check the selected-speed highlight and four statistics. Pause, save, and continue.
3. Open AI evaluation, choose a champion, and switch between Watch bot and Human vs bot. Check the mode highlight, score labels, dropdown, and held controls.
4. Check a run after score 700: sprites change to light ink on a dark board; the human remains blue.
5. Run Play Mode tests for fresh captures in `TestResults/Previews/`. Screenshots in `Docs/Images/` are captures of the actual Unity interface.

These presentation changes preserve the simulation, neural network, evolution settings, save format, and three-scene navigation.
