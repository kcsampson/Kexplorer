using System;
using System.IO;
using Kexplorer.scripting;

namespace Kexplorer.scripts
{
	/// <summary>
	/// Summary description for BaseFileAndFolderScript.
	/// </summary>
	public abstract class BaseFileAndFolderScript : BaseScript, IFileScript, IFolderScript
	{
		public abstract void Run(KExplorerNode folder, FileInfo[] files);

		private string[] validExtensions = null;
		public string[] ValidExtensions
		{
			get { return this.validExtensions; }
			set { this.validExtensions = value; }
		}

		public abstract void Run(KExplorerNode folder);


		private FileValidator validator = null;
		public FileValidator Validator
		{
			get {return this.validator; }
			set { this.validator = value; }
		}


		private FolderValidator validatorFolder = null;
		public FolderValidator ValidatorFolder
		{
			get { return this.validatorFolder; }
			set { this.validatorFolder = value; }
		}
	}
}
