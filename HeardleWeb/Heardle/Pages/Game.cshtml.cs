using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
		public FullTrack CurrentSong { get; set; }
		[BindProperty]
		public string Message { get; set; } = "Loading Context...";
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
			
		}

		private async Task GetUserSongs()
		{
			var spotify = await _spotifyClientBuilder.BuildClient();
			var x = HttpContext.Session.GetString("PlaylistSongInfo");

			if (x == null)
			{
				PlaylistSongInfo = await spotify.Library.GetTracks(new LibraryTracksRequest { Limit = 50, Offset = 0 });
				for (int i = 50; i < PlaylistSongInfo.Total; i += 50)
				{
					PlaylistSongInfo.Items.AddRange(spotify.Library.GetTracks(new LibraryTracksRequest { Limit = 50, Offset = i }).Result.Items);
				}
				HttpContext.Session.SetString("PlaylistSongInfo", JsonConvert.SerializeObject(PlaylistSongInfo));
			}
			else
			{
				PlaylistSongInfo = JsonConvert.DeserializeObject<Paging<SavedTrack>>(x);
			}

			Console.WriteLine(PlaylistSongInfo + " containing " + PlaylistSongInfo.Total + " tracks");

			if (PlaylistSongInfo == null || PlaylistSongInfo.Total == 0)
			{
				Message = "Unable to find any liked songs for the current user";
				return;
			}

			return;
		}

		public async Task<PartialViewResult> OnGetNextSong()
		{
			await GetUserSongs();

			var spotify = await _spotifyClientBuilder.BuildClient();
			PlaylistSongInfo = JsonConvert.DeserializeObject<Paging<SavedTrack>>(HttpContext.Session.GetString("PlaylistSongInfo"));

			var songIndex = new Random().Next((int)PlaylistSongInfo.Total);

			CurrentSong = PlaylistSongInfo.Items[songIndex].Track;
			Console.WriteLine(CurrentSong.Name);
			HttpContext.Session.SetString("CurrentSong", JsonConvert.SerializeObject(CurrentSong));

			return PartialView("songData", this);
		}

		public async Task<IActionResult> OnPostPlayTrack(int timeout)
		{
			var spotify = await _spotifyClientBuilder.BuildClient();
			var deviceId = (HttpContext.Session.GetString("deviceId"));
			CurrentSong = JsonConvert.DeserializeObject<FullTrack>(HttpContext.Session.GetString("CurrentSong"));

			var playRequest = new PlayerResumePlaybackRequest() { Uris = new List<string>() { CurrentSong.Uri }, DeviceId = deviceId, PositionMs = 0 };
			await spotify.Player.ResumePlayback(playRequest);

			return new JsonResult("Playing track " + CurrentSong.Name);
		}

		public async Task<IActionResult> OnPostGuess(string guess)
		{
			var spotify = await _spotifyClientBuilder.BuildClient();
			CurrentSong = JsonConvert.DeserializeObject<FullTrack>(HttpContext.Session.GetString("CurrentSong"));

			var guessResponse = new SearchRequest(SearchRequest.Types.Track, guess);
			var guessedSong = (await spotify.Search.Item(guessResponse)).Tracks.Items.FirstOrDefault();

			Console.WriteLine("Correct Song: " + CurrentSong.Href + ", " + CurrentSong.Name + " by: " + String.Join(" & ", CurrentSong.Artists.Select(x => x.Name)));
			Console.WriteLine("Song Guessed: " + guessedSong.Href + ", " + guessedSong.Name + " by: " + String.Join(" & ", guessedSong.Artists.Select(x => x.Name)));

			Console.WriteLine(guessedSong.Name == CurrentSong.Name);
			Console.WriteLine(guessedSong.Artists.Select(x => x.Name).Except(CurrentSong.Artists.Select(x => x.Name)).ToList().Count);

			if (guessedSong.Id == CurrentSong.Id || (guessedSong.Name == CurrentSong.Name && guessedSong.Artists.Select(x => x.Name).Except(CurrentSong.Artists.Select(x => x.Name)).ToList().Count == 0))
			{
				Console.WriteLine("Correct guessResponse");
				return new JsonResult(true);
			}

			Console.WriteLine("Incorrect Guess");
			return new JsonResult(false);
		}

		public IActionResult OnPostAutoComplete(string songName, string artistName)
		{
			Console.WriteLine(songName);
			var spotify = _spotifyClientBuilder.BuildClient().Result;
			PlaylistSongInfo = JsonConvert.DeserializeObject<Paging<SavedTrack>>(HttpContext.Session.GetString("PlaylistSongInfo"));

			var searchResult = PlaylistSongInfo.Items.Select(item => item.Track).Where(track =>
			{
				return (string.IsNullOrWhiteSpace(songName) || track.Name.ToUpper().Contains(songName.ToUpper())) && (string.IsNullOrWhiteSpace(artistName) || track.Artists.Select(x => x.Name).Any(artist => artist.ToUpper().Contains(artistName.ToUpper())));
				}).ToList();

			return new JsonResult(searchResult);
		}

		public IActionResult OnPostConnectSDK(string deviceId)
		{
			HttpContext.Session.SetString("deviceId", deviceId);
			return new JsonResult("Player Loaded. Retrieving users liked songs...");
		}

		[NonAction]
		public virtual PartialViewResult PartialView(string viewName, object model)
		{
			ViewData.Model = model;

			return new PartialViewResult()
			{
				ViewName = viewName,
				ViewData = ViewData,
				TempData = TempData
			};
		}
	}

}
