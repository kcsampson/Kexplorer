using System;
using System.IO;
using System.Windows.Forms;

namespace Kexplorer.scripting
{
	/// <summary>
	/// Summary description for IScript.
	/// </summary>
	public interface IScript
	{

		/// <summary>
		/// Is the script active to context menu
		/// </summary>
		bool Active { get; set; }


		/// <summary>
		/// Name of script for Context Menus, may have spaces.  The scripts short name
		/// may not have spaces and is the class name.
		/// </summary>
		string LongName { get; set; }

		/// <summary>
		/// For example, what would be shown for help.
		/// </summary>
		string Description { get; set; }


		void Initialize( IScriptHelper x );


		IScriptHelper ScriptHelper { get; set; }


		Shortcut ScriptShortCut { get; set; }

	}
}
