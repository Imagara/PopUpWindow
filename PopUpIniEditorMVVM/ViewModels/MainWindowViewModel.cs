﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Controls;
using PopUpIniEditorMVVM.Views;
using ReactiveUI;

namespace PopUpIniEditorMVVM.ViewModels;

public class MainWindowViewModel : ViewModelBase, INotifyPropertyChanged
{
    private static Encoding UTF8NoBOM => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    
    // Event to signal ViewModel property changes to the View and update the UI display
    public event PropertyChangedEventHandler PropertyChanged;

    public ReactiveCommand<string, Unit> ReplaceAnnouncementCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearDateCommand { get; }
    public ReactiveCommand<Unit, Unit> AddDisplayCommand { get; }
    public ReactiveCommand<Unit, Unit> RemoveDisplayCommand { get; }
    public ReactiveCommand<Window, Unit> AddAnnouncementCommand { get; }
    public ReactiveCommand<Unit, Unit> RemoveAnnouncementCommand { get; }
    public ReactiveCommand<Unit, Unit> UpdateSettingsIniFileCommand { get; }
    public ReactiveCommand<Unit, Unit> UpdateLaunchIniFileCommand { get; }
    public ReactiveCommand<Window, Unit> DirectorySelectCommand { get; }
    public ReactiveCommand<Window, Unit> LaunchSelectCommand { get; }

    // Constructor creating each user command and associated action
    public MainWindowViewModel()
    {
        ReplaceAnnouncementCommand = ReactiveCommand.Create<string>(ReplaceAnnouncementChangeAnswer);
        ClearDateCommand = ReactiveCommand.Create(ClearDate);
        AddDisplayCommand = ReactiveCommand.Create(AddDisplay);
        RemoveDisplayCommand = ReactiveCommand.Create(RemoveDisplay);
        AddAnnouncementCommand = ReactiveCommand.Create<Window>(AddAnnouncementOpenDialog);
        RemoveAnnouncementCommand = ReactiveCommand.Create(RemoveAnnouncement);
        UpdateSettingsIniFileCommand = ReactiveCommand.Create(UpdateSettingsIniFile);
        UpdateLaunchIniFileCommand = ReactiveCommand.Create(UpdateLaunchIniFile);
        DirectorySelectCommand = ReactiveCommand.Create<Window>(DirectorySelect);
        LaunchSelectCommand = ReactiveCommand.Create<Window>(LaunchSelect);
    }

    private void ClearDate()
    {
        LaunchDateStr = null;
    }

    private void ReplaceAnnouncementChangeAnswer(string param)
    {
        _replaceAnnouncementAnswer = param;
    }

    private void UpdateLaunchIniFile()
    {
        DateTime.TryParse(_launchDateStr, out var dt);

        List<string> strs = new();

        strs.Add($"time={(dt != DateTime.MinValue ? dt.ToShortDateString() + " " + _launchTime : _launchTime)}" +
                 $"autodelete={_launchAutoDeleteFile}\n");
        
        strs.AddRange(_announcements
            .Select(item => $"{item.Name}|{item.LastWriteTime}|{item.ActualStart}|{item.ActualEnd}").ToList());

        File.WriteAllLines(_launchPath, strs, UTF8NoBOM);

        foreach (var item in _announcementsPathToRemove)
        {
            File.Delete(item);
        }
        
        FileInfo launchFile = new FileInfo(_launchPath);
        
        foreach (var item in _announcements)
        {
            FileInfo file = new(item.ImagePath);

            if (file.Directory!.ToString() != launchFile.Directory!.ToString())
            {
                File.Copy(item.ImagePath, Path.Combine(launchFile.Directory.ToString(), item.Name));
            }
        }

        new InfoWindow($"Сохранено.").Show();
    }

    // Method to write out changes made to settings.ini
    private void UpdateSettingsIniFile()
    {
        if (_selectedMode is not Label modeLabel)
            return;

        int.TryParse(modeLabel.Content.ToString(), out var mode);

        if (mode is 1 or 2)
        {
            List<string> strs = new()
            {
                "[main]\n" +
                $"mode={mode}"
            };
            if (mode is 1)
            {
                strs.Add($"directory={_directory}\n" +
                         $"activitystart={_activityStart}\n" +
                         $"activityend={_activityEnd}");
            }
            else
            {
                var screens = _displays.Select(item => item.DisplayNum).ToList();
                strs.Add($"screens={string.Join("/", screens)}");
                foreach (var display in _displays)
                {
                    strs.Add($"[display{display.DisplayNum}]\n" +
                             $"directory={display.DirectoryPath}\n" +
                             $"rate={display.Rate}\n");
                }
            }

            // Write the updated settings.ini file
            File.WriteAllLines("settings.ini", strs, UTF8NoBOM);
            if (Environment.OSVersion.Platform.ToString() != "Unix")
                SelectUpdatedFile();
            new InfoWindow($"Сохранено.").Show();
        }
    }

