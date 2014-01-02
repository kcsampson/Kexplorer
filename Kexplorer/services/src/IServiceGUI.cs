using System;
using System.Windows.Forms;

namespace Kexplorer.services
{
	/// <summary>
	/// What a Service GUI does.
	/// </summary>
	public interface IServiceGUI
	{

		DataGrid ServiceGrid { get; }


		ContextMenu ServiceGridMenu { get; }

	}
}
