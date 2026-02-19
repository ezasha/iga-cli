using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.Design;
using IGAE_GUI.Types;
using IGAE_GUI.Utils;

namespace IGAE_GUI.IGZ
{
    public partial class IGZ_GeneralForm : Form {
        TreeNode fixups = new("Fixups");
        TreeNode objects = new("Object List");
        IGZ_File _igz;

        private Dictionary<TreeNode, igObject> igObjectMap = new();
        List<TreeNode> filtered = new();
        List<TreeNode> unfiltered = new();
        
        private AsyncTaskManager _taskManager = new AsyncTaskManager();
        private ParallelExtractionWorker _extractionWorker = new ParallelExtractionWorker(4);

        public IGZ_GeneralForm(IGZ_File igz) {
            InitializeComponent();

            _igz = igz;

            if (_igz.ebr.BaseStream.GetType() == typeof(FileStream)) {
                btnSaveIGZ.Enabled = false;
                btnSaveIGZ.Visible = false;
            }

            if (_igz.fixups != null) Console.WriteLine("Fixups exist");
            if (_igz.fixups != null)
            {
                foreach (IGZ_Fixup fixup in _igz.fixups) {
                if (igz.ebr._endianness == StreamHelper.Endianness.Big) {
                    fixups.Nodes.Add(
                        System.Text.Encoding.ASCII.GetString(BitConverter.GetBytes(fixup.magicNumber).Reverse()
                            .ToArray()));
                }
                else {
                    fixups.Nodes.Add(System.Text.Encoding.ASCII.GetString(BitConverter.GetBytes(fixup.magicNumber)));
                }

                switch (fixup.magicNumber) {
                    case 0x544D4554:
                        if (fixup is IGZ_TMET tmet)
                        {
                            foreach (string item in tmet.typeNames) {
                                fixups.Nodes[fixups.Nodes.Count - 1].Nodes.Add(item);
                            }
                        }
                        break;
                    case 0x54535452:
                        if (fixup is IGZ_TSTR tstr)
                        {
                            foreach (string item in tstr.strings) {
                                fixups.Nodes[fixups.Nodes.Count - 1].Nodes.Add(item);
                            }
                        }
                        break;
                    case 0x54444550:
                        if (fixup is IGZ_TDEP tdep)
                        {
                            foreach (string item in tdep.dependancies) {
                                fixups.Nodes[fixups.Nodes.Count - 1].Nodes.Add(item);
                            }
                        }
                        break;
                    case 0x45584944:
                        if (fixup is IGZ_EXID exid)
                        {
                            for (uint i = 0; i < exid.count; i++) {
                            string hashName = string.Empty;
                            string type;

                            if (Enum.IsDefined(typeof(IGZ_TextureFormat), (IGZ_TextureFormat)exid.hashes[i])) {
                                hashName = ((IGZ_TextureFormat)exid.hashes[i]).ToString().Split('.').Last();
                                if (hashName.EndsWith("_old")) {
                                    hashName = hashName.Substring(0, 4);
                                }
                            }
                            else {
                                hashName = "Unknown";
                            }

                            type = exid.types[i].ToString("X8");

                            fixups.Nodes[fixups.Nodes.Count - 1].Nodes
                                .Add($"{type}: {exid.hashes[i].ToString("X08")} ({hashName})");
                        }
                        }

                        break;
                    case 0x45584E4D:
                        if (fixup is IGZ_EXNM exnm)
                        {
                            IGZ_TSTR? tstr2 = _igz.fixups?.FirstOrDefault(x => x.magicNumber == 0x54535452) as IGZ_TSTR;
                            if (tstr2 != null)
                            {
                                for (uint i = 0; i < exnm.count; i++) {
                                    fixups.Nodes[fixups.Nodes.Count - 1].Nodes
                                        .Add($"{tstr2.strings[exnm.types[i]]}: {tstr2.strings[exnm.names[i]]}");
                                }
                            }
                        }
                        break;
                    case 0x52565442:
                        if (fixup is IGZ_RVTB rvtb)
                        {
                            for (uint i = 0; i < rvtb.count; i++) {
                                fixups.Nodes[fixups.Nodes.Count - 1].Nodes.Add($"{rvtb.offsets[i].ToString("X08")}");
                            }
                        }
                        break;
                    case 0x52545352:
                        if (fixup is IGZ_RSTR rstr)
                        {
                            for (uint i = 0; i < rstr.count; i++) {
                                fixups.Nodes[fixups.Nodes.Count - 1].Nodes.Add($"{rstr.offsets[i]:X08}");
                            }
                        }
                        break;
                    case 0x544D484E:
                        if (fixup is IGZ_TMHN tmhn)
                        {
                            for (uint i = 0; i < tmhn.count; i++) {
                                fixups.Nodes[fixups.Nodes.Count - 1].Nodes
                                    .Add($"{tmhn.sizes[i].ToString("X08")}: {tmhn.offsets[i].ToString("X08")}");
                            }
                        }
                        break;
                    case 0x4D54535A:
                        if (fixup is IGZ_MTSZ mtsz)
                        {
                            for (uint i = 0; i < mtsz.count; i++) {
                                fixups.Nodes[fixups.Nodes.Count - 1].Nodes.Add(mtsz.metaSizes[i].ToString("X08"));
                            }
                        }
                        break;
                }
            }
            }

            AddObject(igz.objectList._objects.ToArray());

            treeItems.Nodes.Add(fixups);
            treeItems.Nodes.Add(objects);
            objects.Nodes.AddRange(filtered.ToArray());

            Config config = Config.Read();

            if (config.darkMode) {
                foreach (Control control in Controls) {
                    Themes.SetControlToDark(control);
                }

                Themes.SetWindowControlToDark(this);
            }
            else {
                foreach (Control control in Controls) {
                    Themes.SetControlToLight(control);
                }

                Themes.SetControlToLight(this);
            }
        }