    private void SelectUpdatedFile()
    {
        Process PrFolder = new Process();
        ProcessStartInfo psi = new ProcessStartInfo();
        string file = Path.Combine(Environment.CurrentDirectory, "settings.ini");
        psi.CreateNoWindow = true;
        psi.WindowStyle = ProcessWindowStyle.Normal;
        psi.FileName = "explorer";
        psi.Arguments = @"/n, /select, " + file;
        PrFolder.StartInfo = psi;
        PrFolder.Start();
    }

    private void ModeChanged()
    {
        if (_selectedMode is not Label modeLabel)
            return;

        bool isFirstMode = modeLabel.Content.ToString() == "1";

        ModeHint = isFirstMode ? "Отображает доступные объявления на экран" : "Циклично показывает изображения";

        IsFirstModeStackPanelVisible = isFirstMode;
        IsSecondModeStackPanelVisible = !isFirstMode;

        if (isFirstMode)
        {
            Displays.Clear();
            Displays.Add(new DisplayClass { DisplayNum = 1, IsModeTwo = false });
        }
        else
        {
            Displays.Clear();
        }
    }


    private async void DirectorySelect(Window window)
    {
        OpenFolderDialog ofd = new OpenFolderDialog();
        ofd.Title = "Select folder";
        var result = await ofd.ShowAsync(window);
        if (result != null)
            Directory = result;
    }

    private async void LaunchSelect(Window window)
    {
        OpenFileDialog op = new OpenFileDialog();

        op.Title = "Выбрать конфигурационный файл";
        op.Filters!.Add(new FileDialogFilter { Name = "Ini Files", Extensions = { "ini" } });
        op.AllowMultiple = false;

        var result = await op.ShowAsync(window);
        if (result != null)
            LaunchPath = result.First();
    }

    private void ImportAnnouncements(string path)
    {
        FileInfo iniFile = new FileInfo(path);
        if (iniFile.Exists && iniFile.Extension == ".ini")
        {
            var announcementsStrs = File.ReadLines(iniFile.FullName).ToList();

            _announcements.Clear();
            foreach (var item in announcementsStrs)
            {
                Regex announcementRegex = new Regex(
                    @"^[A-Za-zА-Яа-я0-9.() —:\\/-]{4,128}[|][0-9.:\s]{10,19}[|][0-9.:\s]{10,19}[|][0-9.:\s]{10,19}",
                    RegexOptions.Compiled);

                if (!announcementRegex.IsMatch(item))
                    continue;

                string[] subs = item.Split('|');

                if (!DateTime.TryParse(subs[1], out var lastWriteTime)
                    || !DateTime.TryParse(subs[2], out var actualStart)
                    || !DateTime.TryParse(subs[3], out var actualEnd))
                    continue;

                _announcements.Add(new AnnouncementClass
                {
                    Name = subs[0],
                    ImagePath = Path.Combine(iniFile.Directory!.ToString(), subs[0]),
                    LastWriteTime = lastWriteTime,
                    ActualStart = actualStart,
                    ActualEnd = actualEnd
                });
            }

            var timeLine = announcementsStrs.FirstOrDefault(s => s.StartsWith("time="));
            if (timeLine != null)
            {
                DateTime.TryParse(timeLine.Substring("time=".Length), out var dt);
                LaunchDateStr = dt.ToShortDateString();
                LaunchTimeStr = dt.ToShortTimeString();
            }

            var autoDeleteLine = announcementsStrs.FirstOrDefault(s => s.StartsWith("autodelete="));
            if (autoDeleteLine != null)
            {
                bool.TryParse(autoDeleteLine.Substring("autodelete=".Length), out var autoDeleteFiles);
                LaunchAutoDeleteFile = autoDeleteFiles;
            }

            OnPropertyChanged(nameof(AnnouncementCountContent));
        }
        else if (iniFile.Extension == ".ini")
            _launchPath = iniFile.FullName;
    }

    private void AddDisplay()
    {
        int.TryParse(SelectedDisplayContent, out var displayNum);

        if (displayNum > 0 && Displays.All(item => item.DisplayNum != displayNum))
        {
            Displays.Add(new DisplayClass
            {
                DisplayNum = displayNum
            });
        }
    }

    private void RemoveDisplay()
    {
        int.TryParse(SelectedDisplayContent, out var display);

        if (Displays.Any(item => item.DisplayNum == display))
            Displays.Remove(Displays.First(item => item.DisplayNum == display));
    }

