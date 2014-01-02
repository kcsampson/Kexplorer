using System;
using System.Collections;

namespace Kexplorer
{
	/// <summary>
	/// Summary description for IKExplorerControl.
	/// </summary>
	public interface IKExplorerControl
	{


		Pipeline MainPipeLine { get; }


		// Each drive gets its own Pipeline....
		Hashtable DrivePipelines { get; }


	}
}
