using Avalonia;
using Avalonia.Controls;

namespace SecureChat.windows;

public partial class PopupWindow : Window {
	public PopupWindow(string message) {
		InitializeComponent();

		// This because WindowStartupLocation does not work on Linux
		PixelRect screenBounds = Screens.Primary!.Bounds;
		int x = (int) (screenBounds.TopLeft.X + (screenBounds.Width - Width) / 2);
		int y = (int) (screenBounds.TopLeft.Y + (screenBounds.Height - Height) / 2);
		Position = new PixelPoint(x, y);
		
		this.FindControl<TextBlock>("DisplayText")!.Text = message;
	}
}