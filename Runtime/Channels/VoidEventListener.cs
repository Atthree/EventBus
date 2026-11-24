using UnityEngine;
using UnityEngine.Events;

namespace Atthree.Events
{
    [AddComponentMenu("Atthree/Events/Void Event Listener")]
    public class VoidEventListener : MonoBehaviour
    {
        [SerializeField] VoidEventChannel channel;
        [SerializeField] UnityEvent response = new UnityEvent();

        public VoidEventChannel Channel
        {
            get => channel;
            set
            {
                if (isActiveAndEnabled && channel != null) channel.RemoveListener(OnRaised);
                channel = value;
                if (isActiveAndEnabled && channel != null) channel.AddListener(OnRaised);
            }
        }

        public UnityEvent Response => response;

        void OnEnable() { if (channel != null) channel.AddListener(OnRaised); }
        void OnDisable() { if (channel != null) channel.RemoveListener(OnRaised); }

        void OnRaised() => response.Invoke();
    }
}
