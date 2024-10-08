namespace CommunityToolkit.Maui.Primitives;

/// <summary>
/// Represents the different reasons media can transition.
/// </summary>
sealed class AndroidPrimitives
    {
	public enum MediaItemTransitionReason {
		/// <summary>The media item has been repeated.</summary>
		MEDIA_ITEM_TRANSITION_REASON_REPEAT,

		/// <summary>Playback has automatically transitioned to the next media item.</summary>
		MEDIA_ITEM_TRANSITION_REASON_AUTO,

		/// <summary>A seek to another media item has occurred.</summary>
		MEDIA_ITEM_TRANSITION_REASON_SEEK,

		/// <summary>The current media item has changed because of a change in the playlist.
		/// This can either be if the media item previously being played has been removed, or when the playlist becomes non-empty after being empty.</summary>
		MEDIA_ITEM_TRANSITION_REASON_PLAYLIST_CHANGED
	};

}
