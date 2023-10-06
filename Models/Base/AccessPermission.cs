using System;
using System.Collections.Generic;
using System.Linq;

namespace Syncfusion.EJ2.FileManager.Base
{
    public class AccessPermission
    {
        /// <summary>
        /// Specifies access to copy a file or folder.
        /// </summary>
        public bool Copy { get; set; } = true;

        /// <summary>
        /// Specifies permission to download a file or folder.
        /// </summary>
        public bool Download { get; set; } = true;

        /// <summary>
        /// Specifies permission to write a file or folder.
        /// </summary>
        public bool Write { get; set; } = true;

        /// <summary>
        /// Specifies permission to write the content of folder.
        /// </summary>
        public bool WriteContents { get; set; } = true;

        /// <summary>
        /// Specifies access to read a file or folder.
        /// </summary>
        public bool Read { get; set; } = true;
        
        /// <summary>
        /// Specifies permission to upload to the folder.
        /// </summary>
        public bool Upload { get; set; } = true;

        /// <summary>
        /// Specifies the access message.
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }
}