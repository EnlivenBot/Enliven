using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;

namespace Bot.Utilities
{
    internal static class FileUtils
    {
        private static IEnumerable<string> RecursiveFilesScan(string dir)
        {
            Directory.CreateDirectory(dir);
            var dirs = new List<string>();
            dirs.AddRange(Directory.GetDirectories(dir));
            var result = new List<string>();
            result.AddRange(Directory.GetFiles(dir));
            dirs.ForEach(s => result.AddRange(RecursiveFilesScan(s)));
            return result;
        }

        public static void DeleteDirectoriesRecursively([Localizable(false)] string dir)
        {
            RecursiveFilesScan(dir).ToList().ForEach(File.Delete);
            DeleteDirectoriesRecursivelyInternal(dir);
        }

        private static void DeleteDirectoriesRecursivelyInternal(string dir)
        {
            Directory.GetDirectories(dir).ToList().ForEach(DeleteDirectoriesRecursivelyInternal);
            Directory.Delete(dir, true);
        }
    }
}