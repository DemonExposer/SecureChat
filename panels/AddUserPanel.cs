﻿using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using System.Collections.Generic;

namespace SecureChat.panels;

public class AddUserPanel : StackPanel {
	public delegate void DataCallback(RsaKeyParameters publicKey, string name);

	public void SetOnEnter(DataCallback callback) {
		using IEnumerator<ILogical> enumerator = this.GetLogicalDescendants().GetEnumerator();

		TextBox? modulusTextBox = null, exponentTextBox = null;
		while (enumerator.MoveNext()) {
			if (enumerator.Current.GetType() == typeof(TextBox)) {
				TextBox textBox = (TextBox) enumerator.Current;
				switch (textBox.Name) {
					case "ModulusTextBox":
						modulusTextBox = (TextBox) enumerator.Current;
						break;
					case "ExponentTextBox":
						exponentTextBox = (TextBox) enumerator.Current;
						break;
				}
			}
		}

		// TODO: add check to see whether TextBoxes are actually filled
		exponentTextBox!.KeyDown += (_, args) => {
			if (args.Key == Key.Enter) {
				callback(
					new RsaKeyParameters(
						false,
						new BigInteger(modulusTextBox!.Text, 16),
						new BigInteger(exponentTextBox.Text, 16)
					),
					modulusTextBox.Text!
				);
			}
		};
	}
}

