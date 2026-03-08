using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace GameLauncher
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            if (Directory.Exists(@"C:\Program Files (x86)\Steam\steamapps\common"))
            {
                GameList.ItemsSource = ScanGames(@"C:\Program Files (x86)\Steam\steamapps\common");
            }
        }

        private List<Game> ScanGames(string folder)
        {
            List<Game> games = new List<Game>();

            foreach (var dir in Directory.GetDirectories(folder))
            {
                var exes = Directory.GetFiles(dir, "*.exe");

                if (exes.Length > 0)
                {
                    games.Add(new Game
                    {
                        Name = Path.GetFileName(dir),
                        ExecutablePath = exes[0],
                        ImagePath = "placeholder.png"
                    });
                }
            }

            return games;
        }

        private void PlayGame_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as System.Windows.Controls.Button;
            string exePath = button.Tag.ToString();

            Process.Start(exePath);
        }
    }
}
