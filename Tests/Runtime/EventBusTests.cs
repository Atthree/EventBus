using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Atthree.Events.Tests
{
    public class EventBusTests
    {
        struct Ping { public int value; }
        struct Pong { }

        class Receiver : MonoBehaviour
        {
            public int count;
            public void OnPing(Ping p) => count++;
        }

        readonly List<Object> cleanup = new List<Object>();

        [SetUp] public void SetUp() => EventBus.Clear();

        [TearDown]
        public void TearDown()
        {
            EventBus.Clear();
            foreach (var o in cleanup) if (o != null) Object.DestroyImmediate(o);
            cleanup.Clear();
        }

        T Make<T>(T o) where T : Object { cleanup.Add(o); return o; }

        // ------------------------------------------------------------------ bus

        [Test]
        public void Publish_DeliversPayload()
        {
            int got = 0;
            EventBus.Subscribe<Ping>(p => got = p.value);
            EventBus.Publish(new Ping { value = 7 });
            Assert.AreEqual(7, got);
        }

        [Test]
        public void Publish_WithoutSubscribers_DoesNothing()
        {
            Assert.DoesNotThrow(() => EventBus.Publish(new Pong()));
            Assert.AreEqual(0, EventBus.SubscriberCount<Pong>());
        }

        [Test]
        public void Types_AreIsolated()
        {
            int pings = 0, pongs = 0;
            EventBus.Subscribe<Ping>(_ => pings++);
            EventBus.Subscribe<Pong>(_ => pongs++);
            EventBus.Publish(new Ping());
            Assert.AreEqual(1, pings);
            Assert.AreEqual(0, pongs);
        }

        [Test]
        public void SameHandler_TwiceCountsOnce()
        {
            int n = 0;
            Action<Ping> h = _ => n++;
            EventBus.Subscribe(h);
            EventBus.Subscribe(h);
            EventBus.Publish(new Ping());
            Assert.AreEqual(1, n);
            Assert.AreEqual(1, EventBus.SubscriberCount<Ping>());
        }

        [Test]
        public void Unsubscribe_Stops()
        {
            int n = 0;
            Action<Ping> h = _ => n++;
            EventBus.Subscribe(h);
            EventBus.Unsubscribe(h);
            EventBus.Publish(new Ping());
            Assert.AreEqual(0, n);
            Assert.AreEqual(0, EventBus.SubscriberCount<Ping>());
        }

        [Test]
        public void SubscriptionDispose_Stops_AndIsIdempotent()
        {
            int n = 0;
            var sub = EventBus.Subscribe<Ping>(_ => n++);
            Assert.IsTrue(sub.IsActive);
            sub.Dispose();
            sub.Dispose();
            default(Subscription).Dispose(); // boş jeton da güvenli
            EventBus.Publish(new Ping());
            Assert.AreEqual(0, n);
            Assert.IsFalse(sub.IsActive);
        }

        [Test]
        public void ThrowingHandler_DoesNotStopOthers()
        {
            int n = 0;
            EventBus.Subscribe<Ping>(_ => throw new InvalidOperationException("boom"));
            EventBus.Subscribe<Ping>(_ => n++);
            LogAssert.Expect(LogType.Exception, new Regex("boom"));
            EventBus.Publish(new Ping());
            Assert.AreEqual(1, n);
        }

        [Test]
        public void UnsubscribeDuringPublish_SkipsRemovedHandler()
        {
            int second = 0;
            Action<Ping> h2 = _ => second++;
            EventBus.Subscribe<Ping>(_ => EventBus.Unsubscribe(h2));
            EventBus.Subscribe(h2);
            EventBus.Publish(new Ping());
            Assert.AreEqual(0, second, "çıkarılan dinleyici aynı yayında çağrılmamalı");
        }

        [Test]
        public void SubscribeDuringPublish_StartsFromNextPublish()
        {
            int late = 0;
            bool added = false;
            EventBus.Subscribe<Ping>(_ =>
            {
                if (added) return;
                added = true;
                EventBus.Subscribe<Ping>(__ => late++);
            });
            EventBus.Publish(new Ping());
            Assert.AreEqual(0, late);
            EventBus.Publish(new Ping());
            Assert.AreEqual(1, late);
        }

        [Test]
        public void SubscribeOnce_FiresOnlyOnce()
        {
            int n = 0;
            EventBus.SubscribeOnce<Ping>(_ => n++);
            EventBus.Publish(new Ping());
            EventBus.Publish(new Ping());
            Assert.AreEqual(1, n);
            Assert.AreEqual(0, EventBus.SubscriberCount<Ping>());
        }

        [UnityTest]
        public IEnumerator OwnerDestroyed_LambdaIsDropped()
        {
            var go = Make(new GameObject("owner"));
            int n = 0;
            var sub = go.Subscribe<Ping>(_ => n++);
            EventBus.Publish(new Ping());
            Assert.AreEqual(1, n);

            Object.Destroy(go);
            yield return null;

            Assert.IsFalse(sub.IsActive);
            EventBus.Publish(new Ping());
            Assert.AreEqual(1, n, "yok edilmiş sahibin dinleyicisi çağrılmamalı");
            Assert.AreEqual(0, EventBus.SubscriberCount<Ping>(), "temizlenmiş olmalı");
        }

        [UnityTest]
        public IEnumerator MonoBehaviourMethod_IsAutoOwned()
        {
            var r = Make(new GameObject("r")).AddComponent<Receiver>();
            EventBus.Subscribe<Ping>(r.OnPing); // sahip verilmedi, metot zaten r'ye ait
            EventBus.Publish(new Ping());
            Assert.AreEqual(1, r.count);

            Object.Destroy(r.gameObject);
            yield return null;
            Assert.DoesNotThrow(() => EventBus.Publish(new Ping()));
            Assert.AreEqual(0, EventBus.SubscriberCount<Ping>());
        }

        [Test]
        public void UnsubscribeAll_RemovesOnlyThatOwner()
        {
            var a = Make(new GameObject("a"));
            var b = Make(new GameObject("b"));
            int na = 0, nb = 0;
            a.Subscribe<Ping>(_ => na++);
            a.Subscribe<Pong>(_ => na++);
            b.Subscribe<Ping>(_ => nb++);

            EventBus.UnsubscribeAll(a);
            EventBus.Publish(new Ping());
            EventBus.Publish(new Pong());
            Assert.AreEqual(0, na);
            Assert.AreEqual(1, nb);
        }

        [Test]
        public void Published_DebugHook_ReportsListenerCount()
        {
            Type seenType = null;
            int seenCount = -1;
            Action<Type, object, int> hook = (t, _, c) => { seenType = t; seenCount = c; };
            EventBus.Published += hook;
            try
            {
                EventBus.Subscribe<Ping>(_ => { });
                EventBus.Subscribe<Ping>(_ => { });
                EventBus.Publish(new Ping());
            }
            finally { EventBus.Published -= hook; }
            Assert.AreEqual(typeof(Ping), seenType);
            Assert.AreEqual(2, seenCount);
        }

        // ------------------------------------------------------------------ channels

        [Test]
        public void Channel_RaiseCallsListeners_AndStoresLastValue()
        {
            var ch = Make(ScriptableObject.CreateInstance<IntEventChannel>());
            int got = 0;
            ch.AddListener(v => got = v);
            ch.Raise(42);
            Assert.AreEqual(42, got);
            Assert.AreEqual(42, ch.LastValue);
        }

        [Test]
        public void Channel_ThrowingListener_DoesNotStopOthers()
        {
            var ch = Make(ScriptableObject.CreateInstance<VoidEventChannel>());
            int n = 0;
            ch.AddListener(() => throw new Exception("kaboom"));
            ch.AddListener(() => n++);
            LogAssert.Expect(LogType.Exception, new Regex("kaboom"));
            ch.Raise();
            Assert.AreEqual(1, n);
        }

        [Test]
        public void Channel_RemoveDuringRaise_SkipsRemoved()
        {
            var ch = Make(ScriptableObject.CreateInstance<FloatEventChannel>());
            int n = 0;
            Action<float> second = _ => n++;
            ch.AddListener(_ => ch.RemoveListener(second));
            ch.AddListener(second);
            ch.Raise(1f);
            Assert.AreEqual(0, n);
            Assert.AreEqual(1, ch.ListenerCount);
        }

        [Test]
        public void ChannelListener_InvokesUnityEvent_OnlyWhileEnabled()
        {
            var ch = Make(ScriptableObject.CreateInstance<StringEventChannel>());
            var go = Make(new GameObject("listener"));
            go.SetActive(false);
            var listener = go.AddComponent<StringEventListener>();
            listener.Channel = ch;
            string got = null;
            listener.Response.AddListener(s => got = s);

            go.SetActive(true);
            Assert.AreEqual(1, ch.ListenerCount);
            ch.Raise("merhaba");
            Assert.AreEqual("merhaba", got);

            go.SetActive(false);
            Assert.AreEqual(0, ch.ListenerCount);
            ch.Raise("yok");
            Assert.AreEqual("merhaba", got);
        }

        [Test]
        public void ChannelListener_SwappingChannel_MovesSubscription()
        {
            var a = Make(ScriptableObject.CreateInstance<IntEventChannel>());
            var b = Make(ScriptableObject.CreateInstance<IntEventChannel>());
            var listener = Make(new GameObject("l")).AddComponent<IntEventListener>();
            listener.Channel = a;
            Assert.AreEqual(1, a.ListenerCount);
            listener.Channel = b;
            Assert.AreEqual(0, a.ListenerCount);
            Assert.AreEqual(1, b.ListenerCount);
        }
    }
}
