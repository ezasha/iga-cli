using IGAE_GUI;
using IGAE_GUI.IGZ;
using IGAE_GUI.Types;

namespace igArchiveExtractor;

internal abstract class Program {
    [STAThread]
    private static void Main(string[] args) {
        if (args.Length == 0) OpenGui();
        else {
            Console.WriteLine("Igae CLI version who knows at this point. IgCauldron save me please");
            Console.WriteLine("Igae 1.0.7f by Neffy. Forked modifications and CLI by hydos & Glitched Gamer");
            var cmd = args[0];
            switch (cmd) {
                case "extractAll":
                    CmdExtractAll(args[1..]);
                    break;
                case "modifyFiles":
                    CmdModifyFiles(args[1..]);
                    break;
                case "extractTexture":
                    CmdExtractTexture(args[1..]);
                    break;
                case "replaceTexture":
                    CmdReplaceTexture(args[1..]);
                    break;
                case "fullyReplaceTexture":
                    CmdReplaceWholeDDSTexture(args[1..]);
                    break;
                case "listFiles":
                    CmdListFiles(args[1..]);
                    break;
                case "createIgaCompactCSV":
                    CmdCreateIgaCSV(args[1..]);
                    break;
                default: {
                    DisplayHelpMessage(args[1..]);
                    break;
                }
            }
        }
    }

    private static void CmdCreateIgaCSV(IReadOnlyList<string> args)
    {
        if (args.Count < 3) throw new Exception("Expected at least 3 arguments");
        var alchemyVersion = GetAlchemyIgaVersion(args[0], args[1]);
        var filePath = args[2];
        var archive = new IGA_File(filePath, alchemyVersion);
        var outputFile = args[3];
        
        var fs = new StreamWriter(outputFile, false, System.Text.Encoding.ASCII);

        for(var i = 0; i < archive.numberOfFiles; i++)
        {
            var wtfJasleenOffset = archive.stream.ReadUInt32WithOffset(IGA_Structure.headerData[archive._version][(int)IGA_HeaderData.ChecksumLocation] + (uint)(i * 4u));
            fs.WriteLine($"{archive.names[i]},{wtfJasleenOffset}");
        }
        fs.Close();
    }

    private static void DisplayHelpMessage(IReadOnlyList<string> args) {
        if (args.Count > 1) {
            switch (args[0]) {
                case "extractAll": {
                    Console.WriteLine("extractAll <platform> <game> <igaFile> <extractLocation>");
                    Console.WriteLine("<platform>: the platform you want to read from. For example: wii, wiiu, ps4, ps3, switch, etc");
                    Console.WriteLine("<game>: the shortened name of the game. For example: ssa, sg, ssf, stt, ssc, si, sli, crash");
                    Console.WriteLine("<igaFile>: the IGA you are trying to extract from");
                    Console.WriteLine("<extractLocation>: the location to extract the contents to");
                    break;
                }

                case "modifyFiles": {
                    Console.WriteLine("modifyFiles <platform> <game> <igaFile> <action> <replacementDirectory> <outputIga>");
                    Console.WriteLine("<platform>: the platform you want to read from. For example: wii, wiiu, ps4, ps3, switch, etc");
                    Console.WriteLine("<game>: the shortened name of the game. For example: ssa, sg, ssf, stt, ssc, si, sli, crash");
                    Console.WriteLine("<igaFile>: the IGA you are trying to modify");
                    Console.WriteLine("<action>: this setting does absolutely nothing due to limitations with igae");
                    break;
                }

                default:
                    Console.Error.WriteLine($"Couldn't help with the command \"{args[0]}\" :( Just ping me if you need help");
                    break;
            }
        }
        else {
            Console.WriteLine("Available Commands:");
            Console.WriteLine("extractAll <platform> <game> <igaFile> <extractLocation>");
            Console.WriteLine("modifyFiles <platform> <game> <igaFile> <action> <inputDirectory> <outputIga>");
            Console.WriteLine("extractTexture <igzFile> <igImage2Name> <outputTexturePath>");
            Console.WriteLine("replaceTexture <igzFile> <igImage2Name> <inputTexturePath> <outputIgzPath>");
            Console.WriteLine("fullyReplaceTexture <igzFile> <igImage2Name> <inputTexturePath> <outputIgzPath>");
            Console.WriteLine("listFiles <platform> <game> <igaFile> <outputFile>");
            Console.WriteLine("createIgaCompactCSV <platform> <game> <igaFile> <outputCSVFile>");
            Console.WriteLine("help");
        }
    }

