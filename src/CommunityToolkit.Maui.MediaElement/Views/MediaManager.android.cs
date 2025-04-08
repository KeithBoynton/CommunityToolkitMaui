using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Android.Content;
using Android.Nfc;
using Android.Views;
using Android.Widget;
using AndroidX.Media3.Common;
using AndroidX.Media3.Common.Text;
using AndroidX.Media3.Common.Util;
using AndroidX.Media3.ExoPlayer;
using AndroidX.Media3.ExoPlayer.Source;
using AndroidX.Media3.Session;
using AndroidX.Media3.UI;
using CommunityToolkit.Maui.Core.Primitives;
using CommunityToolkit.Maui.Media.Services;
using CommunityToolkit.Maui.Services;
using CommunityToolkit.Maui.Views;
using Microsoft.Extensions.Logging;
using AudioAttributes = AndroidX.Media3.Common.AudioAttributes;
using DeviceInfo = AndroidX.Media3.Common.DeviceInfo;
using MediaMetadata = AndroidX.Media3.Common.MediaMetadata;

namespace CommunityToolkit.Maui.Core.Views;

public partial class MediaManager : Java.Lang.Object, IPlayerListener
{
	const int bufferState = 2;
	const int readyState = 3;
	const int endedState = 4;

	static readonly HttpClient client = new();
	readonly SemaphoreSlim seekToSemaphoreSlim = new(1, 1);
	readonly SemaphoreSlim moveToSemaphoreSlim = new(1, 1);

	double? previousSpeed;
	float volumeBeforeMute = 1;

	TaskCompletionSource? seekToTaskCompletionSource;
	TaskCompletionSource? moveToTaskCompletionSource;
	CancellationTokenSource? cancellationTokenSource;
	MediaSession? session;
	MediaItem.Builder? mediaItem;
	BoundServiceConnection? connection;

	/// <summary>
	/// The platform native counterpart of <see cref="MediaElement"/>.
	/// </summary>
	protected PlayerView? PlayerView { get; set; }

	/// <summary>
	/// Occurs when ExoPlayer changes the playback parameters.
	/// </summary>
	/// <paramref name="playbackParameters">Object containing the new playback parameter values.</paramref>
	/// <remarks>
	/// This is part of the <see cref="IPlayerListener"/> implementation.
	/// While this method does not seem to have any references, it's invoked at runtime.
	/// </remarks>
	public void OnPlaybackParametersChanged(PlaybackParameters? playbackParameters)
	{
		if (playbackParameters is null || AreFloatingPointNumbersEqual(playbackParameters.Speed, MediaElement.Speed))
		{
			return;
		}

		MediaElement.Speed = playbackParameters.Speed;
	}

	public void UpdateNotifications()
	{
		if (connection?.Binder?.Service is null)
		{
			System.Diagnostics.Trace.TraceInformation("Notification Service not running.");
			return;
		}

		if (session is not null && Player is not null)
		{
			connection.Binder.Service.UpdateNotifications(session, Player);
		}
	}

