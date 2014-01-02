using System;
using System.Runtime.Serialization.Formatters.Binary;
using System.Windows.Forms;
using Kexplorer.res;
using Kexplorer.scripting;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using C1.C1Zip;

namespace Kexplorer.scripts
{

	#region Inner crypto classes.
	#region Delegates ---------------------------------------------------------------

	/// <summary>
	/// Liefert einen StreamWriter, der den übergebenen Stream kopmrimiert.
	/// </summary>
	public delegate Stream CompressStreamWriterDelegate(Stream uncompressedStream);

	/// <summary>
	/// Liefert einen StreamReader, der den übergebenen Stream dekopmrimiert.
	/// </summary>
	public delegate Stream CompressStreamReaderDelegate(Stream compressedStream);

	#endregion

	#region IOlympCryptography
	/// <summary>
	/// Definiert das Interface für die Klasse OlympCryptography.
	/// (für eine spätere Verwendung der Object Factory)
	/// </summary>
	public interface IOlympCryptography
	{
		#region Properties --------------------------------------------------------------

		#endregion

		#region Methoden ----------------------------------------------------------------

		/// <summary>
		/// Encrypts the plaintext. (without signature)
		/// </summary>
		/// <param name="plaintext">The data to be encrypted. Must not be <c>null</c> (if so,
		/// an <see cref="ArgumentNullException"/> is thrown).</param>
		/// <returns>The encrypted plaintext (-> ciphertext).</returns>
		string Encrypt(string plaintext);

		// -------------------------------------------------------------------------------

		/// <summary>
		/// Encrypts the plaintext. (without signature)
		/// </summary>
		/// <param name="plaintext">The plaintext to be encrypted.</param>
		/// <returns>The encrypted plaintext (-> ciphertext).</returns>
		byte[] Encrypt(byte[] plaintext);

		// -------------------------------------------------------------------------------

		/// <summary>
		/// Encrypts and compress the plaintext. (with signature)
		/// </summary>
		/// <param name="plaintext">The plaintext to be encrypted.</param>
		/// <param name="compressStreamWriter">A StreamWriter for the compressing the stream</param>
		/// <returns>The encrypted plaintext (-> ciphertext).</returns>
		byte[] Encrypt(byte[] plaintext, CompressStreamWriterDelegate compressStreamWriter);

		// -------------------------------------------------------------------------------

		/// <summary>
		/// Decrypt the ciphertext. (without signature)
		/// </summary>
		/// <param name="ciphertext">The data to be decrypted.
		/// Must not be <c>null</c> (if so, an <see cref="ArgumentNullException"/> is thrown).</param>
		/// <returns>The decrypted ciphertext (-> plaintext).
		/// When decryption is not possible then returned the ciphertext</returns>
		string Decrypt(string ciphertext);

		// -------------------------------------------------------------------------------

		/// <summary>
		/// Decrypt the ciphertext. (without signature)
		/// </summary>
		/// <param name="ciphertext">The ciphertext to be decrypted.</param>
		/// <returns>The encrypted plaintext (-> ciphertext).</returns>
		byte[] Decrypt(byte[] ciphertext);

		// -------------------------------------------------------------------------------

		/// <summary>
		/// Decrypt and uncompress the ciphertext. (with signature)
		/// </summary>
		/// <param name="ciphertext">The ciphertext to be decrypted.</param>
		/// <param name="compressStreamReader">A StreamReader for the uncompressing the stream</param>
		/// <returns>The encrypted plaintext (-> ciphertext).</returns>
		byte[] Decrypt(byte[] ciphertext, CompressStreamReaderDelegate compressStreamReader);

		#endregion
	}


	#endregion

	#region OlympCryptograpgy
	/// <summary>
	/// OlympCryptography, a class for encrypting and decrypting data.
	/// </summary>
	public sealed class OlympCryptography : IOlympCryptography
	{
		#region Konstanten ---------------------------------------------------------------

		/// <summary>The name of the Hashcode algorithm used.</summary>
		private const string SHA1 = "SHA1";

		/// <summary>The block size used for converting a stream into a byte array.</summary>
		private const int BLOCK_SIZE = 1024;

		#endregion

		#region Membervariablen ----------------------------------------------------------

