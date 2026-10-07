using System;
using System.Collections.Generic;
using RougeLike.Units;
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

        BuilderSession session;
        public BuilderSession Session => session;

        VisualElement root, previewArea, unitList, bodyList, buffList, slotList, partList, statsRow, energyFill;
        Label energyLabel, partsTitle, messageLabel, unitTitle;
        TextField nameField;
        VisualElement bodySwitch;
        Button deleteButton;

        // Irreversible actions (scrap, delete, body swap) ask for a second click on the same button.
        string pendingConfirm;
        Vector2 dragStart;
        bool dragging, dragMoved;

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
            var left = Add(screen, "panel", "side");
            left.Add(L("Unit Builder", "title"));
            Section(left, "YOUR UNITS");
            unitList = Add(left);
            Section(left, "NEW UNIT FROM BODY");
            bodyList = Add(left);
            Section(left, "RUN BUFFS");
            buffList = Add(left);
            if (Debug.isDebugBuild)
            {
                Section(left, "DEBUG");
                var grant = new Button(GrantAllContent) { text = "Grant all content" };
                grant.style.marginLeft = 0;
                left.Add(grant);
            }

            // Middle: name and body, 3D preview, energy and stats.
            var center = Add(screen, "center");
            var header = Add(center, "panel", "header");
            unitTitle = L("", "title");
            unitTitle.style.marginBottom = 0;
            unitTitle.style.marginRight = 12;
            header.Add(unitTitle);
            nameField = new TextField { isDelayed = true };
            nameField.RegisterValueChangedCallback(e => session.Rename(e.newValue));
            header.Add(nameField);
            bodySwitch = Add(header, "stats");
            bodySwitch.style.flexGrow = 1;
            deleteButton = new Button { text = "Delete unit" };
            deleteButton.AddToClassList("danger");
            deleteButton.clicked += () => Confirm("delete", session.DeleteCurrent);
            header.Add(deleteButton);

            previewArea = Add(center, "preview");
            previewArea.Add(L("Drag to rotate · click a slot to pick it", "hint"));
            previewArea.RegisterCallback<PointerDownEvent>(OnPreviewPointerDown);
            previewArea.RegisterCallback<PointerMoveEvent>(OnPreviewPointerMove);
            previewArea.RegisterCallback<PointerUpEvent>(OnPreviewPointerUp);

            var footer = Add(center, "panel", "footer");
            energyLabel = new Label();
            footer.Add(energyLabel);
            var track = Add(footer, "energy-track");
            energyFill = Add(track, "energy-fill");
            statsRow = Add(footer, "stats");
            messageLabel = L("", "error");
            footer.Add(messageLabel);

            // Right: slots and parts.
            var right = Add(screen, "panel", "side-right");
            var scroll = new ScrollView();
            scroll.style.flexGrow = 1;
            right.Add(scroll);
            Section(scroll, "SLOTS");
            slotList = Add(scroll);
            partsTitle = L("", "section");
            scroll.Add(partsTitle);
            partList = Add(scroll);
        }

        void Refresh()
        {
            var bp = session.Current;
            var body = session.CurrentBody;

            RefreshUnits();
            RefreshBodies();
            RefreshBuffs();

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
                var row = Row(unitList, i == session.SelectedIndex);
                var text = Add(row, "grow");
                text.Add(new Label(bp.name));
                text.Add(L($"{body?.displayName} · {UnitAssembler.EnergyUsed(bp, database)}/{body?.energy} energy · {bp.parts.Count} parts", "sub"));
                row.RegisterCallback<ClickEvent>(_ => { ClearConfirm(); session.Select(index); });
            }
        }

        void RefreshBodies()
        {
            bodyList.Clear();
            foreach (var body in session.OwnedBodies)
            {
                var row = Row(bodyList, false);
                var text = Add(row, "grow");
                text.Add(new Label(body.displayName));
                text.Add(L($"{body.slots.Count} slots · {body.energy} energy", "sub"));
                var id = body.id;
                var add = new Button(() => { ClearConfirm(); session.NewUnit(id); }) { text = "+ New" };
                add.AddToClassList("primary");
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
                var text = Add(row, "grow");
                text.Add(new Label(buff.displayName));
                var who = string.IsNullOrEmpty(buff.requiredTag) ? "all units" : $"units with a {buff.requiredTag} part";
                text.Add(L($"{Describe(buff.modifiers)} for {who}", "sub"));
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
                var b = new Button { text = body.displayName };
                b.AddToClassList("chip");
                if (current) b.AddToClassList("selected");
                else if (bp.parts.Count > 0)
                    SetConfirmLabel(b, "body:" + id, body.displayName, $"Swap to {body.displayName}? Parts are lost");
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
                var row = Row(slotList, slotId == session.SelectedSlotId);
                var text = Add(row, "grow");
                text.Add(new Label(SlotName(slot)));
                if (part != null) text.Add(L($"{part.displayName} · {Describe(part.modifiers)}", "sub"));
                else text.Add(L($"Empty {slot.type} slot", "sub", "empty"));
                row.RegisterCallback<ClickEvent>(_ => { ClearConfirm(); session.SelectSlot(slotId); });

                if (part != null)
                {
                    row.Add(L($"Cost {part.energyCost}", "cost"));
                    var scrap = new Button { text = "Scrap" };
                    scrap.AddToClassList("danger");
                    SetConfirmLabel(scrap, "scrap:" + slotId, "Scrap", "Destroy?");
                    scrap.clicked += () => Confirm("scrap:" + slotId, () => session.Scrap(slotId));
                    // Keep the button's click from also selecting the row.
                    scrap.RegisterCallback<ClickEvent>(e => e.StopPropagation());
                    row.Add(scrap);
                }
            }
        }

        void RefreshParts()
        {
            partList.Clear();
            var bp = session.Current;
            var slot = session.CurrentBody?.GetSlot(session.SelectedSlotId);
            partsTitle.text = slot != null ? $"PARTS FOR {SlotName(slot).ToUpperInvariant()}" : "ALL PARTS (PICK A SLOT TO EQUIP)";

            bool any = false;
            foreach (var (part, count) in session.Inventory(slot?.type))
            {
                any = true;
                var row = Row(partList, false);
                var text = Add(row, "grow");
                text.Add(new Label(part.displayName));
                text.Add(L($"{part.fitsSlot} · {Describe(part.modifiers)}", "sub"));
                row.Add(L($"Cost {part.energyCost}", "cost"));
                row.Add(L($"×{count}", "count"));

                if (slot == null || bp == null) continue;
                int energy = session.EnergyIfEquipped(slot.slotId, part);
                var replaced = database.GetPart(bp.GetPartIn(slot.slotId));
                var equip = new Button { text = replaced != null ? "Replace" : "Equip" };
                equip.AddToClassList("primary");
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
                row.Add(equip);
            }
            if (!any)
                partList.Add(L(slot != null ? $"No {slot.type} parts left. Win battles to find more." : "No parts left.", "empty"));
        }

        void RefreshEnergyAndStats()
        {
            int used = session.EnergyUsed, max = session.EnergyMax;
            energyLabel.text = session.Current != null ? $"Energy  {used} / {max}" : "";
            energyFill.style.width = Length.Percent(max > 0 ? 100f * used / max : 0f);
            energyFill.EnableInClassList("full", max > 0 && used >= max);

            statsRow.Clear();
            var body = session.CurrentBody;
            if (body == null) return;
            var stats = session.CurrentStats;
            foreach (StatType s in Enum.GetValues(typeof(StatType)))
            {
                float value = stats.Get(s), baseValue = stats.GetBase(s);
                var cell = Add(statsRow, "stat");
                cell.Add(L(StatName(s), "sub"));
                var line = Add(cell);
                line.style.flexDirection = FlexDirection.Row;
                line.style.alignItems = Align.FlexEnd;
                line.Add(L(Format(value), "stat-value"));
                float delta = value - baseValue;
                if (Mathf.Abs(delta) > 0.01f)
                {
                    var d = L($" {(delta > 0 ? "+" : "")}{Format(delta)}", delta > 0 ? "up" : "down");
                    d.style.marginBottom = 3;
                    line.Add(d);
                }
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

        void GrantAllContent()
        {
            foreach (var b in database.bodies) if (b != null) session.Collection.AddBody(b.id);
            foreach (var p in database.parts) if (p != null) session.Collection.AddPart(p.id, 2);
            foreach (var b in database.buffs)
                if (b != null && !session.Collection.runBuffIds.Contains(b.id)) session.Collection.runBuffIds.Add(b.id);
            ClearConfirm();
            Refresh();
        }

        static VisualElement Add(VisualElement parent, params string[] classes)
        {
            var e = new VisualElement();
            foreach (var c in classes) e.AddToClassList(c);
            parent.Add(e);
            return e;
        }

        static Label L(string text, params string[] classes)
        {
            var l = new Label(text);
            foreach (var c in classes) l.AddToClassList(c);
            return l;
        }

        static VisualElement Row(VisualElement parent, bool selected)
        {
            var row = Add(parent, "row");
            row.EnableInClassList("selected", selected);
            return row;
        }

        static void Section(VisualElement parent, string text) => parent.Add(L(text, "section"));

        static string SlotName(SlotDefinition slot)
        {
            var words = slot.slotId.Split('_');
            for (int i = 0; i < words.Length; i++)
                if (words[i].Length > 0) words[i] = char.ToUpperInvariant(words[i][0]) + words[i].Substring(1);
            Array.Reverse(words); // arm_left reads as "Left Arm"
            return string.Join(" ", words);
        }

        static string StatName(StatType s) => s switch
        {
            StatType.MaxHealth => "Health",
            _ => s.ToString(),
        };

        static string Format(float v) => Mathf.Approximately(v, Mathf.Round(v)) ? Mathf.Round(v).ToString("0") : v.ToString("0.#");

        static string Describe(List<StatModifier> mods)
        {
            if (mods == null || mods.Count == 0) return "no stat changes";
            var parts = new List<string>();
            foreach (var m in mods)
            {
                string sign = m.value >= 0 ? "+" : "";
                parts.Add(m.op == ModifierOp.Flat
                    ? $"{sign}{Format(m.value)} {StatName(m.stat)}"
                    : $"{sign}{Format(m.value * 100f)}% {StatName(m.stat)}");
            }
            return string.Join(", ", parts);
        }
    }
}
