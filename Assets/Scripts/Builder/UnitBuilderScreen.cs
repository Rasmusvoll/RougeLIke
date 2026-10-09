using System;
using System.Collections.Generic;
using RougeLike.UI;
using RougeLike.Units;
using RougeLike.Robots;
using UnityEngine;
using UnityEngine.UIElements;

namespace RougeLike.Builder
{
    /// <summary>
    /// The unit builder screen: pick a unit or body on the left, the 3D preview in the middle,
    /// slots and the part inventory on the right. The UI is built in code with UI Toolkit.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class UnitBuilderScreen : MonoBehaviour
    {
        [SerializeField] ContentDatabase database;
        [Tooltip("Used to start a run when the builder is opened without one, e.g. when playing this scene directly.")]
        [SerializeField] StarterSet starterSet;
        [SerializeField] UnitPreview preview;
        [SerializeField] StyleSheet styleSheet;
        [SerializeField] string battleScene = "Battle";

        BuilderSession session;
        public BuilderSession Session => session;

        VisualElement root, previewArea, unitList, bodyList, buffList, slotList, partList, statsRow, energyRow;
        Label partsTitle, messageLabel, unitTitle;
        TextField nameField;
        VisualElement bodySwitch;
        Button deleteButton, battleButton;

        // Irreversible actions (scrap, delete, body swap) ask for a second click on the same button.
        string pendingConfirm;
        Vector2 dragStart;
        bool dragging, dragMoved, scrolledToNew;

        void Start()
        {
            if (!RunState.HasRun) RunState.StartNewRun(starterSet);
            session = new BuilderSession(database, RunState.Collection);
            if (session.Current == null)
                foreach (var body in session.OwnedBodies) { session.NewUnit(body.id); break; }
            session.Changed += Refresh;

            BuildLayout();
            Refresh();
        }

        void OnDestroy()
        {
            if (session != null) session.Changed -= Refresh;
        }

        void Update()
        {
            FitCameraToPreviewArea();
        }

        // Layout

        void BuildLayout()
        {
            root = GetComponent<UIDocument>().rootVisualElement;
            root.Clear();
            if (styleSheet != null) root.styleSheets.Add(styleSheet);
            var screen = Add(root, "root");

            // Left: units, bodies, run buffs.
            var left = Add(Add(screen, "column", "side"), "panel", "board");
            left.Add(L("Unit Builder", "title"));
            var leftScroll = new ScrollView();
            leftScroll.style.flexGrow = 1;
            left.Add(leftScroll);
            Section(leftScroll, "Your units");
            unitList = Add(leftScroll);
            Section(leftScroll, "New unit from body");
            bodyList = Add(leftScroll);
            Section(leftScroll, "Run buffs");
            buffList = Add(leftScroll);
            if (Debug.isDebugBuild)
            {
                Section(leftScroll, "Debug");
                var grant = new Button(GrantAllContent) { text = "Grant all content" };
                grant.AddToClassList("small");
                grant.style.marginLeft = 0;
                grant.style.alignSelf = Align.FlexStart;
                leftScroll.Add(grant);
            }
            battleButton = new Button(GoToBattle) { text = $"Go to battle {RunState.BattlesWon + 1}" };
            battleButton.AddToClassList("primary");
            battleButton.AddToClassList("big");
            battleButton.style.marginLeft = 0;
            battleButton.style.marginTop = 10;
            left.Add(battleButton);

            // Middle: name and body, 3D preview, energy and stats.
            var center = Add(screen, "center");
            var headerColumn = Add(center, "column");
            headerColumn.style.paddingBottom = 0;
            var header = Add(headerColumn, "panel", "header");
            unitTitle = L("", "title");
            unitTitle.style.marginBottom = 0;
            unitTitle.style.marginRight = 14;
            header.Add(unitTitle);
            nameField = new TextField { isDelayed = true };
            nameField.RegisterValueChangedCallback(e => session.Rename(e.newValue));
            header.Add(nameField);
            bodySwitch = Add(header, "hrow");
            bodySwitch.style.flexGrow = 1;
            bodySwitch.style.flexWrap = Wrap.Wrap;
            bodySwitch.style.marginTop = 6;
            deleteButton = new Button { text = "Delete unit" };
            deleteButton.AddToClassList("danger");
            deleteButton.clicked += () => Confirm("delete", session.DeleteCurrent);
            header.Add(deleteButton);

            previewArea = Add(center, "preview");
            previewArea.Add(L("Drag to rotate · click a slot to pick it", "hint"));
            previewArea.RegisterCallback<PointerDownEvent>(OnPreviewPointerDown);
            previewArea.RegisterCallback<PointerMoveEvent>(OnPreviewPointerMove);
            previewArea.RegisterCallback<PointerUpEvent>(OnPreviewPointerUp);

            var footerColumn = Add(center, "column");
            footerColumn.style.paddingTop = 0;
            var footer = Add(footerColumn, "panel", "footer");
            energyRow = Add(footer, "energy-row");
            statsRow = Add(footer, "stats");
            messageLabel = L("", "error");
            footer.Add(messageLabel);

            // Right: slots and parts.
            var right = Add(Add(screen, "column", "side-right"), "panel", "board");
            var scroll = new ScrollView();
            scroll.style.flexGrow = 1;
            right.Add(scroll);
            Section(scroll, "Slots");
            slotList = Add(scroll);
            partsTitle = Section(scroll, "");
            partList = Add(scroll);
        }

        void Refresh()
        {
            var bp = session.Current;
            var body = session.CurrentBody;

            RefreshUnits();
            RefreshBodies();
            RefreshBuffs();
            battleButton.SetEnabled(session.Collection.blueprints.Exists(b => UnitAssembler.Validate(b, database, out _)));

            unitTitle.text = body != null ? body.displayName : "No unit";
            nameField.style.display = bp != null ? DisplayStyle.Flex : DisplayStyle.None;
            deleteButton.style.display = bp != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (bp != null && nameField.focusController?.focusedElement != nameField)
                nameField.SetValueWithoutNotify(bp.name);
            SetConfirmLabel(deleteButton, "delete", "Delete unit", "Delete? Parts are lost");

            RefreshBodySwitch();
            RefreshSlots();
            RefreshParts();
            RefreshEnergyAndStats();

            messageLabel.text = session.Message ?? "";
            preview.Show(bp, database, session.SelectedSlotId);
        }

        void RefreshUnits()
        {
            unitList.Clear();
            var units = session.Collection.blueprints;
            if (units.Count == 0) unitList.Add(L("No units yet. Pick a body below.", "empty"));
            for (int i = 0; i < units.Count; i++)
            {
                int index = i;
                var bp = units[i];
                var body = database.GetBody(bp.bodyId);
                var row = Row(unitList, i == session.SelectedIndex, true);
                StoryUI.Socket(row, body != null ? body.icon : null);
                var text = Add(row, "grow");
                text.Add(L(bp.name, "name"));
                text.Add(L($"{body?.displayName} · {bp.parts.Count} parts", "sub"));
                StoryUI.Pips(text, UnitAssembler.EnergyUsed(bp, database), body != null ? body.energy : 0);
                row.RegisterCallback<ClickEvent>(_ => { ClearConfirm(); session.Select(index); });
            }
        }

        void RefreshBodies()
        {
            bodyList.Clear();
            foreach (var body in session.OwnedBodies)
            {
                var row = Row(bodyList, false);
                row.AddToClassList(StoryUI.RarityClass(body.rarity));
                StoryUI.Socket(row, body.icon);
                var text = Add(row, "grow");
                NewTag(Add(text, "hrow"), body.displayName, body.id, row);
                text.Add(L($"{body.slots.Count} slots · {body.energy} energy", "sub"));
                var id = body.id;
                var add = new Button(() => { ClearConfirm(); session.NewUnit(id); }) { text = "+ New" };
                add.AddToClassList("primary");
                add.AddToClassList("small");
                row.Add(add);
            }
        }

        void RefreshBuffs()
        {
            buffList.Clear();
            var buffs = session.RunBuffs;
            if (buffs.Count == 0) buffList.Add(L("None yet. Win battles to earn some.", "empty"));
            foreach (var buff in buffs)
            {
                var row = Row(buffList, false);
                row.AddToClassList(StoryUI.RarityClass(buff.rarity));
                StoryUI.Socket(row, buff.icon, "", true);
                var text = Add(row, "grow");
                NewTag(Add(text, "hrow"), buff.displayName, buff.id, row);
                StoryUI.StatChips(text, buff.modifiers);
                text.Add(L(string.IsNullOrEmpty(buff.requiredTag) ? "All units" : $"Units with a {buff.requiredTag} part", "sub"));
            }
        }

        void RefreshBodySwitch()
        {
            bodySwitch.Clear();
            var bp = session.Current;
            if (bp == null) return;
            foreach (var body in session.OwnedBodies)
            {
                var id = body.id;
                bool current = id == bp.bodyId;
                var b = new Button();
                b.AddToClassList("chip");
                if (current) b.AddToClassList("selected");
                if (body.icon != null)
                {
                    var icon = Add(b, "chip-icon");
                    icon.style.backgroundImage = new StyleBackground(body.icon);
                }
                var label = L(b, body.displayName);
                if (!current && bp.parts.Count > 0)
                {
                    bool armed = pendingConfirm == "body:" + id;
                    if (armed) label.text = $"Swap to {body.displayName}? Parts are lost";
                    b.EnableInClassList("confirm", armed);
                }
                b.clicked += () =>
                {
                    if (current) return;
                    if (bp.parts.Count == 0) session.ChangeBody(id);
                    else Confirm("body:" + id, () => session.ChangeBody(id));
                };
                bodySwitch.Add(b);
            }
        }

        void RefreshSlots()
        {
            slotList.Clear();
            var bp = session.Current;
            var body = session.CurrentBody;
            if (body == null) { slotList.Add(L("Make a unit to start building.", "empty")); return; }

            foreach (var slot in body.slots)
            {
                var slotId = slot.slotId;
                var part = database.GetPart(bp.GetPartIn(slotId));
                var row = Row(slotList, slotId == session.SelectedSlotId, true);
                if (part != null) row.AddToClassList(StoryUI.RarityClass(part.rarity));
                var socket = StoryUI.Socket(row, part != null ? part.icon : null);
                if (part == null) L(socket, "+", "socket-plus");
                var text = Add(row, "grow");
                text.Add(L(SlotName(slot), "name"));
                if (part != null)
                {
                    text.Add(L(part.displayName, "sub"));
                    StoryUI.StatChips(text, part.modifiers);
                }
                else text.Add(L("Empty · takes any part", "sub", "empty"));
                row.RegisterCallback<ClickEvent>(_ => { ClearConfirm(); session.SelectSlot(slotId); });

                if (part != null)
                {
                    var side = Add(row, "card-side");
                    StoryUI.Pips(side, part.energyCost, part.energyCost);
                    var scrap = new Button { text = "Scrap" };
                    scrap.AddToClassList("danger");
                    scrap.AddToClassList("small");
                    scrap.style.marginTop = 6;
                    SetConfirmLabel(scrap, "scrap:" + slotId, "Scrap", "Destroy?");
                    scrap.clicked += () => Confirm("scrap:" + slotId, () => session.Scrap(slotId));
                    // Keep the button's click from also selecting the row.
                    scrap.RegisterCallback<ClickEvent>(e => e.StopPropagation());
                    side.Add(scrap);
                }
            }
        }

        void RefreshParts()
        {
            partList.Clear();
            var bp = session.Current;
            var slot = session.CurrentBody?.GetSlot(session.SelectedSlotId);
            partsTitle.text = slot != null ? $"Parts for {SlotName(slot)}" : "All parts · pick a slot";

            bool any = false;
            foreach (var (part, count) in session.Inventory())
            {
                any = true;
                var row = Row(partList, false);
                row.AddToClassList(StoryUI.RarityClass(part.rarity));
                row.style.alignItems = Align.FlexStart;
                StoryUI.Socket(row, part.icon, "big");
                var text = Add(row, "grow");
                var nameLine = Add(text, "hrow");
                NewTag(nameLine, part.displayName, part.id, row);
                nameLine.Add(L($"×{count}", "count"));
                var typeLine = Add(text, "hrow");
                typeLine.Add(L(part is RobotPartDefinition rp ? $"{rp.Category} · " : $"{part.kind} part · ", "sub"));
                StoryUI.RarityLabel(typeLine, part.rarity);
                StoryUI.StatChips(text, part.modifiers);
                if (part is RobotPartDefinition { type: RobotPartType.Wheel } wheel) text.Add(L($"Drives · {Format(wheel.stride)} top speed, {Format(wheel.grip)} grip", "sub"));
                else if (part.IsLocomotion) text.Add(L($"Walks · {Format(part.stride)} speed per leg", "sub"));
                else if (!UnitAssembler.HasAttackPart(part.tags)) text.Add(L("No attack", "sub"));

                var side = Add(row, "card-side");
                side.style.alignSelf = Align.Stretch;
                StoryUI.Pips(side, part.energyCost, part.energyCost);

                if (slot == null || bp == null) continue;
                int energy = session.EnergyIfEquipped(slot.slotId, part);
                var replaced = database.GetPart(bp.GetPartIn(slot.slotId));
                var equip = new Button { text = replaced != null ? "Replace" : "Equip" };
                equip.AddToClassList("primary");
                equip.AddToClassList("small");
                equip.style.marginTop = 6;
                var slotId = slot.slotId;
                var partId = part.id;
                if (energy > session.EnergyMax)
                {
                    equip.SetEnabled(false);
                    text.Add(L($"Needs {energy} energy, body has {session.EnergyMax}", "warn"));
                }
                else if (replaced != null)
                {
                    text.Add(L($"Destroys the equipped {replaced.displayName}", "warn"));
                }
                equip.clicked += () => { ClearConfirm(); session.Equip(slotId, partId); };
                side.Add(equip);
            }
            if (!any)
                partList.Add(L("No parts left. Win battles to find more.", "empty"));
        }

        void RefreshEnergyAndStats()
        {
            int used = session.EnergyUsed, max = session.EnergyMax;
            energyRow.Clear();
            statsRow.Clear();
            var body = session.CurrentBody;
            if (body == null || session.Current == null) return;

            energyRow.Add(L("Energy", "energy-label"));
            StoryUI.Pips(energyRow, used, max, true);
            var count = L($"{used} / {max}", "energy-count");
            count.EnableInClassList("down", used > max);
            energyRow.Add(count);

            var stats = session.CurrentStats;
            var gait = session.CurrentGait;
            foreach (StatType s in Enum.GetValues(typeof(StatType)))
            {
                float value = stats.Get(s), baseValue = stats.GetBase(s);
                var cell = Add(statsRow, "stat");
                StoryUI.StatIcon(cell, s, true);
                var col = Add(cell);
                col.Add(L(StoryUI.StatName(s), "sub"));
                var line = Add(col, "hrow");
                line.style.alignItems = Align.FlexEnd;
                if (s == StatType.Speed)
                {
                    // Speed comes from legs: say how the unit gets about, or that it can't.
                    line.Add(L(gait.CanMove ? Format(value) : "Can't move", "stat-value"));
                    var drives = RobotAssembler.DescribeMovement(session.Current, database);
                    col.Add(L(drives ?? (gait.CanMove ? gait.Describe() : "Add legs to walk"), "sub", gait.CanMove && !gait.Limps ? "up" : "down"));
                    continue;
                }
                line.Add(L(Format(value), "stat-value"));
                float delta = value - baseValue;
                if (Mathf.Abs(delta) > 0.01f)
                    line.Add(L($"{(delta > 0 ? "+" : "")}{Format(delta)}", "stat-delta", delta > 0 ? "up" : "down"));
                if (s == StatType.Attack && session.Current != null && !UnitAssembler.HasAttackPart(UnitAssembler.CollectTags(session.Current, database)))
                    col.Add(L(!gait.CanMove ? "No attack" : RobotAssembler.IsRobot(session.Current, database) ? "Only rams" : "Only kicks", "sub", "down"));
            }
        }

        // 3D preview input

        void OnPreviewPointerDown(PointerDownEvent e)
        {
            dragging = true;
            dragMoved = false;
            dragStart = e.position;
            previewArea.CapturePointer(e.pointerId);
        }

        void OnPreviewPointerMove(PointerMoveEvent e)
        {
            if (!dragging) return;
            if (((Vector2)e.position - dragStart).sqrMagnitude > 16f) dragMoved = true;
            if (dragMoved) preview.Rotate(-e.deltaPosition.x * 0.5f);
        }

        void OnPreviewPointerUp(PointerUpEvent e)
        {
            if (!dragging) return;
            dragging = false;
            previewArea.ReleasePointer(e.pointerId);
            if (dragMoved) return;
            var slotId = preview.PickSlot(PanelToScreen(e.position));
            ClearConfirm();
            session.SelectSlot(slotId);
        }

        Vector2 PanelToScreen(Vector2 p)
        {
            var size = root.worldBound.size;
            return new Vector2(p.x / size.x * Screen.width, Screen.height - p.y / size.y * Screen.height);
        }

        /// <summary>Keeps the 3D view inside the gap between the panels so the unit stays centred.</summary>
        void FitCameraToPreviewArea()
        {
            var cam = preview != null ? preview.Camera : null;
            if (cam == null || previewArea == null) return;
            var size = root.worldBound.size;
            var r = previewArea.worldBound;
            if (size.x <= 0 || size.y <= 0 || float.IsNaN(r.width)) return;
            cam.rect = new Rect(r.x / size.x, 1f - r.yMax / size.y, r.width / size.x, r.height / size.y);
        }

        // Helpers

        void Confirm(string key, Action action)
        {
            if (pendingConfirm == key)
            {
                pendingConfirm = null;
                action();
            }
            else
            {
                pendingConfirm = key;
                Refresh();
            }
        }

        void ClearConfirm() => pendingConfirm = null;

        void SetConfirmLabel(Button b, string key, string normal, string confirm)
        {
            bool armed = pendingConfirm == key;
            b.text = armed ? confirm : normal;
            b.EnableInClassList("confirm", armed);
        }

        void GoToBattle()
        {
            RunState.NewContentId = null;
            UnityEngine.SceneManagement.SceneManager.LoadScene(battleScene);
        }

        /// <summary>The name, plus a "New" tag if this is the reward just taken. Scrolls its row into view once.</summary>
        void NewTag(VisualElement line, string name, string id, VisualElement row)
        {
            line.Add(L(name, "name"));
            if (string.IsNullOrEmpty(id) || id != RunState.NewContentId) return;
            line.Add(L("New", "new-tag"));
            if (scrolledToNew) return;
            scrolledToNew = true;
            row.schedule.Execute(() => row.GetFirstAncestorOfType<ScrollView>()?.ScrollTo(row)).StartingIn(50);
        }

        void GrantAllContent()
        {
            foreach (var b in database.bodies) if (b != null) session.Collection.AddBody(b.id);
            foreach (var p in database.parts) if (p != null) session.Collection.AddPart(p.id, 2);
            foreach (var b in database.buffs)
                if (b != null && !session.Collection.runBuffIds.Contains(b.id)) session.Collection.runBuffIds.Add(b.id);
            ClearConfirm();
            Refresh();
        }

        static VisualElement Add(VisualElement parent, params string[] classes) => StoryUI.Add(parent, classes);

        static Label L(string text, params string[] classes) => StoryUI.L(text, classes);

        static Label L(VisualElement parent, string text, params string[] classes) => StoryUI.L(parent, text, classes);

        static VisualElement Row(VisualElement parent, bool selected, bool clickable = false)
        {
            var row = Add(parent, "row");
            row.EnableInClassList("selected", selected);
            row.EnableInClassList("clickable", clickable);
            return row;
        }

        static Label Section(VisualElement parent, string text) => StoryUI.Section(parent, text);

        static string SlotName(SlotDefinition slot)
        {
            var words = slot.slotId.Split('_');
            for (int i = 0; i < words.Length; i++)
                if (words[i].Length > 0) words[i] = char.ToUpperInvariant(words[i][0]) + words[i].Substring(1);
            Array.Reverse(words); // arm_left reads as "Left Arm"
            return string.Join(" ", words);
        }

        static string Format(float v) => StoryUI.Format(v);
    }
}