		/// <summary>The secret key used by symmetric encryption/decryption.</summary>
		private byte[] key = 
			{0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x10, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16};
		//			{0x47, 0x2B, 0x44, 0x07, 0x4F, 0x4C, 0x59, 0x4D, 0x50, 0x07, 0x44, 0x49, 0x4A, 0x07, 0x4D, 0x4B};

		#endregion

		#region Konstruktoren ------------------------------------------------------------

		/// <summary>
		/// OlympCryptography constructor.
		/// </summary>
		public OlympCryptography()
		{
		}

		#endregion

		#region properties --------------------------------------------------------------

		#endregion

		#region Methoden -----------------------------------------------------------------

		/// <summary>
		/// Encrypts the plaintext. (without signature)
		/// </summary>
		/// <param name="plaintext">The data to be encrypted. Must not be <c>null</c> (if so,
		/// an <see cref="ArgumentNullException"/> is thrown).</param>
		/// <returns>The encrypted plaintext (-> ciphertext).</returns>
		public string Encrypt(string plaintext)
		{
			if (plaintext == null)
			{
				throw new ArgumentNullException("plaintext");
			}

			string ret = null;

			try
			{
				// Convert the string into a byte[].
				byte[] plainBytes = AnsiStringToByteArray(plaintext);

				// Encrypts the plaintext.
				byte[] encryptedBytes = this.SimpleEncrypt(plainBytes);

				// Convert the byte[] into a string.
				ret = ByteArrayToAnsiString(encryptedBytes);
			}
			catch (Exception ex)
			{
				throw new Exception("Error within OlympCryptography.EncryptText, see inner exception.", ex);
			}

			return ret;
		}

		// -------------------------------------------------------------------------------

		/// <summary>
		/// Encrypts the plaintext. (without signature)
		/// </summary>
		/// <param name="plaintext">The plaintext to be encrypted.</param>
		/// <returns>The encrypted plaintext (-> ciphertext).</returns>
		public byte[] Encrypt(byte[] plaintext)
		{
			byte[] ret;

			try
			{
				ret = SimpleEncrypt(plaintext);
			}
			catch (Exception ex)
			{
				throw new Exception("Error within OlympCryptography.Encrypt, see inner exception.", ex);
			}

			return ret;
		}

		// -------------------------------------------------------------------------------

		/// <summary>
		/// Encrypts and compress the plaintext. (with signature)
		/// </summary>
		/// <param name="plaintext">The plaintext to be encrypted.</param>
		/// <param name="compressStreamWriter">A StreamWriter for the compressing the stream</param>
		/// <returns>The encrypted plaintext (-> ciphertext).</returns>
		public byte[] Encrypt(byte[] plaintext, CompressStreamWriterDelegate compressStreamWriter)
		{
			byte[] ret;

			try
			{
				ret = SignedEncrypt(plaintext, compressStreamWriter);
			}
			catch (Exception ex)
			{
				throw new Exception("Error within OlympCryptography.Encrypt, see inner exception.", ex);
			}

			return ret;
		}

		// -------------------------------------------------------------------------------

		/// <summary>
		/// Decrypt the ciphertext. (without signature)
		/// </summary>
		/// <param name="ciphertext">The data to be decrypted.
		/// Must not be <c>null</c> (if so, an <see cref="ArgumentNullException"/> is thrown).</param>
		/// <returns>The decrypted ciphertext (-> plaintext).
		/// When decryption is not possible then returned the ciphertext</returns>
		public string Decrypt(string ciphertext)
		{
			if (ciphertext == null)
			{
				throw new ArgumentNullException("ciphertext");
			}

			string ret = ciphertext;

			try
			{
				// Convert the string into a byte[].
				byte[] cipherBytes = AnsiStringToByteArray(ciphertext);

				// Decrypt the ciphertext.
				byte[] decryptedBytes = this.SimpleDecrypt(cipherBytes);

				// Convert the byte[] into a string.
				ret = ByteArrayToAnsiString(decryptedBytes);
			}
			catch (Exception ex)
			{
				throw new Exception("Error within OlympCryptography.Decrypt, see inner exception.", ex);
			}

			return ret;
		}

		// -------------------------------------------------------------------------------

