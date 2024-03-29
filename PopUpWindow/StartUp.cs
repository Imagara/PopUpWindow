﻿using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Media;

namespace PopUpWindow
{
    // Define StartUp class which derives from Window
    public class StartUp : Window
    {
        private DateTime _targetTime;
        private readonly Logger _logger = new();

        // Constructor
        public StartUp()
        {
            //Import main(general) settings
            ImportMainSettings();

            //Create history.hy if not exists
            try
            {
                string historyPath = Path.Combine(Environment.CurrentDirectory, "history.hy");
                FileInfo historyFile = new FileInfo(historyPath);

                // Create file if it does not already exist
                if (!historyFile.Exists)
                {
                    historyFile.Create();
                    _logger.CreateLog($"history.hy created");
                }
                else
                    _logger.CreateLog($"history.hy found");
            }
            catch (Exception ex)
            {
                _logger.CreateLog($"Error while creating history.hy: {ex.Message}");
            }


            // Set list of available screens
            MainSettings.AllScreens = Screens.All;


            int mode = MainSettings.Mode;
            _logger.CreateLog($"{mode} mode selected");

            if (mode == 1)
                StartUpWaiter(MainSettings.IniReaderRefreshRate);
            else if (mode == 2)
                OpenWindows();
        }

        private bool CheckActivity()
        {
            try
            {
                TimeOnly timeNow = TimeOnly.Parse(DateTime.Now.ToString("HH:mm"));
                if (timeNow < MainSettings.ActivityStart
                    || timeNow > MainSettings.ActivityEnd)
                    return false;

                return true;
            }
            catch (Exception ex)
            {
                _logger.CreateLog($"StartUpWaiter: Activity error: " + ex.Message);
                return false;
            }
        }