        void AddObject(igObject[] objs) {
            IGZ_RVTB? rvtb = _igz.fixups?.FirstOrDefault(x => x.magicNumber == 0x52565442) as IGZ_RVTB;
            for (int i = 0; i < objs.Length; i++) {
                string objectType;
                var igObject = objs[i];

                if (_igz.version <= 0x09) {
                    IGZ_TMET? types = _igz.fixups?.FirstOrDefault(x => x.magicNumber == 0x544D4554) as IGZ_TMET;
                    try {
                        objectType = types?.typeNames[igObject.name] ?? objs[(int)i].name.ToString("X08");
                    }
                    catch (Exception) {
                        objectType = objs[(int)i].name.ToString("X08");
                    }
                }
                else {
                    IGZ_TSTR? strings = _igz.fixups?.FirstOrDefault(x => x.magicNumber == 0x54535452) as IGZ_TSTR;
                    try {
                        objectType = strings?.strings[igObject.name] ?? objs[i].name.ToString("X08");
                    }
                    catch (Exception) {
                        objectType = objs[i].name.ToString("X08");
                    }
                }
                //potentialParentNode = objects.Nodes.Add($"{i.ToString("X04")} : {(rvtb.offsets[i+1]).ToString("X08")} : {objs[i].length.ToString("X08")} => {objectType}");

                long DeserializeOffset(int offset) {
                if(_igz.version <= 0x06) return (_igz.descriptors[(offset >> 0x18) + 1].offset + (offset & 0x00FFFFFF));
                                    return (_igz.descriptors[(offset >> 0x1B) + 1].offset + (offset & 0x07FFFFFF));
                }

                var name = igObject.offset.ToString("X04") + ": " + objectType;
                if (igObject is igImage2) {
                    try {
                        _igz.ebr.BaseStream.Seek(igObject.offset + 0x8, SeekOrigin.Begin); // seek to igImage 2 + 8
                        var namePointer =
                            DeserializeOffset((int)_igz.ebr.ReadUInt32()); // read a uint32 and deserialize that
                        _igz.ebr.BaseStream.Seek(namePointer, SeekOrigin.Begin); // seek to that offset
                        var tmpName = igObject.offset.ToString("X04") + ": " + _igz.ebr.ReadString(); // read a string
                        if (tmpName.Length < 5)
                        {
                        }
                        // Console.WriteLine(tmpName + " is probably a bad name. falling back to offsets");
                        else name = tmpName;
                    }
                    catch (Exception) {
                        // ignored
                    }
                }

                var treeNode = new TreeNode(name);
                igObjectMap.Add(treeNode, igObject);
                unfiltered.Add(treeNode);
                if (objectType == "igImage2") filtered.Add(treeNode);

                //Console.WriteLine(i.ToString("X04") + " : " + objs[i].children.Count);
                for (uint j = 0; j < igObject.children.Count; j++) {
                    AddObject(igObject.children.ToArray());
                }
            }
        }

