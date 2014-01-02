using System;
using System.Windows.Forms;

namespace Kexplorer.console
{
	/// <summary>
	/// IConsoleGUI
	/// </summary>
	public interface IConsoleGUI
	{

		void Initialize();

		Label LConsole { get; }



	}
}
