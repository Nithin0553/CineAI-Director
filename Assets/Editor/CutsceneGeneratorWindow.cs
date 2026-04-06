// Assets/Editor/CutsceneGeneratorWindow.cs
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public class CutsceneGeneratorWindow : EditorWindow
{
    private CutsceneCompiler compiler;
    private CutsceneRunner runner;

    [MenuItem("CineAI/Generate Cutscene")]
    public static void ShowWindow()
    {
        GetWindow<CutsceneGeneratorWindow>("Cutscene Generator");
    }

    private void OnGUI()
    {
        GUILayout.Space(10);
        GUILayout.Label("CineAI Cutscene Generator", EditorStyles.boldLabel);
        GUILayout.Space(10);

        compiler = EditorGUILayout.ObjectField("Cutscene Compiler", compiler, typeof(CutsceneCompiler), true) as CutsceneCompiler;
        runner = EditorGUILayout.ObjectField("Cutscene Runner", runner, typeof(CutsceneRunner), true) as CutsceneRunner;

        GUILayout.Space(10);

        if (GUILayout.Button("Generate Cutscene", GUILayout.Height(35)))
        {
            if (compiler == null)
            {
                Debug.LogError("Assign a CutsceneCompiler in the window.");
            }
            else
            {
                compiler.GenerateCutscene();
                Debug.Log("✅ Cutscene generation finished.");
            }
        }

        if (GUILayout.Button("Generate + Play", GUILayout.Height(35)))
        {
            if (compiler == null)
            {
                Debug.LogError("Assign a CutsceneCompiler in the window.");
            }
            else
            {
                compiler.GenerateCutscene();

                if (runner != null)
                    runner.Play();
                else
                    Debug.LogWarning("Cutscene generated, but no CutsceneRunner assigned for playback.");
            }
        }
    }
}
#endif