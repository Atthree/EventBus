using UnityEngine;
using UnityEngine.Events;

namespace Atthree.Events
{
    /// <summary>
    /// Kanalı dinleyip Inspector'daki UnityEvent'i çalıştırır: kod yazmadan "kanal yayınlanınca şunu yap".
    /// Kendi kanal tipin için: <c>public class DamageListener : EventChannelListener&lt;DamageData, DamageChannel&gt; {}</c>
    /// </summary>
    public abstract class EventChannelListener<T, TChannel> : MonoBehaviour where TChannel : EventChannel<T>
    {
        [SerializeField] TChannel channel;
        [SerializeField] UnityEvent<T> response = new UnityEvent<T>();

        public TChannel Channel
        {
            get => channel;
            set
            {
                if (isActiveAndEnabled && channel != null) channel.RemoveListener(OnRaised);
                channel = value;
                if (isActiveAndEnabled && channel != null) channel.AddListener(OnRaised);
            }
        }

        public UnityEvent<T> Response => response;

        protected virtual void OnEnable() { if (channel != null) channel.AddListener(OnRaised); }
        protected virtual void OnDisable() { if (channel != null) channel.RemoveListener(OnRaised); }

        protected virtual void OnRaised(T value) => response.Invoke(value);
    }
}
