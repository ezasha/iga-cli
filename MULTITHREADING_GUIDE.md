# Guide de Multithreading pour igArchiveExtractor

## 📋 Vue d'ensemble des changements

Ce guide explique comment rendre votre application multithread pour les opérations d'ouverture, fermeture et extraction de fichiers `.igz`, `.bld`, `.arc` sans crashes.

## 🎯 Problèmes résolus

### Avant (synchrone, bloquant):
```csharp
// ❌ MAUVAIS - Bloque complètement l'UI
private void ExtractAllFiles(object sender, EventArgs e)
{
    for (int i = 0; i < files.Count; i++)
    {
        for (uint j = 0; j < files[i].numberOfFiles; j++)
        {
            files[i].ExtractFile(j, outputPath, out int res); // BLOQUE ICI
            // L'UI est gelée pendant toute l'extraction
        }
    }
}
```

### Après (asynchrone, non-bloquant):
```csharp
// ✅ BON - N'interrompt pas l'UI
private async void ExtractAllFiles(object sender, EventArgs e)
{
    await _taskManager.ExecuteAsync(async (progress, cancellationToken) =>
    {
        // Extraction parallèle avec max 4 fichiers simultanés
        // L'UI reste réactive
        // Suivi de progression en temps réel
    }, "ExtractAllFiles");
}
```

## 🔧 Classes créées

### 1. **AsyncTaskManager.cs**
Gère les opérations asynchrones avec suivi de progression thread-safe.

**Caractéristiques:**
- Rapporte la progression via `IProgress<TaskProgress>`
- Support des `CancellationToken` pour annulation
- Événements: `ProgressChanged`, `TaskCompleted`, `TaskFailed`
- Protection thread-safe avec `ReaderWriterLockSlim`

**Utilisation:**
```csharp
private AsyncTaskManager _taskManager = new AsyncTaskManager();

// Dans une méthode event handler
private async void SomeButtonClick(object sender, EventArgs e)
{
    await _taskManager.ExecuteAsync(async (progress, cancellationToken) =>
    {
        // Votre code asynchrone ici
        progress.Report(new AsyncTaskManager.TaskProgress
        {
            TotalItems = 100,
            CompletedItems = 50,
            CurrentItem = "fichier.txt",
            Status = "Extraction: 50/100"
        });
        
        // Supporter l'annulation
        cancellationToken.ThrowIfCancellationRequested();
    }, "NomDeLOpération");
}
```

### 2. **ParallelExtractionWorker.cs**
Exécute plusieurs tâches en parallèle avec limitation de concurrence.

**Utilisation:**
```csharp
private ParallelExtractionWorker _worker = new ParallelExtractionWorker(maxConcurrentTasks: 4);

var items = new[] { file1, file2, file3 };
await _worker.ExecuteParallelAsync(
    items,
    async (item, cancellationToken) => 
    {
        // Extraction de chaque item
        item.ExtractFile(...);
    },
    progressReporter,
    cancellationToken
);
```

### 3. **ThreadSafeWrappers.cs**
Wrappers `ThreadSafeIGAFile` et `ThreadSafeIGZFile` pour accès thread-safe aux fichiers.

**Utilisation:**
```csharp
var safeFile = new ThreadSafeIGAFile(igaFile);
safeFile.ExtractFileSafe(index, outputPath, out int result);
// Automatiquement protégé par ReaderWriterLockSlim
```

### 4. **ProgressForm.cs**
Formulaire simple pour afficher la progression.

## 📊 Patterns recommandés

### Pattern 1: Extraction simple avec progression
```csharp
private async void ExtractFile(object sender, EventArgs e)
{
    await Task.Run(() =>
    {
        // Opération longue
        file.ExtractFile(index, path, out int res);
        // Mise à jour UI
        UpdateUI();
    });
}
```

### Pattern 2: Extraction en masse avec parallelisme contrôlé
```csharp
private async void ExportAllObjects(object sender, EventArgs e)
{
    await _taskManager.ExecuteAsync(async (progress, cancellationToken) =>
    {
        var semaphore = new SemaphoreSlim(4); // Max 4 concurrent
        var tasks = items.Select(async item =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                // Extraction
                await Task.Run(() => ProcessItem(item), cancellationToken);
                
                // Mise à jour progression
                progress.Report(new AsyncTaskManager.TaskProgress
                {
                    TotalItems = items.Count,
                    CompletedItems = processed++,
                    CurrentItem = item.Name,
                    Status = $"{processed}/{items.Count}"
                });
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        
    }, "ExportAll");
}
```

### Pattern 3: Lecture de fichiers avec cache thread-safe
```csharp
private ReaderWriterLockSlim _fileLock = new();
private Dictionary<string, Data> _cache = new();

public Data GetData(string key, CancellationToken ct)
{
    _fileLock.EnterReadLock();
    try
    {
        if (_cache.TryGetValue(key, out var data))
            return data;
    }
    finally
    {
        _fileLock.ExitReadLock();
    }

    // Pas en cache, charger depuis fichier
    var newData = LoadFromFile(key);
    
    _fileLock.EnterWriteLock();
    try
    {
        _cache[key] = newData;
    }
    finally
    {
        _fileLock.ExitWriteLock();
    }

    return newData;
}
```

## 🔒 Points critiques pour éviter les crashes

