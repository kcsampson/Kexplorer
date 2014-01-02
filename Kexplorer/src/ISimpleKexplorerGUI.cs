using System;
using System.Windows.Forms;

namespace Kexplorer
{
	/// <summary>
	/// Defines the Interface of a IKexplorerGUI.  Program to this interface, instead of an actual
	/// GUI, this will make the GUI easy to change.
	/// 
	/// We'll make this close to the original GUI even through things like Tabs, etc. will get added.
	/// </summary>
	public interface ISimpleKexplorerGUI
	{

		Form MainForm {get; set;}

		TreeView TreeView1 { get; }

		DataGridView DataGridView1 { get; }



		ContextMenu DirTreeMenu { get; }

 
		ContextMenuStrip FileGridMenuStrip { get; }


		string WatchingForFolder { get; set; }

	}
}
