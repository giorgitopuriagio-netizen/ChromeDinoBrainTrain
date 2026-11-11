using System;
using System.Collections.Generic;
using ChromeDino.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ChromeDino.Presentation
{
    public sealed class BoardView : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private Texture2D atlas;
        private Image background;
        private Material spriteMaterial;
        private RectTransform obstacleRoot;
        private RectTransform dinoRoot;
        private RawImage[] ground;
        private RawImage[] clouds;
        private TMP_Text score;
        private TMP_Text overlay;
        private TMP_Text legend;
        private readonly List<RawImage> obstacleViews = new List<RawImage>();
        private readonly List<RawImage> dinoViews = new List<RawImage>();
        private bool ready;
        private bool gameOver;
        public Action Pressed { get; set; }
        public Action Released { get; set; }

        public static BoardView Create(UIFactory ui)
        {
            var rect = UIFactory.Rect("Chrome Dino field", ui.Root, 40, 112, 600, 150);
            rect.localScale = Vector3.one * 2;
            rect.gameObject.AddComponent<RectMask2D>();
            var view = rect.gameObject.AddComponent<BoardView>();
            view.atlas = Resources.Load<Texture2D>("DinoAtlas");
            if (view.atlas == null) throw new InvalidOperationException("DinoAtlas is missing from Resources.");
            view.spriteMaterial = Resources.Load<Material>("DinoInk");
            if (view.spriteMaterial == null) throw new InvalidOperationException("The Dino sprite material is missing from Resources.");
            view.background = ui.Panel(rect, 0, 0, 600, 150, Color.white);
            view.background.raycastTarget = true;
            view.clouds = new RawImage[3];
            for (int i = 0; i < 3; i++) view.clouds[i] = view.Crop(rect, new Rect(165, 100, 94, 29), 0, 0, 47, 14.5f);
            view.ground = new[] { view.Crop(rect, new Rect(2, 0, 1200, 26), 0, 137, 600, 13),
                view.Crop(rect, new Rect(1202, 0, 1200, 26), 600, 137, 600, 13) };
            view.obstacleRoot = UIFactory.Rect("Obstacles", rect, 0, 0, 600, 150);
            view.dinoRoot = UIFactory.Rect("Runners", rect, 0, 0, 600, 150);
            view.score = ui.Label(rect, "", 330, 10, 255, 24, 12, UIFactory.Ink, TextAlignmentOptions.TopRight);
            view.score.characterSpacing = 1.5f;
            view.overlay = ui.Label(rect, "", 100, 53, 420, 40, 17, UIFactory.Ink, TextAlignmentOptions.Center, true);
            view.legend = ui.Label(rect, "", 12, 10, 300, 24, 10, UIFactory.Muted);
            view.Render(null);
            return view;
        }

        private RawImage Crop(Transform parent, Rect source, float x, float y, float width, float height)
        {
            var image = UIFactory.Rect("Sprite", parent, x, y, width, height).gameObject.AddComponent<RawImage>();
            image.texture = atlas;
            image.material = spriteMaterial;
            image.uvRect = new Rect(source.x / atlas.width, source.y / atlas.height, source.width / atlas.width, source.height / atlas.height);
            image.raycastTarget = false;
            return image;
        }

        public void SetReady(bool value) { ready = value; }
        public void SetGameOver(bool value) { gameOver = value; }
        public void OnPointerDown(PointerEventData data) { if (data.button == PointerEventData.InputButton.Left) Pressed?.Invoke(); }
        public void OnPointerUp(PointerEventData data) { if (data.button == PointerEventData.InputButton.Left) Released?.Invoke(); }

        public void Render(DinoWorld world, bool versus = false)
        {
            double distance = world == null ? Time.unscaledTime * 35 : world.Distance;
            int ticks = world == null ? (int)(Time.unscaledTime * 60) : world.Ticks;
            bool night = world != null && (world.Score / 700) % 2 == 1;
            background.color = night ? new Color32(35, 36, 39, 255) : Color.white;
            Color ink = night ? new Color32(224, 225, 228, 255) : UIFactory.Ink;
            score.color = overlay.color = legend.color = ink;
            ground[0].rectTransform.anchoredPosition = new Vector2(-(float)(distance % 600), -137);
            ground[1].rectTransform.anchoredPosition = new Vector2(600 - (float)(distance % 600), -137);
            for (int i = 0; i < ground.Length; i++) ground[i].color = ink;
            for (int i = 0; i < clouds.Length; i++)
            {
                clouds[i].rectTransform.anchoredPosition = new Vector2(650 - (float)((distance * 0.035 + i * 231) % 750), -25 - i * 12);
                clouds[i].color = new Color(ink.r, ink.g, ink.b, night ? 0.35f : 0.26f);
            }
            int count = world == null ? 0 : world.Obstacles.Count;
            while (obstacleViews.Count < count) obstacleViews.Add(Crop(obstacleRoot, new Rect(), 0, 0, 1, 1));
            for (int i = 0; i < obstacleViews.Count; i++)
            {
                var image = obstacleViews[i];
                image.gameObject.SetActive(i < count);
                if (i >= count) continue;
                var obstacle = world.Obstacles[i];
                Rect source;
                if (obstacle.kind == ObstacleKind.Bird) source = new Rect(259 + (ticks / 10 % 2) * 93, 49, 93, 80);
                else
                {
                    float unit = obstacle.kind == ObstacleKind.SmallCactus ? 34 : 50;
                    float origin = obstacle.kind == ObstacleKind.SmallCactus ? 446 : 652;
                    float height = obstacle.kind == ObstacleKind.SmallCactus ? 72 : 101;
                    source = new Rect(origin + unit * obstacle.size * (obstacle.size - 1) / 2, 129 - height, unit * obstacle.size, height);
                }
                image.uvRect = new Rect(source.x / atlas.width, source.y / atlas.height, source.width / atlas.width, source.height / atlas.height);
                image.rectTransform.sizeDelta = new Vector2((float)obstacle.Width, (float)obstacle.Height);
                image.rectTransform.anchoredPosition = new Vector2((float)obstacle.x, (float)(-140 + obstacle.bottom + obstacle.Height));
                image.color = ink;
            }
            int runners = world == null ? 1 : world.Dinos.Length;
            while (dinoViews.Count < runners) dinoViews.Add(Crop(dinoRoot, new Rect(), 0, 0, 44, 47));
            int livingDrawn = 0;
            for (int i = 0; i < dinoViews.Count; i++)
            {
                var image = dinoViews[i];
                var dino = world != null && i < runners ? world.Dinos[i] : null;
                bool visible = i < runners && (dino == null || dino.alive || runners <= 2);
                if (visible && runners > 2 && ++livingDrawn > 60) visible = false;
                image.gameObject.SetActive(visible);
                if (!visible) continue;
                bool duck = dino != null && dino.ducking && dino.alive;
                int frame = dino != null && !dino.alive ? 4 : dino != null && dino.y > 0 ? 0 : 2 + ticks / 5 % 2;
                Rect source = duck ? new Rect(1865 + ticks / 8 % 2 * 119, 34, 118, 61) : new Rect(1338 + frame * 88, 34, 88, 95);
                image.uvRect = new Rect(source.x / atlas.width, source.y / atlas.height, source.width / atlas.width, source.height / atlas.height);
                float height = duck ? 30.5f : 47.5f;
                image.rectTransform.sizeDelta = new Vector2(duck ? 59 : 44, height);
                image.rectTransform.anchoredPosition = new Vector2(50, -140 + height + (float)(dino == null ? 0 : dino.y));
                Color color = versus && i == 1 ? (night ? new Color32(111, 179, 255, 255) : new Color32(39, 99, 190, 255)) : ink;
                if (runners > 2) color.a = 0.24f;
                if (dino != null && !dino.alive) color.a = 0.35f;
                image.color = color;
            }
            int displayedScore = world == null ? 0 : world.Score;
            if (world != null && world.AliveCount == 0)
            {
                displayedScore = 0;
                foreach (var runner in world.Dinos) displayedScore = Math.Max(displayedScore, runner.score);
            }
            int best = PlayerPrefs.GetInt("DinoHighScore", 0);
            score.text = "HI " + best.ToString("D5") + "   " + displayedScore.ToString("D5");
            overlay.text = gameOver ? "GAME OVER" : ready ? "SPACE / TAP TO RUN" : world == null ? "JUMP. DUCK. REPEAT." : "";
            legend.text = versus ? "BOT / GRAY    HUMAN / BLUE" : world != null && runners > 2 ? "RUNNERS ALIVE  " + world.AliveCount : "";
        }
    }
}
