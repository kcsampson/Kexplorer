using System;
using System.IO;

namespace Kexplorer.scripting
{
	/// <summary>
	/// Summary description for IScriptRunParams.
	/// </summary>
	public class ScriptRunParams
	{

		private KExplorerNode folderNode = null;
		private FileInfo[] files = null;

        private KexplorerFtpNode ftpNode = null;
        private string[] ftpFiles = null;

        public KexplorerFtpNode FtpNode { get { return this.ftpNode; } }
        public string[] FtpFiles { get { return this.ftpFiles; } }


		public KExplorerNode FolderNode 
		{
			get { return this.folderNode; }
		}


		public FileInfo[] Files 
		{
			get { return this.files; }
		}



        public ScriptRunParams(KexplorerFtpNode newFtpNode, string[] files)
        {
            this.ftpNode = newFtpNode;
            this.ftpFiles = files; 
        }

		public ScriptRunParams( KExplorerNode newFolderNode, FileInfo[] newFiles )
		{
			this.folderNode = newFolderNode;
			this.files = newFiles;
		}
	}
}