### 1. **Accès concurrent aux fichiers**
❌ **MAUVAIS:**
```csharp
// Deux threads lisent le même fichier = crash
Task.Run(() => file.ExtractFile(0, ...));
Task.Run(() => file.ExtractFile(1, ...)); // Collision!
```

✅ **BON:**
```csharp
// Utiliser ThreadSafeIGAFile
var safeFile = new ThreadSafeIGAFile(file);
Task.Run(() => safeFile.ExtractFileSafe(0, ...));
Task.Run(() => safeFile.ExtractFileSafe(1, ...)); // OK
```

### 2. **Mise à jour d'UI depuis background thread**
❌ **MAUVAIS:**
```csharp
Task.Run(() =>
{
    progressBar.Value = 50; // CRASH! Thread non-UI
});
```

✅ **BON:**
```csharp
Task.Run(() =>
{
    // ...
}).ContinueWith(t =>
{
    progressBar.Value = 50; // UI thread
}, TaskScheduler.FromCurrentSynchronizationContext());

// OU utiliser Invoke
Task.Run(() =>
{
    this.Invoke(() => { progressBar.Value = 50; });
});
```

### 3. **Gestion des ressources**
❌ **MAUVAIS:**
```csharp
FileStream fs = new FileStream(path, ...);
Task.Run(() => ProcessStream(fs)); // fs peut être fermé avant la tâche
```

✅ **BON:**
```csharp
await Task.Run(() =>
{
    using (FileStream fs = new FileStream(path, ...))
    {
        ProcessStream(fs); // Garantit que fs existe pendant la tâche
    }
});
```

## 🚀 Optimisations de performance

### 1. **Limitation de concurrence**
Limiter le nombre de tâches simultanées pour éviter la surcharge:
```csharp
var semaphore = new SemaphoreSlim(4); // Max 4 en parallèle
foreach (var item in items)
{
    await semaphore.WaitAsync();
    _ = Task.Run(async () =>
    {
        try { ProcessItem(item); }
        finally { semaphore.Release(); }
    });
}
```

### 2. **Batching de petites opérations**
```csharp
// ❌ LENT: Une tâche par fichier = overhead
foreach (var file in files)
    await Task.Run(() => Extract(file));

// ✅ RAPIDE: Batch de 10 fichiers
var batches = files.Chunk(10);
foreach (var batch in batches)
    await Task.Run(() => batch.ForEach(f => Extract(f)));
```

### 3. **Configuration du ThreadPool**
```csharp
// Au démarrage de l'app
ThreadPool.GetMinThreads(out int workerThreads, out int ioThreads);
ThreadPool.SetMinThreads(Math.Max(4, Environment.ProcessorCount), ioThreads);
```

## 📝 Modifications de code requises

### Dans Form1.cs:
1. ✅ Ajouter les champs `_taskManager` et `_extractionWorker`
2. ✅ Convertir `ExtractAllFiles()` en `async`
3. ✅ Convertir `ExtractSingleFile()` en `async`
4. ✅ Convertir `PreviewFile()` en `async` (pour charger les IGZ)

### Dans IGZ_GeneralForm.cs:
1. ✅ Convertir `ExportAllObjects()` en `async`
2. ✅ Convertir `ImportTexture()` en `async`
3. ✅ Ajouter support des `CancellationToken`

### Dans IGA_File.cs et IGZ_File.cs:
1. ❌ **NE PAS MODIFIER** directement - trop risqué
2. ✅ Utiliser les wrappers `ThreadSafeIGAFile` et `ThreadSafeIGZFile` à la place

## ⚠️ Considérations supplémentaires

### I/O Concurrence
- Si vous avez un **disque lent** (HDD), limiter à 2-3 extractions simultanées
- Si vous avez un **SSD rapide**, vous pouvez monter jusqu'à 8 concurrent
- **Réseau**: Limiter à 1-2 connexions

### Mémoire
- Chaque tâche consomme de la mémoire
- Pour de grandes extractions, utiliser un nombre limité de workers
- Vider les ressources (`Dispose`) immédiatement

### Cancellation
Toujours supporter `CancellationToken`:
```csharp
if (cancellationToken.IsCancellationRequested)
{
    cancellationToken.ThrowIfCancellationRequested();
    // Ou simplement break d'une boucle
}
```

## 🧪 Test des modifications

```csharp
// Test: Ouvrir multiple fichiers simultanément
var tasks = new[]
{
    Task.Run(() => OpenIGAFile("file1.arc")),
    Task.Run(() => OpenIGAFile("file2.arc")),
    Task.Run(() => OpenIGAFile("file3.arc"))
};
Task.WaitAll(tasks); // Pas de crash = succès!

// Test: Extraction parallèle de 100 fichiers
var files = LoadMultipleFiles();
await ExportAllObjectsAsync(files); // Devrait prendre 5 secondes, pas 50
```

## 📚 Ressources

- [Microsoft Async/Await Documentation](https://docs.microsoft.com/en-us/archive/msdn-magazine/2013/march/async-await-best-practices-in-asynchronous-programming)
- [ReaderWriterLockSlim](https://docs.microsoft.com/en-us/dotnet/api/system.threading.readerwriterlockslim)
- [TPL Dataflow for concurrent operations](https://docs.microsoft.com/en-us/dotnet/standard/parallel-programming/dataflow-task-parallel-library)
- [CancellationToken best practices](https://docs.microsoft.com/en-us/dotnet/standard/threading/cancellation-in-managed-threads)
