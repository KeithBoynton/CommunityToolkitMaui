using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using FluentAssertions;
using Xunit;
using Font = Microsoft.Maui.Font;

namespace CommunityToolkit.Maui.UnitTests.Alerts;

public class SnackbarTests : BaseTest
{
	readonly ISnackbar snackbar = new Snackbar();

	public SnackbarTests()
	{
		Assert.IsType<IAlert>(snackbar, exactMatch: false);
	}

	[Fact]
	public void SnackbarDefaultValues()
	{
		Assert.Null(snackbar.Action);
		Assert.Equal(AlertDefaults.ActionButtonText, snackbar.ActionButtonText);
		Assert.Null(snackbar.Anchor);
		Assert.Equal(Snackbar.GetDefaultTimeSpan(), snackbar.Duration);
		Assert.Equal(string.Empty, snackbar.Text);

		Assert.Equal(Font.SystemFontOfSize(AlertDefaults.FontSize), snackbar.VisualOptions.ActionButtonFont);
		Assert.Equal(AlertDefaults.BackgroundColor, snackbar.VisualOptions.BackgroundColor);
		Assert.Equal(AlertDefaults.CharacterSpacing, snackbar.VisualOptions.CharacterSpacing);
		Assert.Equal(new CornerRadius(4, 4, 4, 4), snackbar.VisualOptions.CornerRadius);
		Assert.Equal(Font.SystemFontOfSize(AlertDefaults.FontSize), snackbar.VisualOptions.Font);
		Assert.Equal(AlertDefaults.TextColor, snackbar.VisualOptions.TextColor);
	}

