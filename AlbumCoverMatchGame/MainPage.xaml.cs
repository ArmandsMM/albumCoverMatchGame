using AlbumCoverMatchGame.models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace AlbumCoverMatchGame
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {

        private ObservableCollection<Song> Songs;
        private ObservableCollection<StorageFile> AllSongs;

        bool _playingMusic = false;
        int _round = 0;
        int _totalScore = 0;

        public MainPage()
        {
            this.InitializeComponent();
            Songs = new ObservableCollection<Song>();
        }

        private async Task RetrieveFilesAndFolders(ObservableCollection<StorageFile> list, StorageFolder parent)
        {
            foreach (var item in await parent.GetFilesAsync())
            {
                if (item.FileType == ".mp3")
                {
                    list.Add(item);
                }

            }

            foreach (var item in await parent.GetFoldersAsync())
            {
                await RetrieveFilesAndFolders(list, item);
            }
        }

        private async Task<List<StorageFile>> PickRandomSongs(ObservableCollection<StorageFile> allSongs)
        {
            Random random = new Random();
            var randomSongs = new List<StorageFile>();

            //find random songs but only once and only from different albums
            while (randomSongs.Count < 10)
            {
                var randomNumber = random.Next(allSongs.Count);
                var randomSong = allSongs[randomNumber];

                MusicProperties randomSongProperties = await randomSong.Properties.GetMusicPropertiesAsync();

                bool isDuplicate = false;
                foreach (var song in randomSongs)
                {
                    MusicProperties songMusicProperties = await song.Properties.GetMusicPropertiesAsync();
                    if (String.IsNullOrEmpty(randomSongProperties.Album) || randomSongProperties.Album == songMusicProperties.Album)
                    {
                        isDuplicate = true;
                    }
                }
                if (!isDuplicate)
                {
                    randomSongs.Add(randomSong);
                }
                
            }
            return randomSongs;
        }

        private async void PopulateSongList(List<StorageFile> files)
        {
            int id = 0;
            foreach (var file in files)
            {
                MusicProperties songProperties = await file.Properties.GetMusicPropertiesAsync();

                StorageItemThumbnail currentThumb = await file.GetThumbnailAsync(ThumbnailMode.MusicView, 200,ThumbnailOptions.UseCurrentScale);
                var albumCover = new BitmapImage();
                albumCover.SetSource(currentThumb);

                var song = new Song();
                song.Id = id;
                song.Title = songProperties.Title;
                song.Artist = songProperties.Artist;
                song.Album = songProperties.Album;
                song.AlbumCover = albumCover;
                song.SongFile = file;

                Songs.Add(song);

                id++;
            }
        }

        private async void SongGridview_ItemClick(object sender, ItemClickEventArgs e)
        {
            //ignore clicks when in cooldown
            if (!_playingMusic)
            {
                return;
            }
            countdown.Pause();
            myMediaElement.Stop();

            //find the correct song
            var clickedSong = (Song)e.ClickedItem;
            var correctSong = Songs.FirstOrDefault(p => p.Selected == true);


            //Evaluate users selectionStart
            Uri uri;
            int score;
            if (clickedSong.Selected)
            {
                uri = new Uri("ms-appx:///Assets/correct.png");
                score = (int) myProgressBar.Value;
            }
            else
            {
                uri = new Uri("ms-appx:///Assets/incorrect.png");
                score = (int) myProgressBar.Value * -1 ;
            }
            StorageFile file = await StorageFile.GetFileFromApplicationUriAsync(uri);
            var filestream = await file.OpenAsync(FileAccessMode.Read);
            await clickedSong.AlbumCover.SetSourceAsync(filestream);

            _totalScore += score;
            _round++;


            resultTextBlock.Text = string.Format("Score: {0} Total Score after {1} Rounds: {2}", score, _round, _totalScore);
            titleTextBlock.Text = string.Format("CorrectSong: {0}", correctSong.Title);
            artistTextBlock.Text = string.Format("Performed by: {0}", correctSong.Artist);
            albumTextBlock.Text = string.Format("On Album by: {0}", correctSong.Album);

            clickedSong.Used = true;
            correctSong.Selected = false;
            correctSong.Used = true;

            if (_round >=1)
            {
                instructionTextBlock.Text = string.Format("Game over. You score {0}", _totalScore);
                playAgain.Visibility = Visibility.Visible;
            }
            else
            {
                StartCoolDown();
            }
            
        }

        private async void playAgain_Click(object sender, RoutedEventArgs e)
        {
            startupProgressRing.IsActive = true;
            await PrepareNewGame();
            startupProgressRing.IsActive = false;
            playAgain.Visibility = Visibility.Collapsed;
        }

        private async Task<ObservableCollection<StorageFile>> SetupMusicList()
        {
            //1. get access to music library
            StorageFolder folder = KnownFolders.MusicLibrary;
            var allSongs = new ObservableCollection<StorageFile>();
            await RetrieveFilesAndFolders(allSongs, folder);
            return allSongs;
        }

        private async Task PrepareNewGame()
        {
            Songs.Clear();
            //2. choose random songs from library
            var randomSongs = await PickRandomSongs(AllSongs);
            //3.. get meta data from songs
            PopulateSongList(randomSongs);


            StartCoolDown();
            //state management
            instructionTextBlock.Text = "Get ready ..";
            resultTextBlock.Text = "";
            titleTextBlock.Text = "";
            artistTextBlock.Text = "";
            albumTextBlock.Text = "";

            _totalScore = 0;
            _round = 0;
        }

        private async void Grid_Loaded(object sender, RoutedEventArgs e)
        {
            startupProgressRing.IsActive = true;
            AllSongs = await SetupMusicList();
            await PrepareNewGame();
            startupProgressRing.IsActive = false;

            StartCoolDown(); 
        }

        private void StartCoolDown()
        {
            _playingMusic = false;
            SolidColorBrush brush = new SolidColorBrush(Colors.Blue);
            myProgressBar.Foreground = brush;
            instructionTextBlock.Text = string.Format("Get ready for round {0} ...", _round + 1);
            instructionTextBlock.Foreground = brush;

            countdown.Begin();
        }

        private void StartCountDown()
        {
            _playingMusic = true;
            SolidColorBrush brush = new SolidColorBrush(Colors.Red);
            myProgressBar.Foreground = brush;
            instructionTextBlock.Text = "GO!";
            instructionTextBlock.Foreground = brush;

            countdown.Begin();
        }

        private async void countdown_Completed(object sender, object e)
        {
            if (!_playingMusic)
            {
                //start playing some music
                var song = PickSong();

                myMediaElement.SetSource(await song.SongFile.OpenAsync(FileAccessMode.Read), song.SongFile.ContentType);
                //start countdown
                StartCountDown();
            }
        }

        private Song PickSong()
        {
            Random random = new Random();
            var unusedSongs = Songs.Where(p => p.Used == false);
            var randomNumber = random.Next(unusedSongs.Count());
            var randomSong = unusedSongs.ElementAt(randomNumber);
            randomSong.Selected = true;

            return randomSong;
        }
    }
}
