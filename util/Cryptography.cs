using System;
using System.Text;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Encodings;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Paddings;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Security;
using SecureChat.model;

namespace SecureChat.util;

public class Cryptography {
	public static string Sign(string text, RsaKeyParameters privateKey) {
		byte[] bytes = Encoding.UTF8.GetBytes(text);
		ISigner signer = new RsaDigestSigner(new Sha256Digest());
		signer.Init(true, privateKey);
		
		signer.BlockUpdate(bytes, 0, bytes.Length);
		byte[] signature = signer.GenerateSignature();

		return Convert.ToBase64String(signature);
	}

	public static bool Verify(string text, string signature, RsaKeyParameters? personalPublicKey, RsaKeyParameters? foreignPublicKey, bool isOwnMessage) {
		switch (isOwnMessage) {
			case true when personalPublicKey == null:
				throw new ArgumentNullException(nameof(personalPublicKey), "must not be null when verifying own messages");
			case false when foreignPublicKey == null:
				throw new ArgumentNullException(nameof(foreignPublicKey), "must not be null when verifying other's messages");
		}
		
		byte[] textBytes = Encoding.UTF8.GetBytes(text);
		byte[] signatureBytes = Convert.FromBase64String(signature);

		ISigner verifier = new RsaDigestSigner(new Sha256Digest());
		verifier.Init(false, isOwnMessage ? personalPublicKey : foreignPublicKey);
		
		verifier.BlockUpdate(textBytes, 0, textBytes.Length);

		return verifier.VerifySignature(signatureBytes);
	}

	public static Message Encrypt(string inputText, RsaKeyParameters personalPublicKey, RsaKeyParameters foreignPublicKey, RsaKeyParameters privateKey) {
		// Encrypt using AES
		CipherKeyGenerator aesKeyGen = new ();
		aesKeyGen.Init(new KeyGenerationParameters(new SecureRandom(), 256));
		byte[] aesKey = aesKeyGen.GenerateKey();

		AesEngine aesEngine = new ();
		PaddedBufferedBlockCipher cipher = new (new CbcBlockCipher(aesEngine), new Pkcs7Padding());
		cipher.Init(true, new KeyParameter(aesKey));
		
		byte[] plainBytes = Encoding.UTF8.GetBytes(inputText);
		byte[] cipherBytes = new byte[cipher.GetOutputSize(plainBytes.Length)];
		int length = cipher.ProcessBytes(plainBytes, 0, plainBytes.Length, cipherBytes, 0);
		length += cipher.DoFinal(cipherBytes, length);
		
		// Encrypt the AES key using RSA
		OaepEncoding rsaEngine = new (new RsaEngine());
		rsaEngine.Init(true, personalPublicKey);
		byte[] personalEncryptedKey = rsaEngine.ProcessBlock(aesKey, 0, aesKey.Length);
		
		rsaEngine.Init(true, foreignPublicKey);
		byte[] foreignEncryptedKey = rsaEngine.ProcessBlock(aesKey, 0, aesKey.Length);

		return new Message {
			Body = Convert.ToBase64String(cipherBytes),
			SenderEncryptedKey = Convert.ToBase64String(personalEncryptedKey),
			ReceiverEncryptedKey = Convert.ToBase64String(foreignEncryptedKey),
			Signature = Sign(inputText, privateKey),
			Receiver = foreignPublicKey,
			Sender = personalPublicKey
		};
	}

	public static DecryptedMessage Decrypt(Message message, RsaKeyParameters privateKey, bool isOwnMessage) {
		// Decrypt the AES key using RSA
		byte[] aesKeyEncrypted = Convert.FromBase64String(isOwnMessage ? message.SenderEncryptedKey : message.ReceiverEncryptedKey);
		OaepEncoding rsaEngine = new (new RsaEngine());
		rsaEngine.Init(false, privateKey);
		
		byte[] aesKey = rsaEngine.ProcessBlock(aesKeyEncrypted, 0, aesKeyEncrypted.Length);
		
		// Decrypt the message using AES
		AesEngine aesEngine = new ();
		PaddedBufferedBlockCipher cipher = new (new CbcBlockCipher(aesEngine), new Pkcs7Padding());
		cipher.Init(false, new KeyParameter(aesKey));

		byte[] encryptedBodyBytes = Convert.FromBase64String(message.Body);
		byte[] plainBytes = new byte[cipher.GetOutputSize(encryptedBodyBytes.Length)];
		int length = cipher.ProcessBytes(encryptedBodyBytes, 0, encryptedBodyBytes.Length, plainBytes, 0);
		length += cipher.DoFinal(plainBytes, length);

		string body = Encoding.UTF8.GetString(plainBytes, 0, length);
		return new DecryptedMessage { Id = message.Id, Body = body, Sender = message.Sender.Modulus.ToString(16), DateTime = DateTime.MinValue};
	}
}