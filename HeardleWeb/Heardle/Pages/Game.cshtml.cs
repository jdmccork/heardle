using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SpotifyAPI.Web;
using static SpotifyAPI.Web.PlayerResumePlaybackRequest;

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
		public Paging<SavedTrack> LikedSongsInfo { get; set; }


		public GameModel(SpotifyClientBuilder spotifyClientBuilder)
		{
			_spotifyClientBuilder = spotifyClientBuilder;
		}

		public PrivateUser Me { get; set; }

		public async Task OnGet()
		{
			var spotify = await _spotifyClientBuilder.BuildClient();

			LikedSongsInfo = await spotify.Library.GetTracks(new LibraryTracksRequest { Limit = 1, Offset = 0 });
			HttpContext.Session.SetString("LikedSongsInfo", JsonConvert.SerializeObject(LikedSongsInfo));
			Console.WriteLine(LikedSongsInfo + " containing " + LikedSongsInfo.Total + " tracks");

			if (LikedSongsInfo == null || LikedSongsInfo.Total == 0)
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
			LikedSongsInfo = JsonConvert.DeserializeObject<Paging<SavedTrack>>(HttpContext.Session.GetString("LikedSongsInfo"));

			var songIndex = new Random().Next((int)LikedSongsInfo.Total);
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

			Console.WriteLine("Correct Song: " + CurrentSong.Href + ", " + CurrentSong.Name);
			Console.WriteLine("Song Guessed: " + guessedSong.Href + ", " + guessedSong.Name);

			if (guessedSong.Id == CurrentSong.Id)
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

		public async Task<IActionResult> OnPost()
		{
			await HttpContext.SignOutAsync();
			return Redirect("/");
		}
	}

}
