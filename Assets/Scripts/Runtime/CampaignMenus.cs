using Pyatnashki.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace Pyatnashki
{
    public sealed partial class SlidingBoardPrototype
    {
        private GameObject homeMenu;
        private Text homeStats, mapDetails, mapSummary;
        private Button deployButton;
        private int selectedMapLevel;
        private int[] deployment;
        private TrapLedger traps;
        private int casualtiesThisRound;
        private readonly Text[] trapSigns = new Text[17];
        private readonly GameObject[] trapMarks = new GameObject[17];
        private readonly System.Collections.Generic.List<Text> mapLevelLabels
            = new System.Collections.Generic.List<Text>();
        private readonly System.Collections.Generic.Dictionary<int, Image> mapRegions
            = new System.Collections.Generic.Dictionary<int, Image>();
        private readonly System.Collections.Generic.Dictionary<int, GameObject> mapHouses
            = new System.Collections.Generic.Dictionary<int, GameObject>();

        private bool IsLevelAvailable(int index) => unlockAllLevelsForDevelopment
            || CampaignMapRules.IsAvailable(campaign, progress, index);
        private int OwnedTerritories
        {
            get { int count = 0; for (int i = 0; i < economy.Count; i++) if (economy.IsOwned(i)) count++; return count; }
        }

        private void BuildHomeMenu()
        {
            var panel = Panel("Main Menu", contentRect, Vector2.zero, new Vector2(700, 1400), PrototypeArt.Dark);
            panel.raycastTarget = true; homeMenu = panel.gameObject;
            Label("Game Title", panel.rectTransform, new Vector2(0, 575), new Vector2(640, 80),
                "П’ЯТНАШКИ · ВОЛОДІННЯ", 32, Gold, FontStyle.Bold);
            var art = MakeRect("Capital Preview", panel.rectTransform, new Vector2(0, 400), Vector2.zero);
            art.localScale = Vector3.one * 1.8f;
            PrototypeArt.Capital(art, 0, 0, 0, 0);
            homeStats = Label("Overall Statistics", panel.rectTransform, new Vector2(0, 130),
                new Vector2(630, 360), "", 25, Ink);
            MakeButton("Campaign Map", panel.rectTransform, new Vector2(0, -130), new Vector2(500, 70), "Карта поселень", ShowLevels);
            MakeButton("Capital", panel.rectTransform, new Vector2(0, -225), new Vector2(500, 70), "Держава та покращення", ShowKingdom);
            MakeButton("Train Five", panel.rectTransform, new Vector2(0, -320), new Vector2(500, 70), "Тренувати 5 воїнів · 25 золота", () => TrainArmy(5));
            MakeButton("Train One", panel.rectTransform, new Vector2(0, -415), new Vector2(500, 70), "Тренувати 1 воїна · 5 золота", () => TrainArmy(1));
            MakeButton("Resume", panel.rectTransform, new Vector2(0, -520), new Vector2(500, 65), "Повернутися на поле", () => homeMenu.SetActive(false));
            Label("Army Rules", panel.rectTransform, new Vector2(0, -620), new Vector2(640, 80),
                "Загін обирається на карті перед боєм.\nУцілілі повертаються, загиблі потребують заміни.", 18, Muted);
            homeMenu.SetActive(false);
        }

        private void ShowHome()
        {
            if (busy) return;
            inputVersion++; RefreshHomeStats();
            homeMenu.SetActive(true); homeMenu.transform.SetAsLastSibling();
            RefreshHomeCapital();
        }

        private void RefreshHomeCapital()
        {
            var art = homeMenu.transform.Find("Capital Preview");
            foreach (Transform child in art) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            PrototypeArt.Capital(art, economy.GetCapital(CapitalStat.Strength), economy.GetCapital(CapitalStat.Terrain),
                economy.GetCapital(CapitalStat.Economy), economy.GetCapital(CapitalStat.Diplomacy));
        }

        private void RefreshHomeStats(string message = "")
        {
            int stars = 0, completed = 0;
            for (int i = 0; i < campaign.Count; i++) { stars += progress.GetBest(i); if (progress.GetBest(i) >= 3) completed++; }
            homeStats.text = CampaignMapRules.RulerTitle(OwnedTerritories)
                + "\nПоселення: " + OwnedTerritories + " / " + economy.Count
                + " · Рівні: " + completed + " / " + campaign.Count
                + "\nЗірки: " + stars + " / " + campaign.Count * 5
                + "\nЗолото: " + economy.Gold + " · Данина: " + economy.TotalHourlyIncome.ToString("0.##") + " / год"
                + "\nНатреновано: " + economy.TrainedWarriors + " / 99"
                + "\nБої: " + economy.Battles + " · Перемоги: " + economy.Victories
                + "\nЗагиблі: " + economy.Casualties + (message.Length > 0 ? "\n" + message : "");
        }

        private void TrainArmy(int count)
        {
            // Mid-battle roster stays fixed; training changes the reserve for subsequent deployments.
            bool trained = economy.Train(count);
            if (trained) SaveEconomy();
            RefreshHomeStats(trained ? "Підготовлено: +" + count : "Недостатньо золота або досягнуто 99 воїнів");
            if (levelMenu.activeSelf)
            {
                SelectMapLevel(selectedMapLevel);
                if (!trained) mapDetails.text += "\nНе вистачає золота або місця у війську";
            }
        }

        private Vector2 MapPosition(int index)
        {
            int settlement = index / 2;
            if (settlement == 0) return new Vector2(0, -470);
            return new Vector2(settlement % 2 == 1 ? -170 : 170, -230 + (settlement - 1) / 2 * 240);
        }

        private void BuildLevelMenu()
        {
            var overlay = Panel("Campaign Map", contentRect, Vector2.zero, new Vector2(700, 1400), PrototypeArt.Dark);
            overlay.raycastTarget = true; levelMenu = overlay.gameObject;
            Label("Map Heading", overlay.rectTransform, new Vector2(0, 630), new Vector2(640, 60), "КАРТА ВОЛОДІНЬ", 30, Gold, FontStyle.Bold);
            mapSummary = Label("Map Summary", overlay.rectTransform, new Vector2(0, 570), new Vector2(640, 45), "", 19, Muted);
            var viewport = Panel("Map Viewport", overlay.rectTransform, new Vector2(0, 210), new Vector2(660, 630), new Color(0.18f, 0.26f, 0.17f));
            viewport.raycastTarget = true; viewport.gameObject.AddComponent<RectMask2D>();
            float height = Mathf.Max(630, 270 + ((campaign.Count + 1) / 2) * 120);
            var map = MakeRect("World", viewport.rectTransform, Vector2.zero, new Vector2(650, height));
            map.anchorMin = map.anchorMax = new Vector2(0.5f, 0); map.pivot = new Vector2(0.5f, 0);
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport.rectTransform; scroll.content = map; scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.verticalNormalizedPosition = 0;
            for (int i = 0; i < campaign.Count; i++)
            {
                int index = i; Vector2 position = MapPosition(i) + new Vector2(0, 560);
                if (campaign[i].Mode == LevelMode.Capture)
                {
                    var region = PrototypeArt.Shape("Region " + index, map, position + new Vector2(0, 20),
                        new Vector2(310, 195), new Color(0.10f, 0.14f, 0.11f));
                    region.transform.SetAsFirstSibling(); mapRegions[index] = region;
                    foreach (int neighbor in campaign[i].UnlockFrom)
                    {
                        Vector2 origin = MapPosition(neighbor) + new Vector2(0, 560);
                        Vector2 delta = position - origin;
                        var road = PrototypeArt.Shape("Frontier Road", map, (position + origin) * 0.5f,
                            new Vector2(delta.magnitude, 5), new Color(0.48f, 0.43f, 0.28f));
                        road.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
                    }
                    var village = PrototypeArt.Shape("Settlement " + index, map, position + new Vector2(0, 58), new Vector2(85, 48), PrototypeArt.Wood);
                    mapHouses[index] = village.gameObject;
                    PrototypeArt.Shape("Settlement Roof", village.transform, new Vector2(0, 30), new Vector2(100, 12), new Color(0.49f, 0.26f, 0.15f));
                    PrototypeArt.Shape("Settlement Gate", village.transform, Vector2.zero, new Vector2(18, 26), PrototypeArt.Dark);
                }
                Vector2 buttonPosition = position + new Vector2(0, campaign[i].Mode == LevelMode.Defense ? -58 : 0);
                var button = MakeButton("Map Level " + index, map, buttonPosition, new Vector2(280, 52), "", () => SelectMapLevel(index));
                levelButtons.Add(button);
                // Empty-caption buttons have no label; create and cache it explicitly before hiding the map.
                mapLevelLabels.Add(Label("Level Caption", button.transform, Vector2.zero,
                    new Vector2(270, 48), "", 18, Ink));
            }
            mapDetails = Label("Deployment Details", overlay.rectTransform, new Vector2(0, -195), new Vector2(640, 155), "", 20, Ink);
            MakeButton("Fewer Warriors", overlay.rectTransform, new Vector2(-240, -310), new Vector2(130, 55), "− воїн", () => ChangeDeployment(-1));
            MakeButton("More Warriors", overlay.rectTransform, new Vector2(-85, -310), new Vector2(130, 55), "+ воїн", () => ChangeDeployment(1));
            MakeButton("Map Training", overlay.rectTransform, new Vector2(160, -310), new Vector2(300, 55), "+5 воїнів · 25 золота", () => TrainArmy(5));
            deployButton = MakeButton("Deploy", overlay.rectTransform, new Vector2(0, -400), new Vector2(500, 65), "Вирушити", () => StartLevel(selectedMapLevel));
            MakeButton("Map Home", overlay.rectTransform, new Vector2(-170, -500), new Vector2(300, 60), "Головне меню", ShowHome);
            MakeButton("Map Capital", overlay.rectTransform, new Vector2(170, -500), new Vector2(300, 60), "Держава", ShowKingdom);
            MakeButton("Map Return", overlay.rectTransform, new Vector2(0, -580), new Vector2(400, 55), "Повернутися", CloseLevels);
            Label("Map Hint", overlay.rectTransform, new Vector2(0, -660), new Vector2(650, 55),
                "3★ захоплення відкривають сусідню область.\nПастки: втрати враховані в мінімальному загоні.", 17, Muted);
            levelMenu.SetActive(false);
        }

        private void ShowLevels()
        {
            if (busy) return;
            inputVersion++;
            for (int i = 0; i < campaign.Count; i++)
            {
                bool available = IsLevelAvailable(i);
                if (mapRegions.TryGetValue(i, out Image region))
                {
                    region.color = available ? new Color(0.27f, 0.35f, 0.21f) : new Color(0.10f, 0.14f, 0.11f);
                    mapHouses[i].SetActive(available);
                }
                levelButtons[i].interactable = available;
                levelButtons[i].targetGraphic.color = available ? PrototypeArt.Wood : new Color(0.12f, 0.15f, 0.12f);
                mapLevelLabels[i].text = available
                    ? (i + 1) + ". " + (campaign[i].Mode == LevelMode.Capture ? SettlementName(i) : "Захист") + " · " + progress.GetBest(i) + "★"
                    : "НЕВІДКРИТА ОБЛАСТЬ";
            }
            mapSummary.text = CampaignMapRules.RulerTitle(OwnedTerritories) + " · Землі: " + OwnedTerritories
                + "\nВійсько: " + economy.TrainedWarriors + " · Золото: " + economy.Gold;
            levelMenu.SetActive(true); levelMenu.transform.SetAsLastSibling();
            SelectMapLevel(IsLevelAvailable(selectedMapLevel) ? selectedMapLevel : 0);
        }

        private string SettlementName(int captureLevel)
        {
            for (int i = 0; i < economy.Count; i++)
                if (economy.GetSettlement(i).CaptureLevel == captureLevel) return economy.GetSettlement(i).Name;
            return campaign[captureLevel].Name;
        }

        private void SelectMapLevel(int index)
        {
            selectedMapLevel = index; LevelDefinition level = campaign[index];
            mapSummary.text = CampaignMapRules.RulerTitle(OwnedTerritories) + " · Землі: " + OwnedTerritories
                + "\nВійсько: " + economy.TrainedWarriors + " · Золото: " + economy.Gold;
            int max = Mathf.Min(level.Supply, economy.TrainedWarriors);
            if (deployment[index] == 0) deployment[index] = max;
            deployment[index] = Mathf.Clamp(deployment[index], Mathf.Min(level.MinimumSupply, max), max);
            mapDetails.text = (index + 1) + ". " + level.Name + " · " + level.Duration.ToString("0") + " с"
                + "\nМінімум: " + level.MinimumSupply + " · Межа рівня: " + level.Supply + " · Загін: " + deployment[index]
                + "\nПастки: " + level.TrapTiles.Length + " × " + level.TrapCharges + " · Найкраще: " + progress.GetBest(index) + "/5★"
                + (max < level.MinimumSupply ? "\nНатренуй ще " + (level.MinimumSupply - max) + " воїнів" : "\nУцілілі повернуться до табору");
            deployButton.interactable = IsLevelAvailable(index) && max >= level.MinimumSupply;
        }

        private void ChangeDeployment(int change)
        {
            deployment[selectedMapLevel] += change; SelectMapLevel(selectedMapLevel);
        }
    }
}
