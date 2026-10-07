using System.Collections.Generic;
using RougeLike.Units;
using UnityEngine;
using UnityEngine.UIElements;

namespace RougeLike.UI
{
    /// <summary>
    /// Building blocks for the storybook UI shared by the builder and battle screens: icon sockets,
    /// stat chips, energy pips and section headings. Looks live in Assets/UI/Builder/UnitBuilder.uss.
    /// </summary>
    public static class StoryUI
    {
        public static VisualElement Add(VisualElement parent, params string[] classes)
        {
            var e = new VisualElement();
            foreach (var c in classes) e.AddToClassList(c);
            parent.Add(e);
            return e;
        }

        public static Label L(string text, params string[] classes)
        {
            var l = new Label(text);
            foreach (var c in classes) l.AddToClassList(c);
            return l;
        }

        public static Label L(VisualElement parent, string text, params string[] classes)
        {
            var l = L(text, classes);
            parent.Add(l);
            return l;
        }

        /// <summary>A heading with a small diamond and a rule running to the right.</summary>
        public static Label Section(VisualElement parent, string text)
        {
            var row = Add(parent, "section-row");
            Add(row, "section-diamond");
            var label = L(row, text, "section");
            Add(row, "section-rule");
            return label;
        }

        /// <summary>A sunken socket holding a content icon. Size is "", "big" or "small".</summary>
        public static VisualElement Socket(VisualElement parent, Sprite icon, string size = "", bool bare = false)
        {
            var socket = Add(parent, "socket");
            if (!string.IsNullOrEmpty(size)) socket.AddToClassList(size);
            if (bare) socket.AddToClassList("bare");
            if (icon != null)
            {
                var img = Add(socket, "socket-image");
                img.style.backgroundImage = new StyleBackground(icon);
            }
            return socket;
        }

        public static string RarityClass(Rarity r) => "card-" + r.ToString().ToLowerInvariant();

        public static Label RarityLabel(VisualElement parent, Rarity r) =>
            L(parent, r.ToString(), "rarity", "rarity-" + r.ToString().ToLowerInvariant());

        public static VisualElement StatIcon(VisualElement parent, StatType s, bool large = false)
        {
            var icon = Add(parent, "stat-icon", "stat-" + s.ToString().ToLowerInvariant());
            if (large) icon.AddToClassList("large");
            icon.tooltip = StatName(s);
            return icon;
        }

        /// <summary>One chip per modifier: the stat's icon and the change, e.g. [claw] +4.</summary>
        public static VisualElement StatChips(VisualElement parent, List<StatModifier> mods)
        {
            var row = Add(parent, "chips");
            if (mods == null || mods.Count == 0)
            {
                L(row, "No stat changes", "sub", "empty");
                return row;
            }
            foreach (var m in mods)
            {
                var chip = Add(row, "stat-chip");
                StatIcon(chip, m.stat);
                L(chip, DescribeValue(m), "stat-chip-text", m.value >= 0 ? "up" : "down");
            }
            return row;
        }

        /// <summary>A row of energy pips: filled for used energy, hollow for what's left.</summary>
        public static VisualElement Pips(VisualElement parent, int filled, int total, bool large = false)
        {
            var row = Add(parent, "pips");
            if (large) row.AddToClassList("large");
            for (int i = 0; i < Mathf.Max(filled, total); i++)
            {
                var pip = Add(row, "pip");
                pip.EnableInClassList("empty", i >= filled);
                pip.EnableInClassList("over", i >= total);
            }
            row.tooltip = $"{filled} energy";
            return row;
        }

        public static string StatName(StatType s) => s switch
        {
            StatType.MaxHealth => "Health",
            _ => s.ToString(),
        };

        public static string Format(float v) => Mathf.Approximately(v, Mathf.Round(v)) ? Mathf.Round(v).ToString("0") : v.ToString("0.#");

        public static string DescribeValue(StatModifier m)
        {
            string sign = m.value >= 0 ? "+" : "";
            return m.op == ModifierOp.Flat ? $"{sign}{Format(m.value)}" : $"{sign}{Format(m.value * 100f)}%";
        }

        public static string Describe(List<StatModifier> mods)
        {
            if (mods == null || mods.Count == 0) return "no stat changes";
            var parts = new List<string>();
            foreach (var m in mods) parts.Add($"{DescribeValue(m)} {StatName(m.stat)}");
            return string.Join(", ", parts);
        }
    }
}