        void SelectionChange(object sender, TreeViewEventArgs e) {
            if (treeItems.SelectedNode.Parent == objects) {
                var igObject = igObjectMap[treeItems.SelectedNode];

                if (igObject.GetType() == typeof(igImage2)) {
                    Console.WriteLine("igImage2 Selected");
                    MemoryStream msImage = new MemoryStream();
                    (igObject as igImage2)?.Extract(msImage);
                    if (msImage.Length == 0) return;
                    msImage.Seek(0x00, SeekOrigin.Begin);
                    pbTexturePreview.Image = TextureHelper.BitmapFromDDS(msImage);
                    pbTexturePreview.Visible = true;
                    btnTextureExtract.Visible = true;
                    btnTextureReplace.Visible = true;
                    msImage.Close();
                }
                else {
                    pbTexturePreview.Image = null;
                    pbTexturePreview.Visible = false;
                    btnTextureExtract.Visible = false;
                    btnTextureReplace.Visible = false;
                }
            }
            else {
                pbTexturePreview.Image = null;
                pbTexturePreview.Visible = false;
                btnTextureExtract.Visible = false;
                btnTextureReplace.Visible = false;
            }
        }

        void ExportTexture(object sender, EventArgs e) {
            using (SaveFileDialog sfd = new SaveFileDialog()) {
                sfd.Filter = "DirectDraw Surface Files (*.dds)|*.dds|All Files (*.*)|*.*";
                sfd.RestoreDirectory = true;
                if (treeItems.SelectedNode.FullPath != "")
                    sfd.FileName =
                        treeItems.SelectedNode.FullPath.Replace("\\", "/")[
                                (treeItems.SelectedNode.FullPath.LastIndexOf("/", StringComparison.Ordinal) + 1)..]
                            .Replace(".png", ".dds");
                if (sfd.FileName.Contains("#"))
                {
                    string[] parts = sfd.FileName.Split("#");
                    sfd.FileName = parts[parts.Length - 2];
                }
                if (sfd.ShowDialog() == DialogResult.OK) {
                    FileStream ofs = new FileStream(sfd.FileName, FileMode.Create, FileAccess.ReadWrite);
                    (igObjectMap[treeItems.SelectedNode] as igImage2)?.Extract(ofs);
                    ofs.Close();
                }
            }
        }

