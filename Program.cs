/*
 * program.cs 
 * 
 * replicates the unix LC command
 * 
 *  Date        Author          Description
 *  ====        ======          ===========
 *  06-26-25    Craig           initial implementation
 *
 */
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace LC
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            EnableVirtualTerminal();

            var showHelp = false;
            var showDirs = false;
            var showFiles = false;
            var recurse = false;
            var showReadOnly = false;
            string? extensionFilter = null;
            List<string> targetDirs = new();

            // Parse args
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                switch (arg)
                {
                    case "-?":
                        showHelp = true;
                        break;
                    case "-d":
                        showDirs = true;
                        break;
                    case "-f":
                        showFiles = true;
                        break;
                    case "-r":
                        recurse = true;
                        break;
                    case "-R":
                        showReadOnly = true;
                        break;
                    case "-e":
                        if (i + 1 < args.Length)
                        {
                            extensionFilter = args[++i].Trim('"');
                        }
                        else
                        {
                            Console.Error.WriteLine("Error: -e option requires an extension argument.");
                            return;
                        }
                        break;
                    default:
                        targetDirs.Add(arg);
                        break;
                }
            }

            if (showHelp)
            {
                PrintHelp();
                return;
            }

            if (!showDirs && !showFiles)
            {
                showDirs = true;
                showFiles = true;
            }

            if (targetDirs.Count == 0)
                targetDirs.Add(Directory.GetCurrentDirectory());

            foreach (string dir in targetDirs)
            {
                if (!Directory.Exists(dir))
                {
                    Console.Error.WriteLine($"Error: Directory '{dir}' does not exist.");
                    continue;
                }

                Console.WriteLine($"\nDirectory: {dir}");

                var (dirs, files, readOnlyFiles) = GetEntries(dir, recurse, extensionFilter);

                if (showDirs)
                {
                    Console.WriteLine("Directories:");
                    PrintInColumns(dirs, dir);
                }

                if (showFiles)
                {
                    Console.WriteLine("\nFiles:");
                    PrintInColumns(files, dir);

                    if (showReadOnly)
                    {
                        Console.WriteLine("\nRead-Only Files:");
                        PrintInColumns(readOnlyFiles, dir);
                    }
                }
            }
        }

        static void PrintHelp()
        {
            Console.WriteLine(
@"Usage: lc [-?defrR] [dirs]
    dirs       : directories to operate on
    Options:
       -?         : display this message
       -d         : show directories only
       -e ""ext""   : filter files by extension (e.g. -e "".png"")
       -f         : show files only
       -r         : recursive mode
       -R         : show read-only files separately");
        }

        static (List<string> dirs, List<string> files, List<string> readOnlyFiles) GetEntries(string root, bool recurse, string? extFilter)
        {
            var allDirs = new List<string>();
            var allFiles = new List<string>();
            var readOnlyFiles = new List<string>();

            var option = recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

            foreach (var path in Directory.EnumerateFileSystemEntries(root, "*", option))
            {
                string name = Path.GetFileName(path);

                if (Directory.Exists(path))
                {
                    allDirs.Add(name);
                }
                else if (File.Exists(path))
                {
                    if (extFilter == null || name.EndsWith(extFilter, StringComparison.OrdinalIgnoreCase))
                    {
                        allFiles.Add(name);

                        if ((File.GetAttributes(path) & FileAttributes.ReadOnly) != 0)
                            readOnlyFiles.Add(name);
                    }
                }
            }

            allDirs.Sort(StringComparer.OrdinalIgnoreCase);
            allFiles.Sort(StringComparer.OrdinalIgnoreCase);
            readOnlyFiles.Sort(StringComparer.OrdinalIgnoreCase);

            return (allDirs, allFiles, readOnlyFiles);
        }

        static void PrintInColumns(List<string> items, string basePath, int columnPadding = 4, int maxColumns = 4)
        {
            if (items.Count == 0)
            {
                Console.WriteLine("(none)");
                return;
            }

            int maxLen = items.Max(s => s.Length) + columnPadding;
            int cols = Math.Min(maxColumns, Math.Max(1, Console.WindowWidth / maxLen));
            int rows = (int)Math.Ceiling(items.Count / (double)cols);

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    int index = c * rows + r;
                    if (index < items.Count)
                    {
                        WriteColored(items[index].PadRight(maxLen), items[index], basePath);
                    }
                }
                Console.WriteLine();
            }
        }

        static void WriteColored(string text, string name, string basePath)
        {
            string fullPath = Path.Combine(basePath, name);

            if (Directory.Exists(fullPath))
                Console.Write("\x1b[34m"); // blue
            else if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                     name.EndsWith(".bat", StringComparison.OrdinalIgnoreCase) ||
                     name.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase))
                Console.Write("\x1b[32m"); // green
            else if (File.GetAttributes(fullPath).HasFlag(FileAttributes.Hidden))
                Console.Write("\x1b[2m");  // dim
            else
                Console.Write("\x1b[0m");  // reset

            Console.Write(text);
            Console.Write("\x1b[0m");
        }

        static void EnableVirtualTerminal()
        {
            const int STD_OUTPUT_HANDLE = -11;
            var handle = GetStdHandle(STD_OUTPUT_HANDLE);
            GetConsoleMode(handle, out int mode);
            SetConsoleMode(handle, mode | 0x0004); // ENABLE_VIRTUAL_TERMINAL_PROCESSING
        }

        [DllImport("kernel32.dll")] static extern IntPtr GetStdHandle(int nStdHandle);
        [DllImport("kernel32.dll")] static extern bool GetConsoleMode(IntPtr hConsoleHandle, out int lpMode);
        [DllImport("kernel32.dll")] static extern bool SetConsoleMode(IntPtr hConsoleHandle, int dwMode);
    }
}
