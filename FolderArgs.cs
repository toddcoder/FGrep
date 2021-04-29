using Core.Computers;

namespace FGrep
{
   public class FolderArgs
   {
      public FolderArgs(FolderName folder)
      {
         Folder = folder;
      }

      public FolderName Folder { get; }
   }
}