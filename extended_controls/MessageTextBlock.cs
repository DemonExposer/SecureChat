using Avalonia.Controls;

namespace SecureChat.extended_controls;

public class MessageTextBlock {
	public long Id { get; init; }
	public string Sender { get; init; }
	public string Receiver { get; init; }
	public Border Border { get; init; }
	public SelectableTextBlock TextBlock { get; init; }
}