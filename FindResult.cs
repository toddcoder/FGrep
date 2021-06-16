using System.Collections.Generic;
using Core.Computers;
using Core.Monads;
using Core.RegexMatching;
using Core.Strings;
using static Core.Monads.MonadFunctions;

namespace FGrep
{
   public class FindResult
   {
      public FindResult()
      {
         File = @"c:\unknown.txt";
         Line = "";
         Result = Result.Empty;
         LineCount = none<int>();
         Folder = none<FolderName>();
         FileNameExtension = "";
      }

      public FileName File { get; set; }

      public int LineNumber { get; set; }

      public string Line { get; set; }

      public Result Result { get; set; }

      public IMaybe<int> LineCount { get; set; }

      public IMaybe<FolderName> Folder { get; set; }

      public string FileNameExtension { get; set; }

      public int IndentLevel { get; set; }

      public FindResult Clone() => new()
      {
         File = File, LineNumber = LineNumber, Line = Line, LineCount = LineCount, Folder = Folder, FileNameExtension = FileNameExtension,
         IndentLevel = IndentLevel, Result = Result
      };

      public string ExactLine(int screenWidth) => Line.Replace("\t", " ").Exactly(screenWidth, normalizeWhitespace: false);

      public IEnumerable<string> MatchIndicators()
      {
         var offset = 0;
         foreach (var (_, index, length) in Result)
         {
            yield return $"{" ".Repeat(index - offset)}{"^".Repeat(length)}";

            offset += index + length;
         }
      }
   }
}