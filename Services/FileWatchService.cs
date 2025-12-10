using System;
using System.IO;

namespace DesignSheet.Services;

public sealed class FileWatchService : IDisposable
{
    private readonly FileSystemWatcher? _watcher;
    private readonly Debouncer _debouncer = new(TimeSpan.FromMilliseconds(500));

    public event EventHandler? Changed;

    public FileWatchService(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            return;

        _watcher = new FileSystemWatcher(folder, "*.csv")
        {
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.LastWrite |
                           NotifyFilters.Size |
                           NotifyFilters.FileName
        };

        _watcher.Changed += OnChanged;
        _watcher.Created += OnChanged;
        _watcher.Deleted += OnChanged;
        _watcher.Renamed += OnRenamed;

        _watcher.EnableRaisingEvents = true;
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        // Debounce to avoid spam when Excel writes temp files etc.
        _debouncer.Execute(() => Changed?.Invoke(this, EventArgs.Empty));
    }

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        _debouncer.Execute(() => Changed?.Invoke(this, EventArgs.Empty));
    }

    public void Dispose()
    {
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Changed -= OnChanged;
            _watcher.Created -= OnChanged;
            _watcher.Deleted -= OnChanged;
            _watcher.Renamed -= OnRenamed;
            _watcher.Dispose();
        }
    }
}
