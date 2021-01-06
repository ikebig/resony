﻿using System;
using System.IO;

namespace Auralsys.Audio
{
    public static partial class CommonHelper
    {
        public static string GetTemporaryFilePath(FileInfo fileInfo)
        {
            var path = fileInfo.FullName;
            string tempPath = path.Substring(0, path.Length - fileInfo.Extension.Length) + "." + Guid.NewGuid().ToString("N").Substring(0, 8) + ".tmp";
            return tempPath;
        }        
    }
}
