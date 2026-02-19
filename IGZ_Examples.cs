using System;
using System.IO;
using System.Windows.Forms;
using System.Threading;
using System.Threading.Tasks;
using IGAE_GUI.Types;

namespace IGAE_GUI.IGZ
{
    /// <summary>
    /// ⚠️ FICHIER D'EXEMPLES DE RÉFÉRENCE SEULEMENT
    /// 
    /// Ce fichier n'est pas destiné à être utilisé directement.
    /// Copiez les patterns que vous voulez adapter dans IGZ_GeneralForm.cs
    /// 
    /// Les exemples montrent comment:
    /// 1. Rendre les opérations asynchrones
    /// 2. Gérer la cancellation
    /// 3. Limiter le parallélisme
    /// 4. Mettre à jour la progression
    /// </summary>
    public class IGZ_AsyncPatterns_Reference
    {
        // ====================================================================
        // EXEMPLE 1: Import texture asynchrone
        // ====================================================================
        
        public async Task ImportTextureAsync(string fileName)
        {
            try
            {
                using var ifs = new FileStream(fileName, FileMode.Open, FileAccess.ReadWrite);
                
                await Task.Run(() =>
                {
                    // Votre code d'import ici
                    // selectedImage?.Replace(ifs);
                });

                MessageBox.Show("Texture imported successfully");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ====================================================================
        // EXEMPLE 2: Export avec progress tracking
        // ====================================================================

        public async Task ExportTextureAsync(
            string fileName, 
            IProgress<int> progress, 
            CancellationToken cancellationToken)
        {
            try
            {
                using (SaveFileDialog sfd = new SaveFileDialog())
                {
                    sfd.Filter = "DirectDraw Surface Files (*.dds)|*.dds";

                    if (sfd.ShowDialog() != DialogResult.OK) return;

                    await Task.Run(() =>
                    {
                        using var ofs = new FileStream(sfd.FileName, FileMode.Create, FileAccess.Write);
                        // image2.Extract(ofs);
                        progress.Report(100);
                    }, cancellationToken);

                    MessageBox.Show("Export successful");
                }
            }
            catch (OperationCanceledException)
            {
                MessageBox.Show("Export cancelled");
            }
        }

        // ====================================================================
        // EXEMPLE 3: Multiple exports avec semaphore (RECOMMENDED)
        // ====================================================================

        public async Task ExportMultipleAsync(
            int itemCount,
            Func<int, Task> exportFunc,
            IProgress<(int completed, int total)> progress,
            CancellationToken cancellationToken)
        {
            var semaphore = new SemaphoreSlim(4); // Max 4 concurrent
            var tasks = new System.Collections.Generic.List<Task>();

            for (int i = 0; i < itemCount; i++)
            {
                int index = i; // Important: capturer la valeur
                await semaphore.WaitAsync(cancellationToken);

                var task = Task.Run(async () =>
                {
                    try
                    {
                        await exportFunc(index);
                        progress.Report((index + 1, itemCount));
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, cancellationToken);

                tasks.Add(task);
            }

            await Task.WhenAll(tasks);
        }

        // ====================================================================
        // EXEMPLE 4: Queue-based processing (pour opérations non-parallélisables)
        // ====================================================================

        public async Task ProcessQueueAsync(
            System.Collections.Generic.Queue<string> items,
            Func<string, Task> processFunc,
            IProgress<int> progress,
            CancellationToken cancellationToken)
        {
            int processed = 0;
            int total = items.Count;

            while (items.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var item = items.Dequeue();

                try
                {
                    await processFunc(item);
                    processed++;
                    progress.Report((int)((processed / (float)total) * 100));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error processing {item}: {ex.Message}");
                }

                // Petite pause pour laisser l'UI réagir
                await Task.Delay(10, cancellationToken);
            }
        }

        // ====================================================================
        // EXEMPLE 5: Utiliser AsyncTaskManager (RECOMMENDED)
        // ====================================================================

        public async Task ExportWithAsyncManagerAsync(
            System.Collections.Generic.List<string> items,
            AsyncTaskManager taskManager)
        {
            await taskManager.ExecuteAsync(async (progress, cancellationToken) =>
            {
                int completed = 0;

                foreach (var item in items)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Process item
                    await ProcessItemAsync(item, cancellationToken);

                    completed++;
                    progress.Report(new AsyncTaskManager.TaskProgress
                    {
                        TotalItems = items.Count,
                        CompletedItems = completed,
                        CurrentItem = item,
                        Status = $"Processing {completed}/{items.Count}"
                    });

                    await Task.Delay(10, cancellationToken);
                }

            }, "ExportWithAsyncManager");
        }

        private Task ProcessItemAsync(string item, CancellationToken ct)
        {
            return Task.CompletedTask; // Placeholder
        }
    }

    // ====================================================================
    // Helper: Simple progress dialog
    // ====================================================================

    public class SimpleProgressDialog : Form
    {
        private ProgressBar? _bar;
        private Label? _status;

        public SimpleProgressDialog(string title)
        {
            Text = title;
            Width = 350;
            Height = 100;
            StartPosition = FormStartPosition.CenterParent;
            
            _status = new Label 
            { 
                Location = new System.Drawing.Point(10, 10), 
                Width = 300, 
                Height = 20, 
                Text = "Processing..." 
            };
            _bar = new ProgressBar 
            { 
                Location = new System.Drawing.Point(10, 35), 
                Width = 300, 
                Height = 20, 
                Minimum = 0, 
                Maximum = 100 
            };
            
            Controls.Add(_status!);
            Controls.Add(_bar!);
        }

        public void SetProgress(int percent, string? status = null)
        {
            if (InvokeRequired)
            {
                Invoke(() => SetProgress(percent, status));
                return;
            }

            _bar!.Value = Math.Min(percent, 100);
            if (status != null) _status!.Text = status;
            Update();
        }
    }
}
