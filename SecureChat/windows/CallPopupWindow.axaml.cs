using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Org.BouncyCastle.Crypto.Parameters;

namespace SecureChat.windows;

public partial class CallPopupWindow : PopupWindow {
	private CallPopupWindowController _controller = null!;

	public ComboBox InputSelectorComboBox = null!;
	public TextBlock TooltipTextBlock = null!;

	public readonly RsaKeyParameters PersonalKey, ForeignKey;
	
	public CallPopupWindow(RsaKeyParameters personalKey, RsaKeyParameters foreignKey) {
		PersonalKey = personalKey;
		ForeignKey = foreignKey;

		InitializeComponent();
	}

	private void InitializeComponent() {
		AvaloniaXamlLoader.Load(this);
		Slider slider = this.FindControl<Slider>("VolumeSlider")!;
		InputSelectorComboBox = this.FindControl<ComboBox>("InputSelector")!;
		TooltipTextBlock = this.FindControl<TextBlock>("TooltipText")!;
		slider.Value = 100;
		TooltipTextBlock.Text = (int) slider.Value + "%";
		slider.Styles.Add(new Style(x => x.OfType<Slider>().Descendant().OfType<Thumb>()) {
			Setters = {
				new Setter(MaxHeightProperty, 15D),
				new Setter(MaxWidthProperty, 15D),
			}
		});
		slider.Styles.Add(new Style(x => x.OfType<Slider>().Descendant().OfType<Track>()) {
			Setters = {
				new Setter(MaxHeightProperty, 3D)
			}
		});
		
		_controller = new CallPopupWindowController(this);

		slider.PointerEntered += _controller.OnMousePointerEnteredSlider;
		slider.PointerExited += _controller.OnMousePointerExitedSlider;
		slider.PointerMoved += _controller.OnMousePointerMovedInSlider;
	}

	private void InputSelector_OnSelectionChanged(object? sender, SelectionChangedEventArgs e) {
		if (_controller == null!)
			return;

		_controller.OnInputDeviceChanged(sender, e);
	}

	private void VolumeSlider_OnValueChanged(object? sender, RangeBaseValueChangedEventArgs e) {
		if (_controller == null!)
			return;
			
		_controller.OnVolumeSliderValueChanged(sender, e);
	}
}