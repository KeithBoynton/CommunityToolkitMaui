using CommunityToolkit.Maui.Views;

namespace CommunityToolkit.Maui.Core.Primitives;

/// <summary>
/// Represents event data for when an add to playlist operation is requested on a playlist.
/// </summary>
sealed class AddMediaToPlaylistRequestedEventArgs : EventArgs
{
	/// <summary>
	/// Initializes a new instance of the <see cref="AddMediaToPlaylistRequestedEventArgs"/> class.
	/// </summary>
	/// <param name="media">The requested media to add.</param>
	/// <param name="index">The requested index position to add at.</param>
	public AddMediaToPlaylistRequestedEventArgs(MediaSource media, int? index)
	{
		RequestedMedia = media;
		RequestedIndex = index;
	}

	/// <summary>
	/// Gets the requested media to add.
	/// </summary>
	public MediaSource RequestedMedia { get; }

	/// <summary>
	/// Gets the requested index position to add at.
	/// </summary>
	public int? RequestedIndex { get; }
}