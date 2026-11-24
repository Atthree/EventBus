using UnityEngine;

namespace Atthree.Events
{
    [CreateAssetMenu(menuName = "Atthree/Events/String Channel", fileName = "OnStringEvent", order = 4)]
    public class StringEventChannel : EventChannel<string> { }
}
