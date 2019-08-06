using System.Collections.Generic;
using System.ComponentModel;
using System.IO;

namespace Bot.Utilities
{
    class FileUtils
    {
        public static List<string> RecursiveFilesScan(string dir) {
            Directory.CreateDirectory(dir);
            var dirs = new List<string>();
            dirs.AddRange(Directory.GetDirectories(dir));
            var toreturn = new List<string>();
            toreturn.AddRange(Directory.GetFiles(dir));
            foreach (var VARIABLE in dirs) toreturn.AddRange(RecursiveFilesScan(VARIABLE));

            return toreturn;
        }

        public static void RecursiveFoldersDelete([Localizable(false)] string dir) {
            foreach (var VARIABLE in RecursiveFilesScan(dir)) File.Delete(VARIABLE);
            RecursiveFoldersDeleteMethod(dir);
        }

        private static void RecursiveFoldersDeleteMethod(string dir) {
            foreach (var directory in Directory.GetDirectories(dir)) RecursiveFoldersDeleteMethod(directory);

            Directory.Delete(dir, true);
        }
    }
}
