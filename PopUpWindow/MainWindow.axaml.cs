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
        private List<string> pictures = new ();
        private int screenNum = 1;
        private Settings settings = new();

        public MainWindow()
        {
            InitializeComponent();
            Start();
        }

        public MainWindow(int _screenNum)
        {
            InitializeComponent();
            screenNum = _screenNum;
            Start();
        }

        private void Start()
        {
            Title = $"Screen #{screenNum}";
            ImportSettings();
            GetAllPictures();
            if (pictures.Count == 0)
                return;
            switch (MainSettings.Mode)
            {
                case 1:
                    MainImage.Source = pictures.Count > 0 ? new Bitmap(pictures.LastOrDefault()) : null;
                    break;
                case 2:
                    Dispatcher.UIThread.Post(action: () => SecondModeCycle(), priority: DispatcherPriority.Background);
                    HelpGrid.IsVisible = false;
                    break;
                default:
                    new InfoWindow($"Invalid operating mode selected").Show();
                    break;
            }
        }

        private void ImportSettings()
        {
            try
            {
                IniManager manager = new IniManager(MainSettings.IniPath);
                
                //settings.Mode = Byte.TryParse(manager.GetPrivateString($"display{screenNum}", "mode"), out var temp)
                //    ? temp
                //    : settings.Mode;
                settings.Rate = Byte.TryParse(manager.GetPrivateString($"display{screenNum}", "rate"), out var temp)
                    ? temp
                    : settings.Rate;
                settings.DirectoryPath =
                    manager.GetPrivateString($"display{screenNum}", "directory").Trim() != string.Empty
                        ? manager.GetPrivateString($"display{screenNum}", "directory")
                        : settings.DirectoryPath;
                settings.Extensions = manager.GetPrivateString($"display{screenNum}", "ext") != string.Empty
                    ? manager.GetPrivateString($"display{screenNum}", "ext").Split('/')
                    : settings.Extensions;
            }
            catch (Exception ex)
            {
                new InfoWindow(ex.Message).Show();
            }
        }

        private void ExportSettings()
        {
            
        }

        private async Task SecondModeCycle()
        {
            while (true)
            {
                if (pictures.Count == 0)
                    break;
                foreach (var item in pictures)
                {
                    MainImage.Source = new Bitmap(item);
                    await Task.Delay(settings.Rate * 1000);
                }
            }
        }

        private void GetAllPictures()
        {
            pictures.Clear();
            
            foreach (string file in Directory
                         .EnumerateFiles(settings.DirectoryPath, "*.*", SearchOption.AllDirectories)
                         .Where(item => settings.Extensions.Any(ext => '.' + ext == Path.GetExtension(item))))
                pictures.Add(file);
        }
        
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
                this.Close();
        }
    }
}