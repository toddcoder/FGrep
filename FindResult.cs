using Core.Computers;
using Core.Monads;
using Core.RegularExpressions;
using static Core.Monads.MonadFunctions;

namespace FGrep
{
   public class FindResult
   {
      public FindResult()
      {
         File = @"c:\unknown.txt";
         Line = "";
         Matcher = new Matcher();
         LineCount = none<int>();
         Folder = none<FolderName>();
         FileNameExtension = "";
      }

      public FileName File { get; set; }

      public int LineNumber { get; set; }

      public string Line { get; set; }

      public Matcher Matcher { get; set; }

      public IMaybe<int> LineCount { get; set; }

      public IMaybe<FolderName> Folder { get; set; }

      public string FileNameExtension { get; set; }

      public int IndentLevel { get; set; }

      public FindResult Clone() => new FindResult
      {
         File = File, LineNumber = LineNumber, Line = Line, LineCount = LineCount, Folder = Folder, FileNameExtension = FileNameExtension,
         IndentLevel = IndentLevel
      };
   }
}