		/// <summary>
		/// Decrypt the ciphertext. (without signature)
		/// </summary>
		/// <param name="ciphertext">The ciphertext to be decrypted.</param>
		/// <returns>The encrypted plaintext (-> ciphertext).</returns>
		public byte[] Decrypt(byte[] ciphertext)
		{
			byte[] ret;

			try
			{
				ret = SimpleDecrypt(ciphertext);
			}
			catch (Exception ex)
			{
				throw new Exception("Error within OlympCryptography.Decrypt, see inner exception.", ex);
			}

			return ret;
		}

		// -------------------------------------------------------------------------------

		/// <summary>
		/// Decrypt and uncompress the ciphertext. (with signature)
		/// </summary>
		/// <param name="ciphertext">The ciphertext to be decrypted.</param>
		/// <param name="compressStreamReader">A StreamReader for the uncompressing the stream</param>
		/// <returns>The encrypted plaintext (-> ciphertext).</returns>
		public byte[] Decrypt(byte[] ciphertext, CompressStreamReaderDelegate compressStreamReader)
		{
			byte[] ret;

			try
			{
				ret = SignedDecrypt(ciphertext, compressStreamReader);
			}
			catch (Exception ex)
			{
				throw new Exception("Error within OlympCryptography.Decrypt, see inner exception.", ex);
			}

			return ret;
		}

		// -------------------------------------------------------------------------------

		/// <summary>
		/// SimpleEncrypts the plaintext.
		/// </summary>
		/// <param name="plaintext">The plaintext to be encrypted.</param>
		/// <returns>The encrypted plaintext (-> ciphertext).</returns>
		private byte[] SimpleEncrypt(byte[] plaintext)
		{
			byte[] ciphertext = null;

			MemoryStream memoryStream = new MemoryStream();
			byte[] IV = new byte[16];

			//Creates the default implementation, which is RijndaelManaged.         
			SymmetricAlgorithm rijn = SymmetricAlgorithm.Create();

			//RNGCryptoServiceProvider is an implementation of a random number generator.
			RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
			// The array is now filled with cryptographically strong random bytes, and none are zero.
			rng.GetNonZeroBytes(IV);

			// creates a symmetric encryptor object with the specified Key and initialization vector (IV).
			ICryptoTransform encryptor = rijn.CreateEncryptor(this.key, IV);
				
			// write the unencrypted initialization vector
			memoryStream.Write(IV, 0, IV.Length);

			// prepare the Crypto Stream
			CryptoStream cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write);

			// write plaintext into compressed and encrypted stream

			cryptoStream.Write(plaintext, 0, plaintext.Length);
			cryptoStream.Close();
			cryptoStream.Clear();

			// Umwandlung in einen Base64 String, damit die Daten auch serialisiert werden können (.xml)
			byte[] binaryData = memoryStream.ToArray();
			string base64String = Convert.ToBase64String(binaryData);
			ciphertext = AnsiStringToByteArray(base64String);

			// Speicher frei geben
			memoryStream.Close();
			encryptor.Dispose();
			rijn.Clear();

			return ciphertext;
		}

		// -------------------------------------------------------------------------------

		/// <summary>
		/// SimpleDecrypt the ciphertext.
		/// </summary>
		/// <param name="ciphertext">The ciphertext to be decrypted.</param>
		/// <returns>The decrypted ciphertext (-> plaintext).</returns>
		private byte[] SimpleDecrypt(byte[] ciphertext)
		{
			byte[] plaintext = null;

			string base64String = ByteArrayToAnsiString(ciphertext);
			byte[] binaryData = Convert.FromBase64String(base64String);

			MemoryStream memoryStream = new MemoryStream(binaryData, 16, binaryData.Length - 16);
			byte[] IV = new byte[16];

			// get the initialization vector
			for(int i=0; i<16; i++)
			{
				IV[i] = binaryData[i];
			}

			//Creates the default implementation, which is RijndaelManaged.         
			SymmetricAlgorithm rijn = SymmetricAlgorithm.Create();
			// creates a symmetric decryptor object with the specified Key and initialization vector (IV).
			ICryptoTransform decryptor = rijn.CreateDecryptor(this.key, IV);

			// prepare the Crypto Stream
			CryptoStream encryptedData = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read);

			// decrypt ciphertext
			MemoryStream decryptedData = this.GetBytes(encryptedData);
			decryptedData.Position = 0;

			plaintext = decryptedData.ToArray();

