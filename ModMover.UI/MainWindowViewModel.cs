using System.IO;
using System.IO.Compression;
using System.Windows.Input;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using Microsoft.Extensions.Configuration;
using Microsoft.WindowsAPICodePack.Shell;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Common;

namespace ModMover.UI;

public class MainWindowViewModel : ViewModelBase
{
    private FileSystemWatcher _downloadsWatcher;
    private FileSystemWatcher _modsWatcher;
    private FileSystemWatcher _trayWatcher;
    private int? _fileCountInDownloads;
    private int? _modCount;

    public int? ModCount
    {
        get => _modCount;
        set => Set(ref _modCount, value);
    }

    public int? FileCountInDownloads
    {
        get => _fileCountInDownloads;
        set => Set(ref _fileCountInDownloads, value);
    }

    private readonly string DownloadsFolder = KnownFolders.Downloads.Path;
    private string ModsFolder;
    private string BackupModsFolder;
    private string TrayFolder;

    public void Initialise()
    {
        var config = new ConfigurationBuilder()
            .AddUserSecrets<MainWindowViewModel>()
            .Build();

        ModsFolder = config["ModsFolder"];
        BackupModsFolder = config["BackupModsFolder"];
        TrayFolder = config["TrayFolder"];

        var files = Directory.GetFiles(DownloadsFolder).ToList();

        FileCountInDownloads = files.Count(x => x.Contains("desktop.ini") == false);

        var modFiles = Directory.GetFiles(ModsFolder).ToList();
        var trayFiles = Directory.GetFiles(TrayFolder).ToList();

        ModCount = modFiles.Count + trayFiles.Count;

        InitialiseDownloadWatcher();
        InitialiseModsAndTrayWatchers();
    }

    private void InitialiseDownloadWatcher()
    {
        _downloadsWatcher = new FileSystemWatcher(DownloadsFolder);

        _downloadsWatcher.IncludeSubdirectories = true;

        _downloadsWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName;

        _downloadsWatcher.Filter = "*.*";
        _downloadsWatcher.Created += OnDownloadsChanged;
        _downloadsWatcher.Changed += OnDownloadsChanged;
        _downloadsWatcher.Deleted += OnDownloadsChanged;
        _downloadsWatcher.EnableRaisingEvents = true;
    }

    private void InitialiseModsAndTrayWatchers()
    {
        _modsWatcher = new FileSystemWatcher(ModsFolder);

        _modsWatcher.IncludeSubdirectories = true;

        _modsWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName;

        _modsWatcher.Filter = "*.*";
        _modsWatcher.Created += OnModsChanged;
        _modsWatcher.Changed += OnModsChanged;
        _modsWatcher.Deleted += OnModsChanged;
        _modsWatcher.EnableRaisingEvents = true;

        _trayWatcher = new FileSystemWatcher(TrayFolder);

        _trayWatcher.IncludeSubdirectories = true;

        _trayWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName;

        _trayWatcher.Filter = "*.*";
        _trayWatcher.Created += OnModsChanged;
        _trayWatcher.Changed += OnModsChanged;
        _trayWatcher.Deleted += OnModsChanged;
        _trayWatcher.EnableRaisingEvents = true;
    }

    private void OnDownloadsChanged(object source, FileSystemEventArgs e)
    {
        //Folders
        var zipFolders = Directory.GetFiles(DownloadsFolder, "*.zip");
        var rarFolders = Directory.GetFiles(DownloadsFolder, "*.rar");

        if(zipFolders.Any())
        {
            ExtractFilesFromZipFolders(zipFolders);
        }

        if(rarFolders.Any())
        {
            ExtractFromRarFolders(rarFolders);
        }

        var folders = Directory.GetDirectories(DownloadsFolder);

        if(folders.Any())
        {
            GetFilesInFolders(folders);
        }

        var files = Directory.GetFiles(DownloadsFolder).ToList();

        FileCountInDownloads = files.Count(x => x.Contains("desktop.ini") == false);
    }

    private void OnModsChanged(object sender, FileSystemEventArgs e)
    {
        var modFiles = Directory.GetFiles(ModsFolder).ToList();
        var trayFiles = Directory.GetFiles(TrayFolder).ToList();

        ModCount = modFiles.Count + trayFiles.Count;
    }

