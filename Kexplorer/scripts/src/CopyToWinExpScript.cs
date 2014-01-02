using System;
using System.IO;
using System.Windows.Forms;
using Kexplorer.scripting;
using System.Collections.Specialized;


namespace Kexplorer.scripts
{
    /// <summary>
    /// Summary description for CopyFileScript.
    /// </summary>
    public class CopyToWinExpScript : BaseFileAndFolderScript
    {
        public CopyToWinExpScript()
        {
            this.LongName = "Edit - Copy To Windows";

            this.Description = "Copy selected files or Windows Explorer Copy Buffer";


            this.Active = true;

            this.ScriptShortCut = Shortcut.CtrlW;

        }


        /// <summary>
        /// Set the copied file(s).  If there were CUT files, reset them.
        /// </summary>
        /// <param name="folder"></param>
        /// <param name="files"></param>
        public override void Run(KExplorerNode folder, FileInfo[] files)
        {
          
            StringCollection paths = new StringCollection();
            foreach (var file in files)
            {
                paths.Add(file.FullName);
            }
            Clipboard.SetFileDropList(paths);


        }

        public override void Run(KExplorerNode folder)
        {
            StringCollection paths = new StringCollection();

            paths.Add( folder.DirInfo.FullName );
            Clipboard.SetFileDropList(paths);
        }

    }
}

