using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using Unity.CodeEditor;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace ChokeHold.EditorTools
{
    /// <summary>
    /// Fisierele .csproj/.sln sunt generate de Unity pentru IDE (Rider) si contin cai absolute
    /// catre instalarea Unity. Daca proiectul a fost mutat de pe alt calculator (ex. de pe Mac),
    /// caile nu mai exista si IDE-ul arata sute de erori false.
    ///
    /// La incarcarea editorului verificam asta si regeneram automat fisierele.
    /// Manual: CHOKEHOLD > Tools > Regenerate IDE Project Files.
    /// </summary>
    public static class IdeProjectFiles
    {
        [InitializeOnLoadMethod]
        private static void FixStaleProjectFilesOnLoad()
        {
            // delayCall: asteptam ca editorul sa termine incarcarea inainte sa atingem IDE-ul.
            EditorApplication.delayCall += () =>
            {
                if (ProjectFilesAreStale())
                {
                    Regenerate();
                }
            };
        }

        [MenuItem("CHOKEHOLD/Tools/Regenerate IDE Project Files")]
        public static void Regenerate()
        {
            SelectRiderIfNoEditorIsSet();

            if (string.IsNullOrEmpty(CodeEditor.CurrentEditorPath))
            {
                Debug.LogWarning(
                    "Nu este setat niciun editor de cod si Rider nu a fost gasit. " +
                    "Porneste Rider sau alege-l din Edit > Preferences > External Tools, " +
                    "apoi ruleaza CHOKEHOLD > Tools > Regenerate IDE Project Files."
                );
                return;
            }

            CodeEditor.CurrentEditor.SyncAll();

            Debug.Log(
                "Fisierele de proiect pentru IDE au fost regenerate " +
                $"(editor: {CodeEditor.CurrentEditorPath}). Redeschide solutia in Rider."
            );
        }

        private static bool ProjectFilesAreStale()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string csproj = Path.Combine(projectRoot, "Assembly-CSharp.csproj");

            if (!File.Exists(csproj))
            {
                return false; // nu au fost generate inca; Unity le creeaza la prima deschidere a unui script
            }

            // Daca referinta catre UnityEngine arata spre un fisier care nu exista pe acest
            // calculator, fisierele au fost generate in alta parte.
            Match match = Regex.Match(
                File.ReadAllText(csproj),
                @"<HintPath>(.*UnityEngine\.CoreModule\.dll)</HintPath>"
            );

            return match.Success && !File.Exists(match.Groups[1].Value);
        }

        private static void SelectRiderIfNoEditorIsSet()
        {
            string current = CodeEditor.CurrentEditorPath;

            if (!string.IsNullOrEmpty(current) && File.Exists(current))
            {
                return;
            }

            foreach (var editor in CodeEditor.Editor.GetFoundScriptEditorPaths())
            {
                if (editor.Value.Contains("Rider") || editor.Key.ToLowerInvariant().Contains("rider"))
                {
                    CodeEditor.SetExternalScriptEditor(editor.Key);
                    return;
                }
            }

            // Rider instalat intr-un folder ales manual nu e detectat de Unity.
            // Daca ruleaza chiar acum, ii luam calea din procesul pornit.
            foreach (Process process in Process.GetProcessesByName("rider64"))
            {
                try
                {
                    string path = process.MainModule.FileName;

                    if (File.Exists(path))
                    {
                        CodeEditor.SetExternalScriptEditor(path);
                        return;
                    }
                }
                catch (System.Exception)
                {
                    // Proces la care nu avem acces - incercam urmatorul.
                }
            }
        }
    }
}