    private static void CmdModifyFiles(IReadOnlyList<string> strings) {
        if (strings.Count != 6) throw new Exception("Expected 6 arguments");
        var alchemyVersion = GetAlchemyIgaVersion(strings[0], strings[1]);
        var igaFile = new IGA_File(strings[2], alchemyVersion);
        var action = strings[3];
        var inputDirectory = strings[4];
        var outputIgaLoc = strings[5];

        var gamePaths = new string[igaFile.numberOfFiles];
        var computerPaths = new string[igaFile.numberOfFiles];

        for (var i = 0; i < igaFile.numberOfFiles; i++) {
            gamePaths[i] = igaFile.names[i];
            computerPaths[i] = Path.Join(inputDirectory, gamePaths[i]);
        }

        var outputFile = new IGA_File(alchemyVersion, gamePaths, igaFile.crc) {
            slop = igaFile.slop,
            flags = igaFile.flags
        };

        try {
            outputFile.Build(outputIgaLoc, computerPaths);
        }
        catch (Exception e) {
            Console.WriteLine("An error occured building the archive. Maybe you are missing a file?");
            Console.WriteLine(e);
            throw;
        }

        Console.WriteLine("It worked maybe? Who knows with this damn tool");
    }

    private static long DeserializeOffset(IGZ_File igz, int offset) {
        if (igz.version <= 0x06) return igz.descriptors[(offset >> 0x18) + 1].offset + (offset & 0x00FFFFFF);
        return igz.descriptors[offset >> 0x1B].offset + ((offset & 0x07FFFFFF) + 1);
    }

    private static void CmdReplaceTexture(IReadOnlyList<string> strings) {
        if (strings.Count != 4) throw new Exception("Expected 4 arguments");
        var igzFile = new IGZ_File(new FileStream(strings[0], FileMode.Open, FileAccess.ReadWrite));
        var igImage2Name = strings[1];
        var imgFs = new FileStream(strings[2], FileMode.Open, FileAccess.Read);
        
        foreach (var igObject in igzFile.objectList._objects.Where(igObject => igObject is igImage2)) {
            igzFile.ebr.BaseStream.Seek(igObject.offset + 0x8, SeekOrigin.Begin); // seek to igImage 2 + 8
            var name = igObject.offset.ToString("X04");
            if (!name.Equals(igImage2Name)) continue;

            Console.WriteLine("Replacing");
            (igObject as igImage2)?.Replace(imgFs);
            var outputIgzFs = new FileStream(strings[3], FileMode.Create, FileAccess.Write);
            igzFile.ebr.BaseStream.Seek(0x00, SeekOrigin.Begin);
            igzFile.ebr.BaseStream.CopyTo(outputIgzFs);
            outputIgzFs.Flush();
            outputIgzFs.Close();
            return;
        }

        Console.Error.WriteLine($"Failed to find a igImage2 matching the offset/name {igImage2Name}.");
    }
    
    private static void CmdReplaceWholeDDSTexture(IReadOnlyList<string> strings) {
        if (strings.Count != 4) throw new Exception("Expected 4 arguments");
        var igzFile = new IGZ_File(new FileStream(strings[0], FileMode.Open, FileAccess.ReadWrite));
        var igImage2Name = strings[1];
        var imgFs = new FileStream(strings[2], FileMode.Open, FileAccess.Read);
        
        foreach (var igObject in igzFile.objectList._objects.Where(igObject => igObject is igImage2)) {
            igzFile.ebr.BaseStream.Seek(igObject.offset + 0x8, SeekOrigin.Begin); // seek to igImage 2 + 8
            var name = igObject.offset.ToString("X04");
            if (!name.Equals(igImage2Name)) continue;

            Console.WriteLine("Replacing");
            (igObject as igImage2)?.ReplaceDDS(imgFs);
            var outputIgzFs = new FileStream(strings[3], FileMode.Create, FileAccess.Write);
            igzFile.ebr.BaseStream.Seek(0x00, SeekOrigin.Begin);
            igzFile.ebr.BaseStream.CopyTo(outputIgzFs);
            outputIgzFs.Flush();
            outputIgzFs.Close();
            return;
        }

        Console.Error.WriteLine($"Failed to find a igImage2 matching the offset/name {igImage2Name}.");
    }
    
