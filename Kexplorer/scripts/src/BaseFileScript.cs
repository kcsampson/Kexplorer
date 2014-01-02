using System;
using System.IO;
using Kexplorer.scripting;

namespace Kexplorer.scripts
{
	/// <summary>
	/// Base class for all File Scripts.
	/// </summary>
	public abstract class BaseFileScript : BaseScript, IFileScript
	{
		public BaseFileScript()
		{
			//
			// TODO: Add constructor logic here
			//
		}

		public abstract void Run(KExplorerNode folder, FileInfo[] files);


		private string[] validExtensions = null;
		public string[] ValidExtensions
		{
			get { return this.validExtensions; }
			set { this.validExtensions = value; }
		}

		private FileValidator validator = null;
		public FileValidator Validator
		{
			get {return this.validator; }
			set { this.validator = value; }
		}


	}
}