        private void ImportTexture(object sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog();
            ofd.RestoreDirectory = true;
            ofd.Filter = "All Files (*.*)|*.*|DirectDraw Surface Files (*.dds)|*.dds";

            if (ofd.ShowDialog() != DialogResult.OK) return;
            var ifs = new FileStream(ofd.FileName, FileMode.Open, FileAccess.ReadWrite);
            (igObjectMap[treeItems.SelectedNode] as igImage2)!.Replace(ifs);
        }
        void ExportDDSTexture(object sender, EventArgs e)
        {
            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Filter = "DirectDraw Surface Files (*.dds)|*.dds|All Files (*.*)|*.*";
                sfd.RestoreDirectory = true;
                if (treeItems.SelectedNode.FullPath != "")
                    sfd.FileName =
                        treeItems.SelectedNode.FullPath.Replace("\\", "/")[
                                (treeItems.SelectedNode.FullPath.LastIndexOf("/", StringComparison.Ordinal) + 1)..]
                            .Replace(".png", ".dds");
                if (sfd.FileName.Contains("#"))
                {
                    string[] parts = sfd.FileName.Split("#");
                    sfd.FileName = parts[parts.Length - 2];
                }
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    FileStream ofs = new FileStream(sfd.FileName, FileMode.Create, FileAccess.ReadWrite);
                    (igObjectMap[treeItems.SelectedNode] as igImage2)?.ExtractDDS(ofs);
                    ofs.Close();
                }
            }
        }

        private void ImportDDSTexture(object sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog();
            ofd.RestoreDirectory = true;
            ofd.Filter = "DirectDraw Surface Files (*.dds)|*.dds";

            if (ofd.ShowDialog() != DialogResult.OK) return;
            var ifs = new FileStream(ofd.FileName, FileMode.Open, FileAccess.ReadWrite);
            (igObjectMap[treeItems.SelectedNode] as igImage2)!.ReplaceDDS(ifs);
        }

        private async void ExportAllObjects(object sender, EventArgs e) {
            using var fbd = new FolderBrowserDialog();
            if (fbd.ShowDialog() != DialogResult.OK) return;

            var exportDir = fbd.SelectedPath;
            var itemsToExport = unfiltered.Where(key =>
            {
                if (igObjectMap.TryGetValue(key, out var igObj))
                    return igObj is igImage2;
                return false;
            }).ToList();

            if (itemsToExport.Count == 0)
            {
                MessageBox.Show("No igImage2 objects to export", "Export All", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Show progress form
            ProgressForm progressForm = new ProgressForm($"Exporting {itemsToExport.Count} textures...");
            progressForm.Show();

            try
            {
                int processedCount = 0;
                int failedCount = 0;

                await _taskManager.ExecuteAsync(async (progress, cancellationToken) =>
                {
                    // Prepare all export tasks with cancellation support
                    var exportTasks = itemsToExport.Select(key =>
                    {
                        return Task.Run(async () =>
                        {
                            try
                            {
                                cancellationToken.ThrowIfCancellationRequested();

                                var nodeName = key.ToString()!.Substring("TreeNode: ".Length);
                                var fileName = nodeName.Replace("\\", "/")[(nodeName.LastIndexOf("/", StringComparison.Ordinal) + 1)..];
                                
                                if (fileName.Contains("#"))
                                {
                                    string[] parts = fileName.Split("#");
                                    fileName = parts[parts.Length - 2];
                                    fileName += ".dds";
                                }

                                var fullPath = Path.Combine(exportDir, fileName.Replace(".png", ".dds"));
                                var dirPath = Path.GetDirectoryName(fullPath);
                                
                                // Ensure directory exists
                                if (!string.IsNullOrEmpty(dirPath))
                                    Directory.CreateDirectory(dirPath);

                                if (igObjectMap[key] is igImage2 image2)
                                {
                                    await Task.Run(() =>
                                    {
                                        using var ofs = new FileStream(fullPath, FileMode.Create, FileAccess.Write);
                                        image2.Extract(ofs);
                                    }, cancellationToken);

                                    Interlocked.Increment(ref processedCount);
                                    
                                    // Update progress
                                    progress.Report(new AsyncTaskManager.TaskProgress
                                    {
                                        TotalItems = itemsToExport.Count,
                                        CompletedItems = processedCount,
                                        CurrentItem = fileName,
                                        Status = $"Exporting: {processedCount}/{itemsToExport.Count}"
                                    });

                                    // Small delay to prevent UI freezing
                                    await Task.Delay(5, cancellationToken);
                                }
                            }
                            catch (OperationCanceledException)
                            {
                                throw;
                            }
                            catch (Exception ex)
                            {
                                Interlocked.Increment(ref failedCount);
                                Console.WriteLine($"Error exporting texture: {ex.Message}");
                            }
                        }, cancellationToken);
                    });

                    // Execute exports with controlled concurrency (max 4 concurrent)
                    var semaphore = new SemaphoreSlim(4);
                    var allTasks = new List<Task>();

                    foreach (var exportTask in exportTasks)
                    {
                        await semaphore.WaitAsync(cancellationToken);
                        var task = Task.Run(async () =>
                        {
                            try
                            {
                                await exportTask;
                            }
                            finally
                            {
                                semaphore.Release();
                            }
                        }, cancellationToken);
                        allTasks.Add(task);
                    }

                    await Task.WhenAll(allTasks);

                    progress.Report(new AsyncTaskManager.TaskProgress
                    {
                        TotalItems = itemsToExport.Count,
                        CompletedItems = processedCount,
                        CurrentItem = "Complete",
                        Status = $"Export finished: {processedCount} succeeded, {failedCount} failed"
                    });

                }, "ExportAllObjects");

                MessageBox.Show($"Export completed: {processedCount} objects exported successfully", 
                    "Export All Progress", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (OperationCanceledException)
            {
                MessageBox.Show("Export cancelled by user", "Export All Progress", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during export: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                progressForm.Close();
                progressForm.Dispose();
            }
        }

        void Save(object sender, EventArgs e) {
            using (SaveFileDialog sfd = new SaveFileDialog()) {
                sfd.Filter = "IGZ Files (*.igz)|*.igz;level.bld;*.pak;*.lang|All Files (*.*)|*.*";
                sfd.RestoreDirectory = true;

                if (sfd.ShowDialog() == DialogResult.OK) {
                    FileStream ofs = new FileStream(sfd.FileName, FileMode.Create, FileAccess.ReadWrite);
                    _igz.ebr.BaseStream.Seek(0x00, SeekOrigin.Begin);
                    _igz.ebr.BaseStream.CopyTo(ofs);
                    ofs.Flush();
                    ofs.Close();
                }
            }
        }

        void ChangeFilter(object sender, EventArgs e) {
            objects.Nodes.Clear();
            if (cbFilterImages.Checked) {
                objects.Nodes.AddRange(filtered.ToArray());
            }
            else {
                objects.Nodes.AddRange(unfiltered.ToArray());
            }
        }

        private new void Closing(object sender, EventArgs e) {
            _igz.ebr?.Close();
        }
    }
}