    private void ExtractFilesFromZipFolders(string[] zipFolders)
    {
        foreach(var zippedFolder in zipFolders)
        {
            try
            {
                ZipFile.ExtractToDirectory(zippedFolder, DownloadsFolder);
            }
            catch(Exception e)
            {
                File.Delete(zippedFolder);
            }
        }
    }

    private void ExtractFromRarFolders(string[] folders)
    {
        foreach(var folder in folders)
        {
            var rarFolder = RarArchive.Open(folder);

            try
            {
                foreach(var entry in rarFolder.Entries)
                {
                    entry.WriteToDirectory(DownloadsFolder,
                        new ExtractionOptions { ExtractFullPath = true, Overwrite = true });
                }
            }
            catch(Exception e)
            {
                rarFolder.Dispose();

                File.Delete(folder);
            }
        }
    }

    private void GetFilesInFolders(string[] folders)
    {
        foreach(var folder in folders)
        {
            var filesInFolder = Directory.GetFiles(folder, "*.*");

            var foldersInFolder = Directory.GetDirectories(folder);

            foreach(var file in filesInFolder)
            {
                var destinationFile = Path.Combine(DownloadsFolder, Path.GetFileNameWithoutExtension(file) + Path.GetExtension(file));

                if(File.Exists(destinationFile) == false)
                {
                    File.Move(file, destinationFile);
                }

                if(File.Exists(file))
                {
                    File.Delete(file);
                }
            }

            GetFilesInFolders(foldersInFolder);

            Directory.Delete(folder);
        }
    }

    public ICommand EmptyModFolderCommand => new RelayCommand(EmptyModFolder);
    private void EmptyModFolder()
    {
        foreach(var folder in Directory.GetDirectories(ModsFolder))
        {
            var destinationFile = Path.Combine(BackupModsFolder, folder);

            Directory.Move(folder, destinationFile);
        }

        foreach(var file in Directory.GetFiles(ModsFolder, "*.*"))
        {
            var destinationFile = Path.Combine(BackupModsFolder, Path.GetFileNameWithoutExtension(file) + Path.GetExtension(file));

            File.Move(file, destinationFile);
        }
    }

    public ICommand SwapFoldersCommand => new RelayCommand(SwapFolders);
    private void SwapFolders()
    {
        var foldersInModsBackupFolder = Directory.GetDirectories(BackupModsFolder);
        var filesInModsBackupFolder = Directory.GetFiles(BackupModsFolder, "*.*");

        var foldersInModsFolder = Directory.GetDirectories(ModsFolder);
        var filesInModsFolder = Directory.GetFiles(ModsFolder, "*.*");

        foreach(var folder in foldersInModsBackupFolder)
        {
            var destinationFile = Path.Combine(DownloadsFolder, folder);

            Directory.Move(folder, destinationFile);
        }

        foreach(var file in filesInModsBackupFolder)
        {
            var destinationFile = Path.Combine(DownloadsFolder, Path.GetFileNameWithoutExtension(file) + Path.GetExtension(file));

            File.Move(file, destinationFile);
        }

        foreach(var folder in foldersInModsFolder)
        {
            var destinationFile = Path.Combine(BackupModsFolder, folder);

            Directory.Move(folder, destinationFile);
        }

        foreach(var file in filesInModsFolder)
        {
            var destinationFile = Path.Combine(BackupModsFolder, Path.GetFileNameWithoutExtension(file) + Path.GetExtension(file));

            File.Move(file, destinationFile);
        }
    }

    public ICommand HalfModFolderCommand => new RelayCommand(HalfModFolder);
    private void HalfModFolder()
    {
        var foldersInModsFolder = Directory.GetDirectories(ModsFolder);
        var filesInModsFolder = Directory.GetFiles(ModsFolder, "*.*");

        foreach(var folder in foldersInModsFolder.Take(foldersInModsFolder.Length / 2))
        {
            var destinationFile = Path.Combine(BackupModsFolder, folder);

            Directory.Move(folder, destinationFile);
        }

        foreach(var file in filesInModsFolder.Take(filesInModsFolder.Length / 2))
        {
            var destinationFile = Path.Combine(BackupModsFolder, Path.GetFileNameWithoutExtension(file) + Path.GetExtension(file));

            File.Move(file, destinationFile);
        }
    }
}