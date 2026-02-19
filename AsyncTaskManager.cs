using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace IGAE_GUI
{
    /// <summary>
    /// Gestionnaire asynchrone pour les opérations longues avec suivi de progression
    /// </summary>
    public class AsyncTaskManager
    {
        public class TaskProgress
        {
            public int TotalItems { get; set; }
            public int CompletedItems { get; set; }
            public string? CurrentItem { get; set; }
            public string? Status { get; set; }
            public int PercentProgress => TotalItems > 0 ? (int)((CompletedItems / (float)TotalItems) * 100) : 0;
            public bool IsComplete => CompletedItems >= TotalItems;
        }

        public event EventHandler<TaskProgress>? ProgressChanged;
        public event EventHandler<string>? TaskCompleted;
        public event EventHandler<Exception>? TaskFailed;

        private CancellationTokenSource? _cts;
        private TaskProgress _progress;
        private readonly ReaderWriterLockSlim _progressLock = new();

        public AsyncTaskManager()
        {
            _progress = new TaskProgress();
        }

        /// <summary>
        /// Lance une opération asynchrone avec suivi de progression
        /// </summary>
        public async Task ExecuteAsync(
            Func<IProgress<TaskProgress>, CancellationToken, Task> operation,
            string operationName)
        {
            _cts = new CancellationTokenSource();
            
            var progress = new Progress<TaskProgress>(p =>
            {
                _progressLock.EnterWriteLock();
                try
                {
                    _progress = p;
                    ProgressChanged?.Invoke(this, p);
                }
                finally
                {
                    _progressLock.ExitWriteLock();
                }
            });

            try
            {
                await operation(progress, _cts.Token);
                TaskCompleted?.Invoke(this, operationName);
            }
            catch (OperationCanceledException)
            {
                TaskFailed?.Invoke(this, new Exception("Opération annulée par l'utilisateur"));
            }
            catch (Exception ex)
            {
                TaskFailed?.Invoke(this, ex);
            }
        }

        public TaskProgress GetCurrentProgress()
        {
            _progressLock.EnterReadLock();
            try
            {
                return new TaskProgress
                {
                    TotalItems = _progress.TotalItems,
                    CompletedItems = _progress.CompletedItems,
                    CurrentItem = _progress.CurrentItem,
                    Status = _progress.Status
                };
            }
            finally
            {
                _progressLock.ExitReadLock();
            }
        }

        public void Cancel()
        {
            _cts?.Cancel();
        }
    }

    /// <summary>
    /// Pool de workers pour paralléliser les extractions
    /// </summary>
    public class ParallelExtractionWorker
    {
        private readonly int _maxConcurrentTasks;
        private readonly SemaphoreSlim _semaphore;

        public ParallelExtractionWorker(int maxConcurrentTasks = 4)
        {
            _maxConcurrentTasks = maxConcurrentTasks;
            _semaphore = new SemaphoreSlim(_maxConcurrentTasks);
        }

        /// <summary>
        /// Exécute des tâches en parallèle avec un maximum de tâches concurrent
        /// </summary>
        public async Task ExecuteParallelAsync<T>(
            IEnumerable<T> items,
            Func<T, CancellationToken, Task> taskFactory,
            IProgress<(T item, bool success)> progress,
            CancellationToken cancellationToken)
        {
            var tasks = items.Select(item => ExecuteWithSemaphoreAsync(item, taskFactory, progress, cancellationToken));
            await Task.WhenAll(tasks);
        }

        private async Task ExecuteWithSemaphoreAsync<T>(
            T item,
            Func<T, CancellationToken, Task> taskFactory,
            IProgress<(T, bool)> progress,
            CancellationToken cancellationToken)
        {
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                await taskFactory(item, cancellationToken);
                progress.Report((item, true));
            }
            catch
            {
                progress.Report((item, false));
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}
