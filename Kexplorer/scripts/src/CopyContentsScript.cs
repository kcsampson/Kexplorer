using System;
using System.IO;
using System.Windows.Forms;
using Kexplorer.scripting;

namespace Kexplorer.scripts
{
    /// <summary>
    /// Summary description for CopyFileScript.
    /// </summary>
    public class CopyContentsScript : BaseFileScript
    {
        public CopyContentsScript()
        {
            this.LongName = "Edit - Copy Contents";

            this.Description = "Copy selected file's contents to text clipboard";


            this.Active = true;

            this.ScriptShortCut = Shortcut.CtrlT;

        }


        /// <summary>
        /// Set the copied file(s).  If there were CUT files, reset them.
        /// </summary>
        /// <param name="folder"></param>
        /// <param name="files"></param>
        public override void Run(KExplorerNode folder, FileInfo[] files)
        {

            if (files.Length > 0)
            {


                StreamReader reader = files[0].OpenText();

                string x = reader.ReadToEnd();


                reader.Close();

                Clipboard.SetText(x);
            }


        }

    }
}
