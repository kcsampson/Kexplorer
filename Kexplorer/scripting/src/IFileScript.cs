using System;
using System.IO;

namespace Kexplorer.scripting
{
	public delegate bool FileValidator(  FileInfo file );
	/// <summary>
	/// IFileScript.  A File Script runs on an array of files that exist
	/// in one directory.  If the use selects only one file, then the file
	/// array is of length one.
	/// </summary>
	public interface IFileScript : IScript
	{

		void Run( KExplorerNode folder, FileInfo[] files );


		string[] ValidExtensions { get; set; }


		FileValidator  Validator { get; set; }


	}
}
