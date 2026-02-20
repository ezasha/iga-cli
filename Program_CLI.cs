using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using IGAE_GUI;
using IGAE_GUI.IGZ;
using IGAE_GUI.Types;
using IGAE_GUI.Utils;

namespace igArchiveExtractorCLI
{
    internal class Program
    {
        private const int STD_OUTPUT_HANDLE = -11;
        private const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;
        private static bool _vtInitTried;
        private static bool _vtEnabled;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

        [STAThread]
        static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                RunInteractiveMenu();
                return 0;
            }

            if (args[0].Equals("--help", StringComparison.OrdinalIgnoreCase) || args[0].Equals("-h", StringComparison.OrdinalIgnoreCase))
            {
                PrintHelp();
                return 1;
            }

            string command = args[0].ToLowerInvariant();

            try
            {
                switch (command)
                {
                    case "list":
                        ListCommand(args);
                        return 0;
                    case "extract":
                        ExtractCommand(args);
                        return 0;
                    case "extract-single":
                        ExtractSingleCommand(args);
                        return 0;
                    case "list-games":
                        ListGamesCommand();
                        return 0;
                    case "igz-extract-images":
                        IGZExtractImagesCommand(args);
                        return 0;
                    case "igz-list-images":
                        if (args.Length < 2)
                        {
                            Console.Error.WriteLine("Usage: iga-cli.exe igz-list-images <igz_or_level_bld>");
                            return 1;
                        }
                        IGZListImagesCommand(args[1]);
                        return 0;
                    default:
                        Console.Error.WriteLine($"ERROR: Unknown command '{command}'.");
                        PrintHelp();
                        return 1;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"ERROR: {ex.Message}");
                if (!string.IsNullOrEmpty(ex.StackTrace))
                {
                    Console.Error.WriteLine(ex.StackTrace);
                }
                return 1;
            }
        }

        static void PrintHelp()
        {
            Console.WriteLine(@"igArchiveExtractor CLI
Usage:
  iga-cli.exe list <archive_file> [--game <game_name>]
  iga-cli.exe extract <archive_file> <output_dir> [--game <game_name>]
  iga-cli.exe extract-single <archive_file> <index> <output_dir> [--game <game_name>]
  iga-cli.exe igz-extract-images <igz_or_level_bld> <output_dir>
    iga-cli.exe igz-list-images <igz_or_level_bld>
  iga-cli.exe list-games
");
        }

        static void RunInteractiveMenu()
        {
            while (true)
            {
                ClearScreen();
                Console.WriteLine(@"╔════════════════════════════════════════════════════════════╗
║                   MAIN MENU                                ║
╠════════════════════════════════════════════════════════════╣
║                                                            ║
║  [L]  List Archive Contents                                ║
║  [E]  Extract All Files                                    ║
║  [S]  Extract Single File                                  ║
║  [G]  List Supported Games                                 ║
║  [H]  Help / CLI & Python                                  ║
║  [Q]  Quit                                                 ║
║                                                            ║
╚════════════════════════════════════════════════════════════╝
");

                Console.Write("Enter choice (L/E/S/G/H/Q): ");

                string choice = Console.ReadLine()?.Trim().ToUpperInvariant() ?? "";
                try
                {
                    switch (choice)
                    {
                        case "L":
                            InteractiveListCommand();
                            PauseReturnToMenu();
                            break;
                        case "E":
                            InteractiveExtractCommand();
                            PauseReturnToMenu();
                            break;
                        case "S":
                            InteractiveExtractSingleCommand();
                            PauseReturnToMenu();
                            break;
                        case "G":
                            ListGamesCommand();
                            PauseReturnToMenu();
                            break;
                        case "H":
                            ShowInteractiveHelp();
                            PauseReturnToMenu();
                            break;
                        case "Q":
                            return;
                        default:
                            Console.WriteLine("Invalid choice.");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR: {ex.Message}");
                    PauseReturnToMenu();
                }
            }
        }

        static void InteractiveListCommand()
        {
            ClearScreen();
            string archiveFile = SelectArchiveFile();
            if (string.IsNullOrWhiteSpace(archiveFile) || !File.Exists(archiveFile))
            {
                Console.WriteLine("File not found.");
                return;
            }

            if (IsIgzFile(archiveFile))
            {
                IGZListImagesCommand(archiveFile);
                return;
            }

            IGA_Version? version = SelectGameVersionInteractive();
            IGA_File igaFile = OpenArchiveForOperation(archiveFile, version, out IGA_Version finalVersion);

            Console.WriteLine($"Listing files in: {Path.GetFileName(archiveFile)}");
            Console.WriteLine($"Game Version: {finalVersion}");
            Console.WriteLine($"Total files: {igaFile.numberOfFiles}");
            Console.WriteLine();
            Console.WriteLine("INDEX       SIZE          PATH");
            Console.WriteLine("==============================================");

            for (uint i = 0; i < igaFile.numberOfFiles; i++)
            {
                uint size = igaFile.localFileHeaders[i].size;
                string path = igaFile.names[i];
                Console.WriteLine($"{i:D4}  {size:D12}  {path}");
            }

            igaFile.Close();
        }

        static void InteractiveExtractCommand()
        {
            ClearScreen();
            string archiveFile = SelectArchiveFile();
            if (string.IsNullOrWhiteSpace(archiveFile) || !File.Exists(archiveFile))
            {
                Console.WriteLine("File not found.");
                return;
            }

            ShowExtractionSelection(archiveFile, string.Empty);

            string outputDir = SelectOutputDirectory();
            ShowExtractionSelection(archiveFile, outputDir);
            if (string.IsNullOrWhiteSpace(outputDir))
            {
                Console.WriteLine("Output directory is required.");
                return;
            }

            if (IsIgzFile(archiveFile))
            {
                IGZExtractImagesCommand(new[] { "igz-extract-images", archiveFile, outputDir });
                return;
            }

            IGA_Version? version = SelectGameVersionInteractive();
            IGA_File igaFile = OpenArchiveForOperation(archiveFile, version, out IGA_Version finalVersion);

            Directory.CreateDirectory(outputDir);
            WriteColoredLine($"[*] Extracting archive: {Path.GetFileName(archiveFile)}", ConsoleColor.Cyan);
            WriteColoredLine($"[*] Output directory: {Path.GetFullPath(outputDir)}", ConsoleColor.Cyan);
            WriteColoredLine($"[*] Game Version: {finalVersion}", ConsoleColor.Cyan);
            WriteColoredLine($"[*] Total files: {igaFile.numberOfFiles}", ConsoleColor.Cyan);
            Console.WriteLine();

            int successCount = 0;
            int failedCount = 0;
            var sw = Stopwatch.StartNew();

            for (uint i = 0; i < igaFile.numberOfFiles; i++)
            {
                try
                {
                    igaFile.ExtractFile(i, outputDir, out int res, true);
                    if (res == 0)
                    {
                        WriteColoredLine($"[OK] [{i + 1:D4}/{igaFile.numberOfFiles:D4}] {igaFile.names[i]}", ConsoleColor.Green);
                        successCount++;
                    }
                    else
                    {
                        WriteColoredLine($"[FAIL] [{i + 1:D4}/{igaFile.numberOfFiles:D4}] {igaFile.names[i]}", ConsoleColor.Red);
                        failedCount++;
                    }
                }
                catch
                {
                    WriteColoredLine($"[FAIL] [{i + 1:D4}/{igaFile.numberOfFiles:D4}] {igaFile.names[i]}", ConsoleColor.Red);
                    failedCount++;
                }
            }

            sw.Stop();
            igaFile.Close();

            Console.WriteLine();
            Console.WriteLine("==============================================");
            WriteColoredLine($"Success:  {successCount}/{igaFile.numberOfFiles}", ConsoleColor.Green);
            if (failedCount > 0)
            {
                WriteColoredLine($"Failed:   {failedCount}/{igaFile.numberOfFiles}", ConsoleColor.Red);
            }
            WriteColoredLine($"Time:     {sw.Elapsed.TotalSeconds:F2}s", ConsoleColor.Yellow);
            Console.WriteLine("==============================================");
        }

        static void InteractiveExtractSingleCommand()
        {
            ClearScreen();
            string archiveFile = SelectArchiveFile();
            if (string.IsNullOrWhiteSpace(archiveFile) || !File.Exists(archiveFile))
            {
                Console.WriteLine("File not found.");
                return;
            }

            ShowExtractionSelection(archiveFile, string.Empty);

            if (IsIgzFile(archiveFile))
            {
                Console.WriteLine("Extract-single is not supported for IGZ files.");
                return;
            }

            string outputDir = SelectOutputDirectory();
            ShowExtractionSelection(archiveFile, outputDir);
            if (string.IsNullOrWhiteSpace(outputDir))
            {
                Console.WriteLine("Output directory is required.");
                return;
            }

            IGA_Version? version = SelectGameVersionInteractive();

            Directory.CreateDirectory(outputDir);
            IGA_File igaFile = OpenArchiveForOperation(archiveFile, version, out IGA_Version finalVersion);

            ClearScreen();
            WriteColoredLine($"Archive: {Path.GetFileName(archiveFile)}", ConsoleColor.Cyan);
            WriteColoredLine($"Game Version: {finalVersion}", ConsoleColor.Cyan);
            WriteColoredLine($"Total files: {igaFile.numberOfFiles}", ConsoleColor.Cyan);
            Console.WriteLine();
            Console.WriteLine("INDEX       SIZE          PATH");
            Console.WriteLine("==============================================================");

            const int pageSize = 40;
            int printed = 0;
            for (uint i = 0; i < igaFile.numberOfFiles; i++)
            {
                uint size = igaFile.localFileHeaders[i].size;
                string path = igaFile.names[i];
                Console.WriteLine($"{i:D4}  {size:D12}  {path}");
                printed++;

                if (printed % pageSize == 0 && i + 1 < igaFile.numberOfFiles)
                {
                    Console.WriteLine();
                    Console.Write("Appuyez sur une touche pour voir la suite...");
                    Console.ReadKey(true);
                    ClearScreen();
                    WriteColoredLine($"Archive: {Path.GetFileName(archiveFile)} (continued)", ConsoleColor.Cyan);
                    Console.WriteLine("INDEX       SIZE          PATH");
                    Console.WriteLine("==============================================================");
                }
            }

            Console.WriteLine();
            Console.Write("Index: ");
            if (!uint.TryParse(Console.ReadLine()?.Trim(), out uint index))
            {
                Console.WriteLine("Index must be a number.");
                igaFile.Close();
                return;
            }

            if (index >= igaFile.numberOfFiles)
            {
                Console.WriteLine("Index out of range.");
                igaFile.Close();
                return;
            }

            igaFile.ExtractFile(index, outputDir, out int res, true);
            igaFile.Close();

            WriteColoredLine(res == 0 ? "Success" : "Failed", res == 0 ? ConsoleColor.Green : ConsoleColor.Red);
        }

        static void ListCommand(string[] args)
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: iga-cli.exe list <archive_file> [--game <game_name>]");
                return;
            }

            string archiveFile = args[1];
            if (!File.Exists(archiveFile))
            {
                Console.Error.WriteLine($"ERROR: File not found: {archiveFile}");
                return;
            }

            IGA_Version? version = ParseGameArgument(args, 2);
            IGA_File igaFile = OpenArchiveForOperation(archiveFile, version, out IGA_Version finalVersion);

            Console.WriteLine($"Listing files in: {Path.GetFileName(archiveFile)}");
            Console.WriteLine($"Game Version: {finalVersion}");
            Console.WriteLine($"Total files: {igaFile.numberOfFiles}");
            Console.WriteLine();
            Console.WriteLine("INDEX       SIZE          PATH");
            Console.WriteLine("==============================================");

            for (uint i = 0; i < igaFile.numberOfFiles; i++)
            {
                uint size = igaFile.localFileHeaders[i].size;
                string path = igaFile.names[i];
                Console.WriteLine($"{i:D4}  {size:D12}  {path}");
            }

            igaFile.Close();
        }

        static void ExtractCommand(string[] args)
        {
            if (args.Length < 3)
            {
                Console.Error.WriteLine("Usage: iga-cli.exe extract <archive_file> <output_dir> [--game <game_name>]");
                return;
            }

            string archiveFile = args[1];
            string outputDir = args[2];

            if (!File.Exists(archiveFile))
            {
                Console.Error.WriteLine($"ERROR: File not found: {archiveFile}");
                return;
            }

            Directory.CreateDirectory(outputDir);
            IGA_Version? version = ParseGameArgument(args, 3);
            IGA_File igaFile = OpenArchiveForOperation(archiveFile, version, out IGA_Version finalVersion);

            Console.WriteLine($"Extracting archive: {Path.GetFileName(archiveFile)}");
            Console.WriteLine($"Output directory: {Path.GetFullPath(outputDir)}");
            Console.WriteLine($"Game Version: {finalVersion}");
            Console.WriteLine($"Total files: {igaFile.numberOfFiles}");
            Console.WriteLine();

            int successCount = 0;
            int failedCount = 0;
            var sw = Stopwatch.StartNew();

            for (uint i = 0; i < igaFile.numberOfFiles; i++)
            {
                try
                {
                    igaFile.ExtractFile(i, outputDir, out int res, true);
                    if (res == 0)
                    {
                        successCount++;
                    }
                    else
                    {
                        failedCount++;
                    }
                }
                catch
                {
                    failedCount++;
                }
            }

            sw.Stop();
            igaFile.Close();

            Console.WriteLine();
            Console.WriteLine("==============================================");
            Console.WriteLine($"Success:  {successCount}/{igaFile.numberOfFiles}");
            if (failedCount > 0)
            {
                Console.WriteLine($"Failed:   {failedCount}/{igaFile.numberOfFiles}");
            }
            Console.WriteLine($"Time:     {sw.Elapsed.TotalSeconds:F2}s");
            Console.WriteLine("==============================================");
        }

        static void ExtractSingleCommand(string[] args)
        {
            if (args.Length < 4)
            {
                Console.Error.WriteLine("Usage: iga-cli.exe extract-single <archive_file> <index> <output_dir> [--game <game_name>]");
                return;
            }

            string archiveFile = args[1];
            if (!uint.TryParse(args[2], out uint index))
            {
                Console.Error.WriteLine("ERROR: Index must be a number.");
                return;
            }
            string outputDir = args[3];

            if (!File.Exists(archiveFile))
            {
                Console.Error.WriteLine($"ERROR: File not found: {archiveFile}");
                return;
            }

            Directory.CreateDirectory(outputDir);
            IGA_Version? version = ParseGameArgument(args, 4);
            IGA_File igaFile = OpenArchiveForOperation(archiveFile, version, out IGA_Version finalVersion);

            Console.WriteLine($"Extracting index {index} from {Path.GetFileName(archiveFile)}");
            Console.WriteLine($"Game Version: {finalVersion}");

            igaFile.ExtractFile(index, outputDir, out int res, true);
            igaFile.Close();

            if (res == 0)
            {
                Console.WriteLine("Success");
            }
            else
            {
                Console.WriteLine("Failed");
            }
        }

        static void ListGamesCommand()
        {
            Console.WriteLine("Validated games:");
            foreach (var version in GetValidatedDetectionOrder())
            {
                Console.WriteLine($"- {version}");
            }

            Console.WriteLine();
            Console.WriteLine("Experimental / not validated in this release:");
            Console.WriteLine("- SkylandersLostIslands");
            Console.WriteLine("- CrashNST");
            Console.WriteLine();
            Console.WriteLine("Note: Auto-detect still tries experimental mappings as fallback.");
        }

        static void ShowInteractiveHelp()
        {
            ClearScreen();
            Console.WriteLine("HELP - CLI & Python");
            Console.WriteLine("===================");
            Console.WriteLine();
            Console.WriteLine("Command-line examples:");
            Console.WriteLine("  iga-cli.exe list \"game.bld\" --game SkylandersTrapTeam");
            Console.WriteLine("  iga-cli.exe extract \"game.bld\" \"out\" --game SkylandersTrapTeam");
            Console.WriteLine("  iga-cli.exe extract-single \"game.bld\" 12 \"out\" --game SkylandersTrapTeam");
            Console.WriteLine("  iga-cli.exe igz-list-images \"level.bld\"");
            Console.WriteLine("  iga-cli.exe igz-extract-images \"level.bld\" \"out_images\"");
            Console.WriteLine();
            Console.WriteLine("Python wrapper examples (iga_wrapper.py):");
            Console.WriteLine("  from iga_wrapper import IGAExtractor");
            Console.WriteLine("  ex = IGAExtractor(cli_exe=\"publish-lite/iga-cli.exe\")");
            Console.WriteLine("  files = ex.list_files(\"game.bld\")");
            Console.WriteLine("  ex.extract(\"game.bld\", \"out\")");
            Console.WriteLine("  ex.extract_single(\"game.bld\", 12, \"out\")");
            Console.WriteLine();
            Console.WriteLine("Tips:");
            Console.WriteLine("- Use [A] Auto-detect first, then force --game if needed.");
            Console.WriteLine("- For IGZ/level.bld textures, use igz-list-images then igz-extract-images.");
        }

        static void IGZExtractImagesCommand(string[] args)
        {
            if (args.Length < 3)
            {
                Console.Error.WriteLine("Usage: iga-cli.exe igz-extract-images <igz_or_level_bld> <output_dir>");
                return;
            }

            string igzFilePath = args[1];
            string outputDir = args[2];

            if (!File.Exists(igzFilePath))
            {
                Console.Error.WriteLine($"ERROR: File not found: {igzFilePath}");
                return;
            }

            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            Console.WriteLine($"Loading file: {igzFilePath}");

            MemoryStream igzStream = OpenIgzStream(igzFilePath);
            igzStream.Seek(0, SeekOrigin.Begin);
            IGZ_File igz = new IGZ_File(igzStream);

            int successCount = 0;
            int failureCount = 0;
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int imageIndex = 0;

            foreach (var igObj in igz.objectList._objects)
            {
                if (igObj is igImage2 img)
                {
                    try
                    {
                        string baseName = TryGetIgzImageName(igz, img, imageIndex);
                        string normalizedBaseName = SanitizeFilename(baseName);
                        string uniqueBaseName = EnsureUniqueName(normalizedBaseName, usedNames);
                        string filename = $"{uniqueBaseName}.png";
                        string outputPath = Path.Combine(outputDir, filename);

                        MemoryStream ddsStream = new MemoryStream();
                        img.Extract(ddsStream);

                        if (ddsStream.Length > 0)
                        {
                            ddsStream.Seek(0, SeekOrigin.Begin);
                            System.Drawing.Bitmap bitmap = TextureHelper.BitmapFromDDS(ddsStream);
                            bitmap.Save(outputPath, ImageFormat.Png);
                            WriteColoredLine($"[OK] {filename} ({img.TextureSize} bytes, {img.width}x{img.height})", ConsoleColor.Green);
                            successCount++;
                        }
                        else
                        {
                            WriteColoredLine($"[FAIL] {filename} (empty data)", ConsoleColor.Red);
                            failureCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        WriteColoredLine($"[FAIL] image #{imageIndex}: {ex.Message}", ConsoleColor.Red);
                        failureCount++;
                    }

                    imageIndex++;
                }
            }

            Console.WriteLine($"Extraction complete: {successCount} images exported, {failureCount} failed");
        }

        static void IGZListImagesCommand(string igzFilePath)
        {
            if (!File.Exists(igzFilePath))
            {
                Console.WriteLine("File not found.");
                return;
            }

            MemoryStream igzStream = OpenIgzStream(igzFilePath);
            igzStream.Seek(0, SeekOrigin.Begin);
            IGZ_File igz = new IGZ_File(igzStream);

            int index = 0;
            Console.WriteLine("INDEX  SIZE(bytes)  WIDTH  HEIGHT  NAME");
            Console.WriteLine("==============================================================");
            foreach (var igObj in igz.objectList._objects)
            {
                if (igObj is igImage2 img)
                {
                    string baseName = TryGetIgzImageName(igz, img, index);
                    string normalizedBaseName = SanitizeFilename(baseName);
                    Console.WriteLine($"{index:D4}  {img.TextureSize,11}  {img.width,5}  {img.height,6}  {normalizedBaseName}.png");
                    index++;
                }
            }

            Console.WriteLine($"Total images: {index}");
        }

        static IGA_Version? ParseGameArgument(string[] args, int startIndex)
        {
            for (int i = startIndex; i < args.Length - 1; i++)
            {
                if (args[i].Equals("--game", StringComparison.OrdinalIgnoreCase))
                {
                    string gameName = args[i + 1];
                    return GameNameToVersion(gameName);
                }
            }
            return null;
        }

        static IGA_Version GameNameToVersion(string gameName)
        {
            string normalized = gameName.Trim();

            return normalized switch
            {
                "SkylandersSpyrosAdventureWii" => IGA_Version.SkylandersSpyrosAdventureWii,
                "SkylandersSpyrosAdventureWiiU" => IGA_Version.SkylandersSpyrosAdventureWiiU,
                "SkylandersSwapForce" => IGA_Version.SkylandersSwapForce,
                "SkylandersLostIslands" => IGA_Version.SkylandersLostIslands,
                "SkylandersTrapTeam" => IGA_Version.SkylandersTrapTeam,
                "SkylandersSuperChargers" => IGA_Version.SkylandersSuperChargers,
                "SkylandersImaginatorsPS4" => IGA_Version.SkylandersImaginatorsPS4,
                "CrashNST" => IGA_Version.CrashNST,
                "Skylanders Spyro's Adventure (3DS/Wii)" => IGA_Version.SkylandersSpyrosAdventureWii,
                "Skylanders Spyro's Adventure (Wii U)" => IGA_Version.SkylandersSpyrosAdventureWiiU,
                "Skylanders Giants (3DS)" => IGA_Version.SkylandersSpyrosAdventureWiiU,
                "Skylanders Giants (Home Console)" => IGA_Version.SkylandersSpyrosAdventureWiiU,
                "Skylanders Giants (Home Console Alpha)" => IGA_Version.SkylandersSpyrosAdventureWiiU,
                "Skylanders Swap Force (3DS)" => IGA_Version.SkylandersSwapForce,
                "Skylanders Swap Force (Home Console)" => IGA_Version.SkylandersSwapForce,
                "Skylanders Swap Force (Home Console Alpha)" => IGA_Version.SkylandersSwapForce,
                "Skylanders Trap Team (3DS)" => IGA_Version.SkylandersTrapTeam,
                "Skylanders Trap Team (Home Console)" => IGA_Version.SkylandersTrapTeam,
                "Skylanders SuperChargers" => IGA_Version.SkylandersSuperChargers,
                "Skylanders Imaginators (PS3/X360/Wii U)" => IGA_Version.SkylandersSuperChargers,
                "Skylanders Imaginators (PS4)" => IGA_Version.SkylandersImaginatorsPS4,
                _ => throw new ArgumentException($"Unknown game name: {gameName}. Use 'list-games' to see all supported games.")
            };
        }

        static string SelectArchiveFile()
        {
            using OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = "Supported game files|*.arc;*.bld;*.pak;*.iga;*.igz;*.lang|All files (*.*)|*.*";
            dialog.Title = "Select Archive or IGZ File";
            return dialog.ShowDialog() == DialogResult.OK ? dialog.FileName : string.Empty;
        }

        static string SelectOutputDirectory()
        {
            using FolderBrowserDialog dialog = new FolderBrowserDialog();
            dialog.Description = "Select Output Folder";
            return dialog.ShowDialog() == DialogResult.OK ? dialog.SelectedPath : string.Empty;
        }

        static IGA_Version? SelectGameVersionInteractive()
        {
            ClearScreen();
            Console.WriteLine("Select Game Version:");
            Console.WriteLine("  [A] Auto-detect");
            var versions = GetValidatedDetectionOrder();
            for (int i = 0; i < versions.Length; i++)
            {
                Console.WriteLine($"  [{i + 1}] {versions[i]}");
            }
            Console.Write($"Choice (A/1-{versions.Length}): ");
            string input = Console.ReadLine()?.Trim() ?? "";
            if (string.Equals(input, "A", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(input))
            {
                return null;
            }

            if (int.TryParse(input, out int index) && index >= 1 && index <= versions.Length)
            {
                return versions[index - 1];
            }

            Console.WriteLine("Invalid selection. Using auto-detect.");
            return null;
        }

        static void ShowExtractionSelection(string archiveFile, string outputDir)
        {
            ClearScreen();
            WriteColoredLine("Extraction setup", ConsoleColor.Cyan);
            Console.WriteLine($"File: {archiveFile}");
            if (string.IsNullOrWhiteSpace(outputDir))
            {
                Console.WriteLine("Output:");
            }
            else
            {
                Console.WriteLine($"Output: {outputDir}");
            }
            Console.WriteLine();
        }

        static void PauseReturnToMenu()
        {
            Console.WriteLine();
            Console.WriteLine("Cliquez sur une touche pour revenir au menu...");
            Console.ReadKey(true);
            ClearScreen();
        }

        static void ClearScreen()
        {
            if (TryEnableVirtualTerminal())
            {
                Console.Write("\u001b[3J\u001b[2J\u001b[H");
                Console.Out.Flush();
                return;
            }

            try
            {
                if (!Console.IsOutputRedirected)
                {
                    int width = Console.WindowWidth;
                    int height = Console.WindowHeight;
                    Console.SetBufferSize(width, height);
                }
            }
            catch
            {
                // Ignore console resize errors.
            }

            Console.Clear();
            Console.SetCursorPosition(0, 0);
        }

        static bool TryEnableVirtualTerminal()
        {
            if (_vtInitTried)
            {
                return _vtEnabled;
            }

            _vtInitTried = true;
            try
            {
                IntPtr handle = GetStdHandle(STD_OUTPUT_HANDLE);
                if (handle == IntPtr.Zero || handle == new IntPtr(-1))
                {
                    return false;
                }

                if (!GetConsoleMode(handle, out uint mode))
                {
                    return false;
                }

                if ((mode & ENABLE_VIRTUAL_TERMINAL_PROCESSING) == 0)
                {
                    if (!SetConsoleMode(handle, mode | ENABLE_VIRTUAL_TERMINAL_PROCESSING))
                    {
                        return false;
                    }
                }

                _vtEnabled = true;
                return true;
            }
            catch
            {
                _vtEnabled = false;
                return false;
            }
        }

        static void WriteColoredLine(string message, ConsoleColor color)
        {
            var previous = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine(message);
            Console.ForegroundColor = previous;
        }

        static IGA_File OpenArchiveForOperation(string archiveFile, IGA_Version? requestedVersion, out IGA_Version resolvedVersion)
        {
            var candidateVersions = BuildCandidateVersions(requestedVersion);

            foreach (var candidate in candidateVersions)
            {
                if (TryOpenWithVersion(archiveFile, candidate, out var file, validateByExtraction: true))
                {
                    resolvedVersion = candidate;
                    return file;
                }
            }

            throw new InvalidOperationException("File is corrupt or unsupported with all available game mappings.");
        }

        static bool TryOpenWithVersion(string archiveFile, IGA_Version version, out IGA_File file, bool validateByExtraction)
        {
            file = null!;
            try
            {
                file = new IGA_File(archiveFile, version);

                if (file.numberOfFiles == 0)
                {
                    file.Close();
                    file = null!;
                    return false;
                }

                if (validateByExtraction)
                {
                    bool probeSucceeded = false;
                    uint probes = Math.Min(3u, file.numberOfFiles);

                    for (uint i = 0; i < probes; i++)
                    {
                        try
                        {
                            if (file.localFileHeaders[i].size == 0)
                            {
                                continue;
                            }

                            using var ms = new MemoryStream();
                            file.ExtractFile(i, ms, out int res, true);
                            if (res == 0)
                            {
                                probeSucceeded = true;
                                break;
                            }
                        }
                        catch
                        {
                            continue;
                        }
                    }

                    if (!probeSucceeded)
                    {
                        file.Close();
                        file = null!;
                        return false;
                    }
                }

                return true;
            }
            catch
            {
                file?.Close();
                file = null!;
                return false;
            }
        }

        static IGA_Version[] BuildCandidateVersions(IGA_Version? requestedVersion)
        {
            var order = GetDetectionOrder().ToList();

            if (!requestedVersion.HasValue)
            {
                return order.ToArray();
            }

            var requested = requestedVersion.Value;
            order.Remove(requested);
            order.Insert(0, requested);
            return order.ToArray();
        }

        static IGA_Version[] GetDetectionOrder()
        {
            return new IGA_Version[]
            {
                IGA_Version.SkylandersSpyrosAdventureWii,
                IGA_Version.SkylandersSpyrosAdventureWiiU,
                IGA_Version.SkylandersSwapForce,
                IGA_Version.SkylandersTrapTeam,
                IGA_Version.SkylandersSuperChargers,
                IGA_Version.SkylandersImaginatorsPS4,

                // Experimental fallback mappings
                IGA_Version.SkylandersLostIslands,
                IGA_Version.CrashNST,
            };
        }

        static IGA_Version[] GetValidatedDetectionOrder()
        {
            return new IGA_Version[]
            {
                IGA_Version.SkylandersSpyrosAdventureWii,
                IGA_Version.SkylandersSpyrosAdventureWiiU,
                IGA_Version.SkylandersSwapForce,
                IGA_Version.SkylandersTrapTeam,
                IGA_Version.SkylandersSuperChargers,
                IGA_Version.SkylandersImaginatorsPS4,
            };
        }

        static bool IsIgzFile(string filePath)
        {
            try
            {
                using FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                byte[] magic = new byte[4];
                if (fs.Read(magic, 0, magic.Length) != magic.Length)
                {
                    return false;
                }

                uint magicValue = BitConverter.ToUInt32(magic, 0);
                return magicValue == 0x015A4749 || magicValue == 0x48475A01;
            }
            catch
            {
                return false;
            }
        }

        static MemoryStream OpenIgzStream(string igzFilePath)
        {
            byte[] fileHeader = new byte[4];
            using (FileStream fs = File.OpenRead(igzFilePath))
            {
                fs.Read(fileHeader, 0, 4);
            }

            MemoryStream igzStream = new MemoryStream();
            bool isIGZDirect = (fileHeader[0] == 0x49 && fileHeader[1] == 0x47 && fileHeader[2] == 0x5A && fileHeader[3] == 0x01) ||
                               (fileHeader[0] == 0x48 && fileHeader[1] == 0x47 && fileHeader[2] == 0x5A && fileHeader[3] == 0x01);

            if (isIGZDirect)
            {
                using (FileStream fs = File.OpenRead(igzFilePath))
                {
                    fs.CopyTo(igzStream);
                }
                return igzStream;
            }

            IGA_Version[] versionCandidates = GetDetectionOrder();
            IGA_File? igaFile = null;

            foreach (var version in versionCandidates)
            {
                try
                {
                    igaFile = new IGA_File(igzFilePath, version);
                    break;
                }
                catch
                {
                    // Try next version
                }
            }

            if (igaFile == null)
            {
                throw new InvalidOperationException("Could not open archive with any known game version");
            }

            bool foundIGZ = false;
            for (int i = 0; i < igaFile.localFileHeaders.Length; i++)
            {
                MemoryStream ms = new MemoryStream((int)igaFile.localFileHeaders[i].size);
                igaFile.ExtractFile((uint)i, ms, out int res, true);

                if (res == 1 && ms.Length > 0)
                {
                    ms.Seek(0, SeekOrigin.Begin);
                    byte[] magic = new byte[4];
                    ms.Read(magic, 0, 4);

                    if ((magic[0] == 0x49 && magic[1] == 0x47 && magic[2] == 0x5A && magic[3] == 0x01) ||
                        (magic[0] == 0x48 && magic[1] == 0x47 && magic[2] == 0x5A && magic[3] == 0x01))
                    {
                        ms.Seek(0, SeekOrigin.Begin);
                        igzStream = ms;
                        foundIGZ = true;
                        break;
                    }
                }
            }

            if (!foundIGZ)
            {
                throw new InvalidOperationException("No embedded IGZ found in archive");
            }

            return igzStream;
        }

        static string SanitizeFilename(string filename)
        {
            if (string.IsNullOrWhiteSpace(filename))
            {
                return "unnamed";
            }

            var invalidChars = Path.GetInvalidFileNameChars();
            foreach (char c in invalidChars)
            {
                filename = filename.Replace(c, '_');
            }

            filename = filename.Replace(':', '_');
            filename = filename.Replace('/', '_');
            filename = filename.Replace('#', '_');
            filename = filename.Replace('@', '_');
            filename = filename.TrimEnd('.', ' ');

            if (string.IsNullOrWhiteSpace(filename))
            {
                return "unnamed";
            }

            return filename;
        }

        static string EnsureUniqueName(string baseName, HashSet<string> usedNames)
        {
            if (usedNames.Add(baseName))
            {
                return baseName;
            }

            int suffix = 1;
            while (true)
            {
                string candidate = $"{baseName}_{suffix:D2}";
                if (usedNames.Add(candidate))
                {
                    return candidate;
                }
                suffix++;
            }
        }

        static string TryGetIgzImageName(IGZ_File igz, igImage2 img, int fallbackIndex)
        {
            try
            {
                igz.ebr.BaseStream.Seek(img.offset + 0x8, SeekOrigin.Begin);
                int encodedPointer = (int)igz.ebr.ReadUInt32();
                long nameOffset = DeserializeOffset(igz, encodedPointer);
                igz.ebr.BaseStream.Seek(nameOffset, SeekOrigin.Begin);
                string candidate = igz.ebr.ReadString();
                if (!string.IsNullOrWhiteSpace(candidate) && candidate.Length >= 2)
                {
                    return Path.GetFileNameWithoutExtension(candidate);
                }
            }
            catch
            {
                // Ignore and fallback.
            }

            return $"{fallbackIndex:D4}_{img.width}x{img.height}";
        }

        static long DeserializeOffset(IGZ_File igz, int encodedOffset)
        {
            if (igz.version <= 0x06)
            {
                return igz.descriptors[(encodedOffset >> 0x18) + 1].offset + (encodedOffset & 0x00FFFFFF);
            }

            return igz.descriptors[(encodedOffset >> 0x1B) + 1].offset + (encodedOffset & 0x07FFFFFF);
        }
    }
}
