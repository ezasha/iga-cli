using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using IGAE_GUI;

namespace igArchiveExtractor_CLI
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                PrintHelp();
                Environment.Exit(0);
            }

            string command = args[0].ToLower();

            try
            {
                switch (command)
                {
                    case "list":
                        ListCommand(args);
                        break;

                    case "extract":
                        ExtractCommand(args);
                        break;

                    case "extract-single":
                        ExtractSingleCommand(args);
                        break;

                    default:
                        Console.Error.WriteLine($"❌ Unknown command: {command}");
                        PrintHelp();
                        Environment.Exit(1);
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"❌ ERROR: {ex.Message}");
                if (!string.IsNullOrEmpty(ex.StackTrace))
                {
                    Console.Error.WriteLine(ex.StackTrace);
                }
                Environment.Exit(1);
            }
        }

        static void PrintHelp()
        {
            Console.WriteLine(@"
╔═══════════════════════════════════════════════════════════════╗
║   igArchiveExtractor CLI - Fast command-line tool for Python   ║
╚═══════════════════════════════════════════════════════════════╝

COMMANDS:
  list <archive>
    List all files in an archive (.arc, .bld, .pak, .iga)
    Output format: INDEX SIZE PATH

  extract <archive> <output_dir>
    Extract all files from archive to output directory

  extract-single <archive> <file_index> <output_dir>
    Extract a single file by index

EXAMPLES:
  iga-cli.exe list game.arc
  iga-cli.exe extract game.arc ./output
  iga-cli.exe extract-single game.arc 5 ./output

SUPPORTED FORMATS:
  Archives: .arc, .bld, .pak, .iga

OUTPUT:
  Progress output to console, errors to stderr
");
        }

        static void ListCommand(string[] args)
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: iga-cli list <archive_file>");
                Environment.Exit(1);
            }

            string archiveFile = args[1];

            if (!File.Exists(archiveFile))
            {
                Console.Error.WriteLine($"❌ File not found: {archiveFile}");
                Environment.Exit(1);
            }

            IGA_Version version = DetectGameVersion(archiveFile);
            
            Console.WriteLine($"📁 Listing files in: {Path.GetFileName(archiveFile)}");
            Console.WriteLine($"🔍 Format detected: {version}");
            Console.WriteLine();

            IGA_File igaFile = new IGA_File(archiveFile, version);

            Console.WriteLine($"📊 Total files: {igaFile.numberOfFiles}");
            Console.WriteLine();
            Console.WriteLine("INDEX       SIZE PATH");
            Console.WriteLine("────────────────────────────────────────");

            for (uint i = 0; i < igaFile.numberOfFiles; i++)
            {
                uint size = igaFile.localFileHeaders[i].size;
                string path = igaFile.names[i];
                Console.WriteLine($"{i:D4}  {size:D12}  {path}");
            }

            igaFile.Close();
            Environment.Exit(0);
        }

        static void ExtractCommand(string[] args)
        {
            if (args.Length < 3)
            {
                Console.Error.WriteLine("Usage: iga-cli extract <archive_file> <output_directory>");
                Environment.Exit(1);
            }

            string archiveFile = args[1];
            string outputDir = args[2];

            if (!File.Exists(archiveFile))
            {
                Console.Error.WriteLine($"❌ File not found: {archiveFile}");
                Environment.Exit(1);
            }

            Directory.CreateDirectory(outputDir);

            IGA_Version version = DetectGameVersion(archiveFile);
            IGA_File igaFile = new IGA_File(archiveFile, version);

            Console.WriteLine($"📦 Extracting archive: {Path.GetFileName(archiveFile)}");
            Console.WriteLine($"📁 Output directory: {Path.GetFullPath(outputDir)}");
            Console.WriteLine($"📊 Total files: {igaFile.numberOfFiles}");
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
                        Console.WriteLine($"✓ [{i + 1:D4}/{igaFile.numberOfFiles:D4}] {igaFile.names[i]}");
                        successCount++;
                    }
                    else
                    {
                        Console.Error.WriteLine($"✗ [{i + 1:D4}/{igaFile.numberOfFiles:D4}] FAILED: {igaFile.names[i]}");
                        failedCount++;
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"✗ [{i + 1:D4}/{igaFile.numberOfFiles:D4}] ERROR: {igaFile.names[i]} - {ex.Message}");
                    failedCount++;
                }
            }

            sw.Stop();
            igaFile.Close();

            Console.WriteLine();
            Console.WriteLine("════════════════════════════════════════");
            Console.WriteLine($"✓ Success:  {successCount}/{igaFile.numberOfFiles}");
            Console.WriteLine($"✗ Failed:   {failedCount}/{igaFile.numberOfFiles}");
            Console.WriteLine($"⏱ Time:     {sw.Elapsed.TotalSeconds:F2}s");
            Console.WriteLine("════════════════════════════════════════");

            Environment.Exit(failedCount > 0 ? 1 : 0);
        }

        static void ExtractSingleCommand(string[] args)
        {
            if (args.Length < 4)
            {
                Console.Error.WriteLine("Usage: iga-cli extract-single <archive_file> <file_index> <output_directory>");
                Environment.Exit(1);
            }

            string archiveFile = args[1];
            
            if (!uint.TryParse(args[2], out uint fileIndex))
            {
                Console.Error.WriteLine($"❌ Invalid file index: {args[2]}");
                Environment.Exit(1);
            }

            string outputDir = args[3];

            if (!File.Exists(archiveFile))
            {
                Console.Error.WriteLine($"❌ File not found: {archiveFile}");
                Environment.Exit(1);
            }

            Directory.CreateDirectory(outputDir);

            IGA_Version version = DetectGameVersion(archiveFile);
            IGA_File igaFile = new IGA_File(archiveFile, version);

            if (fileIndex >= igaFile.numberOfFiles)
            {
                Console.Error.WriteLine($"❌ File index {fileIndex} out of range (max {igaFile.numberOfFiles - 1})");
                igaFile.Close();
                Environment.Exit(1);
            }

            Console.WriteLine($"📦 Extracting single file");
            Console.WriteLine($"   Archive: {Path.GetFileName(archiveFile)}");
            Console.WriteLine($"   File:    {igaFile.names[fileIndex]} (index {fileIndex})");
            Console.WriteLine($"   Size:    {igaFile.localFileHeaders[fileIndex].size} bytes");
            Console.WriteLine();

            try
            {
                igaFile.ExtractFile(fileIndex, outputDir, out int res, true);

                if (res == 0)
                {
                    Console.WriteLine($"✓ Successfully extracted: {igaFile.names[fileIndex]}");
                    Console.WriteLine($"  Output: {Path.Combine(outputDir, igaFile.names[fileIndex])}");
                }
                else
                {
                    Console.Error.WriteLine($"✗ Failed to extract file");
                    igaFile.Close();
                    Environment.Exit(1);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"✗ Exception: {ex.Message}");
                igaFile.Close();
                Environment.Exit(1);
            }

            igaFile.Close();
            Environment.Exit(0);
        }


        static IGA_Version DetectGameVersion(string filename)
        {
            // Auto-detect based on file extension and content
            string ext = Path.GetExtension(filename).ToLower();
            
            // Try to read the magic number from the file
            try
            {
                using (var fs = File.OpenRead(filename))
                {
                    byte[] magic = new byte[4];
                    fs.Read(magic, 0, 4);

                    // IGA files start with 0x1A414749 or 0x4947411A
                    uint magicNum = BitConverter.ToUInt32(magic, 0);
                    
                    if (magicNum == 0x1A414749 || magicNum == 0x4947411A)
                    {
                        // Read version byte at offset 4
                        fs.Seek(4, SeekOrigin.Begin);
                        byte version = (byte)fs.ReadByte();

                        // Map version byte to enum
                        // This is simplified - you may need to adjust based on actual game versions
                        return (IGA_Version)version;
                    }
                }
            }
            catch
            {
                // Fall back to extension-based detection
            }

            // Default detection based on extension
            return ext switch
            {
                ".arc" => IGA_Version.SkylandersImaginatorsPS4,
                ".bld" => IGA_Version.SkylandersTrapTeam,
                ".pak" => IGA_Version.SkylandersSuperChargers,
                ".iga" => IGA_Version.SkylandersSwapForce,
                ".igz" => IGA_Version.SkylandersImaginatorsPS4,
                _ => IGA_Version.SkylandersImaginatorsPS4
            };
        }
    }
}
