using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Atthree.Events.Editor
{
    /// <summary>
    /// Window > Atthree > Event Monitor: Play modunda yayınlanan her olayı (EventBus + kanallar) zaman sırasıyla listeler,
    /// içeriğini ve kaç dinleyiciye ulaştığını gösterir. "Olay neden çalışmadı?" sorusunun cevabı burada.
    /// </summary>
    public class EventMonitorWindow : EditorWindow
    {
        struct Record
        {
            public int frame;
            public float time;
            public string source;   // "Bus" ya da kanal adı
            public string type;
            public string payload;
            public int listeners;
            public UnityEngine.Object context;
        }

        const int MaxRecords = 500;

        static readonly List<Record> records = new List<Record>();
        static bool paused;

        string filter = "";
        bool showBus = true, showChannels = true, onlyUnheard;
        Vector2 scroll, countsScroll;
        int selected = -1;
        bool autoScroll = true;

        [MenuItem("Window/Atthree/Event Monitor")]
        public static void Open() => GetWindow<EventMonitorWindow>("Event Monitor");

        [InitializeOnLoadMethod]
        static void OnEditorLoad() => Rehook();

        // Paket statikleri Play başında (SubsystemRegistration) sıfırlanıyor; ilk sahne yüklenmeden yeniden bağlan.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void OnPlayStart()
        {
            records.Clear();
            Rehook();
        }

        static void Rehook()
        {
            EventBus.Published -= OnBus;
            EventBus.Published += OnBus;
            EventChannelBase.AnyRaised -= OnChannel;
            EventChannelBase.AnyRaised += OnChannel;
        }

        static void OnBus(Type type, object evt, int listeners) =>
            Add("Bus", Nice(type), evt, listeners, null);

        static void OnChannel(EventChannelBase channel, object value, int listeners) =>
            Add(channel.name, Nice(channel.GetType()), value, listeners, channel);

        static void Add(string source, string type, object payload, int listeners, UnityEngine.Object ctx)
        {
            if (paused || openWindows == 0) return;
            records.Add(new Record
            {
                frame = Time.frameCount,
                time = Time.time,
                source = source,
                type = type,
                payload = Describe(payload),
                listeners = listeners,
                context = ctx,
            });
            if (records.Count > MaxRecords) records.RemoveRange(0, records.Count - MaxRecords);
        }

        static int openWindows; // pencere kapalıyken kayıt tutma (bedava olsun)

        void OnEnable() => openWindows++;
        void OnDisable() => openWindows--;

        static string Nice(Type t)
        {
            if (!t.IsGenericType) return t.Name;
            return t.Name.Substring(0, t.Name.IndexOf('`')) + "<" + string.Join(", ", t.GetGenericArguments().Select(Nice)) + ">";
        }

        static string Describe(object o)
        {
            if (o == null) return "(void)";
            if (o is UnityEngine.Object uo) return uo ? uo.name : "(destroyed)";
            var t = o.GetType();
            if (t.IsPrimitive || o is string || o is Vector3 || o is Vector2) return o.ToString();
            try
            {
                var json = JsonUtility.ToJson(o);
                if (!string.IsNullOrEmpty(json) && json != "{}") return json;
            }
            catch { /* JsonUtility desteklemiyor */ }
            return o.ToString();
        }

        void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(50))) { records.Clear(); selected = -1; }
                paused = GUILayout.Toggle(paused, "Pause", EditorStyles.toolbarButton, GUILayout.Width(50));
                showBus = GUILayout.Toggle(showBus, "Bus", EditorStyles.toolbarButton, GUILayout.Width(40));
                showChannels = GUILayout.Toggle(showChannels, "Channels", EditorStyles.toolbarButton, GUILayout.Width(70));
                onlyUnheard = GUILayout.Toggle(onlyUnheard, new GUIContent("No listener", "Sadece hiç dinleyicisi olmayan yayınlar"), EditorStyles.toolbarButton, GUILayout.Width(80));
                autoScroll = GUILayout.Toggle(autoScroll, "Auto-scroll", EditorStyles.toolbarButton, GUILayout.Width(80));
                GUILayout.FlexibleSpace();
                filter = GUILayout.TextField(filter, EditorStyles.toolbarSearchField, GUILayout.Width(180));
            }

            if (!Application.isPlaying && records.Count == 0)
            {
                EditorGUILayout.HelpBox("Play moduna gir: EventBus.Publish ve kanal Raise çağrıları burada görünür.", MessageType.Info);
            }

            DrawRecords();
            DrawSubscriberCounts();
        }

        void DrawRecords()
        {
            var header = EditorStyles.boldLabel;
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Frame", header, GUILayout.Width(55));
                GUILayout.Label("Time", header, GUILayout.Width(55));
                GUILayout.Label("Source", header, GUILayout.Width(110));
                GUILayout.Label("Type", header, GUILayout.Width(150));
                GUILayout.Label("Heard", header, GUILayout.Width(45));
                GUILayout.Label("Payload", header);
            }

            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.ExpandHeight(true));
            for (int i = 0; i < records.Count; i++)
            {
                var r = records[i];
                bool isBus = r.source == "Bus";
                if (isBus && !showBus || !isBus && !showChannels) continue;
                if (onlyUnheard && r.listeners > 0) continue;
                if (filter.Length > 0 &&
                    r.type.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0 &&
                    r.source.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0 &&
                    r.payload.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;

                var prev = GUI.color;
                if (r.listeners == 0) GUI.color = new Color(1f, 0.75f, 0.4f); // kimse dinlemiyor: dikkat
                if (i == selected) GUI.color = new Color(0.6f, 0.85f, 1f);
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    GUILayout.Label(r.frame.ToString(), GUILayout.Width(55));
                    GUILayout.Label(r.time.ToString("0.00"), GUILayout.Width(55));
                    GUILayout.Label(r.source, GUILayout.Width(110));
                    GUILayout.Label(r.type, GUILayout.Width(150));
                    GUILayout.Label(r.listeners.ToString(), GUILayout.Width(45));
                    GUILayout.Label(r.payload, EditorStyles.wordWrappedMiniLabel);
                }
                GUI.color = prev;

                var rect = GUILayoutUtility.GetLastRect();
                if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
                {
                    selected = i;
                    if (r.context != null) EditorGUIUtility.PingObject(r.context);
                    Event.current.Use();
                }
            }
            if (autoScroll && !paused && Event.current.type == EventType.Repaint) scroll.y = float.MaxValue;
            EditorGUILayout.EndScrollView();
        }

        void DrawSubscriberCounts()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("EventBus aboneleri", EditorStyles.boldLabel);
            var counts = EventBus.GetSubscriberCounts().Where(kv => kv.Value > 0).OrderBy(kv => kv.Key.Name).ToList();
            countsScroll = EditorGUILayout.BeginScrollView(countsScroll, GUILayout.Height(Mathf.Clamp(counts.Count * 18 + 6, 24, 120)));
            if (counts.Count == 0) EditorGUILayout.LabelField("(yok)", EditorStyles.miniLabel);
            foreach (var kv in counts) EditorGUILayout.LabelField(Nice(kv.Key), kv.Value.ToString());
            EditorGUILayout.EndScrollView();
        }

        void Update()
        {
            if (Application.isPlaying) Repaint(); // abone sayıları canlı kalsın
        }
    }
}
