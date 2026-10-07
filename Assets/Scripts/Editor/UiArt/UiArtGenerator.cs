using System;
using System.IO;
using RougeLike.Units;
using UnityEditor;
using UnityEngine;

namespace RougeLike.EditorTools
{
    /// <summary>
    /// Draws the 2D UI art in the storybook style (ArtSource/STYLE.md): inked wooden plaques for
    /// buttons, parchment panels, cards trimmed by rarity, stat icons and buff emblems. Everything is
    /// drawn from code so it can be redrawn at any time; the PNGs are committed like any other art.
    /// </summary>
    public static class UiArtGenerator
    {
        public const string UiTexturesFolder = "Assets/UI/Textures";
        public const string StatIconsFolder = "Assets/UI/Textures/Stats";

        // Palette, from ArtSource/STYLE.md.
        public static readonly Color Ink = Hex("2B1F1A");
        public static readonly Color Parchment = Hex("EAD9B0");
        public static readonly Color ParchmentDark = Hex("CDB582");
        public static readonly Color Cream = Hex("F2E4C4");
        public static readonly Color Forest = Hex("4F6B3A");
        public static readonly Color Pine = Hex("2F4A33");
        public static readonly Color Moss = Hex("8A9A4B");
        public static readonly Color Ochre = Hex("D19A3A");
        public static readonly Color Rust = Hex("C2562B");
        public static readonly Color Brick = Hex("9E3B2A");
        public static readonly Color Bark = Hex("6B4A2F");
        public static readonly Color Wood = Hex("A87A4C");
        public static readonly Color Stone = Hex("8C8577");
        public static readonly Color Plum = Hex("6E4A6E");
        public static readonly Color EyrieBlue = Hex("3F6E94");

        [MenuItem("RougeLike/UI/Draw UI Art")]
        public static void DrawAll()
        {
            Directory.CreateDirectory(StatIconsFolder);

            Plaque("button_wood", Wood, 1f);
            Plaque("button_blue", EyrieBlue, 0.5f);
            Plaque("button_red", Brick, 0.5f);
            Plaque("button_ochre", Ochre, 0.7f);
            Plaque("button_parchment", Cream, 0.4f);
            Panel("panel_parchment");
            Frame("frame_wood");
            foreach (Rarity r in Enum.GetValues(typeof(Rarity))) Card($"card_{r.ToString().ToLowerInvariant()}", RarityColor(r));
            Socket("icon_socket");
            Pip("pip_full", true);
            Pip("pip_empty", false);
            foreach (StatType s in Enum.GetValues(typeof(StatType))) StatIcon(s);

            AssetDatabase.Refresh();
            ConfigureImports();
            Debug.Log("Drew UI art into " + UiTexturesFolder);
        }

        public static Color RarityColor(Rarity r) => r switch
        {
            Rarity.Uncommon => Moss,
            Rarity.Rare => EyrieBlue,
            Rarity.Epic => Plum,
            Rarity.Legendary => Ochre,
            _ => ParchmentDark,
        };

        public static Color StatColor(StatType s) => s switch
        {
            StatType.MaxHealth => Brick,
            StatType.Attack => Rust,
            StatType.Defense => EyrieBlue,
            StatType.Speed => Moss,
            _ => Ochre,
        };

        /// <summary>A stat's glyph as a shape in the -1..1 square.</summary>
        public static Func<Vector2, float> StatGlyph(StatType s)
        {
            switch (s)
            {
                case StatType.MaxHealth:
                {
                    var tip = new[] { new Vector2(-0.64f, 0.1f), new Vector2(0.64f, 0.1f), new Vector2(0f, -0.74f) };
                    return p => SdfCanvas.Union(
                        SdfCanvas.Circle(p, new Vector2(-0.3f, 0.22f), 0.36f),
                        SdfCanvas.Circle(p, new Vector2(0.3f, 0.22f), 0.36f),
                        SdfCanvas.Polygon(p, tip));
                }
                case StatType.Attack:
                {
                    // Three claw slashes.
                    var n = new Vector2(-1f, 1f).normalized * 0.34f;
                    return p => SdfCanvas.Union(
                        SdfCanvas.Capsule(p, new Vector2(-0.45f, -0.45f) - n, new Vector2(0.4f, 0.4f) - n, 0.11f),
                        SdfCanvas.Capsule(p, new Vector2(-0.6f, -0.6f), new Vector2(0.6f, 0.6f), 0.13f),
                        SdfCanvas.Capsule(p, new Vector2(-0.4f, -0.4f) + n, new Vector2(0.45f, 0.45f) + n, 0.11f));
                }
                case StatType.Defense:
                {
                    var shield = new[]
                    {
                        new Vector2(-0.62f, 0.6f), new Vector2(0f, 0.76f), new Vector2(0.62f, 0.6f),
                        new Vector2(0.6f, 0.05f), new Vector2(0.4f, -0.42f), new Vector2(0f, -0.8f),
                        new Vector2(-0.4f, -0.42f), new Vector2(-0.6f, 0.05f),
                    };
                    return p => SdfCanvas.Polygon(p, shield);
                }
                case StatType.Speed:
                    // Two chevrons pointing right.
                    return p => SdfCanvas.Union(
                        SdfCanvas.Capsule(p, new Vector2(-0.62f, 0.52f), new Vector2(-0.12f, 0f), 0.13f),
                        SdfCanvas.Capsule(p, new Vector2(-0.12f, 0f), new Vector2(-0.62f, -0.52f), 0.13f),
                        SdfCanvas.Capsule(p, new Vector2(0.02f, 0.52f), new Vector2(0.52f, 0f), 0.13f),
                        SdfCanvas.Capsule(p, new Vector2(0.52f, 0f), new Vector2(0.02f, -0.52f), 0.13f));
                default:
                    // Range: a bullseye.
                    return p => SdfCanvas.Union(
                        SdfCanvas.Ring(p, Vector2.zero, 0.58f, 0.17f),
                        SdfCanvas.Circle(p, Vector2.zero, 0.2f));
            }
        }