	/// <summary>
	/// Occurs when ExoPlayer changes the player state.
	/// </summary>
	/// <paramref name="playWhenReady">Indicates whether the player should start playing the media whenever the media is ready.</paramref>
	/// <paramref name="playbackState">The state that the player has transitioned to.</paramref>
	/// <remarks>
	/// This is part of the <see cref="IPlayerListener"/> implementation.
	/// While this method does not seem to have any references, it's invoked at runtime.
	/// </remarks>
	public void OnPlayerStateChanged(bool playWhenReady, int playbackState)
	{
		if (Player is null || MediaElement.Source is null)
		{
			return;
		}

		Debug.WriteLine($"PlayerState from ExoPlayer [{playbackState}]");

		var newState = playbackState switch
		{
			PlaybackState.StateFastForwarding
				or PlaybackState.StateRewinding
				or PlaybackState.StateSkippingToNext
				or PlaybackState.StateSkippingToPrevious
				or PlaybackState.StateSkippingToQueueItem
				or PlaybackState.StatePlaying => playWhenReady
					? MediaElementState.Playing
					: mediaOpening ? MediaElementState.Opened : MediaElementState.Paused,

			PlaybackState.StatePaused => MediaElementState.Paused,

			PlaybackState.StateConnecting
				or PlaybackState.StateBuffering => MediaElementState.Buffering,

			PlaybackState.StateNone => MediaElementState.None,
			PlaybackState.StateStopped => MediaElement.CurrentState is not MediaElementState.Failed
				? MediaElementState.Stopped
				: MediaElementState.Failed,

			PlaybackState.StateError => MediaElementState.Failed,

			_ => MediaElementState.None,
		};

		// Reset the opening state
		if (mediaOpening && (newState == MediaElementState.Playing || newState == MediaElementState.Opened))
		{
			mediaOpening = false;
		}

		Debug.WriteLine($"Raising XCTMediaElement CurrentStateChanged [{newState}]");
		MediaElement.CurrentStateChanged(newState);
		if (playbackState is readyState)
		{
			MediaElement.Duration = TimeSpan.FromMilliseconds(Player.Duration < 0 ? 0 : Player.Duration);
			MediaElement.Position = TimeSpan.FromMilliseconds(Player.CurrentPosition < 0 ? 0 : Player.CurrentPosition);
		}
	}

	/// <summary>
	/// Creates the corresponding platform view of <see cref="MediaElement"/> on Android.
	/// </summary>
	/// <returns>The platform native counterpart of <see cref="MediaElement"/>.</returns>
	/// <exception cref="NullReferenceException">Thrown when <see cref="Context"/> is <see langword="null"/> or when the platform view could not be created.</exception>
	[MemberNotNull(nameof(Player), nameof(PlayerView), nameof(session))]
	public (PlatformMediaElement platformView, PlayerView PlayerView) CreatePlatformView()
	{
		Player = new ExoPlayerBuilder(MauiContext.Context).Build() ?? throw new InvalidOperationException("Player cannot be null");
		Player.AddListener(this);
		PlayerView = new PlayerView(MauiContext.Context)
		{
			Player = Player,
			UseController = false,
			ControllerAutoShow = false,
			LayoutParameters = new RelativeLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent)
		};
		string randomId = Convert.ToBase64String(Guid.NewGuid().ToByteArray())[..8];
		var mediaSessionWRandomId = new MediaSession.Builder(Platform.AppContext, Player);
		mediaSessionWRandomId.SetId(randomId);
		mediaSessionWRandomId.SetCallback(new MediaSessionCallback());
		session ??= mediaSessionWRandomId.Build() ?? throw new InvalidOperationException("Session cannot be null");
		ArgumentNullException.ThrowIfNull(session.Id);

