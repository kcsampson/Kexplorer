using System;
using System.Collections;
using System.Xml;
using System.Collections.Generic;

namespace Kexplorer.scripting
{
	/// <summary>
	/// Object providing services to scripts.
	/// </summary>
	public interface IScriptHelper
	{

		KExplorerNode FindFolder( KExplorerNode startNode, string relativePath );

		/// <summary>
		/// So that all scripts can access and set common variables to all scripts.
		/// </summary>
		Hashtable VARS { get; }

		// Allows scripts to access other scripts.
		IScript FindScript( string scriptName );

		// Allows scripts to create new scripts.
		void AddScript( IScript script );


		// Allows scripts to remove scripts. (Play nice!)
		void RemoveScript( string scriptName );


		// Allows scripts to force the current folder to be refreshed.
		void RefreshFolder( KExplorerNode folderNode, bool folderOnly );

        void RefreshFolder(KexplorerFtpNode folderNode, bool folderOnly);

		// Run a program.
		void RunProgram( string exe, string options, KExplorerNode atFolderNode, bool asAdmin );

        // Run a program.
        void RunProgram(string exe, string options, KExplorerNode atFolderNode);

		// Run a program.
		void RunProgramInKonsole( string exe, string options, KExplorerNode atFolderNode );

		// Run a program.
		void RunProgram( string exe, string options, KExplorerNode atFolderNode, bool waitForExitm, bool adAdmin );


        XmlDocument ScriptHelperDoc { get; set; }

        List<String> GetValueList(string configXPath);


	}
}