        static Func<Vector2, float> Scaled(Func<Vector2, float> shape, float scale, Vector2 offset = default) =>
            p => shape((p - offset) / scale) * scale;

        static void StatIcon(StatType s)
        {
            var c = new SdfCanvas(64, 64);
            c.Inked(Scaled(StatGlyph(s), 0.82f), StatColor(s), Ink, c.Pixel * 3.5f);
            Save(c, $"{StatIconsFolder}/stat_{s.ToString().ToLowerInvariant()}.png");
        }

        /// <summary>A round medallion with the glyph of the buff's main stat.</summary>
        public static Texture2D BuffEmblem(BuffDefinition buff, int size = 256)
        {
            var stat = buff.modifiers != null && buff.modifiers.Count > 0 ? buff.modifiers[0].stat : StatType.Attack;
            var color = StatColor(stat);
            var c = new SdfCanvas(size, size);
            float px = c.Pixel;
            c.Fill(p => SdfCanvas.Circle(p, Vector2.zero, 0.96f), Ink);
            c.Fill(p => SdfCanvas.Circle(p, Vector2.zero, 0.96f - px * 6f), color, (p, col) =>
            {
                // Lit from the upper left, like the 3D scenes.
                float light = Vector2.Dot(p, new Vector2(-0.6f, 0.8f));
                var shaded = Color.Lerp(col, light > 0.35f ? Color.Lerp(col, Cream, 0.25f) : col * 0.88f, 0.6f);
                shaded.a = 1f;
                return shaded;
            });
            c.Fill(p => SdfCanvas.Ring(p, Vector2.zero, 0.8f, px * 3f), Color.Lerp(Cream, color, 0.25f));
            c.Inked(Scaled(StatGlyph(stat), 0.52f), Cream, Ink, px * 7f);
            return c.ToTexture();
        }

        static void Plaque(string name, Color baseColor, float grain)
        {
            var c = new SdfCanvas(128, 48);
            float px = c.Pixel;
            var half = new Vector2(c.width, c.height) * 0.5f * px;
            Func<Vector2, float> outer = p => SdfCanvas.RoundedBox(p, Vector2.zero, half, px * 12f);
            c.Fill(outer, Ink);
            c.Fill(p => outer(p) + px * 3f, baseColor, (p, col) =>
            {
                float g = SdfCanvas.Noise(new Vector2(p.x * 1.2f, p.y * 9f)) * 0.7f + SdfCanvas.Noise(new Vector2(p.x * 4f, p.y * 22f)) * 0.3f;
                col *= 1f + (g - 0.5f) * 0.22f * grain;
                float top = half.y - px * 3f - p.y, bottom = p.y + half.y - px * 3f;
                if (top < px * 3f) col = Color.Lerp(col, Cream, 0.3f);
                else if (bottom < px * 5f) col *= 0.72f;
                col.a = 1f;
                return col;
            });
            Save(c, $"{UiTexturesFolder}/{name}.png");
        }

        static void Panel(string name)
        {
            var c = new SdfCanvas(96, 96);
            float px = c.Pixel;
            Func<Vector2, float> outer = p => SdfCanvas.RoundedBox(p, Vector2.zero, Vector2.one, px * 14f);
            c.Fill(outer, Ink);
            c.Fill(p => outer(p) + px * 3f, Parchment);
            c.Fill(p => Mathf.Abs(outer(p) + px * 8f) - px, ParchmentDark);
            Save(c, $"{UiTexturesFolder}/{name}.png");
        }