    private static void CmdListFiles(IReadOnlyList<string> strings) {
        if (strings.Count < 3) throw new Exception("Expected at least 3 arguments");
        var alchemyVersion = GetAlchemyIgaVersion(strings[0], strings[1]);
        var filePath = strings[2];
        var archive = new IGA_File(filePath, alchemyVersion);
        var saveToFile = strings.Count > 3;

        if (saveToFile)
        {
            using var outputFile = new StreamWriter(strings[3]);
            foreach (var line in archive.names)
                outputFile.WriteLine(line);
        }
        else
        {
            foreach (var archiveName in archive.names)
            {
                Console.WriteLine(archiveName);
            }
        }
    }

    private static void CmdExtractTexture(IReadOnlyList<string> strings) {
        if (strings.Count != 3) throw new Exception("Expected 3 arguments");
        var igzFile = new IGZ_File(new FileStream(strings[0], FileMode.Open, FileAccess.ReadWrite));
        var igImage2Name = strings[1];
        var outTexPath = strings[2];

        foreach (var igObject in igzFile.objectList._objects.Where(igObject => igObject is igImage2)) {
            igzFile.ebr.BaseStream.Seek(igObject.offset + 0x8, SeekOrigin.Begin); // seek to igImage 2 + 8
            var name = igObject.offset.ToString("X04");
            if (!name.Equals(igImage2Name)) continue;

            Console.WriteLine("Extracting");
            var ofs = new FileStream(outTexPath, FileMode.Create, FileAccess.Write);
            (igObject as igImage2)?.Extract(ofs);
            return;
        }

        Console.Error.WriteLine($"Failed to find a igImage2 matching the offset/name {igImage2Name}.");
    }

    private static uint HashFileName(string name, uint basis = 0x811c9dc5) {
        name = name.ToLower().Replace('\\', '/');
        return name.Aggregate(basis, (current, t) => (current ^ t) * 0x1000193);
    }

    private static void CmdExtractAll(IReadOnlyList<string> strings) {
        if (strings.Count != 4) throw new Exception("Expected 4 arguments");
        var alchemyVersion = GetAlchemyIgaVersion(strings[0], strings[1]);
        var igaFile = new IGA_File(strings[2], alchemyVersion);
        var extractDest = strings[3];

        for (uint i = 0; i < igaFile.numberOfFiles; i++) {
            igaFile.ExtractFile(i, extractDest, out var res);
            var name = igaFile.localFileHeaders[i].path;
            var result = res == 0 ? "succeeded" : "failed because file uses an unsupported compression method";
            Console.WriteLine($"Extracting file {name} {result}");
        }
    }

    private static IGA_Version GetAlchemyIgaVersion(string platform, string game) {
        IGA_Version alchemyVersion;
        switch (game) {
            case "ssa" when platform != "wiiu":
                alchemyVersion = IGA_Version.SkylandersSpyrosAdventureWii;
                break;
            case "ssa" when platform == "wiiu":
            case "sg":
                alchemyVersion = IGA_Version.SkylandersSpyrosAdventureWiiU;
                break;
            case "ssf":
                alchemyVersion = IGA_Version.SkylandersSwapForce;
                break;
            case "stt":
                alchemyVersion = IGA_Version.SkylandersTrapTeam;
                break;
            case "si" when platform == "ps4":
                alchemyVersion = IGA_Version.SkylandersImaginatorsPS4;
                break;
            case "sli":
                alchemyVersion = IGA_Version.SkylandersLostIslands;
                break;
            case "crash":
                alchemyVersion = IGA_Version.CrashNST;
                break;
            default:
                throw new Exception(
                    "Unable to figure out alchemy version :(. Make sure the platform is wii, wiiu, ps4, etc and game is ssa, sg, ssf, stt, ssc, si, sli, crash");
        }

        return alchemyVersion;
    }

    [STAThread]
    private static void OpenGui() {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new Form_igArchiveExtractor(Config.Read()));
    }
}