namespace CommunityToolkit.Maui.Core.Primitives;

/// <summary>
/// Represents event data for when a playlist index has changed.
/// </summary>
public sealed class MediaPlaylistIndexChangedEventArgs : EventArgs
{
	/// <summary>
	/// Initializes a new instance of the <see cref="MediaPlaylistIndexChangedEventArgs"/> class.
	/// </summary>
	/// <param name="oldIndex">The old index position associated to this event.</param>
	/// <param name="newIndex">The new index position associated to this event.</param>
	public MediaPlaylistIndexChangedEventArgs(int oldIndex, int newIndex)
	{
		OldIndex = oldIndex;
		NewIndex = newIndex;
	}

	/// <summary>
	/// Gets the position of the index before it changed.
	/// </summary>
	public int? OldIndex { get; }

	/// <summary>
	/// Gets the position of the index after it changed.
	/// </summary>
	public int? NewIndex { get; }
}
