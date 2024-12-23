using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.OpenSsl;
using SecureChat.extended_controls;
using SecureChat.model;
using SecureChat.util;
using SecureChat.windows;

namespace SecureChat.panels;

public class ChatPanelController {
	public RsaKeyParameters ForeignPublicKey;

	public readonly RsaKeyParameters PersonalPublicKey;
	private readonly RsaKeyParameters _privateKey;

	private readonly Settings _settings = Settings.GetInstance();
	
	private readonly ChatPanel _context;

	public ChatPanelController(ChatPanel context) {
		_context = context;
		
		using (StreamReader reader = File.OpenText(Constants.PublicKeyFile)) {
			PemReader pemReader = new (reader);
			PersonalPublicKey = (RsaKeyParameters) pemReader.ReadObject();
		}
		
		using (StreamReader reader = File.OpenText(Constants.PrivateKeyFile)) {
			PemReader pemReader = new (reader);
			_privateKey = (RsaKeyParameters) ((AsymmetricCipherKeyPair) pemReader.ReadObject()).Private;
		}
	}

	public void AddListenersToContextMenu(MessageTextBlock messageTextBlock, IBrush originalBackground, ContextMenu contextMenu) {
		int start = 0, end = 0;

		MenuItem? copyMenuItem = null, deleteMenuItem = null;
		foreach (MenuItem? contextMenuItem in contextMenu.Items) {
			switch (contextMenuItem!.Name) {
				case "CopyMenuItem":
					copyMenuItem = contextMenuItem;
					break;
				case "DeleteMenuItem":
					deleteMenuItem = contextMenuItem;
					break;
			}
		}
		
		// For some reason this solves automatic unselecting of text, not only for copyMenuItem, but for all of them
		copyMenuItem!.PointerMoved += (_, _) => {
			messageTextBlock.TextBlock.SelectionStart = start;
			messageTextBlock.TextBlock.SelectionEnd = end;
		};

		messageTextBlock.TextBlock.ContextFlyout = null;
		messageTextBlock.TextBlock.PointerReleased += (_, args) => {
			if (args.GetCurrentPoint(_context).Properties.PointerUpdateKind != PointerUpdateKind.RightButtonReleased)
				return;

			start = messageTextBlock.TextBlock.SelectionStart;
			end = messageTextBlock.TextBlock.SelectionEnd;
			
			copyMenuItem.IsVisible = messageTextBlock.TextBlock.SelectedText != "";
			deleteMenuItem!.IsVisible = messageTextBlock.Sender == PersonalPublicKey.Modulus.ToString(16);
			contextMenu.Open((Control) args.Source!);
		};
		
		messageTextBlock.Border.PointerEntered += (_, _) => messageTextBlock.Border.Background = new SolidColorBrush(Color.Parse("#252525")); 
		messageTextBlock.Border.PointerExited += (_, _) => messageTextBlock.Border.Background = originalBackground;
		messageTextBlock.Border.PointerPressed += (_, args) => {
			if (args.GetCurrentPoint(_context).Properties.PointerUpdateKind != PointerUpdateKind.RightButtonPressed)
				return;

			copyMenuItem.IsVisible = false;
			deleteMenuItem!.IsVisible = messageTextBlock.Sender == PersonalPublicKey.Modulus.ToString(16);
			contextMenu.Open((Control) args.Source!);
		};
	}

	public bool Decrypt(Message message, bool isOwnMessage, out DecryptedMessage? decryptedMessage) {
		DecryptedMessage uncheckedMessage = Cryptography.Decrypt(message, _privateKey, isOwnMessage);
		if (Cryptography.Verify(uncheckedMessage.Body, message.Signature, PersonalPublicKey, ForeignPublicKey, isOwnMessage)) {
			decryptedMessage = uncheckedMessage;
			return true;
		}

		decryptedMessage = null;
		return false;
	}

	public DecryptedMessage[] GetPastMessages() { // TODO: Add signature to this request
		List<DecryptedMessage> res = [];
		
		string getVariables = $"requestingUserModulus={PersonalPublicKey.Modulus.ToString(16)}&requestingUserExponent={PersonalPublicKey.Exponent.ToString(16)}&requestedUserModulus={ForeignPublicKey.Modulus.ToString(16)}&requestedUserExponent={ForeignPublicKey.Exponent.ToString(16)}";
		JsonArray messages = JsonNode.Parse(Https.Get($"http://{_settings.IpAddress}:5000/messages?" + getVariables).Body)!.AsArray();
		foreach (JsonNode? messageNode in messages) {
			Message message = Message.Parse(messageNode!.AsObject());
			bool isOwnMessage = Equals(message.Sender, PersonalPublicKey);
			if (!Decrypt(message, isOwnMessage, out DecryptedMessage? decryptedMessage))
				continue; // Just don't add the message if it is not legitimate
			
			res.Add(new DecryptedMessage { Id = message.Id, Body = decryptedMessage!.Body, DateTime = DateTime.MinValue, Sender = message.Sender.Modulus.ToString(16)});
		}

		return res.ToArray();
	}
	
	public long SendMessage(string message) {
		Message encryptedMessage = Cryptography.Encrypt(message, PersonalPublicKey, ForeignPublicKey, _privateKey);
		JsonObject body = new () {
			["sender"] = new JsonObject {
				["modulus"] = PersonalPublicKey.Modulus.ToString(16),
				["exponent"] = PersonalPublicKey.Exponent.ToString(16)
			},
			["receiver"] = new JsonObject {
				["modulus"] = ForeignPublicKey.Modulus.ToString(16),
				["exponent"] = ForeignPublicKey.Exponent.ToString(16)
			},
			["text"] = encryptedMessage.Body,
			["senderEncryptedKey"] = encryptedMessage.SenderEncryptedKey,
			["receiverEncryptedKey"] = encryptedMessage.ReceiverEncryptedKey,
			["signature"] = encryptedMessage.Signature,
			["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
		};
		
		string bodyString = JsonSerializer.Serialize(body);
		string response = Https.Post($"http://{_settings.IpAddress}:5000/messages", bodyString, [new Https.Header {Name = "Signature", Value = Cryptography.Sign(bodyString, _privateKey)}]).Body;
		return long.Parse(response);
	}

	public void DeleteMessage(long id) {
		JsonObject body = new () {
			["id"] = id,
			["signature"] = Cryptography.Sign(id.ToString(), _privateKey)
		};
		_context.RemoveMessage(id);
		Https.Delete($"http://{_settings.IpAddress}:5000/messages", JsonSerializer.Serialize(body));
	}

	public void SetChatRead() {
		JsonObject body = new () {
			["receiver"] = new JsonObject {
				["modulus"] = PersonalPublicKey.Modulus.ToString(16),
				["exponent"] = PersonalPublicKey.Exponent.ToString(16)
			},
			["sender"] = new JsonObject {
				["modulus"] = ForeignPublicKey.Modulus.ToString(16),
				["exponent"] = ForeignPublicKey.Exponent.ToString(16)
			},
			["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
		};
		
		string bodyString = JsonSerializer.Serialize(body);
		Https.Post($"http://{_settings.IpAddress}:5000/makeChatRead", bodyString, [new Https.Header {Name = "Signature", Value = Cryptography.Sign(bodyString, _privateKey)}]);
	}

	public void OnCallButtonClicked(object? sender, EventArgs e) => StartCall();

	private void StartCall() {
		new CallPopupWindow(PersonalPublicKey, ForeignPublicKey).Show(MainWindow.Instance);
	}
}