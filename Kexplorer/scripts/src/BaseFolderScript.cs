using System;
using Kexplorer.scripting;

namespace Kexplorer.scripts
{
	/// <summary>
	/// Base class for all folder scripts.
	/// </summary>
	public abstract class BaseFolderScript : BaseScript, IFolderScript
	{
		private FolderValidator validatorFolder = null;
		public BaseFolderScript()
		{

		}

		public abstract void Run(KExplorerNode folder);


		public FolderValidator ValidatorFolder
		{
			get { return this.validatorFolder; }
			set { this.validatorFolder = value; }
		}

	}
}
