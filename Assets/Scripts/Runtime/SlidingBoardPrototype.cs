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
    public sealed partial class SlidingBoardPrototype : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float slideDuration = 0.16f;
        [SerializeField, Min(0.05f)] private float warriorStepDuration = 0.6f;
        [SerializeField, Min(0.05f)] private float warriorReturnDuration = 0.3f;
        [SerializeField] private LevelSettings[] levels = LevelSettings.Defaults();
        [SerializeField] private bool unlockAllLevelsForDevelopment;
        private readonly List<LevelDefinition> campaign = new List<LevelDefinition>();
        private int levelIndex;
        private CampaignProgress progress;
        private KingdomEconomy economy;
        private KingdomView kingdomView;
        private RectTransform capitalCastle, campArt;
        private Text campCounter;
        private float economyClock, economySaveClock;
        private const string EconomyKey = "Pyatnashki.Kingdom.v1";
        private static long UtcSeconds() => System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        private float ArmyMovementMultiplier => diagnosticRound ? 1f : (float)economy.MovementSpeedMultiplier;
        private float ArmySpawnInterval => Mathf.Max(0.05f, waveSpawnInterval
            / (diagnosticRound ? 1f : (float)economy.SpawnSpeedMultiplier));
        private bool practiceRound;
        private const string LegacyProgressKey = "Pyatnashki.Campaign.Unlocked.v1";
        private const string StarsVersionKey = "Pyatnashki.Campaign.Stars.v2";
        private static string StarsKey(int index) => StarsVersionKey + ".Level." + index;
        private readonly List<Button> levelButtons = new List<Button>();
        private GameObject levelMenu;
        private Text levelTitle, entryCaption, entryArrow, castleArrow;
        private Button nextLevelButton, levelsButton;
        private LevelDefinition CurrentLevel => campaign[levelIndex];
        private bool Defense => !diagnosticRound && round.Mode == LevelMode.Defense;
        private bool MenuOpen => (homeMenu != null && homeMenu.activeSelf) || (levelMenu != null && levelMenu.activeSelf) || (kingdomView != null && kingdomView.IsOpen);
        private int SourceCell => Defense ? RoadLayout.CastleCell : RoadLayout.EntryCell;
        private int DestinationCell => Defense ? RoadLayout.EntryCell : RoadLayout.CastleCell;
        private Vector2 SourceOutside => Defense ? new Vector2(0, 160) : new Vector2(0, -160);
        private Vector2 DestinationOutside => Defense ? new Vector2(0, -160) : new Vector2(0, 160);
        private readonly SiegeRound round = new SiegeRound();
        private bool diagnosticRound;
        private bool capacityStressTest;
        private GameObject resultOverlay;
        private Text resultTitle, resultDetails, resultStars, timeText, goalText;
        private bool RoundEnded => !diagnosticRound &&
            (round.State == SiegeRoundState.Won || round.State == SiegeRoundState.Lost);
        private static readonly Color Ink = PrototypeArt.Parchment;
        private static readonly Color Muted = PrototypeArt.Muted;
        private static readonly Color Gold = PrototypeArt.Gold;
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
        private readonly Outline[] tileBorders = new Outline[SlidingBoard.CellCount];
        private readonly Text[] tileSigns = new Text[SlidingBoard.CellCount];
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
        private Text movesText, correctText, routeText, statusText, warriorText, armyBonusText;
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
            public float Elapsed, Cooldown, ReturnElapsed, Duration;
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
                CampaignMapRules.Validate(campaign);
            }
            catch (System.ArgumentException error)
            {
                Debug.LogError("Invalid campaign settings: " + error.Message, this);
                enabled = false;
                return;
            }
            deployment = new int[campaign.Count];
            LoadProgress();
            LoadEconomy();
            BuildInterface();
            StartLevel(0);
            ShowHome();
        }

        private void Update()
        {
            economyClock += Time.unscaledDeltaTime;
            economySaveClock += Time.unscaledDeltaTime;
            if (economyClock >= 1)
            {
                economyClock = 0; economy.Advance(UtcSeconds());
                if (homeMenu != null && homeMenu.activeSelf) RefreshHomeStats();
            }
            if (economySaveClock >= 60) { economySaveClock = 0; SaveEconomy(); }
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
                || (!unlockAllLevelsForDevelopment && !IsLevelAvailable(index))) return;
            if (!practice && economy.TrainedWarriors < campaign[index].MinimumSupply)
            {
                ShowLevels(); SelectMapLevel(index); return;
            }
            levelIndex = index;
            if (deployment[index] < campaign[index].MinimumSupply)
                deployment[index] = Mathf.Min(campaign[index].Supply, economy.TrainedWarriors);
            if (homeMenu != null) homeMenu.SetActive(false);
            practiceRound = practice;
            levelMenu.SetActive(false);
            kingdomView.Hide();
            StartRound(practice ? 1 : CurrentLevel.ScrambleMoves);
        }

        private void CloseLevels()
        {
            inputVersion++;
            levelMenu.SetActive(false);
        }

        private SettlementDefinition[] SettlementDefinitions()
        {
            var result = new List<SettlementDefinition>();
            foreach (LevelDefinition level in campaign)
            {
                int index = campaign.IndexOf(level);
                if (level.Mode != LevelMode.Capture) continue;
                int number = result.Count;
                SettlementKind kind = (SettlementKind)(number % 3);
                string[] names = { "Лісове село", "Торгове місто", "Прикордонна фортеця" };
                string name = names[number % 3] + (number >= 3 ? " " + (number + 1) : "");
                int defense = index + 1 < campaign.Count && campaign[index + 1].Mode == LevelMode.Defense ? index + 1 : -1;
                result.Add(new SettlementDefinition("settlement-level-" + index, name, kind, index, defense, 20 + number * 10));
            }
            return result.ToArray();
        }

        private void LoadEconomy()
        {
            KingdomSave saved = null;
            string json = PlayerPrefs.GetString(EconomyKey, "");
            try
            {
                if (json.Length > 0) saved = JsonUtility.FromJson<KingdomSave>(json);
                economy = new KingdomEconomy(SettlementDefinitions(), UtcSeconds(), saved);
            }
            catch (System.ArgumentException error)
            {
                PlayerPrefs.SetString(EconomyKey + ".InvalidBackup", json);
                Debug.LogWarning("Kingdom save could not be read; backup preserved. " + error.Message, this);
                economy = new KingdomEconomy(SettlementDefinitions(), UtcSeconds());
            }
            economy.Synchronize(progress, UtcSeconds());
            SaveEconomy();
        }

        private void SaveEconomy()
        {
            if (economy == null) return;
            economy.Advance(UtcSeconds());
            PlayerPrefs.SetString(EconomyKey, JsonUtility.ToJson(economy.Export()));
            PlayerPrefs.Save();
        }
        private void OnApplicationPause(bool paused) { if (paused) SaveEconomy(); }
        private void OnApplicationQuit() => SaveEconomy();

