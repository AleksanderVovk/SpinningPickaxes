#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using HayPileSystem;

[CustomEditor(typeof(HayPileGenerator))]
public sealed class HayPileGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GUILayout.Space(10);

        HayPileGenerator generator = (HayPileGenerator)target;

        if (GUILayout.Button("GENERATE HAY PILE", GUILayout.Height(35)))
        {
            Undo.RegisterFullObjectHierarchyUndo(generator.gameObject, "Generate Hay Pile");
            generator.Generate();
            EditorUtility.SetDirty(generator);
        }

        if (GUILayout.Button("CLEAR HAY PILE"))
        {
            Undo.RegisterFullObjectHierarchyUndo(generator.gameObject, "Clear Hay Pile");
            generator.Clear();
            EditorUtility.SetDirty(generator);
        }

        EditorGUILayout.HelpBox(
            "Для красивого результата начни с 1200–2500 кусков. " +
            "Для более плотного стога увеличивай количество, а не масштаб отдельных кусков.",
            MessageType.Info
        );
    }
}
#endif