        /// <summary>A wooden picture frame with a see-through middle, for the 3D preview window.</summary>
        static void Frame(string name)
        {
            var c = new SdfCanvas(96, 96);
            float px = c.Pixel;
            Func<Vector2, float> outer = p => SdfCanvas.RoundedBox(p, Vector2.zero, Vector2.one, px * 10f);
            Func<Vector2, float> inner = p => SdfCanvas.RoundedBox(p, Vector2.zero, Vector2.one - Vector2.one * px * 12f, px * 4f);
            // The corners outside the frame match the pine table the panels sit on.
            c.Fill(p => Mathf.Max(-outer(p) - px, -(inner(p) - px * 2f)), Pine);
            c.Fill(p => Mathf.Max(outer(p), -(inner(p) - px * 2f)), Ink);
            c.Fill(p => Mathf.Max(outer(p) + px * 2.5f, -inner(p)), Wood, (p, col) =>
            {
                // Grain runs along each side of the frame.
                bool vertical = Mathf.Abs(p.x) > Mathf.Abs(p.y);
                var q = vertical ? new Vector2(p.y, p.x) : p;
                float g = SdfCanvas.Noise(new Vector2(q.x * 1.5f, q.y * 30f));
                col *= 0.9f + g * 0.2f;
                if (-outer(p) < px * 5f) col = Color.Lerp(col, Cream, 0.18f);
                col.a = 1f;
                return col;
            });
            Save(c, $"{UiTexturesFolder}/{name}.png");
        }

        static void Card(string name, Color trim)
        {
            var c = new SdfCanvas(96, 96);
            float px = c.Pixel;
            Func<Vector2, float> outer = p => SdfCanvas.RoundedBox(p, Vector2.zero, Vector2.one, px * 12f);
            c.Fill(outer, Ink);
            c.Fill(p => outer(p) + px * 2.5f, trim);
            c.Fill(p => outer(p) + px * 7f, Ink);
            c.Fill(p => outer(p) + px * 8f, Cream);
            Save(c, $"{UiTexturesFolder}/{name}.png");
        }

        static void Socket(string name)
        {
            var c = new SdfCanvas(96, 96);
            float px = c.Pixel;
            Func<Vector2, float> outer = p => SdfCanvas.RoundedBox(p, Vector2.zero, Vector2.one, px * 18f);
            c.Fill(outer, Bark);
            c.Fill(p => outer(p) + px * 3f, ParchmentDark, (p, col) =>
            {
                // Sunken: darker toward the top-left edge, a soft glow in the middle.
                float d = -outer(p) / 1f;
                var lit = Color.Lerp(col * 0.82f, Color.Lerp(col, Cream, 0.35f), Mathf.Clamp01(d * 2.2f));
                lit.a = 1f;
                return lit;
            });
            Save(c, $"{UiTexturesFolder}/{name}.png");
        }

        static void Pip(string name, bool full)
        {
            var c = new SdfCanvas(32, 32);
            float px = c.Pixel;
            if (full)
            {
                c.Inked(p => SdfCanvas.Circle(p, Vector2.zero, 0.92f - px * 2.5f), Ochre, Ink, px * 2.5f);
                c.Fill(p => SdfCanvas.Circle(p, new Vector2(-0.25f, 0.28f), 0.22f), Color.Lerp(Ochre, Cream, 0.6f));
            }
            else
            {
                c.Inked(p => SdfCanvas.Circle(p, Vector2.zero, 0.92f - px * 2.5f), Color.Lerp(ParchmentDark, Bark, 0.15f), Color.Lerp(Ink, ParchmentDark, 0.45f), px * 2f);
            }
            Save(c, $"{UiTexturesFolder}/{name}.png");
        }

        public static void Save(SdfCanvas c, string path) => Save(c.ToTexture(), path);

        public static void Save(Texture2D tex, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        /// <summary>Imports a PNG as an uncompressed sprite with clean transparent edges.</summary>
        public static void ConfigureSprite(string path, Vector4 border = default)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(path) is not TextureImporter ti) return;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.spriteBorder = border;
            ti.SaveAndReimport();
        }

        static void ConfigureImports()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { UiTexturesFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var file = Path.GetFileNameWithoutExtension(path);
                // Frames are 9-sliced from USS; the borders here only document the safe insets.
                var border = file.StartsWith("button_") ? new Vector4(14, 14, 14, 14)
                    : file.StartsWith("panel_") || file.StartsWith("frame_") ? new Vector4(20, 20, 20, 20)
                    : file.StartsWith("card_") ? new Vector4(14, 14, 14, 14)
                    : Vector4.zero;
                ConfigureSprite(path, border);
            }
        }

        static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var c);
            return c;
        }
    }
}
