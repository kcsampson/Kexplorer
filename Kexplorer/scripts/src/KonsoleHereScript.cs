using System;
using System.IO;
using System.Windows.Forms;
using Kexplorer.console;
using Kexplorer.res;

namespace Kexplorer.scripts
{
	/// <summary>
	/// Summary description for CommandPromptScript.
	/// </summary>
	public class KonsoleHereScript : BaseFolderScript
	{
		public KonsoleHereScript()
		{
			
			this.LongName =  "Konsole Here";

			this.Description ="Open a Konsole Tab at the current folder.";

			this.Active = true;

			this.ScriptShortCut = Shortcut.CtrlK;
		}


		public override void Run(KExplorerNode folder)
		{

			KExplorerConsole console = new KExplorerConsole(KMultiForm.Instance());

			TabPage x = new TabPage("Konsole->" );


			console.Dock = System.Windows.Forms.DockStyle.Fill;

			x.Controls.Add( console );


			KMultiForm.Instance().MainTabControl.TabPages.Add( x );	
		

			KMultiForm.Instance().MainTabControl.SelectedTab = x;


			console.Initialize(folder.FullPath);
			
		}
	}
}
