using System;
using System.Collections.Generic;
using UnityEngine;

namespace Atthree.Events
{
    /// <summary>Değersiz kanal: "oyun duraklatıldı", "seviye bitti" gibi sadece "oldu" bilgisi.</summary>
    [CreateAssetMenu(menuName = "Atthree/Events/Void Channel", fileName = "OnSomething", order = 0)]
    public class VoidEventChannel : EventChannelBase
    {
        readonly List<Action> listeners = new List<Action>();

        public override int ListenerCount => listeners.Count;

        public void Raise()
        {
            Report(null, listeners.Count);
            InvokeAll(listeners, l => l(), this);
        }

        public void AddListener(Action listener)
        {
            if (listener != null && !listeners.Contains(listener)) listeners.Add(listener);
        }

        public void RemoveListener(Action listener) => listeners.Remove(listener);

        public override void ClearListeners() => listeners.Clear();

        public override void RaiseDebug() => Raise();
    }
}
