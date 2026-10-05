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
        [SerializeField, Min(0.01f)] private float slideDuration = 0.16f;
        [SerializeField, Min(0.05f)] private float warriorStepDuration = 0.6f;
        [SerializeField, Min(0.05f)] private float warriorReturnDuration = 0.3f;
        [SerializeField] private LevelSettings[] levels = LevelSettings.Defaults();
        [SerializeField] private bool unlockAllLevelsForDevelopment;
        private readonly List<LevelDefinition> campaign = new List<LevelDefinition>();
        private int levelIndex;
        private CampaignProgress progress;
        private bool practiceRound;
        private const string LegacyProgressKey = "Pyatnashki.Campaign.Unlocked.v1";
        private const string StarsVersionKey = "Pyatnashki.Campaign.Stars.v2";
        private static string StarsKey(int index) => StarsVersionKey + ".Level." + index;
        private readonly List<Button> levelButtons = new List<Button>();
        private readonly List<string> levelCaptions = new List<string>();
        private GameObject levelMenu;
        private Text levelTitle, entryCaption, entryArrow, castleArrow;
        private Button nextLevelButton, levelsButton;
        private LevelDefinition CurrentLevel => campaign[levelIndex];
        private bool Defense => !diagnosticRound && round.Mode == LevelMode.Defense;
        private bool MenuOpen => levelMenu != null && levelMenu.activeSelf;
        private int SourceCell => Defense ? RoadLayout.CastleCell : RoadLayout.EntryCell;
        private int DestinationCell => Defense ? RoadLayout.EntryCell : RoadLayout.CastleCell;
        private Vector2 SourceOutside => Defense ? new Vector2(0, 144) : new Vector2(0, -144);
        private Vector2 DestinationOutside => Defense ? new Vector2(0, -144) : new Vector2(0, 144);
        private readonly SiegeRound round = new SiegeRound();
        private bool diagnosticRound;
        private bool capacityStressTest;
        private GameObject resultOverlay;
        private Text resultTitle, resultDetails, resultStars, timeText, goalText;
        private bool RoundEnded => !diagnosticRound &&
            (round.State == SiegeRoundState.Won || round.State == SiegeRoundState.Lost);
        private static readonly Color Ink = new Color(0.91f, 0.94f, 0.96f);
        private static readonly Color Muted = new Color(0.60f, 0.68f, 0.73f);
        private static readonly Color Gold = new Color(0.97f, 0.73f, 0.31f);
        private readonly SlidingBoard board = new SlidingBoard();
        private readonly TileOccupancy occupancy = new TileOccupancy();
        private readonly List<WarriorView> warriors = new List<WarriorView>();
        [SerializeField, Range(1, 16)] private int waveSize = 12;
        [SerializeField, Min(0.05f)] private float spawnInterval = 0.7f;
        private int deliveredTotal, waveNumber;
        private float entryClock;
        private float waveSpawnInterval;
        private readonly Text[] capacityTexts = new Text[17];
        private readonly Text[] reservationTexts = new Text[17];
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
        private RectTransform canvasRect, safeRect, contentRect, boardRect, emptyMarker;
        private GameObject finalTile;
        private Font font;
        private Text movesText, correctText, routeText, statusText, warriorText;
        private Button shuffleButton, practiceButton, sendWarriorButton, carryTestButton, capacityTestButton;
        private bool busy;
        private int inputVersion;
        [SerializeField, Range(0.05f, 0.5f)] private float swipeThreshold = 0.15f;
        private enum WarriorMotion { None, Enter, Move, Exit }
        private sealed class WarriorView
        {
            public int Id;
            public WarriorSimulation Model;
            public RectTransform Marker;
            public WarriorMotion Motion;
            public Vector2 Start, End, ReturnStart, ReturnTarget, DestinationCentre;
            public float Elapsed, Cooldown, ReturnElapsed;
            public int Destination;
            public bool Entered, Returning;
        }

        private void Awake()
        {
            if (Application.isMobilePlatform) Screen.orientation = ScreenOrientation.Portrait;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (EventSystem.current == null)
            {
                var events = new GameObject("Prototype EventSystem", typeof(EventSystem),
                    typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform, false);
            }
            try
            {
                if (levels == null || levels.Length == 0) levels = LevelSettings.Defaults();
                foreach (LevelSettings settings in levels)
                {
                    if (settings == null) throw new System.ArgumentException("Level settings cannot be null.");
                    campaign.Add(settings.ToDefinition());
                }
            }
            catch (System.ArgumentException error)
            {
                Debug.LogError("Invalid campaign settings: " + error.Message, this);
                enabled = false;
                return;
            }
            LoadProgress();
            BuildInterface();
            StartLevel(0);
            ShowLevels();
        }

        private void Update()
        {
            if (RoundEnded || MenuOpen) return;
            if (!diagnosticRound)
            {
                round.Advance(Time.unscaledDeltaTime);
                if (RoundEnded) { FinishRound(); return; }
            }
            if (!busy) entryClock = Mathf.Max(0f, entryClock - Time.unscaledDeltaTime);
            foreach (WarriorView w in warriors)
            {
                if (w.Returning) AdvanceWarriorReturn(w);
                else if (!busy)
                {
                    if (w.Motion != WarriorMotion.None) AdvanceWarriorMotion(w);
                    else if (w.Cooldown > 0f) w.Cooldown -= Time.unscaledDeltaTime;
                    else PlanWarriorMotion(w);
                }
                if (RoundEnded) { FinishRound(); break; }
            }
            RefreshWarriorInterface();
        }

        private void LateUpdate() => FitSafeArea();

        private void StartLevel(int index, bool practice = false)
        {
            if (busy || index < 0 || index >= campaign.Count
                || (!unlockAllLevelsForDevelopment && !progress.IsUnlocked(index))) return;
            levelIndex = index;
            practiceRound = practice;
            levelMenu.SetActive(false);
            StartRound(practice ? 1 : CurrentLevel.ScrambleMoves);
        }

        private void ShowLevels()
        {
            if (busy) return;
            inputVersion++;
            for (int i = 0; i < levelButtons.Count; i++)
            {
                bool available = unlockAllLevelsForDevelopment || progress.IsUnlocked(i);
                levelButtons[i].interactable = available;
                Text caption = levelButtons[i].GetComponentInChildren<Text>();
                caption.text = (available ? "" : "ЗАБЛОКОВАНО · потрібні 3 зірки\n")
                    + levelCaptions[i] + "\nНайкраще: " + progress.GetBest(i) + " / 5";
                caption.fontSize = 19;
                caption.color = available ? Ink : Muted;
                levelButtons[i].targetGraphic.color = available
                    ? new Color(0.22f, 0.32f, 0.39f) : new Color(0.10f, 0.15f, 0.19f);
            }
            levelMenu.SetActive(true);
            levelMenu.transform.SetAsLastSibling();
        }

        private void CloseLevels()
        {
            inputVersion++;
            levelMenu.SetActive(false);
        }

        private void LoadProgress()
        {
            var ratings = new int[campaign.Count];
            for (int i = 0; i < ratings.Length; i++)
                ratings[i] = Mathf.Clamp(PlayerPrefs.GetInt(StarsKey(i), 0), 0, 5);
            progress = new CampaignProgress(ratings);
            if (!PlayerPrefs.HasKey(StarsVersionKey))
            {
                int oldUnlocked = Mathf.Clamp(PlayerPrefs.GetInt(LegacyProgressKey, 1), 1, campaign.Count);
                progress.MigrateLegacyUnlocks(oldUnlocked);
                SaveProgress();
            }
        }

        private void SaveProgress()
        {
            for (int i = 0; i < progress.Count; i++) PlayerPrefs.SetInt(StarsKey(i), progress.GetBest(i));
            PlayerPrefs.SetInt(StarsVersionKey, 1);
            PlayerPrefs.Save();
        }

        [ContextMenu("Reset campaign progress")]
        private void ResetCampaignProgress()
        {
            // Clear current campaign ratings and the legacy migration state.
            for (int i = 0; i < Mathf.Max(campaign.Count, levels == null ? 0 : levels.Length); i++)
                PlayerPrefs.DeleteKey(StarsKey(i));
            PlayerPrefs.DeleteKey(LegacyProgressKey);
            PlayerPrefs.DeleteKey(StarsVersionKey);
            PlayerPrefs.Save();
            if (campaign.Count > 0)
            {
                progress = new CampaignProgress(new int[campaign.Count]);
                SaveProgress();
                if (!busy) { StartLevel(0); ShowLevels(); }
            }
        }

        private void BuildLevelMenu()
        {
            var overlay = Panel("Level Selection", contentRect, Vector2.zero, new Vector2(700, 1400),
                new Color(0.025f, 0.045f, 0.065f, 0.98f));
            overlay.raycastTarget = true;
            levelMenu = overlay.gameObject;
            Label("Heading", overlay.rectTransform, new Vector2(0, 580), new Vector2(640, 60),
                "ОБЕРИ РІВЕНЬ", 32, Gold, FontStyle.Bold);
            // Scrollable viewport supports campaigns longer than the initial six levels.
            var viewport = Panel("Level Viewport", overlay.rectTransform, new Vector2(0, 105),
                new Vector2(650, 820), new Color(0.04f, 0.07f, 0.10f));
            viewport.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            var list = MakeRect("Level List", viewport.rectTransform, Vector2.zero,
                new Vector2(650, Mathf.Max(820, campaign.Count * 130)));
            list.anchorMin = list.anchorMax = new Vector2(0.5f, 1);
            list.pivot = new Vector2(0.5f, 1);
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = list;
            scroll.viewport = viewport.rectTransform;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            for (int i = 0; i < campaign.Count; i++)
            {
                int index = i;
                LevelDefinition level = campaign[i];
                string details = level.Mode == LevelMode.Capture
                    ? "Захоплення · ціль " + level.CaptureTarget + " / запас " + level.Supply
                    : "Захист · запас " + level.Supply + " / ворог до " + level.EnemyTotal;
                string caption = (i + 1) + ". " + level.Name + "\n" + details
                    + " · " + level.Duration.ToString("0") + " с";
                levelCaptions.Add(caption);
                levelButtons.Add(MakeButton("Level " + (i + 1), list, new Vector2(0, -65 - i * 130),
                    new Vector2(620, 110), caption, () => StartLevel(index)));
                var row = levelButtons[i].GetComponent<RectTransform>();
                row.anchorMin = row.anchorMax = new Vector2(0.5f, 1);
            }
            Label("Campaign Hint", overlay.rectTransform, new Vector2(0, -400), new Vector2(620, 130),
                "Для наступного рівня отримай від 3 зірок.\nЗберігається найкращий результат із 5.\nНавчання не змінює прогрес. Меню — пауза.", 20, Muted);
            MakeButton("Back", overlay.rectTransform, new Vector2(0, -540), new Vector2(320, 60),
                "Повернутися", CloseLevels);
            levelMenu.SetActive(false);
        }

        private void BuildResultOverlay()
        {
            var overlay = Panel("Round Result", contentRect, Vector2.zero, new Vector2(700, 1400),
                new Color(0.025f, 0.045f, 0.065f, 0.96f));
            overlay.raycastTarget = true;
            resultOverlay = overlay.gameObject;
            resultTitle = Label("Result Title", overlay.rectTransform, new Vector2(0, 245),
                new Vector2(640, 70), "", 34, Gold, FontStyle.Bold);
            resultStars = Label("Result Stars", overlay.rectTransform, new Vector2(0, 145),
                new Vector2(620, 75), "", 24, Gold, FontStyle.Bold);
            resultDetails = Label("Result Details", overlay.rectTransform, new Vector2(0, 5),
                new Vector2(620, 180), "", 24, Ink);
            MakeButton("Retry", overlay.rectTransform, new Vector2(0, -155),
                new Vector2(360, 64), "Повторити рівень", () => StartLevel(levelIndex, practiceRound));
            nextLevelButton = MakeButton("Next Level", overlay.rectTransform, new Vector2(0, -235),
                new Vector2(360, 64), "Наступний рівень", () => StartLevel(levelIndex + 1));
            MakeButton("Result Levels", overlay.rectTransform, new Vector2(0, -315),
                new Vector2(360, 64), "Вибір рівня", ShowLevels);
            resultOverlay.SetActive(false);
        }

        private void FinishRound()
        {
            if (!RoundEnded || resultOverlay.activeSelf) return;
            StopAllCoroutines();
            busy = false;
            foreach (WarriorView w in warriors)
            {
                w.Model.CancelReservation();
                w.Motion = WarriorMotion.None;
                w.Returning = false;
            }
            RefreshInterface();
            bool won = round.State == SiegeRoundState.Won;
            int stars = LevelRating.Calculate(CurrentLevel, round);
            if (!practiceRound && progress.Record(levelIndex, stars)) SaveProgress();
            resultStars.text = new string('★', stars) + new string('☆', 5 - stars)
                + "\n" + stars + " / 5 · Найкраще: " + progress.GetBest(levelIndex) + " / 5";
            nextLevelButton.gameObject.SetActive(!practiceRound && levelIndex + 1 < campaign.Count
                && progress.IsUnlocked(levelIndex + 1));
            resultTitle.text = Defense ? (won ? "ЗЕМЛЮ ЗАХИЩЕНО!" : "ЗАХИСТ НЕ ВДАВСЯ")
                : won ? "ЗАМОК ЗАХОПЛЕНО!" : "ЧАС ВИЧЕРПАНО";
            resultTitle.color = won ? Gold : new Color(1f, 0.35f, 0.30f);
            resultDetails.text = (Defense ? "Захисників на рубежі: " + round.Delivered
                + "\nВорогів: " + round.EnemyCount + (round.Delivered == round.EnemyCount ? " · нічия" : "")
                : "Доставлено воїнів: " + round.Delivered + " / " + round.Target)
                + "\nХоди: " + board.MoveCount + "\nВитрачено часу: " + round.Elapsed.ToString("0.0") + " с"
                + (practiceRound ? "\nНавчання: прогрес не змінено" : "");
            resultOverlay.SetActive(true);
            resultOverlay.transform.SetAsLastSibling();
        }

        private void BuildInterface()
        {
            var obj = new GameObject("Prototype Canvas", typeof(RectTransform),
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            obj.transform.SetParent(transform, false);
            obj.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = obj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720, 1440);
            scaler.matchWidthOrHeight = 0.5f;
            canvasRect = obj.GetComponent<RectTransform>();
            var background = Panel("Background", canvasRect, Vector2.zero, Vector2.zero,
                new Color(0.055f, 0.09f, 0.13f));
            background.rectTransform.anchorMin = Vector2.zero;
            background.rectTransform.anchorMax = Vector2.one;
            background.rectTransform.offsetMin = background.rectTransform.offsetMax = Vector2.zero;
            safeRect = MakeRect("Safe Area", canvasRect, Vector2.zero, Vector2.zero);
            contentRect = MakeRect("Content", safeRect, Vector2.zero, new Vector2(700, 1400));
            levelTitle = Label("Title", contentRect, new Vector2(-75, 650), new Vector2(470, 50),
                "", 24, Ink, FontStyle.Bold);
            levelsButton = MakeButton("Levels", contentRect, new Vector2(270, 650),
                new Vector2(140, 50), "Рівні", ShowLevels);
            Label("Instructions", contentRect, new Vector2(0, 590), new Vector2(660, 55),
                "Свайпай плитку до вільної клітинки або торкнися її", 20, Muted);

            boardRect = MakeRect("Board", contentRect, new Vector2(0, -70), new Vector2(592, 592));
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
                    new Vector2(132, 132), "", () => { });
                button.gameObject.AddComponent<TileGestureInput>().Configure(
                    () => board.GetIndexOf(number), () => board.EmptyIndex, () => inputVersion,
                    () => !busy && !MenuOpen && !RoundEnded && !board.IsSolved && board.CanMoveTile(number),
                    () => RequestMove(number), swipeThreshold);
                buttons[tile] = button;
                tiles[tile] = button.GetComponent<RectTransform>();
                images[tile] = button.GetComponent<Image>();
                BuildRoad(tile, tiles[tile]);
                Label("Number", tiles[tile], new Vector2(-43, 43), new Vector2(42, 34),
                    tile.ToString(), 28, new Color(0.08f, 0.12f, 0.15f), FontStyle.Bold);
                capacityTexts[tile] = Label("Capacity", tiles[tile], new Vector2(-40, -46), new Vector2(56, 24),
                    "0/" + tile, 15, new Color(0.12f, 0.20f, 0.23f));
                reservationTexts[tile] = Label("Reserved", tiles[tile], new Vector2(42, -46),
                    new Vector2(40, 24), "", 14, new Color(0.12f, 0.20f, 0.23f));
            }
            var final = Panel("Final Tile", boardRect, CellPosition(15), new Vector2(132, 132), Gold);
            finalTile = final.gameObject;
            BuildRoad(RoadLayout.FinalTile, final.rectTransform);
            Label("Final Label", final.rectTransform, new Vector2(0, 38), new Vector2(125, 30),
                "ФІНІШ", 18, new Color(0.10f, 0.14f, 0.16f), FontStyle.Bold);
            capacityTexts[16] = Label("Final Capacity", final.rectTransform, new Vector2(-40, -46),
                new Vector2(56, 24), "0/16", 15, new Color(0.12f, 0.20f, 0.23f));
            reservationTexts[16] = Label("Final Reserved", final.rectTransform, new Vector2(42, -46),
                new Vector2(40, 24), "", 14, new Color(0.12f, 0.20f, 0.23f));
            finalTile.SetActive(false);
            entryArrow = Label("Entry Arrow", contentRect, new Vector2(216, -400), new Vector2(44, 44),
                "↑", 36, Gold, FontStyle.Bold);
            entryCaption = Label("Entry Caption", contentRect, new Vector2(210, -485), new Vector2(200, 50),
                "ЧЕРГА ВОЇНІВ\n↑", 18, Muted);
            BuildSidePanels();
            shuffleButton = MakeButton("Shuffle", contentRect, new Vector2(-170, -555),
                new Vector2(320, 60), "Повторити рівень", () => StartLevel(levelIndex));
            practiceButton = MakeButton("Practice", contentRect, new Vector2(170, -555),
                new Vector2(320, 60), "Навчальний режим", () => StartLevel(levelIndex, true));
            sendWarriorButton = MakeButton("Send Warrior", contentRect, new Vector2(-160, -485),
                new Vector2(320, 48), "Наступна хвиля", QueueNextWave);
            carryTestButton = MakeButton("Carry Test", contentRect, new Vector2(-170, -625),
                new Vector2(320, 48), "Тест перенесення", () => StartRound(1, true));
            capacityTestButton = MakeButton("Capacity Test", contentRect, new Vector2(170, -625),
                new Vector2(320, 48), "Тест місткості", () => StartRound(1, false, true));
            statusText = Label("Status", contentRect, new Vector2(0, -680),
                new Vector2(660, 40), "", 17, Ink);
            BuildResultOverlay();
            BuildLevelMenu();
            FitSafeArea();
        }

        private void BuildSidePanels()
        {
            Color panel = new Color(0.09f, 0.14f, 0.18f);
            var army = Panel("Army Panel", contentRect, new Vector2(-170, 480), new Vector2(320, 160), panel);
            Label("Army Title", army.rectTransform, new Vector2(0, 60), new Vector2(290, 28),
                "ВІЙСЬКО", 22, Ink, FontStyle.Bold);
            warriorText = Label("Warrior State", army.rectTransform, Vector2.zero, new Vector2(300, 94), "", 17, Ink);
            var progress = Panel("Progress Panel", contentRect, new Vector2(170, 480), new Vector2(320, 160), panel);
            Label("Progress Title", progress.rectTransform, new Vector2(0, 60), new Vector2(290, 28),
                "РАУНД", 22, Ink, FontStyle.Bold);
            timeText = Label("Time", progress.rectTransform, new Vector2(0, 22), new Vector2(290, 30), "", 23, Gold);
            movesText = Label("Moves", progress.rectTransform, new Vector2(0, -12), new Vector2(290, 28), "", 20, Gold);
            correctText = Label("Correct Tiles", progress.rectTransform, new Vector2(0, -45), new Vector2(290, 40), "", 17, Ink);
            Label("Castle Title", contentRect, new Vector2(-72, 380), new Vector2(200, 30), "ЗАМОК", 22, Ink, FontStyle.Bold);
            Color stone = new Color(0.35f, 0.45f, 0.51f);
            Panel("Castle Body", contentRect, new Vector2(-72, 300), new Vector2(100, 60), stone);
            Panel("Left Tower", contentRect, new Vector2(-132, 315), new Vector2(26, 80), stone);
            Panel("Right Tower", contentRect, new Vector2(-12, 315), new Vector2(26, 80), stone);
            Panel("Gate", contentRect, new Vector2(-72, 280), new Vector2(22, 28), panel);
            castleArrow = Label("Castle Arrow", contentRect, new Vector2(-72, 250), new Vector2(44, 44), "↑", 36, Gold);
            routeText = Label("Route", contentRect, new Vector2(190, 310), new Vector2(260, 70), "", 19, Ink);
            goalText = Label("Goal", contentRect, new Vector2(-115, -425), new Vector2(390, 72), "", 20, Muted);
        }

        private void StartRound(int steps, bool carryTest = false, bool capacityTest = false)
        {
            if (busy) return;
            inputVersion++;
            diagnosticRound = carryTest || capacityTest;
            capacityStressTest = capacityTest;
            round.Reset();
            if (!diagnosticRound) round.Start(CurrentLevel);
            else practiceRound = false;
            resultOverlay.SetActive(false);
            sendWarriorButton.gameObject.SetActive(diagnosticRound);
            levelTitle.text = diagnosticRound ? "ПЕРЕВІРКА МЕХАНІКИ"
                : (levelIndex + 1) + ". " + CurrentLevel.Name + (practiceRound ? " · навчання" : "");
            entryArrow.text = castleArrow.text = Defense ? "↓" : "↑";
            goalText.text = diagnosticRound ? "Перевірка механіки\nбез таймера та результату."
                : Defense ? "Збери більше захисників, ніж ворогів.\nБій — після завершення часу."
                : "Достав " + round.Target + " воїнів до замку\nза відведений час.";
            deliveredTotal = waveNumber = 0;
            waveSpawnInterval = capacityTest ? 0.1f : Mathf.Max(0.05f, spawnInterval);
            if (carryTest || capacityTest)
            {
                board.ResetSolved();
                board.TryMoveTile(12);
                board.ResetMoveCount();
            }
            else board.Shuffle(random, Mathf.Max(1, steps));
            finalTile.SetActive(false);
            for (int tile = 1; tile < SlidingBoard.CellCount; tile++)
                tiles[tile].anchoredPosition = CellPosition(board.GetIndexOf(tile));
            statusText.text = capacityTest
                ? "Плитка 12 вмістить 12 воїнів. Склади дошку — ліміти зникнуть."
                : carryTest
                ? "Дочекайся воїнів на плитці 12, потім пересунь її разом із ними."
                : steps == 1
                ? "Один правильний хід — і з’явиться завершальна плитка."
                : "Золоті плитки можна пересунути. Зелені вже на своїх місцях.";
            CreateWave();
            RefreshInterface();
        }

        private void RequestMove(int tile)
        {
            if (busy || MenuOpen || RoundEnded || board.IsSolved || !board.CanMoveTile(tile)) return;
            foreach (WarriorView w in warriors) CancelWarriorMotion(w);
            Vector2 destination = CellPosition(board.EmptyIndex);
            if (!board.TryMoveTile(tile)) return;
            inputVersion++;
            foreach (WarriorView w in warriors) w.Model.NotifyBoardChanged();
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
                statusText.text = "Дошку складено! Завершальна плитка з’єднала маршрут.";
            }
            RefreshInterface();
        }

        private void RefreshInterface(bool updateRoads = true)
        {
            occupancy.UpdateCapacityMode(board, finalTile.activeSelf);
            movesText.text = "Ходи: " + board.MoveCount;
            correctText.text = "На своїх місцях\n" + board.CorrectTileCount + " / 15";
            emptyMarker.anchoredPosition = CellPosition(board.EmptyIndex);
            emptyMarker.gameObject.SetActive(!board.IsSolved);
            shuffleButton.interactable = practiceButton.interactable = !busy && !RoundEnded;
            levelsButton.interactable = !busy;
            carryTestButton.interactable = capacityTestButton.interactable = !busy && !RoundEnded;
            for (int tile = 1; tile < SlidingBoard.CellCount; tile++)
            {
                bool movable = !busy && !RoundEnded && !board.IsSolved && board.CanMoveTile(tile);
                buttons[tile].interactable = movable;
                images[tile].color = movable ? Gold : board.GetIndexOf(tile) == tile - 1
                    ? new Color(0.45f, 0.72f, 0.61f) : new Color(0.70f, 0.76f, 0.78f);
            }
            if (updateRoads) RefreshRoads();
            RefreshWarriorInterface();
        }

        private Vector2 WarriorQueuePosition => CellPosition(SourceCell) + SourceOutside;

        private static Vector2 SlotOffset(WarriorView w) => new Vector2(
            (w.Id % 16 % 4 - 1.5f) * 18f, (w.Id % 16 / 4 - 1.5f) * 18f);

        private bool VisualSlotAvailable(WarriorView w)
        {
            foreach (WarriorView other in warriors)
                if (other != w && other.Id % 16 == w.Id % 16 && !other.Model.Completed
                    && (other.Model.CurrentTile != 0 || other.Motion == WarriorMotion.Enter)) return false;
            return true;
        }

        private Vector2 MarkerBoardPosition(WarriorView w) => boardRect.InverseTransformPoint(w.Marker.position);

        private bool IsQueueHead(WarriorView w)
        {
            foreach (WarriorView candidate in warriors)
                if (!candidate.Model.Completed && candidate.Model.CurrentTile == 0) return candidate == w;
            return false;
        }

        private bool EntryInProgress()
        {
            foreach (WarriorView w in warriors)
                if (w.Motion == WarriorMotion.Enter || (w.Returning && w.Model.CurrentTile == 0)) return true;
            return false;
        }

        private bool WaveCompleted()
        {
            if (warriors.Count == 0) return false;
            foreach (WarriorView w in warriors) if (!w.Model.Completed) return false;
            return true;
        }

        private void CreateWave()
        {
            foreach (WarriorView w in warriors)
            {
                w.Model.Reset();
                w.Marker.gameObject.SetActive(false);
                Destroy(w.Marker.gameObject);
            }
            warriors.Clear();
            occupancy.Clear();
            occupancy.UpdateCapacityMode(board, finalTile.activeSelf);
            entryClock = 0.25f;
            waveNumber++;
            int count = capacityStressTest ? 16
                : diagnosticRound ? Mathf.Clamp(waveSize, 1, 16) : round.Supply;
            for (int i = 0; i < count; i++)
            {
                var marker = Panel("Warrior " + (i + 1), boardRect, WarriorQueuePosition,
                    new Vector2(18, 18), Defense ? new Color(0.12f, 0.55f, 0.88f) : new Color(0.88f, 0.12f + i * 0.012f, 0.18f)).rectTransform;
                Label("Identity", marker, Vector2.zero, new Vector2(18, 18), (i + 1).ToString(), 11,
                    Color.white, FontStyle.Bold);
                var w = new WarriorView { Id = i, Model = new WarriorSimulation(occupancy, Defense),
                    Marker = marker, Cooldown = 0.25f };
                warriors.Add(w);
                BindWarriorMarker(w);
            }
        }

        private void QueueNextWave()
        {
            if (busy || !diagnosticRound || !WaveCompleted()) return;
            CreateWave();
            RefreshWarriorInterface();
        }

        private void RefreshWarriorInterface()
        {
            int queued = 0, onBoard = 0, moving = 0, waiting = 0;
            foreach (WarriorView w in warriors)
            {
                if (w.Model.Completed) continue;
                if (w.Model.CurrentTile == 0) queued++; else onBoard++;
                if (w.Motion != WarriorMotion.None || w.Returning) moving++;
                else if (w.Model.CurrentTile != 0 && w.Cooldown <= 0f) waiting++;
                if (w.Model.CurrentTile == 0 && w.Motion == WarriorMotion.None && !w.Returning)
                    w.Marker.gameObject.SetActive(IsQueueHead(w));
            }
            sendWarriorButton.interactable = diagnosticRound && !busy && WaveCompleted();
            warriorText.text = (Defense ? "На рубежі: " : "Доставлено: ")  + deliveredTotal
                + (diagnosticRound || Defense ? "" : "/" + round.Target) + "\nНа вході: " + queued
                + " • На полі: " + onBoard + "\nРухаються: " + moving + " • Чекають: " + waiting
                + (diagnosticRound ? "\nХвиля: " + waveNumber + " (" + warriors.Count + ")"
                    : "\nЗагальний запас: " + round.Supply);
            entryCaption.text = Defense ? "Захисники: " + round.Delivered + "\nВорог: " + round.EnemyCount + "/" + round.EnemyTotal
                : "ЧЕРГА ВОЇНІВ\n↑";
            entryCaption.color = Defense ? Gold : Muted;
            int seconds = Mathf.CeilToInt((float)round.Remaining);
            timeText.text = diagnosticRound ? "Без таймера"
                : "Час: " + (seconds / 60) + ":" + (seconds % 60).ToString("00");
            timeText.color = !diagnosticRound && round.Remaining <= 15
                ? new Color(1f, 0.35f, 0.30f) : Gold;
            for (int tile = 1; tile <= 16; tile++)
            {
                capacityTexts[tile].text = occupancy.GetCount(tile) + "/"
                    + (occupancy.UnlimitedCapacity ? "∞" : tile.ToString());
                int reserved = occupancy.GetReservedCount(tile);
                reservationTexts[tile].text = reserved == 0 ? "" : "+" + reserved;
            }
        }

        private RectTransform TileTransform(int tile) => tile == RoadLayout.FinalTile
            ? finalTile.GetComponent<RectTransform>() : tiles[tile];

        private int TileCell(int tile) => tile == RoadLayout.FinalTile
            ? board.EmptyIndex : board.GetIndexOf(tile);

        private void BindWarriorMarker(WarriorView w)
        {
            w.Marker.gameObject.SetActive(!w.Model.Completed);
            w.Marker.SetParent(w.Model.CurrentTile == 0 ? boardRect
                : TileTransform(w.Model.CurrentTile), false);
            w.Marker.anchoredPosition = w.Model.CurrentTile == 0 ? WarriorQueuePosition : SlotOffset(w);
            w.Marker.SetAsLastSibling();
        }

        private void CancelWarriorMotion(WarriorView w, bool synchronizeOwnership = true)
        {
            if (synchronizeOwnership) TryTransferWarriorOwnership(w);
            w.Model.CancelReservation();
            Vector3 worldPosition = w.Marker.position;
            w.Motion = WarriorMotion.None;
            w.Cooldown = 0.2f;
            w.Marker.SetParent(w.Model.CurrentTile == 0 ? boardRect
                : TileTransform(w.Model.CurrentTile), false);
            w.Marker.position = worldPosition;
            w.Marker.SetAsLastSibling();
            w.ReturnStart = w.Marker.anchoredPosition;
            w.ReturnTarget = w.Model.CurrentTile == 0 ? WarriorQueuePosition : SlotOffset(w);
            w.ReturnElapsed = 0f;
            w.Returning = !w.Model.Completed &&
                (w.ReturnTarget - w.ReturnStart).sqrMagnitude > 0.01f;
        }

        private void AdvanceWarriorReturn(WarriorView w)
        {
            w.ReturnElapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(w.ReturnElapsed / Mathf.Max(0.05f, warriorReturnDuration));
            w.Marker.anchoredPosition = Vector2.Lerp(w.ReturnStart,
                w.ReturnTarget, t * t * (3f - 2f * t));
            if (t < 1f) return;
            w.Marker.anchoredPosition = w.ReturnTarget;
            w.Returning = false;
            w.Cooldown = 0.2f;
        }

        private void PlanWarriorMotion(WarriorView w)
        {
            if (w.Model.Completed) return;
            bool finalPresent = finalTile.activeSelf;
            if (w.Model.CurrentTile == 0)
            {
                if (!IsQueueHead(w) || !VisualSlotAvailable(w) || entryClock > 0f || EntryInProgress()
                    || !w.Model.CanEnter(board, finalPresent)) return;
                int tile = RoadNetwork.GetTileAt(board, SourceCell, finalPresent);
                BeginWarriorMotion(w, WarriorMotion.Enter, tile, WarriorQueuePosition,
                    CellPosition(SourceCell) + SlotOffset(w));
            }
            else if (w.Model.CanDeliver(board, finalPresent))
                BeginWarriorMotion(w, WarriorMotion.Exit, 0, CellPosition(DestinationCell) + SlotOffset(w),
                    CellPosition(DestinationCell) + DestinationOutside);
            else
            {
                int nextTile = w.Model.GetNextTile(board, finalPresent);
                if (nextTile == 0) return;
                BeginWarriorMotion(w, WarriorMotion.Move, nextTile,
                    MarkerBoardPosition(w), CellPosition(TileCell(nextTile)) + SlotOffset(w));
            }
        }

        private void BeginWarriorMotion(WarriorView w, WarriorMotion motion, int destination, Vector2 start, Vector2 end)
        {
            if (motion != WarriorMotion.Exit && !w.Model.ReserveDestination(board, finalTile.activeSelf, destination)) return;
            if (motion == WarriorMotion.Enter) entryClock = waveSpawnInterval;
            w.DestinationCentre = destination == 0 ? end : CellPosition(TileCell(destination));
            w.Motion = motion;
            w.Destination = destination;
            w.Start = start;
            w.End = end;
            w.Elapsed = 0f;
            w.Entered = false;
            w.Marker.SetParent(boardRect, false);
            w.Marker.SetAsLastSibling();
            w.Marker.anchoredPosition = start;
        }

        private void AdvanceWarriorMotion(WarriorView w)
        {
            w.Elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(w.Elapsed / Mathf.Max(0.05f, warriorStepDuration));
            w.Marker.anchoredPosition = Vector2.Lerp(w.Start, w.End, progress);
            if (!TryTransferWarriorOwnership(w))
            {
                CancelWarriorMotion(w, false);
                return;
            }
            if (progress < 1f) return;
            bool finalPresent = finalTile.activeSelf;
            if (w.Motion == WarriorMotion.Exit && w.Model.TryDeliver(board, finalPresent))
            {
                deliveredTotal++;
                if (!diagnosticRound) round.RecordDelivery();
            }
            w.Motion = WarriorMotion.None;
            w.Cooldown = 0.2f;
            BindWarriorMarker(w);
        }

        private bool TryTransferWarriorOwnership(WarriorView w)
        {
            if (w.Entered || (w.Motion != WarriorMotion.Enter
                && w.Motion != WarriorMotion.Move)) return true;
            bool horizontal = Mathf.Abs(w.End.x - w.Start.x)
                > Mathf.Abs(w.End.y - w.Start.y);
            Vector2 size = TileTransform(w.Destination).rect.size;
            Vector2 markerSize = w.Marker.rect.size;
            Vector2 position = MarkerBoardPosition(w);
            if (!WarriorTraversal.HasEnteredDestination(horizontal ? (position.x - w.Start.x) * Mathf.Sign(w.DestinationCentre.x - w.Start.x)
                    : (position.y - w.Start.y) * Mathf.Sign(w.DestinationCentre.y - w.Start.y),
                horizontal ? Mathf.Abs(w.DestinationCentre.x - w.Start.x)
                    : Mathf.Abs(w.DestinationCentre.y - w.Start.y), horizontal ? size.x : size.y,
                horizontal ? markerSize.x : markerSize.y)) return true;
            w.Entered = w.Motion == WarriorMotion.Enter
                ? w.Model.TryEnter(board, finalTile.activeSelf)
                : w.Model.TryMoveTo(board, finalTile.activeSelf, w.Destination);
            return w.Entered;
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
            RoadNetworkResult route = RoadNetwork.Analyze(board, finalTile.activeSelf, Defense);
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
                + (route.HasCastleRoute ? (Defense ? "Шлях до рубежу відкрито" : "Шлях до замку відкрито")
                    : (Defense ? "Шлях до рубежу розірвано" : "Шлях до замку розірвано"));
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
            float scale = Mathf.Min(available.x / 700f, available.y / 1400f);
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
