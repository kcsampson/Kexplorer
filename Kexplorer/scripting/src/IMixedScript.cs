using System;
using System.IO;

namespace Kexplorer.scripting
{
	/// <summary>
	/// Summary description for IMultiDirectoryScript.
	/// </summary>
	public interface IMultiDirectoryScript : IScript
	{

		void Run( KExplorerNode parentFolderNode, DirectoryInfo[] dirs );

	}
}
