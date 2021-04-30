using System;
using Core.Computers;

namespace FGrep
{
   public class FolderArgs : EventArgs
   {
      public FolderArgs(FolderName folder)
      {
         Folder = folder;
      }

      public FolderName Folder { get; }
   }
}