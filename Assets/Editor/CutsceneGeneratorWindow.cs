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

        compiler = EditorGUILayout.ObjectField(
            "Cutscene Compiler", compiler, typeof(CutsceneCompiler), true) as CutsceneCompiler;

        runner = EditorGUILayout.ObjectField(
            "Cutscene Runner", runner, typeof(CutsceneRunner), true) as CutsceneRunner;

        GUILayout.Space(10);

        if (GUILayout.Button("Generate Cutscene", GUILayout.Height(35)))
        {
            if (compiler == null)
                Debug.LogError("❌ Assign a CutsceneCompiler in the window.");
            else
                SafeGenerate(playAfter: false);
        }

        if (GUILayout.Button("Generate + Play", GUILayout.Height(35)))
        {
            if (compiler == null)
                Debug.LogError("❌ Assign a CutsceneCompiler in the window.");
            else
                SafeGenerate(playAfter: true);
        }
    }

    /// <summary>
    /// FIX: Defers generation to the next editor frame via EditorApplication.delayCall.
    ///
    /// Why this fixes the NullReferenceException:
    ///   The error occurs because clicking Generate triggers a Timeline window
    ///   repaint/disable mid-frame while SerializedObject bindings are still live.
    ///   Deferring to delayCall lets Unity finish all pending UI binding updates
    ///   and the current frame's repaint before we touch the Timeline asset,
    ///   eliminating the disposal race condition entirely.
    /// </summary>
    private void SafeGenerate(bool playAfter)
    {
        // Capture locals — 'this' may be collected before delayCall fires
        CutsceneCompiler c = compiler;
        CutsceneRunner r = runner;

        EditorApplication.delayCall += () =>
        {
            if (c == null)
            {
                Debug.LogError("❌ CutsceneCompiler was destroyed before generation could run.");
                return;
            }

            try
            {
                c.GenerateCutscene();
                Debug.Log("✅ Cutscene generation finished.");

                if (playAfter)
                {
                    if (r != null)
                        r.Play();
                    else
                        Debug.LogWarning("⚠ Cutscene generated, but no CutsceneRunner assigned.");
                }
            }
            catch (System.Exception ex)
            {
                // Surface the real error clearly instead of letting Unity's
                // internal UI bindings swallow it in a cascade of NullRefs
                Debug.LogError($"❌ Cutscene generation failed: {ex.Message}\n{ex.StackTrace}");
            }
        };
    }
}
#endif