using UnityEngine;

namespace Atthree.Events
{
    [CreateAssetMenu(menuName = "Atthree/Events/GameObject Channel", fileName = "OnGameObjectEvent", order = 6)]
    public class GameObjectEventChannel : EventChannel<GameObject> { }
}
