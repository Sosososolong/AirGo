using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DemonFox.Tails.Utils
{
    public class FileOp
    {        
        public void DeleteFiles(List<string> files)
        {
            foreach (string file in files)
            {                
                File.Delete(file);                
            }
        }

        public List<string> FindFilesRecursive(string dirPath, List<string> files = null)
        {
            if (files == null)
            {
                files = new List<string>();
            }
            string[] findedFiles = Directory.GetFiles(dirPath);
            foreach (string file in findedFiles)
            {
                files.Add(file);
            }
            string[] dirs = Directory.GetDirectories(dirPath);
            if (!dirs.Any())
            {
                return files;
            }
            foreach (string dir in dirs)
            {
                if (dir.EndsWith(@"\obj") || dir.EndsWith(@"\bin") || dir.EndsWith(@"\.vs"))
                {
                    continue;
                }
                FindFilesRecursive(dir, files);
            }
            return files;
        }
    }
}
