using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;


namespace GameLauncher
{
    public partial class MainWindow : Window
    {
        private List<Game> allGames = new List<Game>();
        private ICollectionView gameView;
        public MainWindow()
        {
            InitializeComponent();

            if (!Directory.Exists(coverFolder))
                Directory.CreateDirectory(coverFolder);

            LoadGames();
        }

        // LOAD ALL GAMES
        private async void LoadGames()
        {
            Trace.WriteLine("Loading games...");
            allGames = await LoadAllGames();

            gameView = CollectionViewSource.GetDefaultView(allGames);
            gameView.Filter = GameFilter;

            GameList.ItemsSource = gameView;
        }

        private bool GameFilter(object item)
        {
            var game = item as Game;

            if (game == null)
                return false;

            // SEARCH FILTER
            if (!string.IsNullOrWhiteSpace(SearchBox.Text))
            {
                if (!game.Name.ToLower().Contains(SearchBox.Text.ToLower()))
                    return false;
            }

            // LAUNCHER FILTER
            if (FilterBox.SelectedItem is ComboBoxItem selected)
            {
                string filter = selected.Content.ToString();

                if (filter != "All")
                {
                    if (game.ExecutablePath
                        .IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) < 0)
                        return false;

                }
            }

            return true;
        }

        private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            gameView?.Refresh();
        }

        private void FilterBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            gameView?.Refresh();
        }



        private readonly string coverFolder = Path.Combine(Directory.GetCurrentDirectory(), "Covers");


        private async Task<List<Game>> LoadAllGames()
        {
            List<Game> games = new List<Game>();

            // Steam
            foreach (var library in GetSteamLibraries())
            {
                if (Directory.Exists(library))
                    games.AddRange(ScanGames(library));
            }

            // Epic
            games.AddRange(ScanEpicManifests());

            // Riot
            if (Directory.Exists(@"C:\Riot Games"))
                games.AddRange(ScanGames(@"C:\Riot Games"));

            // Blizzard
            if (Directory.Exists(@"C:\Program Files (x86)\Battle.net"))
                games.AddRange(ScanGames(@"C:\Program Files (x86)\Battle.net"));

            // Xbox Game Pass
            if (Directory.Exists(@"C:\XboxGames"))
                games.AddRange(ScanGames(@"C:\XboxGames"));

            // Add cover art
            await AddCoverArt(games);

            // REMOVE DUPLICATES BY EXECUTABLE PATH
            games = games
                .GroupBy(g => g.ExecutablePath)
                .Select(g => g.First())
                .ToList();

            return games;

        }

        // GENERIC FOLDER SCANNER

        private List<Game> ScanGames(string folder)
        {
            List<Game> games = new List<Game>();

            foreach (var dir in Directory.GetDirectories(folder))
            {
                var exes = Directory.GetFiles(dir, "*.exe");

                if (exes.Length > 0 && !games.Any(g => g.ExecutablePath == exes[0]))
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
        // STEAM LIBRARIES

        private List<string> GetSteamLibraries()
        {
            List<string> libraries = new List<string>();

            string steamRoot = @"C:\Program Files (x86)\Steam";
            string libraryFile = Path.Combine(steamRoot, @"steamapps\libraryfolders.vdf");

            if (File.Exists(libraryFile))
            {
                foreach (var line in File.ReadAllLines(libraryFile))
                {
                    if (line.Contains(":\\")) // detects library paths
                    {
                        string path = line.Split('"')[3];
                        libraries.Add(Path.Combine(path, "steamapps", "common"));
                    }
                }
            }

            return libraries;
        }


        // EPIC MANIFEST SCANNER

        private List<Game> ScanEpicManifests()
        {
            List<Game> games = new List<Game>();

            string manifestFolder =
                @"C:\ProgramData\Epic\EpicGamesLauncher\Data\Manifests";

            if (!Directory.Exists(manifestFolder))
                return games;

            foreach (var file in Directory.GetFiles(manifestFolder, "*.item"))
            {
                string text = File.ReadAllText(file);

                string name = ExtractValue(text, "DisplayName");
                string install = ExtractValue(text, "InstallLocation");

                if (Directory.Exists(install))
                {
                    games.Add(new Game
                    {
                        Name = name,
                        ExecutablePath = FindExe(install),
                        ImagePath = "placeholder.png"
                    });
                }
            }

            return games;
        }
        // COVER ART SYSTEM

        private async Task AddCoverArt(List<Game> games)
        {
            foreach (var game in games)
            {
                game.ImagePath = await DownloadCover(game.Name);
            }
        }

        private async Task<string> DownloadCover(string gameName)
        {
            try
            {
                string apiKey = Environment.GetEnvironmentVariable("RAWG_API_KEY");

                if (string.IsNullOrEmpty(apiKey))
                    return "placeholder.png";

                string url =
                    $"https://api.rawg.io/api/games?key={apiKey}&search={gameName}";

                using (HttpClient client = new HttpClient())
                {
                    var response = await client.GetStringAsync(url);

                    JObject json = JObject.Parse(response);

                    var image =
                        json["results"]?[0]?["background_image"]?.ToString();

                    return image ?? "placeholder.png";
                }
            }
            catch
            {
                return "placeholder.png";
            }
        }


        // HELPER FUNCTIONS
        private string ExtractValue(string text, string key)
        {
            int index = text.IndexOf(key);

            if (index == -1)
                return "";

            int start = text.IndexOf(":", index) + 2;
            int end = text.IndexOf(",", start);

            return text.Substring(start, end - start)
                       .Replace("\"", "");
        }

        private string FindExe(string folder)
        {
            var files = Directory.GetFiles(folder, "*.exe",
                SearchOption.AllDirectories);

            if (files.Length > 0)
                return files[0];

            return "";
        }

        private string MakeSafeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');

            return name;
        }
        // PLAY BUTTON
        private void PlayGame_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as System.Windows.Controls.Button;
            string exePath = button.Tag.ToString();

            Process.Start(exePath);
        }
    }
}
