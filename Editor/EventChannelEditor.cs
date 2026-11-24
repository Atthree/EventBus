using UnityEditor;
using UnityEngine;

namespace Atthree.Events.Editor
{
    /// <summary>Kanal asset'ine Play modunda "Raise" düğmesi ve canlı dinleyici sayısı ekler.</summary>
    [CustomEditor(typeof(EventChannelBase), true)]
    public class EventChannelEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var channel = (EventChannelBase)target;
            EditorGUILayout.Space();
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (!Application.isPlaying)
                {
                    EditorGUILayout.LabelField("Play modunda: dinleyici sayısı ve test için Raise düğmesi.", EditorStyles.miniLabel);
                    return;
                }
                EditorGUILayout.LabelField("Dinleyici", channel.ListenerCount.ToString());
                if (GUILayout.Button("Raise (Debug Value ile)")) channel.RaiseDebug();
            }
        }

        public override bool RequiresConstantRepaint() => Application.isPlaying;
    }
}
