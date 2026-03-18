using Newtonsoft.Json;
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
        private readonly string cacheFile = Path.Combine(Directory.GetCurrentDirectory(), "games.json");
        private static readonly HttpClient httpClient = new HttpClient();
        private List<Game> allGames = new List<Game>();
        private ICollectionView gameView;
        private ControllerManager controller;
        private int selectedIndex = 0;

        private readonly string coverFolder = Path.Combine(Directory.GetCurrentDirectory(), "Covers");

        public MainWindow()
        {
            InitializeComponent();

            controller = new ControllerManager();

            controller.OnUp += MoveUp;
            controller.OnDown += MoveDown;
            controller.OnSelect += SelectGame;

            controller.Start();


            if (!Directory.Exists(coverFolder))
                Directory.CreateDirectory(coverFolder);

            _ = LoadGames();

            GameList.Focus();
        }

        // LOAD ALL GAMES
        private async Task LoadGames()
        {
            Trace.WriteLine("Loading games...");

            if (File.Exists(cacheFile))
            {
                // LOAD FROM CACHE
                string json = File.ReadAllText(cacheFile);
                allGames = JsonConvert.DeserializeObject<List<Game>>(json);
            }
            else
            {
                allGames = await LoadAllGames();

                // SAVE TO CACHE
                string json = JsonConvert.SerializeObject(allGames, Formatting.Indented);
                File.WriteAllText(cacheFile, json);
            }

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
                    if (!string.Equals(game.Launcher, filter, StringComparison.OrdinalIgnoreCase))
                        return false;
                }
            }

            return true;
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            gameView?.Refresh();
        }

        private void FilterBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            gameView?.Refresh();
        }

        private async Task<List<Game>> LoadAllGames()
        {
            List<Game> games = new List<Game>();

            // Steam
            foreach (var library in GetSteamLibraries())
            {
                if (Directory.Exists(library))
                    games.AddRange(ScanGames(library, "Steam"));
            }

            // Epic
            games.AddRange(ScanEpicManifests());

            // Riot
            if (Directory.Exists(@"C:\Riot Games"))
                games.AddRange(ScanGames(@"C:\Riot Games", "Riot"));

            // Blizzard
            if (Directory.Exists(@"C:\Program Files (x86)\Battle.net"))
                games.AddRange(ScanGames(@"C:\Program Files (x86)\Battle.net", "Blizzard"));

            // Xbox Game Pass
            if (Directory.Exists(@"C:\XboxGames"))
                games.AddRange(ScanGames(@"C:\XboxGames", "Xbox"));

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
        private List<Game> ScanGames(string folder, string launcher)
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
                        ImagePath = "placeholder.png",
                        Launcher = launcher
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
                        ImagePath = "placeholder.png",
                        Launcher = "Epic"
                    });
                }
            }

            return games;
        }

        // COVER ART SYSTEM
        private async Task AddCoverArt(List<Game> games)
        {
            var tasks = games.Select(async game =>
            {
                game.ImagePath = await DownloadCover(game.Name);
            });

            await Task.WhenAll(tasks);
        }

        private async Task<string> DownloadCover(string gameName)
        {
            try
            {
                string apiKey = Environment.GetEnvironmentVariable("RAWG_API_KEY");

                if (string.IsNullOrEmpty(apiKey))
                    return "placeholder.png";

                string url =
                    $"https://api.rawg.io/api/games?key={apiKey}&search={gameName}&page_size=1";

                var response = await httpClient.GetStringAsync(url);

                JObject json = JObject.Parse(response);

                var result = json["results"]?[0];

                if (result == null)
                    return "placeholder.png";

                string image = result["background_image"]?.ToString();

                string genre =
                    result["genres"]?[0]?["name"]?.ToString();

                string released =
                    result["released"]?.ToString();

                double rating =
                    result["rating"]?.ToObject<double>() ?? 0;

                var game = allGames.FirstOrDefault(g => g.Name == gameName);

                if (game != null)
                {
                    game.Genre = genre;
                    game.ReleaseDate = released;
                    game.Rating = rating;
                }

                return image ?? "placeholder.png";
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

        private async void RefreshLibrary_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (File.Exists(cacheFile))
                    File.Delete(cacheFile);

                allGames.Clear();
                GameList.ItemsSource = null;

                allGames = await LoadAllGames();

                string json = JsonConvert.SerializeObject(allGames, Formatting.Indented);
                File.WriteAllText(cacheFile, json);

                gameView = CollectionViewSource.GetDefaultView(allGames);
                gameView.Filter = GameFilter;

                GameList.ItemsSource = gameView;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Refresh failed: " + ex.Message);
            }
        }


        private void SortBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (gameView == null)
                return;

            gameView.SortDescriptions.Clear();

            switch (SortBox.SelectedIndex)
            {
                case 0:
                    gameView.SortDescriptions.Add(
                        new SortDescription("Name", ListSortDirection.Ascending));
                    break;

                case 1:
                    gameView.SortDescriptions.Add(
                        new SortDescription("Name", ListSortDirection.Descending));
                    break;

                case 2:
                    gameView.SortDescriptions.Add(
                        new SortDescription("Rating", ListSortDirection.Descending));
                    break;

                case 3:
                    gameView.SortDescriptions.Add(
                        new SortDescription("ReleaseDate", ListSortDirection.Descending));
                    break;
            }
        }

        // PLAY BUTTON
        private void PlayGame_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            string exePath = button.Tag.ToString();

            Process.Start(exePath);
        }

        //CONTROLLER METHODS
        private void MoveUp()
        {
            Dispatcher.Invoke(() =>
            {
                if (GameList.Items.Count == 0) return;

                selectedIndex--;
                if (selectedIndex < 0)
                    selectedIndex = GameList.Items.Count - 1;

                GameList.SelectedIndex = selectedIndex;
                GameList.ScrollIntoView(GameList.SelectedItem);
            });
        }

        private void MoveDown()
        {
            Dispatcher.Invoke(() =>
            {
                if (GameList.Items.Count == 0) return;

                selectedIndex++;
                if (selectedIndex >= GameList.Items.Count)
                    selectedIndex = 0;

                GameList.SelectedIndex = selectedIndex;
                GameList.ScrollIntoView(GameList.SelectedItem);
            });
        }

        private void SelectGame()
        {
            Dispatcher.Invoke(() =>
            {
                if (GameList.SelectedItem is Game game)
                {
                    Process.Start(game.ExecutablePath);
                }
            });
        }

    }
}
