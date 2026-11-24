using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Atthree.Events
{
    /// <summary>
    /// ScriptableObject olay kanallarının ortak tabanı. Kanal bir asset'tir: yayınlayan ve dinleyen
    /// birbirini değil, aynı asset'i Inspector'dan referans alır. Tasarımcı kod yazmadan bağlantı kurabilir.
    /// </summary>
    public abstract class EventChannelBase : ScriptableObject
    {
        [TextArea(2, 4)]
        [Tooltip("Bu kanal ne zaman yayınlanır? Sadece not, oyunu etkilemez.")]
        public string description;

        [Tooltip("Her yayında Console'a yaz (hata ayıklama).")]
        public bool logRaises;

        /// <summary>Hata ayıklama: herhangi bir kanal yayınlandığında (kanal, değer, dinleyici sayısı).</summary>
        public static event Action<EventChannelBase, object, int> AnyRaised;

        public abstract int ListenerCount { get; }

        /// <summary>Inspector'daki "Raise" düğmesi için: test değeriyle yayınla.</summary>
        public abstract void RaiseDebug();

        /// <summary>Tüm dinleyicileri sil.</summary>
        public abstract void ClearListeners();

        protected void Report(object value, int listeners)
        {
            if (logRaises) Debug.Log($"[EventChannel] {name} → {value ?? "(void)"} ({listeners} dinleyici)", this);
            AnyRaised?.Invoke(this, value, listeners);
        }

        /// <summary>Dinleyicileri sırayla çağırır; biri hata atarsa diğerleri yine çalışır.</summary>
        protected static void InvokeAll<TAction>(List<TAction> listeners, Action<TAction> call, UnityEngine.Object context)
        {
            if (listeners.Count == 0) return;
            // Anlık kopya: dinleyici çağrılırken listeye ekleme/çıkarma güvenli.
            var snapshot = listeners.ToArray();
            foreach (var l in snapshot)
            {
                if (!listeners.Contains(l)) continue; // bu yayın sırasında çıkmış
                try { call(l); }
                catch (Exception e) { Debug.LogException(e, context); }
            }
        }

#if UNITY_EDITOR
        // Domain reload kapalıyken asset'teki dinleyiciler bir Play'den diğerine taşınmasın.
        protected virtual void OnEnable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        protected virtual void OnDisable() => EditorApplication.playModeStateChanged -= OnPlayModeChanged;

        void OnPlayModeChanged(PlayModeStateChange s)
        {
            if (s == PlayModeStateChange.EnteredEditMode) ClearListeners();
        }
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => AnyRaised = null;
    }

    /// <summary>Değer taşıyan kanal. Kendi tipin için: <c>public class DamageChannel : EventChannel&lt;DamageData&gt; {}</c></summary>
    public abstract class EventChannel<T> : EventChannelBase
    {
        [Tooltip("Inspector'daki Raise düğmesinin yayınlayacağı değer.")]
        [SerializeField] T debugValue;

        readonly List<Action<T>> listeners = new List<Action<T>>();

        public override int ListenerCount => listeners.Count;

        /// <summary>Son yayınlanan değer (hiç yayın yoksa default).</summary>
        public T LastValue { get; private set; }

        public void Raise(T value)
        {
            LastValue = value;
            Report(value, listeners.Count);
            InvokeAll(listeners, l => l(value), this);
        }

        public void AddListener(Action<T> listener)
        {
            if (listener != null && !listeners.Contains(listener)) listeners.Add(listener);
        }

        public void RemoveListener(Action<T> listener) => listeners.Remove(listener);

        public override void ClearListeners() => listeners.Clear();

        public override void RaiseDebug() => Raise(debugValue);
    }
}
