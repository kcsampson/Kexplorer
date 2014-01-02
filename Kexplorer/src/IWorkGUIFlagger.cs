using System;

namespace Kexplorer
{


	/// <summary>
	/// Summary description for IWorkGUIFlagger.
	/// </summary>
	public interface IWorkGUIFlagger
	{

	/// <summary>
	/// A callback to let somebody know we're touching the GUI.
	/// </summary>
	void SignalBeginGUI();


	/// <summary>
	/// A callback to let somebody know we're not touching the GUI.
	/// </summary>
	void SignalEndGUI();
		
	}
}
