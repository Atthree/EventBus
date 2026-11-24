using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Atthree.Events
{
    /// <summary>
    /// Tip güvenli olay otobüsü: sistemler birbirini tanımadan haberleşir.
    ///
    ///   public struct PlayerDied { public Vector3 position; }
    ///
    ///   // MonoBehaviour içinde (önerilen): obje yok olunca abonelik kendiliğinden biter
    ///   this.Subscribe&lt;PlayerDied&gt;(e =&gt; Debug.Log(e.position));
    ///
    ///   // Elle yönetim: dönen jetonu Dispose et
    ///   var sub = EventBus.Subscribe&lt;PlayerDied&gt;(OnPlayerDied);
    ///   sub.Dispose();                                   // ya da EventBus.Unsubscribe&lt;PlayerDied&gt;(OnPlayerDied)
    ///
    ///   EventBus.Publish(new PlayerDied { position = p });
    ///
    /// Güvenlik: yayın sırasında abone olma/çıkma güvenlidir, bir dinleyicinin hatası diğerlerini durdurmaz,
    /// yok edilmiş bir objenin dinleyicisi otomatik silinir.
    /// </summary>
    public static class EventBus
    {
        static readonly Dictionary<Type, IChannel> channels = new Dictionary<Type, IChannel>();

        /// <summary>Hata ayıklama: her yayında (tip, olay, dinleyici sayısı). Event Monitor penceresi bunu kullanır.</summary>
        public static event Action<Type, object, int> Published;

        internal interface IChannel
        {
            int Count { get; }
            Entry[] AllEntries();
            void Remove(Entry entry);
            void Clear();
        }

        /// <summary>Tek bir abonelik kaydı. Silinince <see cref="active"/> false olur, böylece süren bir yayın onu atlar.</summary>
        internal abstract class Entry
        {
            public IChannel channel;
            public Object owner;
            public bool hasOwner;
            public bool once;
            public bool active = true;

            public bool OwnerDestroyed => hasOwner && owner == null;
        }

        sealed class Entry<T> : Entry
        {
            public Action<T> handler;
        }

        sealed class Channel<T> : IChannel
        {
            // Kopyala-yaz dizi: yayın sırasında liste değişse bile gezilen dizi değişmez.
            public Entry<T>[] entries = new Entry<T>[0];
            public int Count => entries.Length;

            public Entry[] AllEntries() => entries;

            public void Add(Entry<T> e)
            {
                var arr = new Entry<T>[entries.Length + 1];
                Array.Copy(entries, arr, entries.Length);
                arr[entries.Length] = e;
                entries = arr;
            }

            public void Remove(Entry entry)
            {
                int i = Array.IndexOf(entries, entry);
                if (i < 0) return;
                entry.active = false;
                var arr = new Entry<T>[entries.Length - 1];
                if (i > 0) Array.Copy(entries, 0, arr, 0, i);
                if (i < entries.Length - 1) Array.Copy(entries, i + 1, arr, i, entries.Length - i - 1);
                entries = arr;
            }

            public void Clear()
            {
                foreach (var e in entries) e.active = false;
                entries = new Entry<T>[0];
            }

            public int IndexOf(Action<T> handler)
            {
                for (int i = 0; i < entries.Length; i++)
                    if (entries[i].handler == handler) return i;
                return -1;
            }
        }

        // Domain reload kapalıyken (Enter Play Mode Options) eski abonelikler sızmasın.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Clear();

        static Channel<T> Get<T>()
        {
            if (!channels.TryGetValue(typeof(T), out var c)) channels[typeof(T)] = c = new Channel<T>();
            return (Channel<T>)c;
        }

        // ------------------------------------------------------------------ subscribe

        /// <summary>
        /// Dinlemeye başla. Aynı metodu iki kez eklemek tek abonelik sayılır.
        /// Metot bir MonoBehaviour'a aitse o obje yok olunca abonelik kendiliğinden biter.
        /// </summary>
        public static Subscription Subscribe<T>(Action<T> handler) => Add(handler, null, false);

        /// <summary>
        /// <paramref name="owner"/> yok edilince abonelik kendiliğinden biter. Lambda kullanırken bunu seç:
        /// lambda'nın sahibi derleyicinin oluşturduğu gizli bir sınıftır, Unity objesi değil.
        /// </summary>
        public static Subscription Subscribe<T>(Action<T> handler, Object owner) => Add(handler, owner, false);

        /// <summary>Sadece bir sonraki yayını dinle, sonra kendiliğinden çık.</summary>
        public static Subscription SubscribeOnce<T>(Action<T> handler, Object owner = null) => Add(handler, owner, true);

        static Subscription Add<T>(Action<T> handler, Object owner, bool once)
        {
            if (handler == null) return default;
            var ch = Get<T>();
            int existing = ch.IndexOf(handler);
            if (existing >= 0) return new Subscription(ch.entries[existing]);

            // Sahip verilmediyse ve metot bir Unity objesine aitse onu sahip say (unutulan Unsubscribe'ı yakalar).
            if (owner is null && handler.Target is Object targetObj) owner = targetObj;

            var e = new Entry<T> { handler = handler, channel = ch, owner = owner, hasOwner = !(owner is null), once = once };
            ch.Add(e);
            return new Subscription(e);
        }

        public static void Unsubscribe<T>(Action<T> handler)
        {
            if (handler == null || !channels.TryGetValue(typeof(T), out var c)) return;
            var ch = (Channel<T>)c;
            int i = ch.IndexOf(handler);
            if (i >= 0) ch.Remove(ch.entries[i]);
        }

        /// <summary>Bu sahibin bu tipteki tüm aboneliklerini bırak.</summary>
        public static void UnsubscribeAll<T>(Object owner)
        {
            if (owner is null || !channels.TryGetValue(typeof(T), out var c)) return;
            RemoveOwned(c, owner);
        }

        /// <summary>Bu sahibin tüm tiplerdeki aboneliklerini bırak.</summary>
        public static void UnsubscribeAll(Object owner)
        {
            if (owner is null) return;
            foreach (var ch in channels.Values) RemoveOwned(ch, owner);
        }

        static void RemoveOwned(IChannel ch, Object owner)
        {
            foreach (var e in ch.AllEntries()) // kopyala-yaz: gezerken silmek güvenli
                if (ReferenceEquals(e.owner, owner)) ch.Remove(e);
        }

        // ------------------------------------------------------------------ publish

        public static void Publish<T>(T evt)
        {
            if (!channels.TryGetValue(typeof(T), out var c))
            {
                Published?.Invoke(typeof(T), evt, 0);
                return;
            }
            var ch = (Channel<T>)c;
            var snapshot = ch.entries;
            Published?.Invoke(typeof(T), evt, snapshot.Length);

            foreach (var e in snapshot)
            {
                if (!e.active) continue; // bu yayın sırasında çıkmış
                if (e.OwnerDestroyed)
                {
                    // Yok edilmiş bir objeye ait: unutulmuş abonelik, sessizce temizle.
                    ch.Remove(e);
                    continue;
                }
                if (e.once) ch.Remove(e);
                try { e.handler(evt); }
                catch (Exception ex) { Debug.LogException(ex, e.owner); }
            }
        }

        // ------------------------------------------------------------------ info

        public static int SubscriberCount<T>() => channels.TryGetValue(typeof(T), out var c) ? c.Count : 0;

        /// <summary>Kayıtlı tüm olay tipleri ve dinleyici sayıları (Event Monitor için).</summary>
        public static List<KeyValuePair<Type, int>> GetSubscriberCounts()
        {
            var list = new List<KeyValuePair<Type, int>>(channels.Count);
            foreach (var kv in channels) list.Add(new KeyValuePair<Type, int>(kv.Key, kv.Value.Count));
            return list;
        }

        /// <summary>Tüm abonelikleri siler. Testler ve özel sahne geçişleri için.</summary>
        public static void Clear()
        {
            foreach (var c in channels.Values) c.Clear();
            channels.Clear();
        }
    }

    /// <summary>
    /// Abonelik jetonu. <c>Dispose()</c> aboneliği bitirir; bir listede tutup topluca bırakılabilir.
    /// Birden fazla kez Dispose etmek güvenlidir.
    /// </summary>
    public readonly struct Subscription : IDisposable
    {
        readonly EventBus.Entry entry;

        internal Subscription(EventBus.Entry entry) { this.entry = entry; }

        /// <summary>Abonelik hâlâ yayın alıyor mu?</summary>
        public bool IsActive => entry != null && entry.active && !entry.OwnerDestroyed;

        public void Dispose()
        {
            if (entry != null && entry.active) entry.channel.Remove(entry);
        }
    }

    /// <summary>MonoBehaviour içinden kısa yazım: <c>this.Subscribe&lt;T&gt;(...)</c>, obje yok olunca abonelik biter.</summary>
    public static class EventBusExtensions
    {
        public static Subscription Subscribe<T>(this Object owner, Action<T> handler) => EventBus.Subscribe(handler, owner);

        public static Subscription SubscribeOnce<T>(this Object owner, Action<T> handler) => EventBus.SubscribeOnce(handler, owner);
    }
}