        async void StartUpWaiter(int seconds)
        {
            while (true)
            {
                if (!CheckActivity())
                {
                    await Task.Delay(seconds * 1000);
                    continue;
                }

                try
                {
                    List<string> imagesPaths = GetImages();

                    // If there are images to show, open windows
                    if (imagesPaths.Count > 0)
                    {
                        FileManager manager = new FileManager(Path.Combine(MainSettings.Directory, "launch.ini"));

                        bool autoDel = true;
                        if (bool.TryParse(manager.GetPrivateString("autodelete"), out var temp))
                        {
                            autoDel = temp;
                        }


                        if (MainSettings.Windows.Count == 0)
                        {
                            OpenWindows(imagesPaths, autoDel);
                            _logger.CreateLog($"StartUpWaiter: Windows opened");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.CreateLog("An error occurred while running StartUpWaiter: " + ex.Message);
                }

                await Task.Delay(seconds * 1000);
            }
        }

        private List<string> GetImages()
        {
            Regex regex = new Regex(
                @"^[A-Za-zА-Яа-я0-9.() —:\\/-]{4,128}[|][0-9.:\s]{10,19}[|][0-9.:\s]{10,19}[|][0-9.:\s]{10,19}",
                RegexOptions.Compiled);

            var allStrs = File.ReadAllLines(Path.Combine(MainSettings.Directory, "launch.ini"));

            List<string> imagesPaths = new();

            foreach (var item in allStrs)
            {
                if (!regex.IsMatch(item))
                    continue;

                string[] subs = item.Split('|');

                DateTime lastWriteTime;
                DateTime actualStart;
                DateTime actualEnd;

                if (!DateTime.TryParse(subs[1], out lastWriteTime)
                    || !DateTime.TryParse(subs[2], out actualStart)
                    || !DateTime.TryParse(subs[3], out actualEnd))
                {
                    _logger.CreateLog(
                        $"error to parse str: {item}");
                    continue;
                }

                if (actualStart > DateTime.Now)
                    continue;

                if (actualEnd < DateTime.Now)
                    continue;


                FileInfo file = new FileInfo(Path.Combine(MainSettings.Directory, subs[0]));

                if (!file.Exists)
                    continue;

                if (MainSettings.Extensions.All(ext => ext != file.Extension))
                    continue;

                var historyPath = Path.Combine(Environment.CurrentDirectory, "history.hy");
                FileManager historyManager = new(historyPath);

                if (!historyManager.IsHistoryContains(subs[0], lastWriteTime))
                    imagesPaths.Add(subs[0]);
            }

            _logger.CreateLog(
                $"got {imagesPaths.Count} files from {MainSettings.Directory}");
            return imagesPaths;
        }


        private void ImportMainSettings()
        {
            try
            {
                FileManager manager = new FileManager(MainSettings.IniPath);

                MainSettings.Mode = Int32.TryParse(manager.GetPrivateString("main", "mode"), out var mode)
                    ? mode
                    : MainSettings.Mode;

                MainSettings.IniReaderRefreshRate =
                    Byte.TryParse(manager.GetPrivateString("main", "inirefreshrate"), out var temp)
                        ? temp
                        : MainSettings.IniReaderRefreshRate;

                if (mode == 1)
                {
                    MainSettings.Directory =
                        manager.GetPrivateString($"main", "directory").Trim() != string.Empty
                            ? manager.GetPrivateString($"main", "directory")
                            : MainSettings.Directory;
                    MainSettings.ActivityStart = TimeOnly.TryParse(manager.GetPrivateString("main", "activitystart"),
                        out var timeStart)
                        ? timeStart
                        : MainSettings.ActivityStart;
                    MainSettings.ActivityEnd =
                        TimeOnly.TryParse(manager.GetPrivateString("main", "activityend"), out var timeEnd)
                            ? timeEnd
                            : MainSettings.ActivityEnd;
                }
                else if (mode == 2)
                {
                    MainSettings.ScreensInUse = manager.GetPrivateString($"main", "screens") != string.Empty
                        ? manager.GetPrivateString($"main", "screens").Split('/')
                            .Where(i => !string.IsNullOrWhiteSpace(i)).Select(byte.Parse).ToArray()
                        : new byte[] { 1 };

                    MainSettings.IsBlackoutMode = bool.TryParse(manager.GetPrivateString("main", "isblackout"),
                        out bool isblackout)
                        ? isblackout
                        : MainSettings.IsBlackoutMode;
                    
                    MainSettings.BlackoutStart = TimeOnly.TryParse(manager.GetPrivateString("main", "blackoutstart"),
                        out var timeStart)
                        ? timeStart
                        : MainSettings.BlackoutStart;
                    
                    MainSettings.BlackoutEnd =
                        TimeOnly.TryParse(manager.GetPrivateString("main", "blackoutend"), out var timeEnd)
                            ? timeEnd
                            : MainSettings.BlackoutEnd;

                    MainSettings.BlackoutBackground =
                        Color.TryParse(manager.GetPrivateString("main", "background"), out var color)
                            ? color.ToString()
                            : MainSettings.BlackoutBackground;
                }


                _logger.CreateLog($"Main settings for mode {mode} successfully imported");
            }
            catch (Exception ex)
            {
                _logger.CreateLog("Import main settings error: " + ex.Message);
            }
        }

        private void OpenWindows(List<string> filePaths = null, bool autoDel = true)
        {
            int index = 1;
            try
            {
                foreach (var item in MainSettings.AllScreens)
                {
                    var xPos = item.Bounds.Position.X;
                    if (MainSettings.ScreensInUse.Any(x => x == index))
                    {
                        MainWindow mw;
                        if (filePaths != null && MainSettings.Mode == 1 && filePaths.Count > 0)
                            mw = new MainWindow(index, filePaths, autoDel);
                        else
                            mw = new MainWindow(index);
                        mw.Position = new PixelPoint(xPos, 0);
                        MainSettings.Windows.Add(mw);
                        mw.Show();
                    }

                    index++;
                }

                _logger.CreateLog("window opened successfully");
            }
            catch (Exception ex)
            {
                _logger.CreateLog("Window opening error: " + ex.Message);
            }
        }
    }
}