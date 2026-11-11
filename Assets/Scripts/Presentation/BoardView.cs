using System;
using System.Collections.Generic;
using ChromeDino.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ChromeDino.Presentation
{
    public sealed class BoardView : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private Texture2D atlas;
        private Image background;
        private RectTransform obstacleRoot;
        private RectTransform dinoRoot;
        private RawImage[] ground;
        private RawImage[] clouds;
        private Text score;
        private Text overlay;
        private Text legend;
        private readonly List<RawImage> obstacleViews = new List<RawImage>();
        private readonly List<RawImage> dinoViews = new List<RawImage>();
        private bool ready;
        private bool gameOver;
        public Action Pressed { get; set; }
        public Action Released { get; set; }

        public static BoardView Create(UIFactory ui)
        {
            var rect = UIFactory.Rect("Chrome Dino field", ui.Root, 60, 105, 600, 150);
            rect.localScale = Vector3.one * (1160f / 600);
            rect.gameObject.AddComponent<RectMask2D>();
            var view = rect.gameObject.AddComponent<BoardView>();
            view.atlas = Resources.Load<Texture2D>("DinoAtlas");
            if (view.atlas == null) throw new InvalidOperationException("DinoAtlas is missing from Resources.");
            view.background = ui.Panel(rect, 0, 0, 600, 150, Color.white);
            view.background.raycastTarget = true;
            view.clouds = new RawImage[3];
            for (int i = 0; i < 3; i++) view.clouds[i] = view.Crop(rect, new Rect(165, 100, 94, 29), 0, 0, 47, 14.5f);
            view.ground = new[] { view.Crop(rect, new Rect(2, 0, 1200, 26), 0, 137, 600, 13),
                view.Crop(rect, new Rect(1202, 0, 1200, 26), 600, 137, 600, 13) };
            view.obstacleRoot = UIFactory.Rect("Obstacles", rect, 0, 0, 600, 150);
            view.dinoRoot = UIFactory.Rect("Runners", rect, 0, 0, 600, 150);
            view.score = ui.Label(rect, "", 360, 8, 225, 22, 14, UIFactory.Ink, TextAnchor.UpperRight);
            view.overlay = ui.Label(rect, "", 150, 47, 420, 38, 18, UIFactory.Ink, TextAnchor.MiddleCenter);
            view.legend = ui.Label(rect, "", 10, 8, 260, 20, 11, UIFactory.Muted);
            view.Render(null);
            return view;
        }

        private RawImage Crop(Transform parent, Rect source, float x, float y, float width, float height)
        {
            var image = UIFactory.Rect("Sprite", parent, x, y, width, height).gameObject.AddComponent<RawImage>();
            image.texture = atlas;
            image.uvRect = new Rect(source.x / atlas.width, source.y / atlas.height, source.width / atlas.width, source.height / atlas.height);
            image.raycastTarget = false;
            return image;
        }

        public void SetReady(bool value) { ready = value; }
        public void SetGameOver(bool value) { gameOver = value; }
        public void OnPointerDown(PointerEventData data) { if (data.button == PointerEventData.InputButton.Left) Pressed?.Invoke(); }
        public void OnPointerUp(PointerEventData data) { if (data.button == PointerEventData.InputButton.Left) Released?.Invoke(); }

        public void Render(DinoWorld world, bool versus = false, bool humanOnly = false)
        {
            double distance = world == null ? Time.unscaledTime * 35 : world.Distance;
            int ticks = world == null ? (int)(Time.unscaledTime * 60) : world.Ticks;
            bool night = world != null && (world.Score / 700) % 2 == 1;
            background.color = night ? new Color32(35, 36, 39, 255) : Color.white;
            Color ink = night ? new Color32(224, 225, 228, 255) : UIFactory.Ink;
            score.color = overlay.color = legend.color = ink;
            ground[0].rectTransform.anchoredPosition = new Vector2(-(float)(distance % 600), -137);
            ground[1].rectTransform.anchoredPosition = new Vector2(600 - (float)(distance % 600), -137);
            for (int i = 0; i < ground.Length; i++) ground[i].color = night ? Color.white : new Color(0.65f, 0.65f, 0.65f);
            for (int i = 0; i < clouds.Length; i++)
            {
                clouds[i].rectTransform.anchoredPosition = new Vector2(650 - (float)((distance * 0.035 + i * 231) % 750), -25 - i * 12);
                clouds[i].color = night ? new Color(1, 1, 1, 0.35f) : Color.white;
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
                image.color = night ? new Color(1.8f, 1.8f, 1.8f) : Color.white;
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
                Color color = versus && i == 1 || humanOnly ? new Color32(70, 140, 220, 255) : Color.white;
                if (runners > 2) color.a = 0.2f;
                if (dino != null && !dino.alive) color.a = 0.35f;
                image.color = color;
            }
            int best = PlayerPrefs.GetInt("DinoHighScore", 0);
            score.text = "HI " + best.ToString("D5") + "   " + (world == null ? "00000" : world.Score.ToString("D5"));
            overlay.text = gameOver ? "G A M E  O V E R" : ready ? "ПРОБЕЛ / КАСАНИЕ — СТАРТ" : world == null ? "CHROME DINO" : "";
            legend.text = versus ? "Серый — бот   Синий — человек" : humanOnly ? "Игрок" : world != null && runners > 2 ? "Популяция: " + world.AliveCount : "";
        }
    }
}
