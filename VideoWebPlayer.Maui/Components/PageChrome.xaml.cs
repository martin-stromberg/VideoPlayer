using Microsoft.Maui.Controls;
using Microsoft.Maui.Layouts;

namespace VideoWebPlayer.Maui.Components;

public class PageChrome : ContentView
{
	private readonly Label _titleLabel;
	private readonly ContentView _headerActionsHost;
	private readonly ContentView _bodyHost;
	private readonly ContentView _footerHost;

	public static readonly BindableProperty HeaderTitleProperty =
		BindableProperty.Create(nameof(HeaderTitle), typeof(string), typeof(PageChrome), string.Empty,
			propertyChanged: (b, _, n) => ((PageChrome)b).OnHeaderTitleChanged((string?)n));

	public static readonly BindableProperty HeaderActionsProperty =
		BindableProperty.Create(nameof(HeaderActions), typeof(View), typeof(PageChrome), default(View),
			propertyChanged: (b, _, n) => ((PageChrome)b).OnHeaderActionsChanged((View?)n));

	public static readonly BindableProperty BodyProperty =
		BindableProperty.Create(nameof(Body), typeof(View), typeof(PageChrome), default(View),
			propertyChanged: (b, _, n) => ((PageChrome)b).OnBodyChanged((View?)n));

	public static readonly BindableProperty FooterProperty =
		BindableProperty.Create(nameof(Footer), typeof(View), typeof(PageChrome), default(View),
			propertyChanged: (b, _, n) => ((PageChrome)b).OnFooterChanged((View?)n));

	public string HeaderTitle
	{
		get => (string)GetValue(HeaderTitleProperty);
		set => SetValue(HeaderTitleProperty, value);
	}

	public View? HeaderActions
	{
		get => (View?)GetValue(HeaderActionsProperty);
		set => SetValue(HeaderActionsProperty, value);
	}

	public View? Body
	{
		get => (View?)GetValue(BodyProperty);
		set => SetValue(BodyProperty, value);
	}

	public View? Footer
	{
		get => (View?)GetValue(FooterProperty);
		set => SetValue(FooterProperty, value);
	}

	public PageChrome()
	{
		_titleLabel = new Label
		{
			FontSize = 36,
			FontAttributes = FontAttributes.Bold,
			TextColor = Color.FromArgb("#FFDDAA"),
			Padding = new Thickness(20, 20, 20, 15),
			FontFamily = "OpenSansSemibold",
			HorizontalOptions = LayoutOptions.Start,
			VerticalOptions = LayoutOptions.Center,
			Shadow = new Shadow
			{
				Brush = Brush.Black,
				Offset = new Point(5, 5),
				Radius = 6
			}
		};

		_headerActionsHost = new ContentView
		{
			HorizontalOptions = LayoutOptions.End,
			VerticalOptions = LayoutOptions.Center,
			Margin = 20
		};

		_bodyHost = new ContentView();
		_footerHost = new ContentView();

		var root = new Grid { BackgroundColor = Color.FromArgb("#181820"), Padding = 0 };
		root.RowDefinitions.Add(new RowDefinition { Height = 100 });
		root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star });
		root.RowDefinitions.Add(new RowDefinition { Height = 47 });
		root.ColumnDefinitions.Add(new ColumnDefinition { Width = 100 });
		root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
		root.ColumnDefinitions.Add(new ColumnDefinition { Width = 100 });

		// Header background
		root.Add(new Image { Source = "background_header_left.png", Aspect = Aspect.Fill }, 0, 0);
		root.Add(new Image { Source = "background_header_middle.png", Aspect = Aspect.Fill }, 1, 0);
		root.Add(new Image { Source = "background_header_right.png", Aspect = Aspect.Fill }, 2, 0);

		var headerGrid = new Grid();
		headerGrid.Add(_titleLabel);
		headerGrid.Add(_headerActionsHost);
		root.Add(headerGrid, 1, 0);

		// Content background
		root.Add(new Image { Source = "background_content_left.png", Aspect = Aspect.Fill }, 0, 1);
		root.Add(new Image { Source = "background_content_middle.png", Aspect = Aspect.Fill }, 1, 1);
		root.Add(new Image { Source = "background_content_right.png", Aspect = Aspect.Fill }, 2, 1);
		root.Add(_bodyHost, 1, 1);

		// Footer background
		root.Add(new Image { Source = "background_footer_left.png", Aspect = Aspect.Fill }, 0, 2);
		root.Add(new Image { Source = "background_footer_middle.png", Aspect = Aspect.Fill }, 1, 2);
		root.Add(new Image { Source = "background_footer_right.png", Aspect = Aspect.Fill }, 2, 2);
		root.Add(_footerHost, 1, 2);

		Content = root;
	}

	private void OnHeaderTitleChanged(string? newTitle)
		=> _titleLabel.Text = newTitle ?? string.Empty;

	private void OnHeaderActionsChanged(View? view)
		=> _headerActionsHost.Content = view;

	private void OnBodyChanged(View? view)
		=> _bodyHost.Content = view;

	private void OnFooterChanged(View? view)
		=> _footerHost.Content = view;
}
