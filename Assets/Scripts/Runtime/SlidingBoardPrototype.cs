using System.Collections;
using System.Collections.Generic;
using Pyatnashki.Domain;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Pyatnashki
{
    /// <summary>Sliding board and road preview built at runtime in SampleScene.</summary>
    public sealed class SlidingBoardPrototype : MonoBehaviour
    {
        [SerializeField, Min(1)] private int scrambleMoves = 24;
        [SerializeField, Min(0.01f)] private float slideDuration = 0.16f;
        [SerializeField, Min(0.05f)] private float warriorStepDuration = 0.6f;
        [SerializeField, Min(0.05f)] private float warriorReturnDuration = 0.3f;
        private static readonly Color Ink = new Color(0.91f, 0.94f, 0.96f);
        private static readonly Color Muted = new Color(0.60f, 0.68f, 0.73f);
        private static readonly Color Gold = new Color(0.97f, 0.73f, 0.31f);
        private readonly SlidingBoard board = new SlidingBoard();
        private readonly WarriorSimulation warrior = new WarriorSimulation();
        private readonly System.Random random = new System.Random();
        private readonly RectTransform[] tiles = new RectTransform[SlidingBoard.CellCount];
        private readonly Button[] buttons = new Button[SlidingBoard.CellCount];
        private readonly Image[] images = new Image[SlidingBoard.CellCount];
        private readonly List<Image>[] roads = new List<Image>[RoadLayout.FinalTile + 1];
        private readonly List<RoadLink> roadLinks = new List<RoadLink>();
        private sealed class RoadLink
        {
            public int Cell, Next;
            public RoadPorts Direction;
            public Image Image;
        }
        private RectTransform canvasRect, safeRect, contentRect, boardRect, emptyMarker, warriorMarker;
        private GameObject finalTile;
        private Font font;
        private Text movesText, correctText, routeText, statusText, warriorText;
        private Button shuffleButton, practiceButton, sendWarriorButton, carryTestButton;
        private bool busy;
        private enum WarriorMotion { None, Enter, Move, Exit }
        private WarriorMotion warriorMotion;
        private Vector2 warriorStart, warriorEnd;
        private float warriorElapsed, warriorCooldown;
        private int warriorDestination;
        private bool destinationEntered, warriorReturning;
        private Vector2 warriorReturnStart, warriorReturnTarget;
        private float warriorReturnElapsed;

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

        private void Update()
        {
            // Returning follows its owning tile while that tile is also sliding.
            if (warriorReturning) AdvanceWarriorReturn();
            else if (!busy)
            {
                if (warriorMotion != WarriorMotion.None) AdvanceWarriorMotion();
                else if (warriorCooldown > 0f) warriorCooldown -= Time.unscaledDeltaTime;
                else PlanWarriorMotion();
            }
            RefreshWarriorInterface();
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
            BuildRoadLinks();
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
                BuildRoad(tile, tiles[tile]);
                Label("Number", tiles[tile], new Vector2(-43, 43), new Vector2(42, 34),
                    tile.ToString(), 28, new Color(0.08f, 0.12f, 0.15f), FontStyle.Bold);
                Label("Capacity", tiles[tile], new Vector2(-40, -46), new Vector2(56, 24),
                    "≤ " + tile, 17, new Color(0.12f, 0.20f, 0.23f));
            }
            var final = Panel("Final Tile", boardRect, CellPosition(15), new Vector2(132, 132), Gold);
            finalTile = final.gameObject;
            BuildRoad(RoadLayout.FinalTile, final.rectTransform);
            Label("Final Label", final.rectTransform, new Vector2(0, 38), new Vector2(125, 30),
                "ФІНІШ", 18, new Color(0.10f, 0.14f, 0.16f), FontStyle.Bold);
            finalTile.SetActive(false);
            Label("Entry Arrow", contentRect, new Vector2(325, -221), new Vector2(60, 44),
                "←", 36, Gold, FontStyle.Bold);
            Label("Castle Arrow", contentRect, new Vector2(-325, -221), new Vector2(60, 44),
                "←", 36, Gold, FontStyle.Bold);
            Label("Entry Caption", contentRect, new Vector2(540, -235), new Vector2(310, 65),
                "ВХІД ВОЇНІВ\nБірюзова дорога — доступний шлях\nЧервоний маркер — воїн", 17, Muted);
            Label("Exit Caption", contentRect, new Vector2(-540, -235), new Vector2(310, 65),
                "ВИХІД ДО ЗАМКУ\n≤ число — місткість плитки", 18, Muted);
            BuildSidePanels();
            shuffleButton = MakeButton("Shuffle", contentRect, new Vector2(-150, -346),
                new Vector2(280, 54), "Нове поле", () => StartRound(scrambleMoves));
            practiceButton = MakeButton("Practice", contentRect, new Vector2(150, -346),
                new Vector2(280, 54), "Навчальний режим", () => StartRound(1));
            sendWarriorButton = MakeButton("Send Warrior", contentRect, new Vector2(540, -309),
                new Vector2(300, 46), "Ще один воїн", QueueNextWarrior);
            carryTestButton = MakeButton("Carry Test", contentRect, new Vector2(-540, -309),
                new Vector2(300, 46), "Тест перенесення", () => StartRound(1, true));
            statusText = Label("Status", contentRect, new Vector2(0, -399),
                new Vector2(1360, 40), "", 21, Ink);
            warriorMarker = Panel("Warrior", boardRect, Vector2.zero, new Vector2(32, 32),
                new Color(0.88f, 0.16f, 0.18f)).rectTransform;
            Panel("Warrior Centre", warriorMarker, Vector2.zero, new Vector2(10, 10), Color.white);
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
            warriorText = Label("Warrior State", left.rectTransform, new Vector2(0, -112),
                new Vector2(300, 82), "", 18, Ink);
            var right = Panel("Progress Panel", contentRect, new Vector2(540, 10),
                new Vector2(320, 370), panel);
            Label("Progress Title", right.rectTransform, new Vector2(0, 135),
                new Vector2(290, 40), "ДОШКА", 27, Ink, FontStyle.Bold);
            movesText = Label("Moves", right.rectTransform, new Vector2(0, 60),
                new Vector2(290, 45), "", 27, Gold, FontStyle.Bold);
            correctText = Label("Correct Tiles", right.rectTransform, Vector2.zero,
                new Vector2(290, 65), "", 23, Ink);
            routeText = Label("Route", right.rectTransform, new Vector2(0, -65),
                new Vector2(290, 52), "", 20, Ink);
            Label("Goal", right.rectTransform, new Vector2(0, -132), new Vector2(290, 58),
                "Склади 1–15 рядок за рядком.\nДороги рухаються без обертання.", 17, Muted);
        }

        private void StartRound(int steps, bool carryTest = false)
        {
            if (busy) return;
            warriorMotion = WarriorMotion.None;
            warriorReturning = false;
            destinationEntered = false;
            warriorCooldown = 0.25f;
            warrior.Reset();
            if (carryTest)
            {
                board.ResetSolved();
                board.TryMoveTile(15);
                board.ResetMoveCount();
            }
            else board.Shuffle(random, Mathf.Max(1, steps));
            finalTile.SetActive(false);
            for (int tile = 1; tile < SlidingBoard.CellCount; tile++)
                tiles[tile].anchoredPosition = CellPosition(board.GetIndexOf(tile));
            statusText.text = carryTest
                ? "Дочекайся воїна на плитці 15, потім пересунь її разом із ним."
                : steps == 1
                ? "Один правильний хід — і з’явиться завершальна плитка."
                : "Золоті плитки можна пересунути. Зелені вже на своїх місцях.";
            BindWarriorMarker();
            RefreshInterface();
        }

        private void RequestMove(int tile)
        {
            if (busy || board.IsSolved || !board.CanMoveTile(tile)) return;
            CancelWarriorMotion();
            Vector2 destination = CellPosition(board.EmptyIndex);
            if (!board.TryMoveTile(tile)) return;
            warrior.NotifyBoardChanged();
            busy = true;
            foreach (RoadLink link in roadLinks) link.Image.gameObject.SetActive(false);
            RefreshInterface(false);
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
                statusText.text = "Дошку складено! Завершальна плитка відкрила маршрут до замку.";
            }
            RefreshInterface();
        }

        private void RefreshInterface(bool updateRoads = true)
        {
            movesText.text = "Ходи: " + board.MoveCount;
            correctText.text = "На своїх місцях\n" + board.CorrectTileCount + " / 15";
            emptyMarker.anchoredPosition = CellPosition(board.EmptyIndex);
            emptyMarker.gameObject.SetActive(!board.IsSolved);
            shuffleButton.interactable = practiceButton.interactable = !busy;
            carryTestButton.interactable = !busy;
            for (int tile = 1; tile < SlidingBoard.CellCount; tile++)
            {
                bool movable = !busy && !board.IsSolved && board.CanMoveTile(tile);
                buttons[tile].interactable = movable;
                images[tile].color = movable ? Gold : board.GetIndexOf(tile) == tile - 1
                    ? new Color(0.45f, 0.72f, 0.61f) : new Color(0.70f, 0.76f, 0.78f);
            }
            if (updateRoads) RefreshRoads();
            RefreshWarriorInterface();
        }

        private Vector2 WarriorQueuePosition => CellPosition(RoadLayout.EntryCell) + new Vector2(144, 0);

        private RectTransform TileTransform(int tile) => tile == RoadLayout.FinalTile
            ? finalTile.GetComponent<RectTransform>() : tiles[tile];

        private int TileCell(int tile) => tile == RoadLayout.FinalTile
            ? board.EmptyIndex : board.GetIndexOf(tile);

        private void BindWarriorMarker()
        {
            warriorMarker.gameObject.SetActive(!warrior.Completed);
            warriorMarker.SetParent(warrior.CurrentTile == 0 ? boardRect
                : TileTransform(warrior.CurrentTile), false);
            warriorMarker.anchoredPosition = warrior.CurrentTile == 0 ? WarriorQueuePosition : Vector2.zero;
            warriorMarker.SetAsLastSibling();
        }

        private void CancelWarriorMotion(bool synchronizeOwnership = true)
        {
            if (synchronizeOwnership) TryTransferWarriorOwnership();
            Vector3 worldPosition = warriorMarker.position;
            warriorMotion = WarriorMotion.None;
            warriorCooldown = 0.2f;
            warriorMarker.SetParent(warrior.CurrentTile == 0 ? boardRect
                : TileTransform(warrior.CurrentTile), false);
            warriorMarker.position = worldPosition;
            warriorMarker.SetAsLastSibling();
            warriorReturnStart = warriorMarker.anchoredPosition;
            warriorReturnTarget = warrior.CurrentTile == 0 ? WarriorQueuePosition : Vector2.zero;
            warriorReturnElapsed = 0f;
            warriorReturning = !warrior.Completed &&
                (warriorReturnTarget - warriorReturnStart).sqrMagnitude > 0.01f;
        }

        private void AdvanceWarriorReturn()
        {
            warriorReturnElapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(warriorReturnElapsed / Mathf.Max(0.05f, warriorReturnDuration));
            warriorMarker.anchoredPosition = Vector2.Lerp(warriorReturnStart,
                warriorReturnTarget, t * t * (3f - 2f * t));
            if (t < 1f) return;
            warriorMarker.anchoredPosition = warriorReturnTarget;
            warriorReturning = false;
            warriorCooldown = 0.2f;
        }

        private void QueueNextWarrior()
        {
            if (busy || !warrior.QueueNext()) return;
            warriorCooldown = 0.2f;
            BindWarriorMarker();
            RefreshWarriorInterface();
        }

        private void PlanWarriorMotion()
        {
            if (warrior.Completed) return;
            bool finalPresent = finalTile.activeSelf;
            if (warrior.CurrentTile == 0)
            {
                if (!warrior.CanEnter(board, finalPresent)) return;
                int tile = RoadNetwork.GetTileAt(board, RoadLayout.EntryCell, finalPresent);
                BeginWarriorMotion(WarriorMotion.Enter, tile, WarriorQueuePosition,
                    CellPosition(RoadLayout.EntryCell));
            }
            else if (warrior.CanDeliver(board, finalPresent))
                BeginWarriorMotion(WarriorMotion.Exit, 0, CellPosition(RoadLayout.CastleCell),
                    CellPosition(RoadLayout.CastleCell) + new Vector2(-144, 0));
            else
            {
                int nextTile = warrior.GetNextTile(board, finalPresent);
                if (nextTile == 0) return;
                BeginWarriorMotion(WarriorMotion.Move, nextTile,
                    CellPosition(TileCell(warrior.CurrentTile)), CellPosition(TileCell(nextTile)));
            }
        }

        private void BeginWarriorMotion(WarriorMotion motion, int destination, Vector2 start, Vector2 end)
        {
            warriorMotion = motion;
            warriorDestination = destination;
            warriorStart = start;
            warriorEnd = end;
            warriorElapsed = 0f;
            destinationEntered = false;
            warriorMarker.SetParent(boardRect, false);
            warriorMarker.SetAsLastSibling();
            warriorMarker.anchoredPosition = start;
        }

        private void AdvanceWarriorMotion()
        {
            warriorElapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(warriorElapsed / Mathf.Max(0.05f, warriorStepDuration));
            warriorMarker.anchoredPosition = Vector2.Lerp(warriorStart, warriorEnd, progress);
            if (!TryTransferWarriorOwnership())
            {
                CancelWarriorMotion(false);
                return;
            }
            if (progress < 1f) return;
            bool finalPresent = finalTile.activeSelf;
            if (warriorMotion == WarriorMotion.Exit) warrior.TryDeliver(board, finalPresent);
            warriorMotion = WarriorMotion.None;
            warriorCooldown = 0.2f;
            BindWarriorMarker();
        }

        private bool TryTransferWarriorOwnership()
        {
            if (destinationEntered || (warriorMotion != WarriorMotion.Enter
                && warriorMotion != WarriorMotion.Move)) return true;
            bool horizontal = Mathf.Abs(warriorEnd.x - warriorStart.x)
                > Mathf.Abs(warriorEnd.y - warriorStart.y);
            Vector2 size = TileTransform(warriorDestination).rect.size;
            Vector2 markerSize = warriorMarker.rect.size;
            Vector2 position = boardRect.InverseTransformPoint(warriorMarker.position);
            if (!WarriorTraversal.HasEnteredDestination(Vector2.Distance(warriorStart, position),
                Vector2.Distance(warriorStart, warriorEnd), horizontal ? size.x : size.y,
                horizontal ? markerSize.x : markerSize.y)) return true;
            destinationEntered = warriorMotion == WarriorMotion.Enter
                ? warrior.TryEnter(board, finalTile.activeSelf)
                : warrior.TryMoveTo(board, finalTile.activeSelf, warriorDestination);
            return destinationEntered;
        }

        private void RefreshWarriorInterface()
        {
            sendWarriorButton.interactable = !busy && warrior.Completed;
            string state = warrior.Completed ? "Воїн дістався замку"
                : warriorReturning ? "Повернення до центру"
                : busy ? "Пересування дошки"
                : warriorMotion != WarriorMotion.None ? "Воїн рухається"
                : warrior.CurrentTile == 0 ? "Очікує входу справа" : "Очікує продовження шляху";
            warriorText.text = "Доставлено: " + warrior.DeliveredCount + "\n"
                + (warrior.CurrentTile == 0 ? "На вході: " + (warrior.Completed ? 0 : 1)
                    : "Воїн на плитці: " + warrior.CurrentTile) + "\n" + state;
        }

        private void BuildRoad(int tile, RectTransform parent)
        {
            roads[tile] = new List<Image>();
            Color dark = new Color(0.15f, 0.24f, 0.28f);
            roads[tile].Add(Panel("Road Centre", parent, Vector2.zero, new Vector2(16, 16), dark));
            RoadPorts ports = RoadLayout.GetPorts(tile);
            foreach (RoadPorts direction in new[] { RoadPorts.North, RoadPorts.East,
                RoadPorts.South, RoadPorts.West })
            {
                if ((ports & direction) == 0) continue;
                bool vertical = direction == RoadPorts.North || direction == RoadPorts.South;
                Vector2 position = direction == RoadPorts.North ? new Vector2(0, 33)
                    : direction == RoadPorts.South ? new Vector2(0, -33)
                    : direction == RoadPorts.East ? new Vector2(33, 0) : new Vector2(-33, 0);
                roads[tile].Add(Panel("Road " + direction, parent, position,
                    vertical ? new Vector2(16, 66) : new Vector2(66, 16), dark));
            }
        }

        private void BuildRoadLinks()
        {
            for (int cell = 0; cell < SlidingBoard.CellCount; cell++)
                foreach (RoadPorts direction in new[] { RoadPorts.East, RoadPorts.South })
                {
                    if (!RoadNetwork.TryGetNeighbor(cell, direction, out int next)) continue;
                    Vector2 midpoint = (CellPosition(cell) + CellPosition(next)) * 0.5f;
                    var image = Panel("Connection " + cell + " to " + next, boardRect,
                        midpoint, direction == RoadPorts.East ? new Vector2(12, 16)
                            : new Vector2(16, 12), Color.white);
                    image.gameObject.SetActive(false);
                    roadLinks.Add(new RoadLink { Cell = cell, Next = next,
                        Direction = direction, Image = image });
                }
        }

        private RoadPorts PortsAtCell(int cell)
        {
            int tile = RoadNetwork.GetTileAt(board, cell, finalTile.activeSelf);
            return tile == 0 ? RoadPorts.None : RoadLayout.GetPorts(tile);
        }

        private void RefreshRoads()
        {
            RoadNetworkResult route = RoadNetwork.Analyze(board, finalTile.activeSelf);
            Color connected = new Color(0.04f, 0.48f, 0.51f);
            Color disconnected = new Color(0.15f, 0.24f, 0.28f);
            for (int tile = 1; tile <= RoadLayout.FinalTile; tile++)
            {
                int cell = tile == RoadLayout.FinalTile ? board.EmptyIndex : board.GetIndexOf(tile);
                Color color = route.IsReachable(cell) ? connected : disconnected;
                foreach (Image road in roads[tile]) road.color = color;
            }
            foreach (RoadLink link in roadLinks)
            {
                bool joined = RoadNetwork.AreConnected(PortsAtCell(link.Cell),
                    PortsAtCell(link.Next), link.Direction);
                link.Image.gameObject.SetActive(joined);
                link.Image.color = route.IsReachable(link.Cell) ? connected : disconnected;
            }
            routeText.text = "Доступні плитки: " + route.ReachableTileCount + " / 16\n"
                + (route.HasCastleRoute ? "Шлях до замку відкрито" : "Шлях до замку розірвано");
            routeText.color = route.HasCastleRoute ? new Color(0.35f, 0.87f, 0.77f) : Ink;
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
