using System;
using Pyatnashki.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace Pyatnashki
{
    public sealed class KingdomView : MonoBehaviour
    {
        private KingdomEconomy economy;
        private Action close;
        private Image[] mapNodes;
        private GameObject[] mapFlags;
        private Font font;
        private Text treasury, income, feedback;
        private RectTransform capitalArt;
        private Text[] settlementLabels, upgradeLabels;
        private Button collectButton;
        private Button[] upgradeButtons;
        private float refreshClock;
        public bool IsOpen => gameObject.activeSelf;

        public void Configure(KingdomEconomy economy, Font font, Action collect, Func<CapitalStat, bool> upgrade,
            Action close)
        {
            this.economy = economy; this.font = font; this.close = close;
            TextAt("Heading", transform, new Vector2(0, 645), new Vector2(650, 50), "НАШІ ВОЛОДІННЯ", 30, PrototypeArt.Gold);
            treasury = TextAt("Treasury", transform, new Vector2(0, 585), new Vector2(650, 40), "", 24, PrototypeArt.Parchment);
            income = TextAt("Income", transform, new Vector2(0, 542), new Vector2(650, 40), "", 17, PrototypeArt.Muted);
            var map = PrototypeArt.Shape("Influence Map", transform, new Vector2(0, 295), new Vector2(650, 440), new Color(0.18f, 0.25f, 0.16f));
            PrototypeArt.Border(map, new Color(0.53f, 0.43f, 0.24f));
            capitalArt = PrototypeArt.Shape("Capital Art", map.transform, new Vector2(0, 125), Vector2.zero, Color.clear).rectTransform;
            TextAt("Capital Title", map.transform, new Vector2(0, 202), new Vector2(300, 25), "СТОЛИЦЯ", 19, PrototypeArt.Gold);
            settlementLabels = new Text[economy.Count];
            // Map symbols remain compact; a scrollable roster carries the full campaign details.
            mapNodes = new Image[Mathf.Min(3, economy.Count)];
            mapFlags = new GameObject[mapNodes.Length];
            for (int i = 0; i < mapNodes.Length; i++)
            {
                var node = PrototypeArt.Shape("Map Settlement " + i, map.transform,
                    new Vector2((i - 1) * 200, -155), new Vector2(130, 60), PrototypeArt.Wood);
                mapNodes[i] = node;
                mapFlags[i] = PrototypeArt.Shape("Territory Flag", node.transform, new Vector2(0, 37), new Vector2(20, 9), PrototypeArt.Gold).gameObject;
                PrototypeArt.Border(node, PrototypeArt.Gold, 1);
                TextAt("Map Name", node.transform, Vector2.zero, new Vector2(124, 55), economy.GetSettlement(i).Name, 15, PrototypeArt.Parchment);
                var road = PrototypeArt.Shape("Trade Road", map.transform,
                    new Vector2((i - 1) * 100, -25), new Vector2(3, 220), new Color(0.60f, 0.54f, 0.32f));
                road.rectTransform.localRotation = Quaternion.Euler(0, 0, (i - 1) * 38);
                road.transform.SetAsFirstSibling();
            }
            var viewport = PrototypeArt.Shape("Settlement Viewport", map.transform, new Vector2(0, -50), new Vector2(600, 125), PrototypeArt.Dark);
            viewport.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            var list = PrototypeArt.Shape("Settlement List", viewport.transform, Vector2.zero,
                new Vector2(600, Mathf.Max(125, economy.Count * 40)), Color.clear).rectTransform;
            list.anchorMin = list.anchorMax = new Vector2(0.5f, 1); list.pivot = new Vector2(0.5f, 1);
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport.rectTransform; scroll.content = list; scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            for (int i = 0; i < economy.Count; i++)
            {
                settlementLabels[i] = TextAt("Settlement " + i, list, new Vector2(0, -20 - i * 40), new Vector2(580, 38), "", 16, PrototypeArt.Parchment);
                settlementLabels[i].rectTransform.anchorMin = settlementLabels[i].rectTransform.anchorMax = new Vector2(0.5f, 1);
            }
            collectButton = ButtonAt("Collect", new Vector2(0, 35), new Vector2(380, 55), "Зібрати данину", () =>
            {
                long before = economy.Gold; collect();
                feedback.text = "Зібрано: " + (economy.Gold - before) + " золота"; Refresh();
            });
            var upgradeViewport = PrototypeArt.Shape("Upgrade Viewport", transform, new Vector2(0, -230), new Vector2(650, 410), PrototypeArt.Dark);
            upgradeViewport.raycastTarget = true;
            upgradeViewport.gameObject.AddComponent<RectMask2D>();
            var upgradeContent = PrototypeArt.Shape("Upgrade List", upgradeViewport.transform, Vector2.zero, new Vector2(640, 570), Color.clear).rectTransform;
            upgradeContent.anchorMin = upgradeContent.anchorMax = new Vector2(0.5f, 1);
            upgradeContent.pivot = new Vector2(0.5f, 1);
            var upgradeScroll = upgradeViewport.gameObject.AddComponent<ScrollRect>();
            upgradeScroll.viewport = upgradeViewport.rectTransform; upgradeScroll.content = upgradeContent;
            upgradeScroll.horizontal = false; upgradeScroll.movementType = ScrollRect.MovementType.Clamped;
            upgradeButtons = new Button[6]; upgradeLabels = new Text[6];
            for (int i = 0; i < 6; i++)
            {
                CapitalStat stat = (CapitalStat)i;
                upgradeButtons[i] = ButtonAt("Upgrade " + stat, new Vector2(0, -45 - i * 95), new Vector2(630, 80), "", () =>
                {
                    bool purchased = upgrade(stat);
                    feedback.text = purchased ? "Столицю покращено" : "Покращення недоступне"; RebuildCapital(); Refresh();
                });
                var upgradeRect = upgradeButtons[i].GetComponent<RectTransform>();
                upgradeRect.SetParent(upgradeContent, false);
                upgradeRect.anchorMin = upgradeRect.anchorMax = new Vector2(0.5f, 1);
                upgradeRect.anchoredPosition = new Vector2(0, -45 - i * 95);
                upgradeLabels[i] = TextAt("Caption", upgradeButtons[i].transform, Vector2.zero, new Vector2(610, 76), "", 18, PrototypeArt.Parchment);
            }
            feedback = TextAt("Feedback", transform, new Vector2(0, -470), new Vector2(650, 55), "Данина накопичується й без гри. Землі залишаються твоїми.", 17, PrototypeArt.Muted);
            ButtonAt("Close", new Vector2(0, -565), new Vector2(350, 60), "Повернутися", () => close());
            TextAt("Prototype Note", transform, new Vector2(0, -640), new Vector2(650, 50),
                "Прокрути покращення вниз для розвитку війська.\nТемп виходу та швидкість руху діють на полі.", 16, PrototypeArt.Muted);
            RebuildCapital(); Refresh();
        }

        public void SetEconomy(KingdomEconomy value) { economy = value; RebuildCapital(); Refresh(); }
        public void Open() { gameObject.SetActive(true); transform.SetAsLastSibling(); RebuildCapital(); Refresh(); }
        public void Hide() => gameObject.SetActive(false);
        private void Update() { refreshClock += Time.unscaledDeltaTime; if (refreshClock >= 1) { refreshClock = 0; Refresh(); } }
        private void RebuildCapital()
        {
            foreach (Transform child in capitalArt) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            PrototypeArt.Capital(capitalArt, economy.GetCapital(CapitalStat.Strength), economy.GetCapital(CapitalStat.Terrain),
                economy.GetCapital(CapitalStat.Economy), economy.GetCapital(CapitalStat.Diplomacy));
        }
        private void Refresh()
        {
            treasury.text = "Скарбниця: " + economy.Gold + " золота";
            income.text = economy.TotalHourlyIncome.ToString("0.##") + " / год · запас до " + economy.StorageHours + " год · до збору " + economy.Collectable;
            collectButton.interactable = economy.Collectable > 0;
            for (int i = 0; i < mapNodes.Length; i++)
            {
                mapNodes[i].color = economy.IsOwned(i) ? PrototypeArt.Wood : new Color(0.13f, 0.17f, 0.12f);
                mapFlags[i].SetActive(economy.IsOwned(i));
            }
            for (int i = 0; i < economy.Count; i++)
            {
                var d = economy.GetSettlement(i);
                settlementLabels[i].text = d.Name + (economy.IsOwned(i)
                    ? " · " + economy.GetControlStars(i) + "★ · " + economy.GetHourlyIncome(i).ToString("0.##") + " / год · " + economy.GetStored(i).ToString("0.0") + " зол."
                        + (economy.IsSecured(i) ? " · захищено" : "")
                    : " · приєднати: рівень " + (d.CaptureLevel + 1) + " на 3★");
            }
            string[] names = { "Міцність", "Неприступність", "Економічний розвиток", "Міжнародні відносини", "Військо: підкріплення", "Військо: маршова підготовка" };
            string[] effects = { "Стіни та башти", "Рів та скелясті підступи", "+10% данини і +2 год запасу за ступінь", "Прапори союзників і легіонери", "+15% темпу виходу за ступінь", "+10% швидкості руху за ступінь" };
            for (int i = 0; i < 6; i++)
            {
                int tier = economy.GetCapital((CapitalStat)i), cost = economy.UpgradeCost((CapitalStat)i);
                upgradeLabels[i].text = names[i] + " · " + tier + "/3 · " + (tier == 3 ? "МАКСИМУМ" : cost + " золота") + "\n" + effects[i];
                upgradeButtons[i].interactable = tier < 3 && economy.Gold >= cost;
            }
        }
        private Text TextAt(string name, Transform parent, Vector2 position, Vector2 size, string value, int fontSize, Color color)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position; rect.sizeDelta = size;
            var label = rect.gameObject.AddComponent<Text>(); label.font = font; label.fontSize = fontSize;
            label.color = color; label.alignment = TextAnchor.MiddleCenter; label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap; label.text = value; return label;
        }
        private Button ButtonAt(string name, Vector2 position, Vector2 size, string caption, Action action)
        {
            var image = PrototypeArt.Shape(name, transform, position, size, PrototypeArt.Wood);
            image.raycastTarget = true; PrototypeArt.Border(image, PrototypeArt.Gold, 1);
            var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            var navigation = button.navigation; navigation.mode = Navigation.Mode.None; button.navigation = navigation;
            button.onClick.AddListener(() => action());
            if (caption.Length > 0) TextAt("Label", image.transform, Vector2.zero, size, caption, 22, PrototypeArt.Parchment);
            return button;
        }
    }
}
