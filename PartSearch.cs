using SFS.Parts;
using SFS.Parts.Modules;
using SFS.Builds;
using SFS.Translations;
using SFS.UI;
using SFS.UI.ModGUI;
using UITools;
using System;
using ModType = SFS.UI.ModGUI.Type;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace AMPartSearch
{
    public class PartSearch : MonoBehaviour
    {
        private ClosableWindow window;
        private GameObject holder;

        private string query = "";
        private Label summaryLabel;
        private List<Transform> permanentUI = new List<Transform>();

        private class IconCache
        {
            public RenderTexture texture;
            public Vector2 size;
        }
        private static Dictionary<string, IconCache> iconCache = new Dictionary<string, IconCache>();
        private static HashSet<string> iconFailed = new HashSet<string>();

        private class IconRequest
        {
            public RawImage slot;
            public Part part;
            public string key;
        }
        private readonly List<IconRequest> iconQueue = new List<IconRequest>();

        private static FieldInfo buttonField;

        private const int MaxRows = 40;
        private const float DragThreshold = 25f;

        private static PartSearch instance;
        private TMPro.TMP_InputField searchField;
        public static bool IsTyping =>
            instance != null && instance.searchField != null && instance.searchField.isFocused;

        private void Awake()
        {
            instance = this;
        }

        private void Update()
        {
            if (holder == null) InitializeUI();

            if (iconQueue.Count > 0)
            {
                IconRequest req = iconQueue[0];
                iconQueue.RemoveAt(0);
                if (req.slot == null) return;
                if (iconFailed.Contains(req.key)) return;

                IconCache entry;
                if (!iconCache.TryGetValue(req.key, out entry))
                {
                    entry = RenderIcon(req.part, req.key);
                    if (entry == null) return;
                }
                if (req.slot != null)
                {
                    req.slot.texture = entry.texture;
                    LayoutIcon(req.slot.rectTransform, entry.size);
                }
            }
        }

        private void InitializeUI()
        {
            holder = Builder.CreateHolder(Builder.SceneToAttach.CurrentScene, "AMPartSearch Holder");

            window = UIToolsBuilder.CreateClosableWindow(
                holder.transform,
                Builder.GetRandomID(),
                380, 440,
                260, 200,
                true, true, 0.95f,
                "Part Search");
            window.RegisterPermanentSaving("AMPartSearch.Window");

            window.CreateLayoutGroup(ModType.Vertical, TextAnchor.UpperCenter, 4f);
            window.EnableScrolling(ModType.Vertical);

            Label header = Builder.CreateLabel(window.ChildrenHolder, 344, 22, 0, 0,
                "Search all parts (name, id, description)");
            permanentUI.Add(header.rectTransform);

            TextInput input = Builder.CreateTextInput(window.ChildrenHolder, 344, 34, 0, 0, "", OnQueryChanged);
            permanentUI.Add(input.rectTransform);
            searchField = input.field;

            summaryLabel = Builder.CreateLabel(window.ChildrenHolder, 344, 20, 0, 0, "");
            permanentUI.Add(summaryLabel.rectTransform);

            if (buttonField == null)
                buttonField = typeof(SFS.UI.ModGUI.Button).GetField("_button",
                    BindingFlags.NonPublic | BindingFlags.Instance);

            Refresh();
        }

        private void OnQueryChanged(string text)
        {
            query = text;
            Refresh();
        }

        private void Refresh()
        {
            if (window == null) return;

            iconQueue.Clear();

            for (int i = window.ChildrenHolder.childCount - 1; i >= 0; i--)
            {
                Transform child = window.ChildrenHolder.GetChild(i);
                if (!permanentUI.Contains(child))
                    UnityEngine.Object.Destroy(child.gameObject);
            }

            List<Part> parts = CollectParts();
            if (parts.Count == 0)
            {
                SetSummary("no parts loaded yet - re-enter the build scene");
                return;
            }

            string q = (query ?? "").Trim().ToLowerInvariant();
            if (q.Length == 0)
            {
                SetSummary(parts.Count + " parts loaded - type to search");
                return;
            }

            List<Part> starts = new List<Part>();
            List<Part> contains = new List<Part>();
            foreach (var p in parts)
            {
                string name = SafeName(p).ToLowerInvariant();
                string desc = SafeDesc(p).ToLowerInvariant();
                if (name.StartsWith(q)) starts.Add(p);
                else if (name.Contains(q) || desc.Contains(q)) contains.Add(p);
            }
            starts.Sort((a, b) => string.CompareOrdinal(SafeName(a), SafeName(b)));
            contains.Sort((a, b) => string.CompareOrdinal(SafeName(a), SafeName(b)));

            List<Part> results = new List<Part>(starts);
            results.AddRange(contains);

            SetSummary(results.Count + " match" + (results.Count == 1 ? "" : "es") +
                       " for \"" + query + "\"" +
                       (results.Count > MaxRows ? " (showing first " + MaxRows + ")" : ""));

            int shown = 0;
            foreach (var p in results)
            {
                if (shown >= MaxRows) break;
                AddRow(p);
                shown++;
            }
        }

        private void AddRow(Part p)
        {
            string display = SafeName(p);
            if (display.Length > 34) display = display.Substring(0, 34) + "...";

            SFS.UI.ModGUI.Button row = Builder.CreateButton(window.ChildrenHolder, 344, 32, 0, 0,
                () => ShowDetails(p), "  " + display);

            AddIcon(row.rectTransform, p);

            SFS.UI.ButtonPC pc = buttonField != null ?
                buttonField.GetValue(row) as SFS.UI.ButtonPC : null;
            if (pc != null && pc.ButtonText != null)
                pc.ButtonText.rectTransform.offsetMin = new Vector2(34f, 0f);

            if (pc != null) WireDrag(pc, p);
        }

        private void AddIcon(Transform row, Part p)
        {
            string key = SafeId(p);

            GameObject iconGO = new GameObject("Icon", typeof(RectTransform));
            iconGO.transform.SetParent(row, false);
            RawImage raw = iconGO.AddComponent<RawImage>();
            raw.raycastTarget = false;

            RectTransform rt = raw.rectTransform;
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(4f, 0f);

            IconCache entry;
            if (iconCache.TryGetValue(key, out entry) && entry.texture != null)
            {
                raw.texture = entry.texture;
                LayoutIcon(rt, entry.size);
            }
            else if (!iconFailed.Contains(key))
            {
                IconRequest req = new IconRequest { slot = raw, part = p, key = key };
                iconQueue.Add(req);
            }
        }

        private static void LayoutIcon(RectTransform rt, Vector2 size)
        {
            float h = 26f;
            float w = size.y > 0.001f ? h * (size.x / size.y) : h;
            w = Mathf.Clamp(w, 10f, 80f);
            rt.sizeDelta = new Vector2(w, h);
        }

        private void WireDrag(SFS.UI.ButtonPC pc, Part p)
        {
            Vector2 start = Vector2.zero;
            bool taken = false;

            Action<SFS.Input.OnInputStartData> down = (SFS.Input.OnInputStartData d) =>
            {
                start = d.position.pixel;
                taken = false;
            };

            Action<SFS.Input.OnInputStayData> hold = (SFS.Input.OnInputStayData d) =>
            {
                if (taken) return;
                Vector2 delta = start - d.position.pixel;
                if (delta.magnitude <= DragThreshold) return;
                taken = true;
                if (BuildManager.main != null && BuildManager.main.holdGrid != null)
                {
                    VariantRef variant = new VariantRef(p, 0, 0);
                    BuildManager.main.holdGrid.TakePart_PickGrid(variant, d.position.World(0f));
                }
            };

            pc.onDown += down;
            pc.onHold += hold;
        }

        private void ShowDetails(Part p)
        {
            if (MsgDrawer.main != null)
                MsgDrawer.main.Log(SafeName(p) + " [" + SafeId(p) + "]\n" + SafeDesc(p));
        }

        private IconCache RenderIcon(Part p, string key)
        {
            if (PartIconCreator.main == null)
                return null;

            Part temp = null;
            RenderTexture tex = null;
            Vector2 size = Vector2.zero;
            try
            {

                int a = 0, b = 0;
                try
                {
                    if (p.variants == null || p.variants.Length == 0 ||
                        p.variants[0] == null || p.variants[0].variants == null ||
                        p.variants[0].variants.Length == 0)
                    { a = -1; b = -1; }
                }
                catch { a = -1; b = -1; }

                temp = PartsLoader.CreatePart(new VariantRef(p, a, b), true);
                if (temp == null) { iconFailed.Add(key); return null; }

                if (BuildManager.main != null && BuildManager.main.pickGrid != null &&
                    BuildManager.main.pickGrid.createdPartsHolder != null)
                    temp.transform.parent = BuildManager.main.pickGrid.createdPartsHolder.transform;

                temp.gameObject.SetActive(true);
                tex = PartIconCreator.main.CreatePartIcon_PickGrid(temp, out size);
            }
            catch (Exception e)
            {
                Debug.Log("[AMPartSearch] icon failed for " + key + ": " + e.Message);
            }
            finally
            {

                if (temp != null) UnityEngine.Object.Destroy(temp.gameObject);
            }

            if (tex == null)
            {
                iconFailed.Add(key);
                return null;
            }

            IconCache entry = new IconCache { texture = tex, size = size };
            iconCache[key] = entry;
            return entry;
        }

        private List<Part> CollectParts()
        {
            var seen = new HashSet<Part>();
            var list = new List<Part>();

            try
            {
                if (SFS.Base.partsLoader != null && SFS.Base.partsLoader.parts != null)
                {
                    foreach (var kvp in SFS.Base.partsLoader.parts)
                    {
                        if (kvp.Value != null && seen.Add(kvp.Value))
                            list.Add(kvp.Value);
                    }
                }
            }
            catch { }

            if (list.Count == 0)
            {
                var all = Resources.FindObjectsOfTypeAll<Part>();
                foreach (var p in all)
                    if (p != null && seen.Add(p)) list.Add(p);
            }

            return list;
        }

        private static string SafeName(Part p)
        {
            try
            {
                if (p.displayName != null && p.displayName.Field != null)
                {
                    string s = p.displayName.Field;
                    if (!string.IsNullOrEmpty(s)) return s;
                }
            }
            catch { }
            try { return p.Name; } catch { }
            return "(unnamed)";
        }

        private static string SafeId(Part p)
        {
            try { return p.Name; } catch { }
            return "?";
        }

        private static string SafeDesc(Part p)
        {
            try
            {
                if (p.description != null && p.description.Field != null)
                {
                    string s = p.description.Field;
                    if (!string.IsNullOrEmpty(s)) return s.Replace("\n", " ");
                }
            }
            catch { }
            return "";
        }

        private void SetSummary(string text)
        {
            if (summaryLabel != null) summaryLabel.Text = text;
        }

        private void OnDestroy()
        {
            instance = null;
            iconQueue.Clear();
            if (holder != null) UnityEngine.Object.Destroy(holder);
        }
    }
}