#if UNITY_EDITOR
        [ContextMenu("Economy debug/Add 5000 gold")]
        private void DebugAddGold()
        {
            if (!Application.isPlaying || economy == null)
            {
                Debug.Log("Start Play mode before testing the economy.", this);
                return;
            }
            economy.Advance(UtcSeconds());
            KingdomSave saved = economy.Export();
            saved.gold = checked(saved.gold + 5000);
            ApplyDebugEconomy(saved);
            Debug.Log("Added 5000 test gold. Treasury: " + economy.Gold, this);
        }

        [ContextMenu("Economy debug/Accrue 1 hour tribute")]
        private void DebugAccrueTribute()
        {
            if (!Application.isPlaying || economy == null)
            {
                Debug.Log("Start Play mode before testing the economy.", this);
                return;
            }
            economy.Advance(UtcSeconds());
            KingdomSave before = economy.Export();
            var simulated = new KingdomEconomy(SettlementDefinitions(), before.lastUtcSeconds, before);
            simulated.Advance(checked(before.lastUtcSeconds + 3600));
            KingdomSave after = simulated.Export();
            // Keep the real timestamp: test time must not suppress subsequent normal income.
            after.lastUtcSeconds = before.lastUtcSeconds;
            ApplyDebugEconomy(after);
            Debug.Log("Accrued one test hour of tribute, subject to ownership and storage caps. Collectable: "
                + economy.Collectable, this);
        }

        private void ApplyDebugEconomy(KingdomSave saved)
        {
            economy = new KingdomEconomy(SettlementDefinitions(), UtcSeconds(), saved);
            SaveEconomy();
            if (kingdomView != null) kingdomView.SetEconomy(economy);
            if (homeMenu != null) RefreshHomeStats();
        }
