using System.Collections.Generic;
using RougeLike.Units;
using UnityEngine;

namespace RougeLike.Builder
{
    /// <summary>
    /// Shows the unit being built in 3D: the assembled model on a turntable, a marker on each slot,
    /// and a close-up camera framed on the body. Markers carry colliders so slots can be clicked.
    /// </summary>
    public class UnitPreview : MonoBehaviour
    {
        [SerializeField] Camera previewCamera;
        [SerializeField] Material markerMaterial;
        [SerializeField] Color emptySlotColor = new(0.35f, 0.85f, 1f);
        [SerializeField] Color selectedSlotColor = new(1f, 0.8f, 0.2f);
        [SerializeField] float markerSize = 0.08f;
        [SerializeField] float pitch = 18f;
        [SerializeField] float idleSpinSpeed = 12f;
        [SerializeField] float idleDelay = 3f;
        [Tooltip("Optional stand the unit poses on, e.g. a tree stump. Its top should sit at standHeight.")]
        [SerializeField] GameObject standPrefab;
        [SerializeField] float standHeight = 0.42f;
        [SerializeField] float standScale = 1.5f;
        [Tooltip("Seconds between idle show-offs, where the unit tries out one of its parts.")]
        [SerializeField] float showOffInterval = 5f;

        Transform pivot;
        GameObject model;
        UnitAnimator animator;
        UnitBlueprint shownBlueprint;
        readonly Dictionary<string, string> shownParts = new();
        float nextShowOff;
        readonly Dictionary<string, Renderer> markers = new();
        MaterialPropertyBlock block;
        float yaw = 150f;
        float lastInteraction = -999f;
        Vector3 focus;
        float distance = 3f;

        public Camera Camera => previewCamera;

        void Awake()
        {
            pivot = new GameObject("Pivot").transform;
            pivot.SetParent(transform, false);
            block = new MaterialPropertyBlock();
            if (markerMaterial == null) markerMaterial = new Material(Shader.Find("Unlit/Color"));
            if (standPrefab != null)
            {
                var stand = Instantiate(standPrefab, pivot);
                stand.name = "Stand";
                stand.transform.localPosition = new Vector3(0f, -standHeight * standScale, 0f);
                stand.transform.localScale = Vector3.one * standScale;
            }
        }

        void LateUpdate()
        {
            if (Time.unscaledTime - lastInteraction > idleDelay) yaw += idleSpinSpeed * Time.unscaledDeltaTime;
            pivot.localRotation = Quaternion.Euler(0f, yaw, 0f);

            if (animator != null && Time.unscaledTime >= nextShowOff)
            {
                animator.FlourishRandom();
                nextShowOff = Time.unscaledTime + showOffInterval * Random.Range(0.8f, 1.3f);
            }

            if (previewCamera != null)
            {
                var rot = Quaternion.Euler(pitch, 0f, 0f);
                previewCamera.transform.SetPositionAndRotation(focus - rot * Vector3.forward * distance, rot);
            }
        }

        public void Rotate(float degrees)
        {
            yaw += degrees;
            lastInteraction = Time.unscaledTime;
        }

        public void Show(UnitBlueprint bp, ContentDatabase db, string selectedSlotId)
        {
            if (model != null) Destroy(model);
            markers.Clear();
            model = bp != null ? UnitAssembler.SpawnVisual(bp, db, pivot) : null;
            animator = model != null ? model.GetComponent<UnitAnimator>() : null;
            if (model == null) { shownBlueprint = null; return; }
            animator.UnscaledTime = true;
            ShowOffNewParts(bp);

            var body = db.GetBody(bp.bodyId);
            foreach (var slot in body.slots)
            {
                var m = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                m.name = $"Slot {slot.slotId}";
                m.transform.SetParent(model.transform, false);
                m.transform.localPosition = slot.localPosition;
                m.transform.localScale = Vector3.one * markerSize;
                var r = m.GetComponent<Renderer>();
                r.sharedMaterial = markerMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                // A generous hit area so the part sitting on the slot can be clicked too.
                ((SphereCollider)m.GetComponent<Collider>()).radius = 2f;
                m.AddComponent<SlotMarker>().slotId = slot.slotId;
                markers[slot.slotId] = r;
            }
            Highlight(bp, selectedSlotId);
            Frame();
        }

        /// <summary>When a part is put on the unit being shown, the unit tries it out straight away.</summary>
        void ShowOffNewParts(UnitBlueprint bp)
        {
            bool sameUnit = bp == shownBlueprint;
            string changed = null;
            foreach (var a in bp.parts)
                if (sameUnit && (!shownParts.TryGetValue(a.slotId, out var old) || old != a.partId))
                    changed = a.slotId;
            shownBlueprint = bp;
            shownParts.Clear();
            foreach (var a in bp.parts) shownParts[a.slotId] = a.partId;

            if (changed != null && animator.Flourish(changed))
                nextShowOff = Time.unscaledTime + showOffInterval;
            else if (!sameUnit)
                nextShowOff = Time.unscaledTime + 1.2f;
        }

        void Highlight(UnitBlueprint bp, string selectedSlotId)
        {
            foreach (var (slotId, r) in markers)
            {
                bool selected = slotId == selectedSlotId;
                bool empty = string.IsNullOrEmpty(bp.GetPartIn(slotId));
                r.enabled = selected || empty;
                block.SetColor("_Color", selected ? selectedSlotColor : emptySlotColor);
                r.SetPropertyBlock(block);
                r.transform.localScale = Vector3.one * (selected ? markerSize * 1.4f : markerSize);
            }
        }

        void Frame()
        {
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) { focus = pivot.position; return; }
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            // Bounds change as the model spins; a sphere around them keeps the framing steady.
            focus = new Vector3(pivot.position.x, bounds.center.y, pivot.position.z);
            float radius = Mathf.Max(bounds.extents.magnitude, 0.5f);
            float fov = previewCamera != null ? previewCamera.fieldOfView : 30f;
            distance = radius / Mathf.Sin(fov * 0.5f * Mathf.Deg2Rad) * 0.8f;
        }

        /// <summary>Returns the slot under a screen position, or null.</summary>
        public string PickSlot(Vector2 screenPos)
        {
            if (previewCamera == null || model == null) return null;
            var ray = previewCamera.ScreenPointToRay(screenPos);
            return Physics.Raycast(ray, out var hit, 100f) && hit.collider.TryGetComponent<SlotMarker>(out var m)
                ? m.slotId
                : null;
        }
    }
}