	[Fact(Timeout = (int)TestDuration.Short)]
	public async Task SnackbarShow_CancellationTokenExpires()
	{
		var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1));

		// Ensure CancellationToken expires
		await Task.Delay(100, TestContext.Current.CancellationToken);

		await Assert.ThrowsAsync<OperationCanceledException>(() => snackbar.Show(cts.Token));
	}

	[Fact(Timeout = (int)TestDuration.Short)]
	public async Task SnackbarShow_CancellationTokenCanceled()
	{
		var cts = new CancellationTokenSource();

		await Assert.ThrowsAsync<OperationCanceledException>(() =>
		{
			cts.Cancel();
			return snackbar.Show(cts.Token);
		});
	}

	[Fact(Timeout = (int)TestDuration.Short)]
	public async Task SnackbarDismiss_CancellationTokenExpires()
	{
		var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1));

		// Ensure CancellationToken expires
		await Task.Delay(100, TestContext.Current.CancellationToken);

		await Assert.ThrowsAsync<OperationCanceledException>(() => snackbar.Dismiss(cts.Token));
	}

	[Fact(Timeout = (int)TestDuration.Short)]
	public async Task SnackbarDismiss_CancellationTokenCanceled()
	{
		var cts = new CancellationTokenSource();

		await Assert.ThrowsAsync<OperationCanceledException>(() =>
		{
			cts.Cancel();
			return snackbar.Dismiss(cts.Token);
		});
	}

	[Fact(Timeout = (int)TestDuration.Short)]
	public async Task SnackbarShow_IsShownTrue()
	{
		await snackbar.Show(TestContext.Current.CancellationToken);
		Assert.True(Snackbar.IsShown);
	}

	[Fact(Timeout = (int)TestDuration.Short)]
	public async Task SnackbarDismissed_IsShownFalse()
	{
		await snackbar.Dismiss(TestContext.Current.CancellationToken);
		Assert.False(Snackbar.IsShown);
	}

	[Fact(Timeout = (int)TestDuration.Short)]
	public async Task SnackbarShow_ShownEventRaised()
	{
		var receivedEvents = new List<EventArgs>();
		Snackbar.Shown += (sender, e) =>
		{
			receivedEvents.Add(e);
		};
		await snackbar.Show(TestContext.Current.CancellationToken);
		Assert.Single(receivedEvents);
	}

	[Fact(Timeout = (int)TestDuration.Short)]
	public async Task SnackbarDismiss_DismissedEventRaised()
	{
		var receivedEvents = new List<EventArgs>();
		Snackbar.Dismissed += (sender, e) =>
		{
			receivedEvents.Add(e);
		};
		await snackbar.Dismiss(TestContext.Current.CancellationToken);
		Assert.Single(receivedEvents);
	}

	[Fact(Timeout = (int)TestDuration.Short)]
	public async Task VisualElement_DisplaySnackbar_ShownEventReceived()
	{
		var receivedEvents = new List<EventArgs>();
		Snackbar.Shown += (_, e) =>
		{
			receivedEvents.Add(e);
		};
		var button = new Button();
		await button.DisplaySnackbar("message", token: TestContext.Current.CancellationToken);
		Assert.Single(receivedEvents);
	}

	[Fact]
	public void SnackbarMake_NewSnackbarCreatedWithValidProperties()
	{
		var action = () => { };
		var anchor = new Button();
		var expectedSnackbar = new Snackbar
		{
			Anchor = anchor,
			Action = action,
			Duration = TimeSpan.MaxValue,
			Text = "Test",
			ActionButtonText = "Ok",
			VisualOptions = new SnackbarOptions
			{
				Font = Font.Default,
				BackgroundColor = Colors.Red,
				CharacterSpacing = 10,
				CornerRadius = new CornerRadius(1, 2, 3, 4),
				TextColor = Colors.RosyBrown,
				ActionButtonFont = Font.SystemFontOfSize(5),
				ActionButtonTextColor = Colors.Aqua
			}
		};

		var currentSnackbar = Snackbar.Make(
			"Test",
			action,
			"Ok",
			TimeSpan.MaxValue,
			new SnackbarOptions
			{
				Font = Font.Default,
				BackgroundColor = Colors.Red,
				CharacterSpacing = 10,
				CornerRadius = new CornerRadius(1, 2, 3, 4),
				TextColor = Colors.RosyBrown,
				ActionButtonFont = Font.SystemFontOfSize(5),
				ActionButtonTextColor = Colors.Aqua
			},
			anchor);

		currentSnackbar.Should().BeEquivalentTo(expectedSnackbar);
	}

	[Fact(Timeout = (int)TestDuration.Short)]
	public async Task SnackbarShow_CancellationTokenCancelled_ReceiveException()
	{
		using var cancellationTokenSource = new CancellationTokenSource();

		cancellationTokenSource.Cancel();

		await snackbar.Invoking(x => x.Show(cancellationTokenSource.Token)).Should().ThrowExactlyAsync<OperationCanceledException>();
	}

	[Fact(Timeout = (int)TestDuration.Short)]
	public async Task SnackbarDismiss_CancellationTokenCancelled_ReceiveException()
	{
		using var cancellationTokenSource = new CancellationTokenSource();

		cancellationTokenSource.Cancel();

		await snackbar.Invoking(x => x.Dismiss(cancellationTokenSource.Token)).Should().ThrowExactlyAsync<OperationCanceledException>();
	}

	[Fact(Timeout = (int)TestDuration.Short)]
	public async Task SnackbarShow_CancellationTokenNotCancelled_NotReceiveException()
	{
		using var cancellationTokenSource = new CancellationTokenSource();
		await snackbar.Invoking(x => x.Show(cancellationTokenSource.Token)).Should().NotThrowAsync<OperationCanceledException>();
	}

	[Fact(Timeout = (int)TestDuration.Short)]
	public async Task SnackbarDismiss_CancellationTokenNotCancelled_NotReceiveException()
	{
		using var cancellationTokenSource = new CancellationTokenSource();
		await snackbar.Invoking(x => x.Dismiss(cancellationTokenSource.Token)).Should().NotThrowAsync<OperationCanceledException>();
	}

	[Fact(Timeout = (int)TestDuration.Short)]
	public async Task SnackbarShow_CancellationTokenNone_NotReceiveException()
	{
		await snackbar.Invoking(x => x.Show(TestContext.Current.CancellationToken)).Should().NotThrowAsync<OperationCanceledException>();
	}

	[Fact(Timeout = (int)TestDuration.Short)]
	public async Task SnackbarDismiss_CancellationTokenNone_NotReceiveException()
	{
		await snackbar.Invoking(x => x.Dismiss(TestContext.Current.CancellationToken)).Should().NotThrowAsync<OperationCanceledException>();
	}

	[Fact]
	public async Task SnackbarNullValuesThrowArgumentNullException()
	{
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
		Assert.Throws<ArgumentNullException>(() => new Snackbar
		{
			Text = null
		});
		Assert.Throws<ArgumentNullException>(() => new Snackbar
		{
			ActionButtonText = null
		});
		Assert.Throws<ArgumentNullException>(() => Snackbar.Make(null));
		Assert.Throws<ArgumentNullException>(() => Snackbar.Make(string.Empty, actionButtonText: null));
		await Assert.ThrowsAsync<ArgumentNullException>(() => new Button().DisplaySnackbar(null, token: TestContext.Current.CancellationToken));
		await Assert.ThrowsAsync<ArgumentNullException>(() => new Button().DisplaySnackbar(string.Empty, actionButtonText: null, token: TestContext.Current.CancellationToken));
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
	}
}