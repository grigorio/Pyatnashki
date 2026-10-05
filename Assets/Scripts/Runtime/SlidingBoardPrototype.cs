using System.Collections;
using Pyatnashki.Domain;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Pyatnashki
{
    /// <summary>Stage one: UI built at runtime by the component in SampleScene.</summary>
    public sealed class SlidingBoardPrototype : MonoBehaviour
    {
        [SerializeField, Min(1)] private int scrambleMoves = 24;
        [SerializeField, Min(0.01f)] private float slideDuration = 0.16f;
        private static readonly Color Ink = new Color(0.91f, 0.94f, 0.96f);
        private static readonly Color Muted = new Color(0.60f, 0.68f, 0.73f);
        private static readonly Color Gold = new Color(0.97f, 0.73f, 0.31f);
        private readonly SlidingBoard board = new SlidingBoard();
        private readonly System.Random random = new System.Random();
        private readonly RectTransform[] tiles = new RectTransform[SlidingBoard.CellCount];
        private readonly Button[] buttons = new Button[SlidingBoard.CellCount];
        private readonly Image[] images = new Image[SlidingBoard.CellCount];
        private RectTransform canvasRect, safeRect, contentRect, boardRect, emptyMarker;
        private GameObject finalTile;
        private Font font;
        private Text movesText, correctText, statusText;
        private Button shuffleButton, practiceButton;
        private bool busy;

        private void Awake()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (EventSystem.current == null)
            {
                var events = new GameObject("Prototype EventSystem", typeof(EventSystem),
                    typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform, false);
            }
            BuildInterface();
            StartRound(scrambleMoves);
        }

        private void LateUpdate() => FitSafeArea();

        private void BuildInterface()
        {
            var obj = new GameObject("Prototype Canvas", typeof(RectTransform),
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            obj.transform.SetParent(transform, false);
            obj.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = obj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = 0.5f;
            canvasRect = obj.GetComponent<RectTransform>();
            var background = Panel("Background", canvasRect, Vector2.zero, Vector2.zero,
                new Color(0.055f, 0.09f, 0.13f));
            background.rectTransform.anchorMin = Vector2.zero;
            background.rectTransform.anchorMax = Vector2.one;
            background.rectTransform.offsetMin = background.rectTransform.offsetMax = Vector2.zero;
            safeRect = MakeRect("Safe Area", canvasRect, Vector2.zero, Vector2.zero);
            contentRect = MakeRect("Content", safeRect, Vector2.zero, new Vector2(1500, 840));
            Label("Title", contentRect, new Vector2(0, 385), new Vector2(1100, 60),
                "П’ЯТНАШКИ · ОБЛОГА", 38, Ink, FontStyle.Bold);
            Label("Instructions", contentRect, new Vector2(0, 337), new Vector2(1150, 40),
                "Натискай на плитку поруч із порожньою клітинкою", 23, Muted);

            boardRect = MakeRect("Board", contentRect, new Vector2(0, -5), new Vector2(592, 592));
            Panel("Board Frame", boardRect, Vector2.zero, new Vector2(592, 592),
                new Color(0.11f, 0.16f, 0.20f));
            for (int i = 0; i < SlidingBoard.CellCount; i++)
                Panel("Cell " + i, boardRect, CellPosition(i), new Vector2(132, 132),
                    new Color(0.075f, 0.12f, 0.16f));
            emptyMarker = Label("Empty Cell", boardRect, Vector2.zero, new Vector2(130, 70),
                "ВІЛЬНО", 17, Muted).rectTransform;
            for (int tile = 1; tile < SlidingBoard.CellCount; tile++)
            {
                int number = tile;
                var button = MakeButton("Tile " + tile, boardRect, Vector2.zero,
                    new Vector2(132, 132), "", () => RequestMove(number));
                buttons[tile] = button;
                tiles[tile] = button.GetComponent<RectTransform>();
                images[tile] = button.GetComponent<Image>();
                Label("Number", tiles[tile], new Vector2(0, 12), new Vector2(125, 70),
                    tile.ToString(), 46, new Color(0.08f, 0.12f, 0.15f), FontStyle.Bold);
                Label("Capacity", tiles[tile], new Vector2(0, -40), new Vector2(125, 26),
                    "Місткість: " + tile, 15, new Color(0.12f, 0.20f, 0.23f));
            }
            var final = Panel("Final Tile", boardRect, CellPosition(15), new Vector2(132, 132), Gold);
            finalTile = final.gameObject;
            Panel("Final Road", final.rectTransform, Vector2.zero, new Vector2(132, 22),
                new Color(0.28f, 0.22f, 0.13f));
            Label("Final Label", final.rectTransform, new Vector2(0, 38), new Vector2(125, 30),
                "ФІНІШ", 18, new Color(0.10f, 0.14f, 0.16f), FontStyle.Bold);
            finalTile.SetActive(false);
            BuildSidePanels();
            shuffleButton = MakeButton("Shuffle", contentRect, new Vector2(-150, -346),
                new Vector2(280, 54), "Нове поле", () => StartRound(scrambleMoves));
            practiceButton = MakeButton("Practice", contentRect, new Vector2(150, -346),
                new Vector2(280, 54), "Навчальний режим", () => StartRound(1));
            statusText = Label("Status", contentRect, new Vector2(0, -399),
                new Vector2(1360, 40), "", 21, Ink);
            FitSafeArea();
        }

        private void BuildSidePanels()
        {
            Color panel = new Color(0.09f, 0.14f, 0.18f);
            var left = Panel("Castle Panel", contentRect, new Vector2(-540, 10),
                new Vector2(320, 370), panel);
            Label("Castle Title", left.rectTransform, new Vector2(0, 135),
                new Vector2(290, 40), "ЗАМОК", 27, Ink, FontStyle.Bold);
            Color stone = new Color(0.35f, 0.45f, 0.51f);
            Panel("Castle Body", left.rectTransform, new Vector2(0, 5), new Vector2(140, 100), stone);
            Panel("Left Tower", left.rectTransform, new Vector2(-83, 23), new Vector2(38, 135), stone);
            Panel("Right Tower", left.rectTransform, new Vector2(83, 23), new Vector2(38, 135), stone);
            Panel("Gate", left.rectTransform, new Vector2(0, -26), new Vector2(37, 42), panel);
            Label("Castle Caption", left.rectTransform, new Vector2(0, -105),
                new Vector2(290, 60), "Напрямок наступу\n←", 23, Muted);
            var right = Panel("Progress Panel", contentRect, new Vector2(540, 10),
                new Vector2(320, 370), panel);
            Label("Progress Title", right.rectTransform, new Vector2(0, 135),
                new Vector2(290, 40), "ДОШКА", 27, Ink, FontStyle.Bold);
            movesText = Label("Moves", right.rectTransform, new Vector2(0, 60),
                new Vector2(290, 45), "", 27, Gold, FontStyle.Bold);
            correctText = Label("Correct Tiles", right.rectTransform, Vector2.zero,
                new Vector2(290, 65), "", 23, Ink);
            Label("Goal", right.rectTransform, new Vector2(0, -105), new Vector2(290, 90),
                "Склади числа 1–15\nзліва направо,\nрядок за рядком", 21, Muted);
        }

        private void StartRound(int steps)
        {
            if (busy) return;
            board.Shuffle(random, Mathf.Max(1, steps));
            finalTile.SetActive(false);
            for (int tile = 1; tile < SlidingBoard.CellCount; tile++)
                tiles[tile].anchoredPosition = CellPosition(board.GetIndexOf(tile));
            statusText.text = steps == 1
                ? "Один правильний хід — і з’явиться завершальна плитка."
                : "Золоті плитки можна пересунути. Зелені вже на своїх місцях.";
            RefreshInterface();
        }

        private void RequestMove(int tile)
        {
            if (busy || board.IsSolved || !board.CanMoveTile(tile)) return;
            Vector2 destination = CellPosition(board.EmptyIndex);
            if (!board.TryMoveTile(tile)) return;
            busy = true;
            RefreshInterface();
            StartCoroutine(AnimateMove(tile, destination));
        }

        private IEnumerator AnimateMove(int tile, Vector2 destination)
        {
            Vector2 start = tiles[tile].anchoredPosition;
            float elapsed = 0f;
            float duration = Mathf.Max(0.01f, slideDuration);
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                tiles[tile].anchoredPosition = Vector2.Lerp(start, destination, t * t * (3f - 2f * t));
                yield return null;
            }
            tiles[tile].anchoredPosition = destination;
            busy = false;
            if (board.IsSolved)
            {
                finalTile.SetActive(true);
                statusText.text = "Дошку складено! Завершальна плитка заповнила вільну клітинку.";
            }
            RefreshInterface();
        }

        private void RefreshInterface()
        {
            movesText.text = "Ходи: " + board.MoveCount;
            correctText.text = "На своїх місцях\n" + board.CorrectTileCount + " / 15";
            emptyMarker.anchoredPosition = CellPosition(board.EmptyIndex);
            emptyMarker.gameObject.SetActive(!board.IsSolved);
            shuffleButton.interactable = practiceButton.interactable = !busy;
            for (int tile = 1; tile < SlidingBoard.CellCount; tile++)
            {
                bool movable = !busy && !board.IsSolved && board.CanMoveTile(tile);
                buttons[tile].interactable = movable;
                images[tile].color = movable ? Gold : board.GetIndexOf(tile) == tile - 1
                    ? new Color(0.45f, 0.72f, 0.61f) : new Color(0.70f, 0.76f, 0.78f);
            }
        }

        private void FitSafeArea()
        {
            if (Screen.width <= 0 || Screen.height <= 0) return;
            UnityEngine.Rect area = Screen.safeArea;
            safeRect.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            safeRect.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
            safeRect.offsetMin = safeRect.offsetMax = Vector2.zero;
            Vector2 available = Vector2.Scale(canvasRect.rect.size, safeRect.anchorMax - safeRect.anchorMin);
            float scale = Mathf.Min(available.x / 1500f, available.y / 840f);
            contentRect.localScale = Vector3.one * Mathf.Max(0.01f, scale);
        }

        private static Vector2 CellPosition(int index) => new Vector2(
            (index % 4 - 1.5f) * 144f, (1.5f - index / 4) * 144f);

        private static RectTransform MakeRect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        private static Image Panel(string name, Transform parent, Vector2 position, Vector2 size, Color color)
        {
            var image = MakeRect(name, parent, position, size).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private Text Label(string name, Transform parent, Vector2 position, Vector2 size,
            string value, int fontSize, Color color, FontStyle style = FontStyle.Normal)
        {
            var text = MakeRect(name, parent, position, size).gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            text.text = value;
            return text;
        }

        private Button MakeButton(string name, Transform parent, Vector2 position, Vector2 size,
            string caption, UnityEngine.Events.UnityAction action)
        {
            var image = Panel(name, parent, position, size, new Color(0.22f, 0.32f, 0.39f));
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;
            var colors = button.colors;
            colors.normalColor = colors.disabledColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.95f, 0.83f);
            colors.pressedColor = new Color(0.80f, 0.85f, 0.87f);
            button.colors = colors;
            button.onClick.AddListener(action);
            if (!string.IsNullOrEmpty(caption))
                Label("Label", image.rectTransform, Vector2.zero, size, caption, 22, Ink);
            return button;
        }
    }
}
