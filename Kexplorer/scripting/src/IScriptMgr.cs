using System;
using System.IO;
using System.ServiceProcess;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Linq;

namespace Kexplorer.scripting
{
	/// <summary>
	/// Interface of how scripts are managed.
	/// </summary>
	public interface IScriptMgr
	{


		IScriptHelper ScriptHelper { get; set; }

		List<IFileScript> FileScripts { get; set; }

		List<IFolderScript> FolderScripts { get; set; }

        List<IFtpFolderScript> FtpFolderScripts { get; set; }

        List<IFTPFileScript> FTPFileScripts { get; set; }


		List<IServiceScript> ServiceScripts { get; set; }

        void RunFtpFileScript(string longName, KexplorerFtpNode foldernode, string[] files);

        void RunFtpFolderScript(string longName, KexplorerFtpNode foldernode);

		void RunFileScript( string longName
						  , KExplorerNode folderNode
						  , FileInfo[] files );


		void RunFolderScript( string longName
							, KExplorerNode folderNode );


		void RunServiceScript( string longName
								, ServiceController[] services 
								, Form mainForm
								, DataGrid serviceGrid );





		void InitializeScripts( IScriptHelper withThisHelper );

	}
}