#endif

        private void ShowKingdom()
        {
            if (busy) return;
            inputVersion++;
            economy.Advance(UtcSeconds());
            kingdomView.Open();
        }

        private void BuildKingdomView()
        {
            var panel = Panel("Kingdom", contentRect, Vector2.zero, new Vector2(700, 1400),
                new Color(0.07f, 0.10f, 0.075f, 0.99f));
            panel.raycastTarget = true;
            kingdomView = panel.gameObject.AddComponent<KingdomView>();
            kingdomView.Configure(economy, font,
                () => { economy.Collect(UtcSeconds()); SaveEconomy(); },
                stat => { bool purchased = economy.TryUpgrade(stat, UtcSeconds());
                    if (purchased)
                    {
                        SaveEconomy(); RefreshCapitalCastle();
                        entryClock = Mathf.Min(entryClock, ArmySpawnInterval);
                    }
                    return purchased; },
                () => { inputVersion++; kingdomView.Hide(); if (homeMenu != null) { RefreshHomeStats(); RefreshHomeCapital(); } });
            kingdomView.Hide();
        }

        private void RefreshCapitalCastle()
        {
            if (armyBonusText != null)
                armyBonusText.text = "Вихід ×" + (diagnosticRound ? 1 : economy.SpawnSpeedMultiplier).ToString("0.00")
                    + " · Рух ×" + ArmyMovementMultiplier.ToString("0.00");
            if (capitalCastle == null) return;
            foreach (Transform child in capitalCastle) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            PrototypeArt.CastleFront(capitalCastle,
                economy.GetCapital(CapitalStat.Strength),
                economy.GetCapital(CapitalStat.Terrain),
                economy.GetCapital(CapitalStat.Economy),
                economy.GetCapital(CapitalStat.Diplomacy));
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
            PlayerPrefs.DeleteKey(EconomyKey);
            PlayerPrefs.DeleteKey(LegacyProgressKey);
            PlayerPrefs.DeleteKey(StarsVersionKey);
            PlayerPrefs.Save();
            if (campaign.Count > 0)
            {
                progress = new CampaignProgress(new int[campaign.Count]);
                SaveProgress();
                economy = new KingdomEconomy(SettlementDefinitions(), UtcSeconds());
                SaveEconomy();
                if (kingdomView != null) kingdomView.SetEconomy(economy);
                if (!busy) { StartLevel(0); ShowLevels(); }
            }
        }

        private void BuildResultOverlay()
        {
            var overlay = Panel("Round Result", contentRect, Vector2.zero, new Vector2(700, 1400),
                new Color(0.07f, 0.10f, 0.075f, 0.97f));
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
                new Vector2(360, 64), "Карта володінь", ShowLevels);
            MakeButton("Result Levels", overlay.rectTransform, new Vector2(0, -315),
                new Vector2(360, 64), "Вибір рівня", ShowLevels);
            MakeButton("Result Kingdom", overlay.rectTransform, new Vector2(0, -395),
                new Vector2(360, 64), "Наші володіння", ShowKingdom);
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
            RefreshTrapSigns();
            RefreshInterface();
            bool won = round.State == SiegeRoundState.Won;
            if (!practiceRound) { economy.RecordBattle(won); SaveEconomy(); }
            int stars = LevelRating.Calculate(CurrentLevel, round);
            if (!practiceRound && progress.Record(levelIndex, stars))
            {
                SaveProgress();
                economy.Synchronize(progress, UtcSeconds());
                SaveEconomy();
            }
            resultStars.text = new string('★', stars) + new string('☆', 5 - stars)
                + "\n" + stars + " / 5 · Найкраще: " + progress.GetBest(levelIndex) + " / 5";
            nextLevelButton.gameObject.SetActive(!practiceRound);
            resultTitle.text = Defense ? (won ? "ЗЕМЛЮ ЗАХИЩЕНО!" : "ЗАХИСТ НЕ ВДАВСЯ")
                : won ? "ЗАМОК ЗАХОПЛЕНО!" : "ЧАС ВИЧЕРПАНО";
            resultTitle.color = won ? Gold : new Color(1f, 0.35f, 0.30f);
            resultDetails.text = (Defense ? "Захисників на рубежі: " + round.Delivered
                + "\nВорогів: " + round.EnemyCount + (round.Delivered == round.EnemyCount ? " · нічия" : "")
                : "Доставлено воїнів: " + round.Delivered + " / " + round.Target)
                + "\nВтрати: " + casualtiesThisRound + " · Військо: " + economy.TrainedWarriors
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
                new Color(0.075f, 0.11f, 0.08f));
            background.rectTransform.anchorMin = Vector2.zero;
            background.rectTransform.anchorMax = Vector2.one;
            background.rectTransform.offsetMin = background.rectTransform.offsetMax = Vector2.zero;
            safeRect = MakeRect("Safe Area", canvasRect, Vector2.zero, Vector2.zero);
            contentRect = MakeRect("Content", safeRect, Vector2.zero, new Vector2(700, 1400));
            levelTitle = Label("Title", contentRect, new Vector2(-90, 650), new Vector2(350, 50),
                "", 24, Ink, FontStyle.Bold);
            levelsButton = MakeButton("Levels", contentRect, new Vector2(155, 650),
                new Vector2(105, 50), "Рівні", ShowLevels);
            MakeButton("Kingdom Shortcut", contentRect, new Vector2(275, 650),
                new Vector2(125, 50), "Меню", ShowHome);


            boardRect = MakeRect("Board", contentRect, new Vector2(0, -25), new Vector2(660, 660));
            PrototypeArt.Board(boardRect);
            Panel("Board Frame", boardRect, Vector2.zero, new Vector2(660, 660),
                new Color(0.13f, 0.18f, 0.12f));
            for (int i = 0; i < SlidingBoard.CellCount; i++)
                Panel("Cell " + i, boardRect, CellPosition(i), new Vector2(154, 154),
                    new Color(0.10f, 0.14f, 0.09f));
            BuildRoadLinks();
            emptyMarker = Label("Empty Cell", boardRect, Vector2.zero, new Vector2(130, 70),
                "ПРОГАЛИНА", 16, Muted).rectTransform;
            for (int tile = 1; tile < SlidingBoard.CellCount; tile++)
            {
                int number = tile;
                var button = MakeButton("Tile " + tile, boardRect, Vector2.zero,
                    new Vector2(154, 154), "", () => { });
                button.gameObject.AddComponent<TileGestureInput>().Configure(
                    () => board.GetIndexOf(number), () => board.EmptyIndex, () => inputVersion,
                    () => !busy && !MenuOpen && !RoundEnded && !board.IsSolved && board.CanMoveTile(number),
                    () => RequestMove(number), swipeThreshold);
                buttons[tile] = button;
                tiles[tile] = button.GetComponent<RectTransform>();
                images[tile] = button.GetComponent<Image>();
                tileBorders[tile] = button.GetComponent<Outline>();
                PrototypeArt.Terrain(tile, tiles[tile]);
                BuildRoad(tile, tiles[tile]);
                trapSigns[tile] = Label("Trap Warning", tiles[tile], new Vector2(0, -29), new Vector2(108, 20), "", 13,
                    new Color(1f, 0.35f, 0.25f), FontStyle.Bold);
                var trapMark = MakeRect("Trap Spikes", tiles[tile], Vector2.zero, Vector2.zero);
                foreach (int side in new[] { -1, 1 })
                {
                    var spike = PrototypeArt.Shape("Spike", trapMark, Vector2.zero, new Vector2(30, 5), new Color(0.78f, 0.22f, 0.14f));
                    spike.rectTransform.localRotation = Quaternion.Euler(0, 0, side * 45);
                }
                trapMarks[tile] = trapMark.gameObject;
                trapMark.gameObject.SetActive(false);
                PrototypeArt.Badge(tiles[tile], new Vector2(-43, 48), new Vector2(38, 28));
                PrototypeArt.Badge(tiles[tile], new Vector2(-42, -49), new Vector2(46, 22));
                PrototypeArt.Badge(tiles[tile], new Vector2(46, -49), new Vector2(26, 22));
                tileSigns[tile] = Label("Position Sign", tiles[tile], new Vector2(47, 48),
                    new Vector2(25, 25), "", 20, Gold, FontStyle.Bold);
                Label("Number", tiles[tile], new Vector2(-43, 48), new Vector2(38, 28),
                    tile.ToString(), 24, Ink, FontStyle.Bold);
                capacityTexts[tile] = Label("Capacity", tiles[tile], new Vector2(-42, -49), new Vector2(46, 22),
                    "0/" + tile, 14, Ink);
                reservationTexts[tile] = Label("Reserved", tiles[tile], new Vector2(46, -49),
                    new Vector2(26, 22), "", 12, Gold);
            }
            var final = Panel("Final Tile", boardRect, CellPosition(15), new Vector2(154, 154), PrototypeArt.TerrainColor(16));
            PrototypeArt.Border(final, Gold, 3);
            PrototypeArt.Terrain(16, final.rectTransform);
            finalTile = final.gameObject;
            BuildRoad(RoadLayout.FinalTile, final.rectTransform);
            PrototypeArt.Badge(final.rectTransform, new Vector2(-43, 48), new Vector2(38, 28));
            PrototypeArt.Badge(final.rectTransform, new Vector2(-42, -49), new Vector2(46, 22));
            PrototypeArt.Badge(final.rectTransform, new Vector2(46, -49), new Vector2(26, 22));
            Label("Final Label", final.rectTransform, new Vector2(-43, 48), new Vector2(38, 28),
                "16", 24, Gold, FontStyle.Bold);
            Label("Bridge Sign", final.rectTransform, new Vector2(47, 48), new Vector2(25, 25),
                "✦", 20, Gold, FontStyle.Bold);
            capacityTexts[16] = Label("Final Capacity", final.rectTransform, new Vector2(-42, -49),
                new Vector2(46, 22), "0/16", 14, Ink);
            reservationTexts[16] = Label("Final Reserved", final.rectTransform, new Vector2(46, -49),
                new Vector2(26, 22), "", 12, Gold);
            finalTile.SetActive(false);
            campArt = MakeRect("Military Camp", boardRect, Vector2.zero, Vector2.zero);
            PrototypeArt.Camp(campArt);
            campCounter = Label("Camp Count", contentRect, new Vector2(240, -487), new Vector2(150, 28), "", 16, Gold);
            entryArrow = Label("Entry Arrow", contentRect, new Vector2(216, -400), new Vector2(44, 44),
                "↑", 36, Gold, FontStyle.Bold);
            entryCaption = Label("Entry Caption", contentRect, new Vector2(210, -485), new Vector2(200, 50),
                "ВІЙСЬКОВИЙ ТАБІР\n↑", 18, Muted);
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
            BuildKingdomView();
            BuildHomeMenu();
            FitSafeArea();
        }

        private void BuildSidePanels()
        {
            var stats = Panel("Compact Statistics", contentRect, new Vector2(0, 560), new Vector2(660, 85), PrototypeArt.Dark);
            PrototypeArt.Card(stats);
            warriorText = Label("Warriors", stats.rectTransform, new Vector2(-190, 0), new Vector2(260, 78), "", 16, Ink);
            timeText = Label("Time", stats.rectTransform, new Vector2(35, 19), new Vector2(155, 32), "", 23, Gold);
            movesText = Label("Moves", stats.rectTransform, new Vector2(35, -18), new Vector2(155, 30), "", 16, Muted);
            correctText = Label("Tiles", stats.rectTransform, new Vector2(225, 0), new Vector2(185, 65), "", 17, Ink);
            capitalCastle = MakeRect("Castle Front", contentRect, new Vector2(0, 430), Vector2.zero);
            RefreshCapitalCastle();
            castleArrow = Label("Castle Arrow", contentRect, new Vector2(-80, 329), new Vector2(44, 35), "↑", 28, Gold);
            routeText = Label("Route", contentRect, new Vector2(130, 330), new Vector2(380, 34), "", 16, Ink);
            armyBonusText = Label("Army Bonuses", contentRect, new Vector2(80, 501), new Vector2(450, 25), "", 15, Gold);
            goalText = Label("Goal", contentRect, new Vector2(-125, -430), new Vector2(390, 70), "", 18, Muted);
        }

        private void StartRound(int steps, bool carryTest = false, bool capacityTest = false)
        {
            if (busy) return;
            inputVersion++;
            diagnosticRound = carryTest || capacityTest;
            capacityStressTest = capacityTest;
            round.Reset();
            if (!diagnosticRound)
            {
                int supply = practiceRound ? CurrentLevel.Supply
                    : Mathf.Clamp(deployment[levelIndex], CurrentLevel.MinimumSupply,
                        Mathf.Min(CurrentLevel.Supply, economy.TrainedWarriors));
                round.Start(CurrentLevel.WithSupply(supply));
            }
            else practiceRound = false;
            casualtiesThisRound = 0;
            traps = diagnosticRound ? null : new TrapLedger(CurrentLevel);
            resultOverlay.SetActive(false);
            sendWarriorButton.gameObject.SetActive(diagnosticRound);
            levelTitle.text = diagnosticRound ? "ПЕРЕВІРКА МЕХАНІКИ"
                : (levelIndex + 1) + ". " + CurrentLevel.Name + (practiceRound ? " · навчання" : "");
            RefreshCapitalCastle();
            entryArrow.text = castleArrow.text = Defense ? "↓" : "↑";
            campArt.anchoredPosition = WarriorQueuePosition;
            campArt.SetAsFirstSibling();
            campCounter.rectTransform.anchoredPosition = Defense ? new Vector2(-80, 474) : new Vector2(240, -487);
            campCounter.transform.SetAsLastSibling();
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
            RefreshTrapSigns();
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
                bool correct = board.GetIndexOf(tile) == tile - 1;
                images[tile].color = PrototypeArt.TerrainColor(tile);
                tileBorders[tile].effectColor = movable ? Gold : correct
                    ? new Color(0.55f, 0.78f, 0.42f) : new Color(0.25f, 0.30f, 0.18f);
                tileBorders[tile].effectDistance = movable ? new Vector2(3, -3) : new Vector2(1, -1);
                int delta = board.EmptyIndex - board.GetIndexOf(tile);
                tileSigns[tile].text = correct ? "✓" : movable ? (Mathf.Abs(delta) == 1 ? "↔" : "↕") : "";
                tileSigns[tile].color = movable ? Gold : new Color(0.66f, 0.86f, 0.50f);
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
                PrototypeArt.Warrior(marker.GetComponent<Image>(), Defense);
                Label("Identity", marker, new Vector2(0, -1), new Vector2(12, 10), (i + 1).ToString(), 8,
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
            int queued = 0, onBoard = 0;
            foreach (WarriorView w in warriors)
            {
                if (w.Model.Completed) continue;
                if (w.Model.CurrentTile == 0) queued++; else onBoard++;
                if (w.Model.CurrentTile == 0 && w.Motion == WarriorMotion.None && !w.Returning)
                    w.Marker.gameObject.SetActive(IsQueueHead(w));
            }
            sendWarriorButton.interactable = diagnosticRound && !busy && WaveCompleted();
            warriorText.text = "Доставлено: " + deliveredTotal + " / " + round.Supply
                + "\nНа полі: " + onBoard + " · Втрати: " + casualtiesThisRound
                + "\nУ таборі: " + queued;
            campCounter.text = "ТАБІР: " + queued;
            entryCaption.text = Defense ? "Захисники: " + round.Delivered + "\nВорог: " + round.EnemyCount + "/" + round.EnemyTotal
                : "";
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
            w.Cooldown = 0.2f / ArmyMovementMultiplier;
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
            w.Cooldown = 0.2f / ArmyMovementMultiplier;
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
            if (motion == WarriorMotion.Enter) entryClock = ArmySpawnInterval;
            w.DestinationCentre = destination == 0 ? end : CellPosition(TileCell(destination));
            w.Motion = motion;
            w.Destination = destination;
            w.Start = start;
            w.End = end;
            w.Elapsed = 0f;
            // Snapshot each segment to avoid a position jump when buying an upgrade mid-round.
            w.Duration = Mathf.Max(0.05f, warriorStepDuration / ArmyMovementMultiplier);
            w.Entered = false;
            w.Marker.SetParent(boardRect, false);
            w.Marker.SetAsLastSibling();
            w.Marker.anchoredPosition = start;
        }

        private void AdvanceWarriorMotion(WarriorView w)
        {
            w.Elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(w.Elapsed / w.Duration);
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
            w.Cooldown = 0.2f / ArmyMovementMultiplier;
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
            if (w.Entered && traps != null && traps.Trigger(w.Model.CurrentTile))
            {
                w.Model.Kill(); w.Motion = WarriorMotion.None; w.Returning = false;
                w.Marker.gameObject.SetActive(false); casualtiesThisRound++;
                round.RecordCasualty();
                if (!practiceRound) { economy.LoseWarrior(); SaveEconomy(); }
                RefreshTrapSigns();
                statusText.text = "Пастка спрацювала! Втрати: " + casualtiesThisRound;
            }
            return w.Entered;
        }

        private void RefreshTrapSigns()
        {
            for (int tile = 1; tile <= 15; tile++)
            {
                if (trapSigns[tile] == null) continue;
                int charges = traps == null ? 0 : traps.GetCharges(tile);
                trapSigns[tile].text = charges > 0 ? "ПАСТКА ×" + charges : "";
                trapMarks[tile].SetActive(charges > 0);
            }
        }

        private void BuildRoad(int tile, RectTransform parent)
        {
            roads[tile] = new List<Image>();
            RoadPorts ports = RoadLayout.GetPorts(tile);
            // Draw every road bed first, then every surface, so junctions have no dark seams.
            for (int pass = 0; pass < 2; pass++)
            {
                float width = pass == 0 ? 26 : 22;
                Color color = pass == 0 ? new Color(0.25f, 0.28f, 0.16f) : PrototypeArt.Road;
                var centre = Panel(pass == 0 ? "Road Bed" : "Road Centre", parent,
                    Vector2.zero, new Vector2(width, width), color);
                if (pass == 1) roads[tile].Add(centre);
                foreach (RoadPorts direction in new[] { RoadPorts.North, RoadPorts.East,
                    RoadPorts.South, RoadPorts.West })
                {
                    if ((ports & direction) == 0) continue;
                    bool vertical = direction == RoadPorts.North || direction == RoadPorts.South;
                    Vector2 position = direction == RoadPorts.North ? new Vector2(0, 38.5f)
                        : direction == RoadPorts.South ? new Vector2(0, -38.5f)
                        : direction == RoadPorts.East ? new Vector2(38.5f, 0) : new Vector2(-38.5f, 0);
                    var road = Panel((pass == 0 ? "Road Bed " : "Road ") + direction, parent,
                        position, vertical ? new Vector2(width, 77) : new Vector2(77, width), color);
                    if (pass == 1) roads[tile].Add(road);
                }
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
                        midpoint, direction == RoadPorts.East ? new Vector2(12, 22)
                            : new Vector2(22, 12), Color.white);
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
            Color connected = PrototypeArt.OpenRoad;
            Color disconnected = PrototypeArt.Road;
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
            routeText.text = "Дороги: " + route.ReachableTileCount + "/16 · "
                + (route.HasCastleRoute ? (Defense ? "Шлях до рубежу відкрито" : "Шлях до замку відкрито")
                    : (Defense ? "Шлях до рубежу розірвано" : "Шлях до замку розірвано"));
            routeText.color = route.HasCastleRoute ? new Color(0.72f, 0.88f, 0.49f) : Ink;
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
            (index % 4 - 1.5f) * 160f, (1.5f - index / 4) * 160f);

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
            var image = Panel(name, parent, position, size, PrototypeArt.Wood);
            image.raycastTarget = true;
            PrototypeArt.Border(image, new Color(0.54f, 0.43f, 0.24f), 2);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;
            var colors = button.colors;
            colors.normalColor = colors.disabledColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.95f, 0.83f);
            colors.pressedColor = new Color(0.78f, 0.72f, 0.57f);
            button.colors = colors;
            button.onClick.AddListener(action);
            if (!string.IsNullOrEmpty(caption))
                Label("Label", image.rectTransform, Vector2.zero, size, caption, 22, Ink);
            return button;
        }
    }
}
