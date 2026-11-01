using System;
using System.IO;

namespace Flow.Launcher.Plugin.Flowy
{
    // Reine Hilfsfunktionen ohne Flow-Abhängigkeiten, damit sie testbar sind
    public static class PathHelper
    {
        public static bool LooksLikePath(string input)
        {
            if (input.Length >= 2 && char.IsLetter(input[0]) && input[1] == ':') return true;
            return input.StartsWith(@"\\") || input.StartsWith("%");
        }

        // Zerlegt z.B. "c:\win" in Ordner "c:\" und Filter "win"; false, wenn die Eingabe kein Pfad ist
        public static bool TryParsePathInput(string search, out string dir, out string filter)
        {
            dir = null;
            filter = "";

            string input = search.Trim();
            if (!LooksLikePath(input)) return false;

            input = Environment.ExpandEnvironmentVariables(input).Replace('/', '\\');
            if (input.Length == 2 && input[1] == ':') input += "\\";

            if (input.EndsWith("\\"))
            {
                dir = input;
            }
            else
            {
                dir = Path.GetDirectoryName(input);
                filter = Path.GetFileName(input);
            }
            return true;
        }

        // Ein abschließender Backslash würde sonst das schließende Anführungszeichen escapen
        public static string QuoteArg(string arg) => "\"" + (arg.EndsWith("\\") ? arg + "\\" : arg) + "\"";
    }
}
