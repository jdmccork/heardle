using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Newtonsoft.Json;
using SpotifyAPI.Web;

namespace Heardle.Pages
{
	public class GameModel : PageModel
	{
		private readonly SpotifyClientBuilder _spotifyClientBuilder;

		[BindProperty]
		public string Guess { get; set; }
		[BindProperty]
		public FullTrack CurrentSong { get; set; }
		[BindProperty]
		public string Message { get; set; }
		public Paging<SavedTrack> PlaylistSongInfo { get; set; }
		public string Token { get; set; }

		public GameModel(SpotifyClientBuilder spotifyClientBuilder)
		{
			_spotifyClientBuilder = spotifyClientBuilder;
			Token = spotifyClientBuilder.GetToken();
		}

		public PrivateUser Me { get; set; }

		public async Task OnGet()
		{
			var spotify = await _spotifyClientBuilder.BuildClient();
			if (HttpContext.Session.GetString("PlaylistSongInfo") != null)
			{
				CurrentSong = JsonConvert.DeserializeObject<FullTrack>(HttpContext.Session.GetString("CurrentSong"));
				return;
			}

			PlaylistSongInfo = await spotify.Library.GetTracks(new LibraryTracksRequest { Limit = 1, Offset = 0 });
			HttpContext.Session.SetString("PlaylistSongInfo", JsonConvert.SerializeObject(PlaylistSongInfo));
			Console.WriteLine(PlaylistSongInfo + " containing " + PlaylistSongInfo.Total + " tracks");

			if (PlaylistSongInfo == null || PlaylistSongInfo.Total == 0)
			{
				Message = "Unable to find any liked songs for the current user";
				return;
			}

			await GetNextSong();
			return;
		}

		private async Task GetNextSong()
		{
			var spotify = await _spotifyClientBuilder.BuildClient();
			PlaylistSongInfo = JsonConvert.DeserializeObject<Paging<SavedTrack>>(HttpContext.Session.GetString("PlaylistSongInfo"));

			var songIndex = new Random().Next((int)PlaylistSongInfo.Total);
			var likedSongs = await spotify.Library.GetTracks(new LibraryTracksRequest { Limit = 1, Offset = songIndex });

			CurrentSong = likedSongs.Items[0].Track;
			Console.WriteLine(CurrentSong.Name);
			HttpContext.Session.SetString("CurrentSong", JsonConvert.SerializeObject(CurrentSong));
		}

		public async Task OnPostGuess()
		{
			var spotify = await _spotifyClientBuilder.BuildClient();
			CurrentSong = JsonConvert.DeserializeObject<FullTrack>(HttpContext.Session.GetString("CurrentSong"));

			var guess = new SearchRequest(SearchRequest.Types.Track, Guess);
			var guessedSong = (await spotify.Search.Item(guess)).Tracks.Items.FirstOrDefault();

			Console.WriteLine("Correct Song: " + CurrentSong.Href + ", " + CurrentSong.Name + " by: " + String.Join(" & ", CurrentSong.Artists.Select(x => x.Name)));
			Console.WriteLine("Song Guessed: " + guessedSong.Href + ", " + guessedSong.Name + " by: " + String.Join(" & ", guessedSong.Artists.Select(x => x.Name)));

			Console.WriteLine(guessedSong.Name == CurrentSong.Name);
			Console.WriteLine(guessedSong.Artists.Select(x => x.Name).Except(CurrentSong.Artists.Select(x => x.Name)).ToList().Count);

			if (guessedSong.Id == CurrentSong.Id 
				|| (
					guessedSong.Name == CurrentSong.Name 
					&& guessedSong.Artists.Select(x => x.Name).Except(CurrentSong.Artists.Select(x => x.Name)).ToList().Count == 0))
			{
				Message = "Correct Guess";
				Console.WriteLine("Correct guess");
				await GetNextSong();
			}
			else
			{
				Message = "Incorrect Guess";
				Console.WriteLine("Incorrect Guess");
			}
		}

		public IActionResult OnPostAutoComplete(string search)
		{
			Console.WriteLine(search);
			var spotify = _spotifyClientBuilder.BuildClient().Result;

			var guess = new SearchRequest(SearchRequest.Types.Track, search) { Limit = 50 };
			var searchResult = spotify.Search.Item(guess).Result.Tracks.Items;

			var test = new LibraryCheckTracksRequest(searchResult.Select(x => x.Id).ToList());
			var tracksInPlaylist = spotify.Library.CheckTracks(test).Result;

			var candidateSongs = searchResult.Where((track, index) => tracksInPlaylist[index] || (track.Name == CurrentSong.Name && track.Artists.Select(x => x.Name).Except(CurrentSong.Artists.Select(x => x.Name)).ToList().Count == 0));

			return new JsonResult(candidateSongs);
		}

		public async Task<IActionResult> OnPostConnectSDK(string deviceId)
		{
			var spotify = await _spotifyClientBuilder.BuildClient();
			CurrentSong = JsonConvert.DeserializeObject<FullTrack>(HttpContext.Session.GetString("CurrentSong"));

			var response = await spotify.Player.TransferPlayback(new PlayerTransferPlaybackRequest(new List<string>() { deviceId }));
			var request = new PlayerResumePlaybackRequest() { Uris = new List<string>() { CurrentSong.Uri }, DeviceId = deviceId, PositionMs = 0 };

			var playback = await spotify.Player.ResumePlayback(request);

			return new JsonResult(response);
		}

		public async Task<IActionResult> OnPost()
		{
			await HttpContext.SignOutAsync();
			return Redirect("/");
		}
	}

}
