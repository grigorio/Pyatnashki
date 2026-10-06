using Pyatnashki.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace Pyatnashki
{
    public sealed partial class SlidingBoardPrototype
    {
        private readonly Image[] trapGlows = new Image[17];
        private readonly GameObject[] trapMarks = new GameObject[17];
        private readonly Text[] trapSigns = new Text[17], deathSigns = new Text[17];
        private readonly Image[][] trapFrames = new Image[17][];
        private static readonly Color Danger = new Color(1f, 0.25f, 0.12f);
        private static readonly string[] ScoutPhrases = { "Krava thol!", "Vesh' kar!", "Torvak nai!", "Zharu vek!" };

        private void BuildHazardVisuals(int tile, RectTransform parent)
        {
            trapGlows[tile] = Panel("Trap Glow", parent, Vector2.zero, new Vector2(148, 148), Danger);
            trapFrames[tile] = new[] {
                Panel("Trap North", parent, new Vector2(0, 73), new Vector2(148, 4), Danger),
                Panel("Trap South", parent, new Vector2(0, -73), new Vector2(148, 4), Danger),
                Panel("Trap West", parent, new Vector2(-73, 0), new Vector2(4, 148), Danger),
                Panel("Trap East", parent, new Vector2(73, 0), new Vector2(4, 148), Danger)
            };
            var badge = Panel("Trap Skull", parent, Vector2.zero, new Vector2(36, 32), new Color(.13f, .06f, .04f, .9f));
            trapMarks[tile] = badge.gameObject;
            Panel("Skull", badge.transform, new Vector2(0, 3), new Vector2(22, 18), Color.white);
            Panel("Left Eye", badge.transform, new Vector2(-5, 4), new Vector2(4, 5), Color.black);
            Panel("Right Eye", badge.transform, new Vector2(5, 4), new Vector2(4, 5), Color.black);
            Panel("Jaw", badge.transform, new Vector2(0, -8), new Vector2(14, 6), Color.white);
            trapSigns[tile] = Label("Trap Warning", parent, new Vector2(0, -28), new Vector2(144, 23), "", 15, Color.white, FontStyle.Bold);
            var memorial = Panel("Deaths Memorial", parent, new Vector2(0, 48), new Vector2(42, 25), new Color(.12f, .08f, .06f, .95f));
            deathSigns[tile] = Label("Death Count", memorial.transform, Vector2.zero, new Vector2(42, 25), "", 16, Color.white);
            trapGlows[tile].gameObject.SetActive(false);
            foreach (Image edge in trapFrames[tile]) edge.gameObject.SetActive(false);
            trapMarks[tile].SetActive(false);
            trapSigns[tile].gameObject.SetActive(false);
            memorial.gameObject.SetActive(false);
        }

        private void RefreshHazardVisuals()
        {
            for (int tile = 1; tile <= 15; tile++)
            {
                if (trapGlows[tile] == null) continue;
                int charges = traps == null ? 0 : traps.GetCharges(tile);
                int deaths = traps == null ? 0 : traps.GetDeaths(tile);
                bool armed = charges > 0, marked = armed || deaths > 0;
                trapGlows[tile].gameObject.SetActive(armed);
                foreach (Image edge in trapFrames[tile])
                {
                    edge.gameObject.SetActive(marked);
                    edge.color = armed ? Danger : new Color(.55f, .35f, .28f, .6f);
                }
                trapMarks[tile].SetActive(marked);
                trapSigns[tile].gameObject.SetActive(marked);
                trapSigns[tile].text = armed ? "ПАСТКА ×" + charges : "ЗАГИБЛІ: " + deaths;
                trapSigns[tile].color = armed ? Color.white : new Color(.95f, .8f, .65f);
                deathSigns[tile].transform.parent.gameObject.SetActive(deaths > 0);
                deathSigns[tile].text = "†" + deaths;
                images[tile].color = armed ? Color.Lerp(PrototypeArt.TerrainColor(tile), new Color(.65f, .12f, .08f), .48f)
                    : PrototypeArt.TerrainColor(tile);
            }
        }

        private void WarnIfTrapAhead(WarriorView w)
        {
            if (w.Role == WarriorRole.Infantry || traps == null || w.Model.Completed
                || w.Model.CurrentTile == 0 || !w.Marker.gameObject.activeInHierarchy) return;
            int tile = w.Model.InspectNextRoadTile(board, finalTile.activeSelf);
            if (tile == 0 || traps.GetCharges(tile) == 0 || !w.WarnedTraps.Add(tile)) return;
            if (w.Speech == null)
            {
                var bubble = Panel("Scout Speech " + w.Id, boardRect, Vector2.zero, new Vector2(140, 44), new Color(1f, .93f, .76f));
                w.Speech = bubble.rectTransform;
                var outline = bubble.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(.2f, .13f, .07f); outline.effectDistance = new Vector2(2, -2);
                var tail = Panel("Speech Tail", w.Speech, new Vector2(-15, -23), new Vector2(10, 10), bubble.color);
                tail.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
                w.SpeechText = Label("Scout Phrase", w.Speech, Vector2.zero, new Vector2(132, 38), "", 17, new Color(.2f, .13f, .07f), FontStyle.Bold);
                w.SpeechAlpha = bubble.gameObject.AddComponent<CanvasGroup>();
                w.SpeechAlpha.blocksRaycasts = false; w.SpeechAlpha.interactable = false;
            }
            w.SpeechText.text = ScoutPhrases[(w.Id + tile + (int)w.Role) % ScoutPhrases.Length];
            w.SpeechRemaining = 2.2f;
            w.SpeechAlpha.alpha = 1;
            w.Speech.gameObject.SetActive(true);
            w.Speech.SetAsLastSibling();
        }

        private void UpdateHazardFeedback()
        {
            if (boardRect == null) return;
            float pulse = .14f + .09f * (.5f + .5f * Mathf.Sin(Time.unscaledTime * 4f));
            for (int tile = 1; tile <= 15; tile++)
                if (trapGlows[tile] != null && trapGlows[tile].gameObject.activeSelf)
                    trapGlows[tile].color = new Color(Danger.r, Danger.g, Danger.b, pulse);
            foreach (WarriorView w in warriors)
            {
                if (w.Speech == null) continue;
                if (!MenuOpen) w.SpeechRemaining -= Time.unscaledDeltaTime;
                bool visible = !w.Model.Completed && !RoundEnded && w.SpeechRemaining > 0 && w.Marker.gameObject.activeInHierarchy;
                w.Speech.gameObject.SetActive(visible);
                if (!visible) continue;
                Vector2 position = MarkerBoardPosition(w) + new Vector2(0, 46);
                position.x = Mathf.Clamp(position.x, -250, 250);
                w.Speech.anchoredPosition = position;
                w.SpeechAlpha.alpha = Mathf.Clamp01(w.SpeechRemaining / .3f);
                w.Speech.SetAsLastSibling();
            }
        }
    }
}
