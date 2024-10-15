namespace CommunityToolkit.Maui.Views;

/// <summary>
/// Represents a list of sources that can be played by <see cref="MediaElement"/>.
/// </summary>
public sealed class PlaylistMediaSource : MediaSource
{
	/// <summary>
	/// Backing store for the <see cref="Sources"/> property.
	/// </summary>
	public static readonly BindableProperty SourcesProperty =
		BindableProperty.Create(nameof(Sources), typeof(IList<MediaSource>), typeof(PlaylistMediaSource), propertyChanged: OnPlaylistSourcesChanged);

	/// <summary>
	/// Gets or sets the list of media sources.
	/// This is a bindable property.
	/// </summary>
	public IList<MediaSource>? Sources
	{
		get => (IList<MediaSource>?)GetValue(SourcesProperty);
		set => SetValue(SourcesProperty, value);
	}

	/// <summary>
	/// Gets or sets the starting index of the playlist.
	/// </summary>
	public int StartIndex;

	static void OnPlaylistSourcesChanged(BindableObject bindable, object oldValue, object newValue) =>
		((PlaylistMediaSource)bindable).OnSourceChanged();
}