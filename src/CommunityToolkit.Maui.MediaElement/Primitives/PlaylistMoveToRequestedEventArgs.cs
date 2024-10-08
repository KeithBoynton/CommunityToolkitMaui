namespace CommunityToolkit.Maui.Core.Primitives;

/// <summary>
/// Represents event data for when a move operation is requested on a playlist.
/// </summary>
sealed class PlaylistMoveToRequestedEventArgs : EventArgs
{
	/// <summary>
	/// Initializes a new instance of the <see cref="PlaylistMoveToRequestedEventArgs"/> class.
	/// </summary>
	/// <param name="requestedIndex">The requested index to move to.</param>
	public PlaylistMoveToRequestedEventArgs(int requestedIndex)
	{
		RequestedIndex = requestedIndex;
	}

	/// <summary>
	/// Gets the requested index to move to.
	/// </summary>
	public int RequestedIndex { get; }
}