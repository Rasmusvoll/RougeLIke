using RougeLike.UI;
using RougeLike.Units;
using UnityEngine;
using UnityEngine.UIElements;

namespace RougeLike.Battle
{
    /// <summary>
    /// The battle UI and placement input. Top bar: battle name, counts, speed and Start. Bottom bar:
    /// the player's units as cards to drag onto the blue half. A result panel when the fight ends.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class BattleScreen : MonoBehaviour
    {
        static readonly float[] Speeds = { 1f, 2f, 4f };

        [SerializeField] BattleManager battle;
        [SerializeField] Camera worldCamera;
        [SerializeField] StyleSheet[] styleSheets;

        VisualElement root, bottomBar, cardRow, resultPanel;
        Label titleLabel, infoLabel, hintLabel, resultTitle, resultText;
        Button startButton, speedButton, autoButton, clearButton, resultButton;

        BattleUnit dragging;
        int speedIndex;

        void Start()
        {
            if (worldCamera == null) worldCamera = Camera.main;
            BuildLayout();
            battle.Changed += Refresh;
            Refresh();
        }

        void OnDestroy()
        {
            if (battle != null) battle.Changed -= Refresh;
            Time.timeScale = 1f;
        }

        // Layout

        void BuildLayout()
        {
            root = GetComponent<UIDocument>().rootVisualElement;
            root.Clear();
            foreach (var s in styleSheets) if (s != null) root.styleSheets.Add(s);
            root.pickingMode = PickingMode.Ignore;
            var screen = Add(root, "battle-root");
            screen.pickingMode = PickingMode.Ignore;

            var top = Add(screen, "panel", "battle-top");
            var titles = Add(top, "grow");
            titleLabel = L("", "title");
            titleLabel.style.marginBottom = 0;
            titles.Add(titleLabel);
            infoLabel = L("", "sub");
            titles.Add(infoLabel);
            speedButton = new Button(CycleSpeed);
            speedButton.AddToClassList("big");
            startButton = new Button(battle.StartBattle) { text = "Start battle" };
            startButton.AddToClassList("gold");
            startButton.AddToClassList("big");
            top.Add(speedButton);
            top.Add(startButton);

            var middle = Add(screen, "battle-middle");
            middle.pickingMode = PickingMode.Ignore;
            hintLabel = L("", "battle-hint");
            hintLabel.pickingMode = PickingMode.Ignore;
            middle.Add(hintLabel);

            resultPanel = Add(middle, "panel", "battle-result");
            resultTitle = L("", "battle-result-title");
            resultPanel.Add(resultTitle);
            resultText = L("", "sub");
            resultText.style.marginBottom = 14;
            resultText.style.whiteSpace = WhiteSpace.Normal;
            resultPanel.Add(resultText);
            resultButton = new Button(battle.Continue);
            resultButton.AddToClassList("primary");
            resultButton.AddToClassList("big");
            resultPanel.Add(resultButton);

            bottomBar = Add(screen, "panel", "battle-bottom");
            var barHeader = Add(bottomBar);
            barHeader.style.flexDirection = FlexDirection.Row;
            barHeader.style.alignItems = Align.Center;
            var section = StoryUI.Section(barHeader, "Your army");
            section.parent.style.flexGrow = 1;
            section.parent.style.marginTop = 0;
            section.parent.style.marginBottom = 0;
            section.parent.style.marginRight = 8;
            autoButton = new Button(battle.AutoPlace) { text = "Place all" };
            autoButton.AddToClassList("small");
            barHeader.Add(autoButton);
            clearButton = new Button(battle.ClearPlacement) { text = "Clear" };
            clearButton.AddToClassList("small");
            barHeader.Add(clearButton);
            cardRow = Add(bottomBar, "battle-cards");
        }

        void Refresh()
        {
            var phase = battle.Phase;
            bool placing = phase == BattlePhase.Placement;
            int alivePlayers = battle.PlayerUnits.FindAll(u => u.IsAlive).Count;
            int aliveEnemies = battle.EnemyUnits.FindAll(u => u.IsAlive).Count;

            titleLabel.text = $"Battle {battle.BattleNumber}" + (battle.Wave != null ? $" · {battle.Wave.displayName}" : "");
            infoLabel.text = placing
                ? $"{battle.PlayerUnits.Count} of your units placed · {battle.EnemyUnits.Count} enemies"
                : $"Your units {alivePlayers} · Enemies {aliveEnemies}";

            startButton.style.display = placing ? DisplayStyle.Flex : DisplayStyle.None;
            startButton.SetEnabled(battle.PlayerUnits.Count > 0);
            speedButton.style.display = phase == BattlePhase.Fighting ? DisplayStyle.Flex : DisplayStyle.None;
            speedButton.text = $"Speed {Speeds[speedIndex]}x";
            bottomBar.style.display = placing ? DisplayStyle.Flex : DisplayStyle.None;

            hintLabel.text = !placing ? ""
                : battle.Roster.Count == 0 ? "You have no units. Go back to the builder and make some."
                : "Drag units onto the blue half · drag to move · right-click to take back";
            hintLabel.style.display = hintLabel.text.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;

            bool over = phase is BattlePhase.Victory or BattlePhase.Defeat;
            resultPanel.style.display = over ? DisplayStyle.Flex : DisplayStyle.None;
            if (over)
            {
                bool won = phase == BattlePhase.Victory;
                resultTitle.text = won ? "Victory!" : "Defeat";
                resultTitle.EnableInClassList("up", won);
                resultTitle.EnableInClassList("down", !won);
                resultText.text = won
                    ? $"{alivePlayers} of your {battle.PlayerUnits.Count} units survived. Rewards come in a later update."
                    : "Your army was wiped out. The run is over.";
                resultButton.text = won ? "Back to builder" : "Start a new run";
            }

            if (placing) RefreshCards();
        }

        void RefreshCards()
        {
            cardRow.Clear();
            var db = battle.Database;
            var buffs = UnitAssembler.ResolveBuffs(RunState.Collection.runBuffIds, db);
            for (int i = 0; i < battle.Roster.Count; i++)
            {
                int index = i;
                var bp = battle.Roster[i];
                var body = db.GetBody(bp.bodyId);
                bool canField = battle.CanField(i);
                bool placed = battle.PlacedUnit(i) != null;

                var card = Add(cardRow, "row", "battle-card");
                card.EnableInClassList("clickable", canField && !placed);
                card.EnableInClassList("dim", placed || !canField);
                card.EnableInClassList("selected", placed);
                StoryUI.Socket(card, body != null ? body.icon : null, "big");
                var text = Add(card, "grow");
                text.Add(L(bp.name, "name"));
                if (!canField)
                {
                    text.Add(L("Can't fight: invalid build", "warn"));
                    continue;
                }
                var stats = UnitAssembler.ComputeStats(bp, buffs, db);
                text.Add(L($"{body?.displayName} · {(UnitAssembler.CollectTags(bp, db).Contains("ranged") ? "ranged" : "melee")}", "sub"));
                var statLine = Add(text, "chips");
                foreach (var s in new[] { StatType.MaxHealth, StatType.Attack, StatType.Defense, StatType.Speed })
                {
                    var chip = Add(statLine, "stat-chip");
                    StoryUI.StatIcon(chip, s);
                    chip.Add(L(StoryUI.Format(stats.Get(s)), "stat-chip-text"));
                }
                text.Add(L(placed ? "On the field" : "Drag onto the field", placed ? "up" : "hint"));
                if (!placed) card.RegisterCallback<PointerDownEvent>(e => BeginDragFromRoster(index, e));
            }
        }

        // Placement input. UI Toolkit starts drags from cards; the field itself is read with the
        // legacy input API because it's plain 3D space, not UI.

        void BeginDragFromRoster(int index, PointerDownEvent e)
        {
            if (e.button != 0 || dragging != null) return;
            dragging = battle.PlacePlayerUnit(index, new Vector3(0f, 0f, -100f));
            if (dragging != null) dragging.gameObject.SetActive(false);
        }

        void Update()
        {
            if (battle.Phase != BattlePhase.Placement || worldCamera == null)
            {
                dragging = null;
                return;
            }

            Vector2 mouse = Input.mousePosition;
            bool overUI = IsOverUI(mouse);
            bool onField = battle.TryGroundPoint(worldCamera.ScreenPointToRay(mouse), out var point);

            if (dragging != null)
            {
                dragging.gameObject.SetActive(onField && !overUI);
                if (onField) dragging.transform.position = battle.ClampToPlayerZone(point, dragging.Radius);
                if (!Input.GetMouseButton(0))
                {
                    // Dropped back on the UI (or off the field) returns the unit to the army.
                    var u = dragging;
                    dragging = null;
                    if (overUI || !onField) battle.RemovePlayerUnit(u);
                    else battle.NotifyChanged();
                }
                return;
            }

            if (overUI || !onField) return;
            if (Input.GetMouseButtonDown(0)) dragging = PlacedUnitAt(point);
            else if (Input.GetMouseButtonDown(1)) battle.RemovePlayerUnit(PlacedUnitAt(point));
        }

        BattleUnit PlacedUnitAt(Vector3 point)
        {
            BattleUnit best = null;
            float bestD = float.MaxValue;
            foreach (var u in battle.PlayerUnits)
            {
                var d = u.transform.position - point;
                d.y = 0f;
                float dist = d.magnitude;
                if (dist < u.Radius + 0.4f && dist < bestD) { bestD = dist; best = u; }
            }
            return best;
        }

        bool IsOverUI(Vector2 screenPos)
        {
            var panel = root?.panel;
            if (panel == null) return false;
            var p = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screenPos.x, Screen.height - screenPos.y));
            for (var e = panel.Pick(p); e != null; e = e.parent)
                if (e.ClassListContains("panel")) return e.resolvedStyle.display != DisplayStyle.None;
            return false;
        }

        void CycleSpeed()
        {
            speedIndex = (speedIndex + 1) % Speeds.Length;
            Time.timeScale = Speeds[speedIndex];
            Refresh();
        }

        // Helpers

        static VisualElement Add(VisualElement parent, params string[] classes) => StoryUI.Add(parent, classes);

        static Label L(string text, params string[] classes) => StoryUI.L(text, classes);
    }
}
