using System;
using Core.Computers;

namespace FGrep
{
   public class FileArgs : EventArgs
   {
      public FileArgs(FileName file)
      {
         File = file;
      }

      public FileName File { get; }
   }
}