		return (Player, PlayerView);
	}

	/// <summary>
	/// Occurs when ExoPlayer changes the playback state.
	/// </summary>
	/// <paramref name="playbackState">The state that the player has transitioned to.</paramref>
	/// <remarks>
	/// This is part of the <see cref="IPlayerListener"/> implementation.
	/// While this method does not seem to have any references, it's invoked at runtime.
	/// </remarks>
	public void OnPlaybackStateChanged(int playbackState)
	{
		if (MediaElement.Source is null)
		{
			return;
		}

		Debug.WriteLine($"PlaybackState from ExoPlayer [{playbackState}]");

		MediaElementState newState = MediaElement.CurrentState;
		Debug.WriteLine($"CurrentState of XCTMediaElement {newState}");
		switch (playbackState)
		{
			case bufferState:
				newState = MediaElementState.Buffering;
				break;
			case endedState:
				newState = MediaElementState.Stopped;
				MediaElement.MediaEnded();
				break;
			case readyState:
				seekToTaskCompletionSource?.TrySetResult();
				break;
		}

		Debug.WriteLine($"Raising XCTMediaElement CurrentStateChanged [{newState}]");
		MediaElement.CurrentStateChanged(newState);
	}

	/// <summary>
	/// Occurs when ExoPlayer encounters an error.
	/// </summary>
	/// <paramref name="error">An instance of <seealso cref="PlaybackException"/> containing details of the error.</paramref>
	/// <remarks>
	/// This is part of the <see cref="IPlayerListener"/> implementation.
	/// While this method does not seem to have any references, it's invoked at runtime.
	/// </remarks>
	public void OnPlayerError(PlaybackException? error)
	{
		var errorMessage = string.Empty;
		var errorCode = string.Empty;
		var errorCodeName = string.Empty;

		if (!string.IsNullOrWhiteSpace(error?.LocalizedMessage))
		{
			errorMessage = $"Error message: {error.LocalizedMessage}";
		}

		if (error?.ErrorCode is not null)
		{
			errorCode = $"Error code: {error.ErrorCode}";
		}

		if (!string.IsNullOrWhiteSpace(error?.ErrorCodeName))
		{
			errorCodeName = $"Error codename: {error.ErrorCodeName}";
		}

		var message = string.Join(", ", new[]
		{
			errorCodeName,
			errorCode,
			errorMessage
		}.Where(static s => !string.IsNullOrEmpty(s)));

		MediaElement.MediaFailed(new MediaFailedEventArgs(message));

		Logger.LogError("{LogMessage}", message);
	}

	public void OnVideoSizeChanged(VideoSize? videoSize)
	{
		MediaElement.MediaWidth = videoSize?.Width ?? 0;
		MediaElement.MediaHeight = videoSize?.Height ?? 0;
	}

	/// <summary>
	/// Occurs when ExoPlayer changes volume.
	/// </summary>
	/// <param name="volume">The new value for volume.</param>
	/// <remarks>
	/// This is part of the <see cref="IPlayerListener"/> implementation.
	/// While this method does not seem to have any references, it's invoked at runtime.
	/// </remarks>
	public void OnVolumeChanged(float volume)
	{
		if (Player is null)
		{
			return;
		}

		// When currently muted, ignore
		if (MediaElement.ShouldMute)
		{
			return;
		}

		MediaElement.Volume = volume;
	}

	protected virtual partial void PlatformPlay()
	{
		if (Player is null || MediaElement.Source is null)
		{
			return;
		}

		Player.Prepare();
		Player.Play();
	}

	protected virtual partial void PlatformPause()
	{
		if (Player is null || MediaElement.Source is null)
		{
			return;
		}

		Player.Pause();
	}

	[MemberNotNull(nameof(Player))]
	protected virtual async partial Task PlatformSeek(TimeSpan position, CancellationToken token)
	{
		if (Player is null)
		{
			throw new InvalidOperationException($"{nameof(IExoPlayer)} is not yet initialized");
		}

		await seekToSemaphoreSlim.WaitAsync(token);

		seekToTaskCompletionSource = new();
		try
		{
			Player.SeekTo((long)position.TotalMilliseconds);

			// Here, we don't want to throw an exception
			// and to keep the execution on the thread that called this method
			await seekToTaskCompletionSource.Task.WaitAsync(TimeSpan.FromMinutes(2), token).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing | ConfigureAwaitOptions.ContinueOnCapturedContext);

			MediaElement.SeekCompleted();
		}
		finally
		{
			seekToSemaphoreSlim.Release();
		}
	}

	protected virtual async partial Task PlatformMoveTo(int index, CancellationToken token)
	{
		if (Player is null)
		{
			throw new InvalidOperationException($"{nameof(IExoPlayer)} is not yet initialized");
		}

		if (MediaElement.Source is not PlaylistMediaSource)
		{
			return;
		}

		await moveToSemaphoreSlim.WaitAsync(token);

		moveToTaskCompletionSource = new();

		try
		{
			Player.SeekTo(index, 0);

			// Here, we don't want to throw an exception
			// and to keep the execution on the thread that called this method
			await moveToTaskCompletionSource.Task.WaitAsync(TimeSpan.FromMinutes(2), token).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing | ConfigureAwaitOptions.ContinueOnCapturedContext);
		}
		finally
		{
			moveToSemaphoreSlim.Release();
		}
	}

	protected virtual async partial Task PlatformAddMediaToPlaylist(MediaSource media, int? index)
	{
		if (Player is null)
		{
			return;
		}

		await MainThread.InvokeOnMainThreadAsync(() =>
		{

			if (mediaItems is not null)
			{
				var mediaItem = CreateBasicMediaItem(media);

				// If the index isn't specified or it's outside the existing playlist range
				if (index is null || index < 0 || index >= Player.MediaItemCount)
				{
					Player.AddMediaItem(mediaItem);
				}
				else
				{
					Player.AddMediaItem((int)index, mediaItem);
				}
			}
		});
	}

	protected virtual partial Task PlatformMovePrevious(CancellationToken token)
	{
		throw new NotImplementedException();
	}

	protected virtual partial Task PlatformMoveNext(CancellationToken token)
	{
		throw new NotImplementedException();
	}

	protected virtual partial void PlatformStop()
	{
		if (Player is null || MediaElement.Source is null)
		{
			return;
		}

		Player.SeekTo(0);
		Player.Stop();
		MediaElement.Position = TimeSpan.Zero;
	}

	bool mediaOpening = false;
	List<MediaItem> mediaItems = new List<MediaItem>();
	protected virtual async partial ValueTask PlatformUpdateSource()
	{
		var hasSetSource = false;

		if (Player is null)
		{
			return;
		}

		if (connection is null)
		{
			StartService();
		}

		if (MediaElement.Source is null)
		{
			Player.ClearMediaItems();
			mediaItems.Clear();
			MediaElement.Duration = TimeSpan.Zero;
			MediaElement.CurrentStateChanged(MediaElementState.None);

			return;
		}

		if (MediaElement.Source is PlaylistMediaSource mediaSource)
		{
			if (mediaSource.Sources is null || mediaSource.Sources.Count == 0)
			{
				Player.ClearMediaItems();
				mediaItems.Clear();
				MediaElement.Duration = TimeSpan.Zero;
				MediaElement.CurrentStateChanged(MediaElementState.None);

				return;
			}
		}

		MediaElement.CurrentStateChanged(MediaElementState.Opening);
		mediaOpening = true;
		Player.PlayWhenReady = MediaElement.ShouldAutoPlay;
		cancellationTokenSource ??= new();

		if (MediaElement.Source is PlaylistMediaSource playlistMediaSource)
		{
			if (playlistMediaSource.Sources is not null)
			{
				Player.ClearMediaItems();
				mediaItems.Clear();

				/* Need to use AddMediaItems instead of this but that hasn't been exposed and the MediaSourceFactory
				 * isn't exposed to create media sources to give to the SetMediaSources method */
				//var mediaItems = new List<MediaItem>();
				foreach (var playlistItem in playlistMediaSource.Sources)
				{
					// ConfigureAwait(true) is required to prevent crash on startup
					var result = await SetPlayerData(playlistItem, cancellationTokenSource.Token).ConfigureAwait(true);
					var item = result?.Build();
					//var item = CreateBasicMediaItem(playlistItem);
					if (item != null)
					{
						//mediaItems.Add(item);
						Player.AddMediaItem(item);
						mediaItems.Add(item);
					}
				}

				/*
				try
				{
					//Player.AddMediaItems(mediaItems);
					Player.SetMediaItems(mediaItems);
					//Player.SetMediaItem(mediaItems[0]);
				}
				catch (Exception ex)
				{
					Debug.WriteLine($"Exception {ex}");
				}
				*/
				if (playlistMediaSource.StartIndex > 0 && playlistMediaSource.StartIndex < playlistMediaSource.Sources.Count)
				{
					Player.SeekTo(playlistMediaSource.StartIndex, -1);
				}

				Debug.WriteLine($"PlaylistPosition[{Player.CurrentMediaItemIndex}]");

				Player.Prepare();

				Debug.WriteLine($"PlaylistPosition[{Player.CurrentMediaItemIndex}]");
				//Player.Play();
				hasSetSource = true;

				if (hasSetSource && Player.PlayerError is null)
				{
					MediaElement.MediaOpened();
					UpdateNotifications();
				}
			}
		}
		else
		{
			// ConfigureAwait(true) is required to prevent crash on startup
			var result = await SetPlayerData(MediaElement.Source, cancellationTokenSource.Token).ConfigureAwait(true);
			var item = result?.Build();

			if (item?.MediaMetadata is not null)
			{
				Player.SetMediaItem(item);
				Player.Prepare();
				hasSetSource = true;
			}

			if (hasSetSource && Player.PlayerError is null)
			{
				MediaElement.MediaOpened();
				UpdateNotifications();
			}
		}
	}

	protected virtual partial void PlatformUpdateAspect()
	{
		if (PlayerView is null)
		{
			return;
		}

		PlayerView.ResizeMode = MediaElement.Aspect switch
		{
			Aspect.AspectFill => AspectRatioFrameLayout.ResizeModeZoom,
			Aspect.Fill => AspectRatioFrameLayout.ResizeModeFill,
			Aspect.Center or Aspect.AspectFit => AspectRatioFrameLayout.ResizeModeFit,
			_ => throw new NotSupportedException($"{nameof(Aspect)}: {MediaElement.Aspect} is not yet supported")
		};
	}

	protected virtual partial void PlatformUpdateSpeed()
	{
		if (Player is null)
		{
			return;
		}

		// First time we're getting a playback speed, set initial value
		previousSpeed ??= MediaElement.Speed;

		if (MediaElement.Speed > 0)
		{
			Player.SetPlaybackSpeed((float)MediaElement.Speed);

			if (previousSpeed is 0)
			{
				Player.Play();
			}

			previousSpeed = MediaElement.Speed;
		}
		else
		{
			previousSpeed = 0;
			Player.Pause();
		}
	}

	protected virtual partial void PlatformUpdateShouldShowPlaybackControls()
	{
		if (PlayerView is null)
		{
			return;
		}

		PlayerView.UseController = MediaElement.ShouldShowPlaybackControls;
	}

	protected virtual partial void PlatformUpdatePosition()
	{
		if (Player is null)
		{
			return;
		}

		if (MediaElement.Duration != TimeSpan.Zero)
		{
			MediaElement.Position = TimeSpan.FromMilliseconds(Player.CurrentPosition);
		}
	}

	protected virtual partial void PlatformUpdateVolume()
	{
		if (Player is null)
		{
			return;
		}

		// If the user changes while muted, change the internal field
		// and do not update the actual volume.
		if (MediaElement.ShouldMute)
		{
			volumeBeforeMute = (float)MediaElement.Volume;
			return;
		}

		Player.Volume = (float)MediaElement.Volume;
	}

	protected virtual partial void PlatformUpdateShouldKeepScreenOn()
	{
		if (PlayerView is null)
		{
			return;
		}

		PlayerView.KeepScreenOn = MediaElement.ShouldKeepScreenOn;
	}

	protected virtual partial void PlatformUpdateShouldMute()
	{
		if (Player is null)
		{
			return;
		}

		// We're going to mute state. Capture current volume first so we can restore later.
		if (MediaElement.ShouldMute)
		{
			volumeBeforeMute = Player.Volume;
		}
		else if (!AreFloatingPointNumbersEqual(volumeBeforeMute, Player.Volume) && Player.Volume > 0)
		{
			volumeBeforeMute = Player.Volume;
		}

		Player.Volume = MediaElement.ShouldMute ? 0 : volumeBeforeMute;
	}

	protected virtual partial void PlatformUpdateShouldLoopPlayback()
	{
		if (Player is null)
		{
			return;
		}

		Player.RepeatMode = MediaElement.ShouldLoopPlayback ? RepeatModeUtil.RepeatToggleModeOne : RepeatModeUtil.RepeatToggleModeNone;
	}

	protected override void Dispose(bool disposing)
	{
		base.Dispose(disposing);

		if (disposing)
		{
			session?.Release();
			session?.Dispose();
			session = null;

			cancellationTokenSource?.Dispose();
			cancellationTokenSource = null;

			if (connection is not null)
			{
				StopService(connection);
				connection.Dispose();
				connection = null;
			}

			client.Dispose();
		}
	}

	static async Task<byte[]> GetBytesFromMetadataArtworkUrl(string? url, CancellationToken cancellationToken = default)
	{
		byte[] artworkData = [];
		try
		{
			var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
			var stream = response.IsSuccessStatusCode ? await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false) : null;

			if (stream is null)
			{
				return artworkData;
			}

			using var memoryStream = new MemoryStream();
			await stream.CopyToAsync(memoryStream, cancellationToken).ConfigureAwait(false);
			var bytes = memoryStream.ToArray();
			return bytes;
		}
		catch
		{
			return artworkData;
		}
	}

	[MemberNotNull(nameof(connection))]
	void StartService()
	{
		var intent = new Intent(Android.App.Application.Context, typeof(MediaControlsService));
		connection = new BoundServiceConnection(this);
		connection.MediaControlsServiceTaskRemoved += HandleMediaControlsServiceTaskRemoved;

		Android.App.Application.Context.StartForegroundService(intent);
		Android.App.Application.Context.ApplicationContext?.BindService(intent, connection, Bind.AutoCreate);
	}

	void StopService(in BoundServiceConnection boundServiceConnection)
	{
		boundServiceConnection.MediaControlsServiceTaskRemoved -= HandleMediaControlsServiceTaskRemoved;

		var serviceIntent = new Intent(Platform.AppContext, typeof(MediaControlsService));
		Android.App.Application.Context.StopService(serviceIntent);
		Platform.AppContext.UnbindService(boundServiceConnection);
	}

	void HandleMediaControlsServiceTaskRemoved(object? sender, EventArgs e) => Player?.Stop();

	async Task<MediaItem.Builder?> SetPlayerData(MediaSource? mediaSource, CancellationToken cancellationToken = default)
	{
		if (mediaSource is null)
		{
			return null;
		}

		switch (mediaSource)
		{
			case UriMediaSource uriMediaSource:
				{
					var uri = uriMediaSource.Uri;
					if (!string.IsNullOrWhiteSpace(uri?.AbsoluteUri))
					{
						return await CreateMediaItem(uri.AbsoluteUri, cancellationToken).ConfigureAwait(false);
					}

					break;
				}
			case FileMediaSource fileMediaSource:
				{
					var filePath = fileMediaSource.Path;
					if (!string.IsNullOrWhiteSpace(filePath))
					{
						return await CreateMediaItem(filePath, cancellationToken).ConfigureAwait(false);
					}

					break;
				}
			case ResourceMediaSource resourceMediaSource:
				{
					var package = PlayerView?.Context?.PackageName ?? "";
					var path = resourceMediaSource.Path;
					if (!string.IsNullOrWhiteSpace(path))
					{
						var assetFilePath = $"asset://{package}{Path.PathSeparator}{path}";
						return await CreateMediaItem(assetFilePath, cancellationToken).ConfigureAwait(false);
					}

					break;
				}
			default:
				throw new NotSupportedException($"{mediaSource.GetType().FullName} is not yet supported for {nameof(MediaElement.Source)}");
		}

		return mediaItem;
	}

	MediaItem? CreateBasicMediaItem(MediaSource? mediaSource)
	{
		if (mediaSource is null)
		{
			return null;
		}

		switch (mediaSource)
		{
			case UriMediaSource uriMediaSource:
				{
					var uri = uriMediaSource.Uri;
					if (uri is not null)
					{
						return MediaItem.FromUri(Android.Net.Uri.Parse(uri.ToString()));
					}

					break;
				}
			case FileMediaSource fileMediaSource:
				{
					var filePath = fileMediaSource.Path;
					if (!string.IsNullOrWhiteSpace(filePath))
					{
						return MediaItem.FromUri(Android.Net.Uri.Parse(filePath));
					}

					break;
				}
			case ResourceMediaSource resourceMediaSource:
				{
					var package = PlayerView?.Context?.PackageName ?? "";
					var path = resourceMediaSource.Path;
					if (!string.IsNullOrWhiteSpace(path))
					{
						var assetFilePath = $"asset://{package}{Path.PathSeparator}{path}";
						return MediaItem.FromUri(Android.Net.Uri.Parse(assetFilePath));
					}

					break;
				}
			default:
				throw new NotSupportedException($"{mediaSource.GetType().FullName} is not yet supported for {nameof(MediaElement.Source)}");
		}

		return null;
	}

	async Task<MediaItem.Builder> CreateMediaItem(string url, CancellationToken cancellationToken = default)
	{
		MediaMetadata.Builder mediaMetaData = new();
		mediaMetaData.SetArtist(MediaElement.MetadataArtist);
		mediaMetaData.SetTitle(MediaElement.MetadataTitle);
		var data = await GetBytesFromMetadataArtworkUrl(MediaElement.MetadataArtworkUrl, cancellationToken).ConfigureAwait(true);
		mediaMetaData.SetArtworkData(data, (Java.Lang.Integer)MediaMetadata.PictureTypeFrontCover);

		mediaItem = new MediaItem.Builder();
		mediaItem.SetUri(url);
		mediaItem.SetMediaId(url);
		mediaItem.SetMediaMetadata(mediaMetaData.Build());

		return mediaItem;
	}

	MediaItem? lastPlayedMedia = null;
	/// <summary>
	/// Occurs when ExoPlayer transitions to another media item or starts repeating the same media item.
	/// </summary>
	/// <paramref name="mediaItem">The new media item.</paramref>
	/// <paramref name="reason">The reason why the transition occurred.</paramref>
	/// <remarks>
	/// This is part of the <see cref="IPlayerListener"/> implementation.
	/// While this method does not seem to have any references, it's invoked at runtime.
	/// </remarks>
	public void OnMediaItemTransition(MediaItem? mediaItem, int reason)
	{
		if (Player is null)
		{
			return;
		}

		if (reason == MediaItemTransitionReason.PlaylistChanged)
		{
			if (mediaItem is not null)
			{
				lastPlayedMedia = mediaItem;
			}
			return;
		}

		MainThread.BeginInvokeOnMainThread(() =>
		{
			if (MediaElement.Source is PlaylistMediaSource mediaSource)
			{
				if (mediaSource != null && mediaSource.Sources != null && mediaItem != null)
				{
					var oldIndex = lastPlayedMedia == null ? -1 : mediaItems.IndexOf(lastPlayedMedia);
					var newIndex = mediaItems.IndexOf(mediaItem);
					var arg = new MediaPlaylistIndexChangedEventArgs(oldIndex, newIndex);

					lastPlayedMedia = mediaItem;
					MediaElement?.PlaylistIndexChanged(arg);
				}
			}
		});
	}

	static class PlaybackState
	{
		public const int StateBuffering = 6;
		public const int StateConnecting = 8;
		public const int StateFailed = 7;
		public const int StateFastForwarding = 4;
		public const int StateNone = 0;
		public const int StatePaused = 2;
		public const int StatePlaying = 3;
		public const int StateRewinding = 5;
		public const int StateSkippingToNext = 10;
		public const int StateSkippingToPrevious = 9;
		public const int StateSkippingToQueueItem = 11;
		public const int StateStopped = 1;
		public const int StateError = 7;
	}

	static class MediaItemTransitionReason
	{
		public const int Repeat = 0;
		public const int Auto = 1;
		public const int Seek = 2;
		public const int PlaylistChanged = 3;
	}
}