using System;
using System.IO;

namespace Kexplorer.scripting
{
	public delegate bool FolderValidator(  DirectoryInfo file );

	/// <summary>
	/// Summary description for FolderScript.
	/// </summary>
	public interface IFolderScript : IScript
	{
		void Run( KExplorerNode folder  );


		FolderValidator  ValidatorFolder { get; set; }

	}
}
