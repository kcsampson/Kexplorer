using System;
using System.Collections.Generic;
using System.Text;
using System.Collections;

namespace Kexplorer
{
    class FtpFileInfo
    {


        public String name = null;
        public String size = null;
        public String lastupdate = null;
        public String ftplistline = null;
        public String ext = null;

        public bool isDir = false;


        public FtpFileInfo(String ftplistline, String siteType)
        {
            this.ftplistline = ftplistline;
            name = "xxx";
            size = "yyy";
            lastupdate = "34534-34534-345-34";

            if ( siteType.Equals("unix") ){

                ParseUnixLine(ftplistline);
             

            } else {
                isDir = (ftplistline.IndexOf("<DIR>") > 0);
                name = ftplistline.Substring(ftplistline.IndexOf("<DIR>") + 5).Trim();
                ParseWindowsLine(ftplistline);
            }

        }


        public override string ToString()
        {
            return name;
        }



        private void ParseUnixLine(String line)
        {

            String[] split = line.Split(' ' );

            List<String> bsplit = new List<String>();

            foreach ( String s in split ){
                if ( s.Trim().Length != 0 ){
                    bsplit.Add( s );
                }
            }

            isDir = bsplit[0].StartsWith("d");

            if (bsplit.Count < 3)
            {
                name = bsplit[0];
                return;
            }

            if (bsplit.Count == 8)
            {
                name = bsplit[7];

                size = bsplit[3];

                lastupdate = split[4] + " " + split[5] + " " + split[6];


                    ext = "";
                
            }
            else if ( bsplit.Count > 8 )
            {

                name = bsplit[8];
                if (bsplit.Count > 9)
                {
                    for (int i = 9; i < bsplit.Count; i++)
                    {
                        name = name + " " + bsplit[i];
                    }
                }

                size = bsplit[4];

                lastupdate = split[5] + " " + split[6] + " " + split[7];


                if (name.Contains("."))
                {
                    int i = name.LastIndexOf(".");

                    ext = name.Substring(i + 1);


                }
                else
                {
                    ext = "";
                }
            }


        }


        private void ParseWindowsLine(String line)
        {
            String[] split = line.Split(' ');

            List<String> bsplit = new List<String>();

            foreach (String s in split)
            {
                if (s.Trim().Length != 0)
                {
                    bsplit.Add(s);
                }
            }
            if (bsplit.Count < 3)
            {
                return;
            }
            name = bsplit[3];
            if (bsplit.Count > 4)
            {
                for (int i = 4; i < bsplit.Count; i++)
                {
                    name = name + " " + bsplit[i];
                }
            }

            lastupdate = bsplit[0] + " " + bsplit[1];

            size = bsplit[2];

            if (name.Contains("."))
            {
                int i = name.LastIndexOf(".");

                ext = name.Substring(i + 1);


            }
            else
            {
                ext = "";
            }

        }

    }
}
