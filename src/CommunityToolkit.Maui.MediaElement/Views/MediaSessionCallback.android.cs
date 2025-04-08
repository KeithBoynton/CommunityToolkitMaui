using Android.Runtime;
using AndroidX.Media3.Session;
using Java.Interop;

namespace CommunityToolkit.Maui.Core.Views;

class MediaSessionCallback : Java.Lang.Object, MediaSession.ICallback
{
	public MediaSessionCallback()
	{
		Console.WriteLine("Testing");
	}
	global::AndroidX.Media3.Session.MediaSession.ConnectionResult? OnConnect(global::AndroidX.Media3.Session.MediaSession? session, global::AndroidX.Media3.Session.MediaSession.ControllerInfo? controller)
	{
		return null;
	}

	global::Google.Common.Util.Concurrent.IListenableFuture? OnCustomCommand(global::AndroidX.Media3.Session.MediaSession? session, global::AndroidX.Media3.Session.MediaSession.ControllerInfo? controller, global::AndroidX.Media3.Session.SessionCommand? customCommand, global::Android.OS.Bundle? args)
	{
		return null;
	}

	void OnDisconnected(global::AndroidX.Media3.Session.MediaSession? session, global::AndroidX.Media3.Session.MediaSession.ControllerInfo? controller)
	{

	}

	bool OnMediaButtonEvent(global::AndroidX.Media3.Session.MediaSession? session, global::AndroidX.Media3.Session.MediaSession.ControllerInfo? controllerInfo, global::Android.Content.Intent? intent)
	{
		return false;
	}

	void OnPostConnect(global::AndroidX.Media3.Session.MediaSession? session, global::AndroidX.Media3.Session.MediaSession.ControllerInfo? controller)
	{

	}

	global::Google.Common.Util.Concurrent.IListenableFuture? OnSetMediaItems(global::AndroidX.Media3.Session.MediaSession? mediaSession, global::AndroidX.Media3.Session.MediaSession.ControllerInfo? controller, global::System.Collections.Generic.IList<global::AndroidX.Media3.Common.MediaItem>? mediaItems, int startIndex, long startPositionMs)
	{
		return null;
	}
}