			// Speicher frei geben
			memoryStream.Close();
			decryptedData.Close();
			encryptedData.Close();
			decryptor.Dispose();
			rijn.Clear();

			return plaintext;
		}

		// -------------------------------------------------------------------------------

		/// <summary>
		/// SignedEncrypts the plaintext.
		/// </summary>
		/// <param name="plaintext">The plaintext to be encrypted.</param>
		/// <param name="compressStreamWriter">A StreamWriter for the compressing the stream</param>
		/// <returns>The encrypted plaintext (-> ciphertext).</returns>
		private byte[] SignedEncrypt(byte[] plaintext, CompressStreamWriterDelegate compressStreamWriter)
		{
			byte[] ciphertext = null;

			MemoryStream memoryStream = new MemoryStream();
			byte[] IV = new byte[16];

			//Creates the default implementation, which is RijndaelManaged.         
			SymmetricAlgorithm rijn = SymmetricAlgorithm.Create();

			//RNGCryptoServiceProvider is an implementation of a random number generator.
			RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
			// The array is now filled with cryptographically strong random bytes, and none are zero.
			rng.GetNonZeroBytes(IV);

			// creates a symmetric encryptor object with the specified Key and initialization vector (IV).
			ICryptoTransform encryptor = rijn.CreateEncryptor(this.key, IV);
				
			// write the unencrypted initialization vector
			BinaryFormatter formatter = new BinaryFormatter();
			formatter.Serialize(memoryStream, IV);

			// write the digital signature and the DSA parameters
			SHA1 sha1Provider = new SHA1CryptoServiceProvider();
			DSACryptoServiceProvider dsaProvider = new DSACryptoServiceProvider();
			byte[] hashbytes = sha1Provider.ComputeHash(plaintext);
			byte[] signature = dsaProvider.SignHash(hashbytes, CryptoConfig.MapNameToOID(SHA1));	
			formatter.Serialize(memoryStream, signature);

			// very important !!!!
			// only the public key is serialized -> false as argument to ExportParameters.
			formatter.Serialize(memoryStream, dsaProvider.ExportParameters(false));

			if (compressStreamWriter != null)
			{
				// write plaintext into compressed and encrypted stream
				MemoryStream compressedStream = new MemoryStream();
				using (Stream sw = compressStreamWriter(compressedStream))
				{
					sw.Write(plaintext, 0, plaintext.Length);
					sw.Flush();
					sw.Close();
				}

				using (CryptoStream cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write))
				{
					cryptoStream.Write(compressedStream.ToArray(), 0, compressedStream.ToArray().Length);
					cryptoStream.Flush();
					cryptoStream.Close();
				}
			}
			else
			{
				// write plaintext into encrypted stream
				using (CryptoStream cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write))
				{
					cryptoStream.Write(plaintext, 0, plaintext.Length);
					cryptoStream.Flush();
					cryptoStream.Close();
				}
			}

			ciphertext = memoryStream.ToArray();

			return ciphertext;
		}

		// -------------------------------------------------------------------------------

		/// <summary>
		/// SignedDecrypt the ciphertext.
		/// </summary>
		/// <param name="ciphertext">The ciphertext to be decrypted.</param>
		/// <param name="compressStreamReader">A StreamReader for the uncompressing the stream</param>
		/// <returns>The decrypted ciphertext (-> plaintext).</returns>
		private byte[] SignedDecrypt(byte[] ciphertext, CompressStreamReaderDelegate compressStreamReader)
		{
			byte[] plaintext = null;

			MemoryStream memoryStream = new MemoryStream(ciphertext);

			// get the initialization vector
			BinaryFormatter formatter = new BinaryFormatter();
			byte[] IV = formatter.Deserialize(memoryStream) as byte[];

			// get signature and DSA parameters
			byte[] signature = formatter.Deserialize(memoryStream) as byte[];
			DSAParameters dsaParameters = (DSAParameters) formatter.Deserialize(memoryStream);
			DSACryptoServiceProvider dsaVerifier = new DSACryptoServiceProvider();
			dsaVerifier.ImportParameters(dsaParameters);

			//Creates the default implementation, which is RijndaelManaged.         
			SymmetricAlgorithm rijn = SymmetricAlgorithm.Create();
			// creates a symmetric decryptor object with the specified Key and initialization vector (IV).
			ICryptoTransform decryptor = rijn.CreateDecryptor(this.key, IV);

			// prepare the Crypto Stream
			CryptoStream encryptedData = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read);

			MemoryStream plainData = null;

			if (compressStreamReader != null)
			{
				// decrypt ciphertext
				MemoryStream decryptedData = this.GetBytes(encryptedData);
				decryptedData.Position = 0;
					
				// decompress ciphertext
				using (Stream sr = compressStreamReader(decryptedData))
				{
					plainData = this.GetBytes(sr);
					sr.Close();
				}
				plainData.Position = 0;
			}
			else
			{
				// decrypt ciphertext
				plainData = this.GetBytes(encryptedData);
				plainData.Position = 0;
			}

			// Check Digital signature
			SHA1 sha1Provider = new SHA1CryptoServiceProvider();
			byte[] hashbytes = sha1Provider.ComputeHash(plainData);
			if(!dsaVerifier.VerifyHash(hashbytes, CryptoConfig.MapNameToOID(SHA1), signature))
			{
				throw new Exception("OlympCryptography.SignedDecrypt: Invalid digital signature - data manipulated!");
			}

			plaintext = plainData.ToArray();

			return plaintext;
		}

		// -------------------------------------------------------------------------------

		/// <summary>
		/// Converts the compression reader into a memory stream (byte array).
		/// </summary>
		/// <param name="stream">The stream to read from.</param>
		/// <returns>The required byte array.</returns>
		private MemoryStream GetBytes(Stream stream)
		{
			MemoryStream memoryStream = new MemoryStream(BLOCK_SIZE);

			int bytesRead = 0;
			byte[] buffer = new byte[BLOCK_SIZE];

			do
			{
				bytesRead = stream.Read(buffer, 0, BLOCK_SIZE);
				memoryStream.Write(buffer, 0, bytesRead);
			}
			while (bytesRead == BLOCK_SIZE);

			return memoryStream;
		}

		// -------------------------------------------------------------------------------

		/// <summary>
		/// Konvertiert einen Ansi String in ein Byte Array.
		/// </summary>
		/// <param name="text">Der zu konvertierende Ansi String</param>
		/// <returns>Das Byte Array.</returns>
		private byte[] AnsiStringToByteArray(string text)
		{
			Encoding encodingAnsi = Encoding.Default;
			byte[] result = encodingAnsi.GetBytes(text);

			return result;
		}

		// -------------------------------------------------------------------------------

		/// <summary>
		/// Konvertiert eine Byte Array in einen Ansi String
		/// </summary>
		/// <param name="byteArray">Das Byte Array</param>
		/// <returns>Der Ansi String</returns>
		private string ByteArrayToAnsiString(byte[] byteArray)
		{
			Encoding encodingAnsi = Encoding.Default;
			string result = encodingAnsi.GetString(byteArray);

			return result;
		}

		#endregion

	}

	#endregion



	#endregion

	/// <summary>
	/// Summary description for AccountRepositoryGuess.
	/// </summary>
	public class AccountRepositoryGuess : BaseFileScript
	{
		public AccountRepositoryGuess()
		{
			this.Description = "PW Help on a user in an AccountRepo";

			this.Validator = new FileValidator( this.ValidateIsAccountRepo );

			this.LongName = "PW Help on a user in an AccountRepo";

			this.Active = true;

		}

		public override void Run(KExplorerNode folder, FileInfo[] files)
		{
			while ( true )
			{
				string[] checkName = QuickDialog2.DoQuickDialog(
					"AccountRepo Check", "UserId","", "Password (optional)", "");


				if ( checkName == null )
				{
					return;
				}


				string filePath;
				byte[] ciphertext = null;


				try
				{
					filePath = files[0].FullName;

					FileStream fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
					using(BinaryReader reader = new BinaryReader(fileStream))
					{
						ciphertext = reader.ReadBytes((int)fileStream.Length);
					}
				
					byte[] plaintext = null;

					IOlympCryptography olympCryptography = new OlympCryptography();
					plaintext = olympCryptography.Decrypt(ciphertext, new CompressStreamReaderDelegate(CompressStreamReader));
			

					string reallyPlain = System.Text.UTF8Encoding.UTF8.GetString( plaintext );


					XmlDocument doc = new XmlDocument();


					// Seems sometimes in the encoding, we get a garbage character at the beginning.
					if ( reallyPlain.StartsWith("<"))
					{
						doc.LoadXml( reallyPlain );
					} 
					else
					{
						doc.LoadXml( reallyPlain.Substring(1) );
					}



					// If user enters nothing.  Give him a message box of all the user ids.
					if ( checkName[0].Trim().Length == 0)
					{
						StringBuilder names = new StringBuilder();
						XmlNodeList nameNodes = doc.SelectNodes("//UserState[@StateKey='PasswordHash']");

						foreach ( XmlNode nameNode in nameNodes )
						{
							string userId = nameNode.Attributes["UserId"].Value;
							XmlNode lockedState = doc.SelectSingleNode("//UserState[@StateKey='Locked' and @UserId='"+userId+"']");
							
							bool locked = lockedState.Attributes["StateValue"].Value.Equals("True");
					

							names.Append( userId +  ((locked) ? " (locked);" : "; "));

						}
						if ( MessageBox.Show(names.ToString(), "User ID's", MessageBoxButtons.OKCancel )
							== DialogResult.Cancel )
						{
							return;
						} else
						{
							continue;
						}
			
					}
						// They entered a name and a password.  check it.  check it for locked.  If locked, prompt to unluck.
					else 
					{
						XmlNode nameNode = 
							doc.SelectSingleNode("//UserState[@StateKey='PasswordHash' and @UserId='"+checkName[0]+"']");

						if ( nameNode == null)
						{
							if ( MessageBox.Show( "User Not Found","USer not found"+checkName[0], MessageBoxButtons.OKCancel )
								== DialogResult.Cancel )
							{
								return;
							} else
							{
								continue;
							}
						}

						XmlNode userNode = doc.SelectSingleNode("//User[@UserId='"+checkName[0]+"']");

						KeyedHashAlgorithm hashAlgorithm = KeyedHashAlgorithm.Create("HMACSHA1");

						// we use the user ID as the "secret" key :-)
						hashAlgorithm.Key = System.Text.Encoding.Default.GetBytes( checkName[0] );

						string storedHash = nameNode.Attributes["StateValue"].Value;
					
						if ( checkName[1].Trim().Length > 0 )
						{
							// compute the hash code
							byte[] hash = hashAlgorithm.ComputeHash(System.Text.Encoding.Default.GetBytes( checkName[1] ));

							string passHash =  Convert.ToBase64String(hash);


							//EndOfValidity="2009-01-09T18:59:00.0000000" 
							if ( userNode.Attributes["EndOfValidity"] != null )
							{
								string validityEnd = userNode.Attributes["EndOfValidity"].Value;

								DateTime dt = Convert.ToDateTime(validityEnd);
								if ( dt < DateTime.Now )
								{

									DialogResult dr = MessageBox.Show("Expired:" + validityEnd + ", Extend out one year?"
									                                  , "AccountRepo Helper", MessageBoxButtons.YesNoCancel);
									if ( dr ==  DialogResult.Cancel )
									{
										return;
									} else if ( dr == DialogResult.Yes )
									{
										DateTime newDT = DateTime.Now.AddYears(1);
										string newValidity = newDT.ToString("yyyy-MM-dd");
										
										
										


										userNode.Attributes["EndOfValidity"].Value = newValidity;


										MemoryStream ms = new MemoryStream();

										doc.Save( ms );

										ciphertext = olympCryptography.Encrypt(ms.GetBuffer()
											, new CompressStreamWriterDelegate(CompressStreamWriter));


										fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
										using(BinaryWriter writer = new BinaryWriter(fileStream))
										{
											writer.Write(ciphertext, 0, ciphertext.Length);
											writer.Flush();
										}
									}
								}

							}

							// User entered correct password.  Let's go the extra mile and see if the user is locked
							// expired.. If so, let's force unlock the user.
							XmlNode lockedState = doc.SelectSingleNode("//UserState[@StateKey='Locked' and @UserId='"+checkName[0]+"']");
							
							bool locked = lockedState.Attributes["StateValue"].Value.Equals("True");

							if ( !passHash.Equals( storedHash ))
							{
								string newPass = QuickDialog.DoQuickDialog("Force new password", "New Password (blank=no change)", "");

								if ( newPass == null )
								{
									return;
								} else if ( newPass.Trim().Length > 0 )
								{
									hash = hashAlgorithm.ComputeHash(System.Text.Encoding.Default.GetBytes( checkName[1] ));

									passHash =  Convert.ToBase64String(hash);

									nameNode.Attributes["StateValue"].Value = passHash;

									MemoryStream ms = new MemoryStream();

									doc.Save( ms );

									ciphertext = olympCryptography.Encrypt(ms.GetBuffer()
										, new CompressStreamWriterDelegate(CompressStreamWriter));


									fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
									using(BinaryWriter writer = new BinaryWriter(fileStream))
									{
										writer.Write(ciphertext, 0, ciphertext.Length);
										writer.Flush();
									}
									
								} else
								{
									continue;
								}
							}

							if ( !locked )
							{
								if (MessageBox.Show("PW IS GOOD for " + checkName[0] + " and locked=" + locked.ToString(),
									"AccountRepo help", MessageBoxButtons.OKCancel)
									== DialogResult.Cancel )
								{
									return;
								} 
								else
								{
									continue;
								}
							} 
							else
							{
								DialogResult dr = MessageBox.Show("User is locked.  unlock?", "AccountRepo Help", MessageBoxButtons.YesNoCancel);

								if ( dr == DialogResult.Cancel)
								{
									return;
								} else if ( dr == DialogResult.No )
								{
									continue;
								} else // Yes
								{

									lockedState.Attributes["StateValue"].Value = "False";

									MemoryStream ms = new MemoryStream();

									doc.Save( ms );

									ciphertext = olympCryptography.Encrypt(ms.GetBuffer()
														, new CompressStreamWriterDelegate(CompressStreamWriter));


									fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
									using(BinaryWriter writer = new BinaryWriter(fileStream))
									{
										writer.Write(ciphertext, 0, ciphertext.Length);
										writer.Flush();
									}

								}

							}



						
						}
						else
						{

							string[] abc123 = new string[]{"!", "@", "#", "$", "%", "^", "&", "*", "(", ")"};

							bool found = false;
							foreach ( string pCharTest in abc123 )
							{
								// compute the hash code
								byte[] hash = hashAlgorithm.ComputeHash(System.Text.Encoding.Default.GetBytes( 
								                                        	"ABCD123" + pCharTest ));

								string passHash =  Convert.ToBase64String(hash);



								if ( passHash.Equals( storedHash ))
								{
									

									found = true;

									string userId = nameNode.Attributes["UserId"].Value;
									XmlNode lockedState = doc.SelectSingleNode("//UserState[@StateKey='Locked' and @UserId='"+userId+"']");
							
									bool locked = lockedState.Attributes["StateValue"].Value.Equals("True");
					

									if ( MessageBox.Show(pCharTest + ((locked) ? " (locked)" : ""), "Hint Hint", MessageBoxButtons.OKCancel )
										== DialogResult.Cancel )
									{
										return;
									} 
									else
									{
										break;
									}
								}
							}
							if ( !found )
							{
								if (MessageBox.Show("None found", "Hint Hint", MessageBoxButtons.OK) == DialogResult.Cancel )
								{
									return;
								} 
								else
								{
									continue;
								}
							}

						}

					
					}
				} 
				catch (Exception  )
				{
					// Exceptions are secret.
					//Console.WriteLine( e.StackTrace );
					//Console.WriteLine( e.Message );
				}

				
			}


			
		}

		/// <summary>
		/// Liefert einen C1ZStreamReader, der den übergebenen Stream dekopmrimiert.
		/// </summary>
		/// <param name="compressedStream">Der komprimierte Stream</param>
		/// <returns>Ein StreamReader der den Stream entpackt</returns>
		private Stream CompressStreamReader(Stream compressedStream)
		{
			return new C1ZStreamReader(compressedStream);
		}

		
		/// <summary>
		/// Liefert einen C1ZStreamWriter, der den übergebenen Stream kopmrimiert.
		/// </summary>
		/// <param name="uncompressedStream">Der unkomprimierte Stream</param>
		/// <returns>Ein StreamWriter der den Stream komprimiert</returns>
		private Stream CompressStreamWriter(Stream uncompressedStream)
		{
			return new C1ZStreamWriter(uncompressedStream);
		}

		private bool ValidateIsAccountRepo( FileInfo file )
		{
			return file.Name.ToLower().Equals("accountrepository.bin");
		}



	}
}
