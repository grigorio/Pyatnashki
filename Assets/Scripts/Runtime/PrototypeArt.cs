using UnityEngine;
using UnityEngine.UI;

namespace Pyatnashki
{
    /// <summary>Lightweight board-game art built from uGUI shapes; decoration never intercepts input.</summary>
    public static class PrototypeArt
    {
        public static readonly Color Parchment = new Color(0.94f, 0.88f, 0.73f);
        public static readonly Color Muted = new Color(0.72f, 0.72f, 0.58f);
        public static readonly Color Gold = new Color(0.95f, 0.72f, 0.29f);
        public static readonly Color Wood = new Color(0.27f, 0.20f, 0.13f);
        public static readonly Color Dark = new Color(0.10f, 0.14f, 0.11f);
        public static readonly Color Road = new Color(0.51f, 0.40f, 0.25f);
        public static readonly Color OpenRoad = new Color(0.94f, 0.79f, 0.48f);

        public static Image Shape(string name, Transform parent, Vector2 position, Vector2 size, Color color)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static Outline Border(Image image, Color color, float width = 2)
        {
            var outline = image.gameObject.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(width, -width);
            outline.useGraphicAlpha = true;
            return outline;
        }

        public static Color TerrainColor(int tile) => Color.Lerp(
            new Color(0.32f, 0.41f, 0.25f), new Color(0.43f, 0.48f, 0.30f), (tile * 7 % 11) / 10f);

        public static void Board(Transform parent)
        {
            Shape("Board Shadow", parent, new Vector2(3, -7), new Vector2(628, 628), new Color(0.04f, 0.06f, 0.04f));
            Shape("Timber Frame", parent, Vector2.zero, new Vector2(620, 620), Wood);
            Shape("Frame Inlay", parent, Vector2.zero, new Vector2(606, 606), new Color(0.57f, 0.43f, 0.23f));
            foreach (int x in new[] { -1, 1 }) foreach (int y in new[] { -1, 1 })
            {
                Shape("Corner Iron", parent, new Vector2(x * 302, y * 302), new Vector2(18, 18), Dark);
                Shape("Corner Rivet", parent, new Vector2(x * 302, y * 302), new Vector2(5, 5), Gold);
            }
        }

        public static void Terrain(int tile, Transform parent)
        {
            // Corner-only scenery leaves road ports and the central troop area clear.
            foreach (int side in new[] { -1, 1 })
            {
                float y = tile % 2 == 0 ? 20 : -20;
                Shape("Grass Tuft", parent, new Vector2(side * 49, y), new Vector2(3, 9), new Color(0.53f, 0.61f, 0.34f));
                Shape("Grass Blade", parent, new Vector2(side * 45, y - 2), new Vector2(3, 6), new Color(0.23f, 0.34f, 0.19f));
                Shape("Stone Shadow", parent, new Vector2(side * 48, -y - 2), new Vector2(11, 7), new Color(0.24f, 0.29f, 0.20f));
                Shape("Stone", parent, new Vector2(side * 48, -y), new Vector2(9, 6), new Color(0.59f, 0.59f, 0.44f));
            }
        }

        public static void Badge(Transform parent, Vector2 position, Vector2 size)
        {
            var plate = Shape("Inset Plate", parent, position, size, new Color(0.13f, 0.20f, 0.13f, 0.96f));
            Border(plate, new Color(0.53f, 0.53f, 0.32f), 1);
        }

        public static void Card(Image image)
        {
            Border(image, new Color(0.45f, 0.37f, 0.22f), 2);
            Shape("Card Rule", image.transform, new Vector2(0, image.rectTransform.sizeDelta.y / 2 - 36),
                new Vector2(image.rectTransform.sizeDelta.x - 32, 2), new Color(0.47f, 0.39f, 0.25f));
        }

        public static void Warrior(Image root, bool defense)
        {
            // All shapes fit the existing 18x18 ownership footprint.
            root.color = Color.clear;
            Color uniform = defense ? new Color(0.22f, 0.48f, 0.75f) : new Color(0.73f, 0.25f, 0.19f);
            Shape("Foot Shadow", root.transform, new Vector2(0, -6), new Vector2(16, 5), new Color(0.04f, 0.07f, 0.04f, 0.7f));
            Shape("Left Boot", root.transform, new Vector2(-3, -7), new Vector2(4, 3), Wood);
            Shape("Right Boot", root.transform, new Vector2(3, -7), new Vector2(4, 3), Wood);
            Shape("Tabard", root.transform, new Vector2(0, -1), new Vector2(12, 10), uniform);
            Shape("Helmet", root.transform, new Vector2(0, 6), new Vector2(9, 5), new Color(0.76f, 0.78f, 0.70f));
            Shape("Visor", root.transform, new Vector2(0, 5), new Vector2(6, 2), Dark);
            Shape("Shield", root.transform, new Vector2(7, -1), new Vector2(3, 8), Gold);
        }

        public static void Castle(Transform parent)
        {
            Color stone = new Color(0.58f, 0.58f, 0.46f);
            Shape("Castle Shadow", parent, new Vector2(-72, 277), new Vector2(160, 16), new Color(0.04f, 0.06f, 0.04f, 0.7f));
            Shape("Castle Keep", parent, new Vector2(-72, 307), new Vector2(100, 62), stone);
            foreach (int x in new[] { -132, -12 })
            {
                var tower = Shape("Stone Tower", parent, new Vector2(x, 315), new Vector2(28, 80), stone);
                Border(tower, new Color(0.32f, 0.34f, 0.27f), 1);
                for (int tooth = -1; tooth <= 1; tooth++)
                    Shape("Battlement", parent, new Vector2(x + tooth * 10, 356), new Vector2(7, 10), stone);
                Shape("Arrow Slit", parent, new Vector2(x, 323), new Vector2(4, 14), Dark);
            }
            for (int row = 0; row < 3; row++)
                Shape("Masonry Seam", parent, new Vector2(-72, 296 + row * 15), new Vector2(96, 1), new Color(0.42f, 0.43f, 0.34f));
            Shape("Gate Arch", parent, new Vector2(-72, 290), new Vector2(28, 30), Wood);
            Shape("Gate Opening", parent, new Vector2(-72, 287), new Vector2(20, 25), Dark);
            Shape("Banner Pole", parent, new Vector2(-72, 350), new Vector2(3, 28), Gold);
            Shape("Castle Banner", parent, new Vector2(-59, 356), new Vector2(24, 13), new Color(0.67f, 0.20f, 0.16f));
        }
    }
}