    private async void AddAnnouncementOpenDialog(Window window)
    {
        OpenFileDialog op = new OpenFileDialog();

        op.Title = "Выбрать обьявление";
        op.Filters!.Add(new FileDialogFilter
            { Name = "Изображения", Extensions = { "png", "jpeg", "jpg" } });
        op.AllowMultiple = true;

        var result = await op.ShowAsync(window);
        if (result != null)
        {
            foreach (var item in result)
            {
                FileInfo imageFileInfo = new FileInfo(item);
                if (!imageFileInfo.Exists)
                    continue;

                if (_announcements.Any(it => it.Name == imageFileInfo.Name))
                {
                    IsReplaceAnnouncementOpened = true;

                    FileInfo launchFile = new FileInfo(_launchPath);

                    OldAnnouncementImage = Path.Combine(launchFile.Directory!.ToString(),
                        _announcements.First(i => i.Name == imageFileInfo.Name).Name);
                    NewAnnouncementImage = item;

                    ReplaceNewFileName = "";

                    ReplaceFileName = imageFileInfo.Name;

                    while (IsReplaceAnnouncementOpened)
                    {
                        await Task.Delay(200);
                        switch (_replaceAnnouncementAnswer)
                        {
                            case "replace":
                                IsReplaceAnnouncementOpened = false;
                                _replaceAnnouncementAnswer = "none";

                                try
                                {
                                    _announcementsPathToRemove.Add(Path.Combine(launchFile.Directory.ToString(), imageFileInfo.Name));
                                    
                                    Announcements.Remove(Announcements.First(item => item.Name == imageFileInfo.Name));
                                    
                                    AddAnnouncement(imageFileInfo.Name, item, imageFileInfo.LastWriteTime);
                                }
                                catch (Exception e)
                                {
                                    new InfoWindow("Error while removing announcement: " + e.Message).Show();
                                }

                                break;
                            case "rename":
                                FileInfo newFile = new FileInfo(Path.Combine(launchFile.Directory.ToString(),_replaceNewFileName+imageFileInfo.Extension));
                                if (newFile.Exists)
                                {
                                    _replaceAnnouncementAnswer = "none";
                                    ReplaceNewFileName = "";
                                    break;
                                }
                                
                                IsReplaceAnnouncementOpened = false;
                                _replaceAnnouncementAnswer = "none";
                                AddAnnouncement(_replaceNewFileName + imageFileInfo.Extension, item,
                                    imageFileInfo.LastWriteTime);
                                break;
                            case "skip":
                                IsReplaceAnnouncementOpened = false;
                                _replaceAnnouncementAnswer = "none";
                                break;
                        }
                    }
                }
                else
                {
                    AddAnnouncement(imageFileInfo.Name, item, imageFileInfo.LastWriteTime);
                }
            }
        }

        OnPropertyChanged(nameof(AnnouncementCountContent));
    }

    private void AddAnnouncement(string name, string path, DateTime lastWrite)
    {
        Announcements.Add(new AnnouncementClass
        {
            Name = name,
            ImagePath = path,
            LastWriteTime = lastWrite
        });
    }

    private void RemoveAnnouncement()
    {
        if (_selectedAnnouncement is not AnnouncementClass announcement)
            return;

        try
        {
            Announcements.Remove(announcement);
            OnPropertyChanged(nameof(AnnouncementCountContent));
        }
        catch (Exception e)
        {
            new InfoWindow("Error while removing announcement: " + e.Message).Show();
        }
    }

    private List<string> _announcementsPathToRemove = new();

    private string _replaceAnnouncementAnswer = "none";

    public string AnnouncementCountContent => $"Обьявлений : {_announcements.Count}";

    private Object _selectedMode = 1;

    public Object SelectedMode
    {
        get => _selectedMode;
        set
        {
            _selectedMode = value;
            OnPropertyChanged();
            ModeChanged();
        }
    }

    private string _oldAnnouncementImage;

    public string OldAnnouncementImage
    {
        get => _oldAnnouncementImage;
        set
        {
            _oldAnnouncementImage = value;
            OnPropertyChanged();
        }
    }

    private string _newAnnouncementImage;

    public string NewAnnouncementImage
    {
        get => _newAnnouncementImage;
        set
        {
            _newAnnouncementImage = value;
            OnPropertyChanged();
        }
    }

    private string _replaceFileName;

    public string ReplaceFileName
    {
        get => _replaceFileName;
        set
        {
            _replaceFileName = value;
            OnPropertyChanged();
        }
    }

    private string _replaceNewFileName;

    public string ReplaceNewFileName
    {
        get => _replaceNewFileName;
        set
        {
            _replaceNewFileName = value;
            OnPropertyChanged();
        }
    }

