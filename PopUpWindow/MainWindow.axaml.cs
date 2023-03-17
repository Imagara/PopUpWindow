using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using Avalonia.Interactivity;
using System.Linq;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using System.Threading.Tasks;
using Avalonia.Input;
using Avalonia.Markup.Xaml;

namespace PopUpWindow
{
    public partial class MainWindow : Window
    {
        private readonly List<string> imagesPaths = new();
        private readonly int _screenNum = 1;
        private readonly Settings _settings = new();

        private readonly bool _autoDel;

        public MainWindow() => InitializeComponent();

        public MainWindow(int screenNum, List<string> imagesPaths, bool autoDel = true)
        {
            InitializeComponent();
            ImportSettings();

            _screenNum = screenNum;
            this.imagesPaths = imagesPaths;
            _autoDel = autoDel;
            Title = $"Screen #{screenNum}";

            if (MainSettings.Mode == 1)
                MainImage.Source = new Bitmap(imagesPaths.First());
            else
                Close();
        }

        public MainWindow(int screenNum)
        {
            InitializeComponent();
            _screenNum = screenNum;
            Title = $"Screen #{screenNum}";
            ImportSettings();
            if (MainSettings.Mode == 2)
            {
                SecondModeCycle();
                HelpGrid.IsVisible =
                    bool.TryParse(
                        new FileManager(Environment.CurrentDirectory).GetPrivateString("main", "leftpanel"),
                        out var leftPanel)
                        ? leftPanel
                        : false;
            }
            else
                Close();
        }

        private void ImportSettings()
        {
            try
            {
                FileManager manager = new FileManager(MainSettings.IniPath);

                _settings.Rate = Byte.TryParse(manager.GetPrivateString($"display{_screenNum}", "rate"), out var temp)
                    ? temp
                    : _settings.Rate;
                _settings.DirectoryPath =
                    manager.GetPrivateString($"display{_screenNum}", "directory").Trim() != string.Empty
                        ? manager.GetPrivateString($"display{_screenNum}", "directory")
                        : _settings.DirectoryPath;
                _settings.Extensions = manager.GetPrivateString($"display{_screenNum}", "ext") != string.Empty
                    ? manager.GetPrivateString($"display{_screenNum}", "ext").Split('/')
                    : _settings.Extensions;
            }
            catch (Exception ex)
            {
                new InfoWindow(ex.Message).Show();
            }
        }

        private void ExportSettings()
        {
        }

        private async void SecondModeCycle()
        {
            GetAllPictures();
            while (true)
            {
                if (imagesPaths.Count == 0)
                    break;
                foreach (var item in imagesPaths)
                {
                    MainImage.Source = new Bitmap(item);
                    await Task.Delay(_settings.Rate * 1000);
                }
            }
        }

        private void GetAllPictures()
        {
            imagesPaths.Clear();

            foreach (string file in Directory
                         .EnumerateFiles(_settings.DirectoryPath, "*.*", SearchOption.TopDirectoryOnly)
                         .Where(item => _settings.Extensions.Any(ext => '.' + ext == Path.GetExtension(item))))
            {
                imagesPaths.Add(file);
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape && MainSettings.Mode == 1)
            {
                FileInfo file = new FileInfo(imagesPaths.First());
                string historyPath = Environment.CurrentDirectory + "\\history.hy";
                new FileManager(historyPath).WriteHistoryString(file.Name,
                    file.LastWriteTime);
                if (_autoDel && file.Exists)
                    file.Delete();
                imagesPaths.Remove(imagesPaths.First());
                if (imagesPaths.Count <= 0)
                {
                    foreach (Window window in MainSettings.Windows)
                        window.Close();
                    MainSettings.Windows.Clear();
                }
            }
        }
    }
}