    private bool _isReplaceAnnouncementOpened;

    public bool IsReplaceAnnouncementOpened
    {
        get => _isReplaceAnnouncementOpened;
        set
        {
            _isReplaceAnnouncementOpened = value;
            OnPropertyChanged();
        }
    }

    private Object _selectedDisplay;

    public Object SelectedDisplay
    {
        get => _selectedDisplay;
        set
        {
            _selectedDisplay = value;
            OnPropertyChanged();
            SelectedDisplayContent = ((DisplayClass)_selectedDisplay).DisplayNum.ToString();
        }
    }

    private Object _selectedAnnouncement;

    public Object SelectedAnnouncement
    {
        get => _selectedAnnouncement;
        set
        {
            _selectedAnnouncement = value;
            OnPropertyChanged();
        }
    }

    private bool _isFirstModeStackPanelVisible;

    public bool IsFirstModeStackPanelVisible
    {
        get => _isFirstModeStackPanelVisible;
        set
        {
            _isFirstModeStackPanelVisible = value;
            OnPropertyChanged();
        }
    }

    private bool _isSecondModeStackPanelVisible;

    public bool IsSecondModeStackPanelVisible
    {
        get => _isSecondModeStackPanelVisible;
        set
        {
            _isSecondModeStackPanelVisible = value;
            OnPropertyChanged();
        }
    }

    private string _directory;

    public string Directory
    {
        get => _directory;
        set
        {
            _directory = value;
            OnPropertyChanged();
        }
    }

    private string _launchPath;

    public string LaunchPath
    {
        get => _launchPath;
        set
        {
            _launchPath = value;
            ImportAnnouncements(_launchPath);
            OnPropertyChanged();
        }
    }

    private ObservableCollection<DisplayClass> _displays = new();

    public ObservableCollection<DisplayClass> Displays
    {
        get => _displays;
        set
        {
            _displays = value;
            OnPropertyChanged();
        }
    }

    private ObservableCollection<AnnouncementClass> _announcements = new();

    public ObservableCollection<AnnouncementClass> Announcements
    {
        get => _announcements;
        set
        {
            _announcements = value;
            OnPropertyChanged();
        }
    }

    private string _modeHint;

    public string ModeHint
    {
        get => _modeHint;
        set
        {
            _modeHint = value;
            OnPropertyChanged();
        }
    }

    private bool _isBlackout;

    public bool IsBlackout
    {
        get => _isBlackout;
        set
        {
            _isBlackout = value;
            OnPropertyChanged();
        }
    }

    private string _launchDateStr;

    public string LaunchDateStr
    {
        get => _launchDateStr;
        set
        {
            _launchDateStr = value;
            OnPropertyChanged();
        }
    }

    private string _launchTime = "09:00:00";

    public string LaunchTimeStr
    {
        get => _launchTime;
        set
        {
            _launchTime = value;
            OnPropertyChanged();
        }
    }

    private string _activityStart = "09:00:00";

    public string ActivityStart
    {
        get => _activityStart;
        set
        {
            _activityStart = value;
            OnPropertyChanged();
        }
    }

    private string _activityEnd = "21:00:00";

    public string ActivityEnd
    {
        get => _activityEnd;
        set
        {
            _activityEnd = value;
            OnPropertyChanged();
        }
    }

    private bool _launchAutoDeleteFile;

    public bool LaunchAutoDeleteFile
    {
        get => _launchAutoDeleteFile;
        set
        {
            _launchAutoDeleteFile = value;
            OnPropertyChanged();
        }
    }

    private string _updateSettingsIniFileContent = "Создать";

    public string UpdateSettingsIniFileContent
    {
        get => _updateSettingsIniFileContent;
        set
        {
            _updateSettingsIniFileContent = value;
            OnPropertyChanged();
        }
    }

    private string _selectedDisplayContent;

    public string SelectedDisplayContent
    {
        get => _selectedDisplayContent;
        set
        {
            _selectedDisplayContent = value;
            OnPropertyChanged();
        }
    }

    public void OnPropertyChanged([CallerMemberName] string property = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
    }

    public class DisplayClass
    {
        public int DisplayNum { get; set; }
        public string? DirectoryPath { get; set; }
        public int? Rate { get; set; } = 60;
        public bool? IsModeTwo { get; set; }
    }

    public class AnnouncementClass
    {
        public string Name { get; set; }
        public string ImagePath { get; set; }
        public DateTime LastWriteTime { get; set; }
        public DateTime? ActualStart { get; set; } = DateTime.Now;
        public DateTime? ActualEnd { get; set; } = DateTime.MaxValue;
    }
}