using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Core.Applications;
using Core.Assertions;
using Core.Computers;
using Core.Dates;
using Core.Enumerables;
using Core.Exceptions;
using Core.Monads;
using Core.Numbers;
using Core.Matching;
using Core.Strings;
using static System.Console;
using static Core.Monads.MonadFunctions;
using static Core.Strings.StringFunctions;

namespace FGrep
{
   internal class Program : CommandLineInterface, ICommandFile
   {
      protected Pattern pattern;
      protected int lineTally;
      protected int fileTally;
      protected IMaybe<Stopwatch> _stopwatch;
      protected Func<string, string> truncFunc;
      protected int screenWidth;

      public Program()
      {
         Pattern.Must().Not.BeNullOrEmpty().OrThrow();
         pattern = Pattern;
         Truncate = none<string>();
         Include = string.Empty;
         IncludeExt = string.Empty;
         Exclude = string.Empty;
         ExcludeExt = string.Empty;
         File = none<FileName>();
         Folder = none<FolderName>();
         Replacement = string.Empty;
         Pattern = string.Empty;
         Not = false;
         Unless = none<string>();
         Color = none<string>();
         Friendly = true;
         OutputFile = none<FileName>();
         Input = string.Empty;
         Unfriendly = false;

         screenWidth = WindowWidth - 3;
      }

      protected static bool IsMatch(string input, string pattern) => input.IsMatch(pattern);

      protected bool IsMatch(string input) => IsMatch(input, Pattern);

      protected static void Main()
      {
         try
         {
            var program = new Program
            {
               Application = "fgrep",
               Shortcuts = "p = pattern; F = folder; f = file; I = include; X = exclude; i = ignore-case; m = multiline; s = stopwatch; " +
                  "h = help; v = verbose; r = replacement; u = unless"
            };
            program.Run();
         }
         catch (Exception exception)
         {
            WriteLine(exception);
         }
      }

      [EntryPoint(EntryPointType.This)]
      public void EntryPoint()
      {
         if (Help)
         {
            displayHelp();
            return;
         }

         Core.Matching.Pattern.IsFriendly = Friendly;

         _stopwatch = maybe(Verbose || Stopwatch, () => new Stopwatch());
         _stopwatch.IfThen(s => s.Start());

         if (Find)
         {
            find();
         }
         else if (Replace)
         {
            replaceAction();
         }
         else if (Regex)
         {
            regexAction();
         }
         else if (Sub)
         {
            substitute();
         }
         else if (Unfriendly)
         {
            unfriendly();
         }
         else if (Pattern.IsNotEmpty())
         {
            find();
         }
         else
         {
            throw "Didn't recognize action".Throws();
         }

         if (_stopwatch.If(out var stopwatch))
         {
            stopwatch.Stop();
            WriteLine();
            WriteLine($"Elapsed time: {stopwatch.Elapsed.ToLongString(true)}");
         }

         if (Find && Verbose)
         {
            WriteLine($"File count  : {fileTally}");
            WriteLine($"Line count  : {lineTally}");
         }
      }

      protected void find()
      {
         if (Folder.If(out var folder))
         {
            findAction(folder);
         }
         else if (File.If(out var file))
         {
            findLines(file);
         }
         else if (Mark)
         {
            findLinesMarked();
         }
         else
         {
            findLines();
         }
      }

      protected void findAction(FolderName folder)
      {
         folder.Must().Value.Must().Exist().OrThrow();

         var finder = new Finder(Pattern, Not, Unless, Include, IncludeExt, Exclude, ExcludeExt);

         if (Replacement.IsNotEmpty())
         {
            var replacement = Replacement.Replace("^", "$");
            truncFunc = line => line.Substitute(Pattern, replacement);
         }
         else if (Truncate.If(out var truncate))
         {
            if (truncate.IsIntegral())
            {
               var amount = truncate.ToInt();
               truncFunc = line => line.Exactly(amount);
            }
            else if (truncate == "-")
            {
               truncFunc = line => line;
            }
            else
            {
               truncFunc = line => line.Exactly(80);
            }
         }
         else
         {
            truncFunc = line => line;
         }

         fileTally = 0;
         lineTally = 0;

         if (Mark)
         {
            matchFolderMarked(folder, finder);
         }
         else
         {
            matchFolder(folder, finder, result => truncFunc(result.Line), true);
         }
      }

      protected void findLines(FileName file)
      {
         file.Must().Exist().OrThrow();

         var filePattern = new Finder(Pattern, Not, Unless, Include, IncludeExt, Exclude, ExcludeExt);
         var width = 0;

         foreach (var result in filePattern.FileLines(file))
         {
            if (result.LineCount.If(out var lineCount))
            {
               width = lineCount;
            }

            WriteLine($"{result.LineNumber.RightJustify(width, '0')} | {result.Line.Exactly(80)}");
         }
      }

      protected void findLines()
      {
         var filePattern = new Finder(Pattern, Not, Unless, "", "", "", "");

         while (true)
         {
            var line = ReadLine();
            if (line == null)
            {
               break;
            }

            if (filePattern.MatchedLine(line))
            {
               WriteLine(line);
            }
         }
      }

      protected void findLinesMarked()
      {
         var filePattern = new Finder(Pattern, Not, Unless, "", "", "", "");

         while (true)
         {
            var line = ReadLine();
            if (line == null)
            {
               break;
            }

            if (filePattern.FindResult(line).If(out var result))
            {
               WriteLine(result.ExactLine(screenWidth));

               foreach (var indicator in result.MatchIndicators())
               {
                  Write(indicator);
               }

               WriteLine();
            }
         }
      }

      protected void replaceAction()
      {
         Replacement.Must().Not.BeNullOrEmpty().OrThrow();

         replaceText();
      }

      protected void regexAction()
      {
         WriteLine(pattern.Regex);
      }

      public bool Find { get; set; }

      public bool Replace { get; set; }

      public bool Regex { get; set; }

      public bool Unfriendly { get; set; }

      public string Pattern { get; set; }

      public string Replacement { get; set; }

      public string Input { get; set; }

      public IMaybe<FolderName> Folder { get; set; }

      public IMaybe<FileName> File { get; set; }

      public bool Backup { get; set; }

      public bool Verbose { get; set; }

      public bool Help { get; set; }

      public string Include { get; set; }

      public string IncludeExt { get; set; }

      public string Exclude { get; set; }

      public string ExcludeExt { get; set; }

      public bool Stopwatch { get; set; }

      public IMaybe<string> Truncate { get; set; }

      public bool Delete { get; set; }

      public bool Mark { get; set; }

      public bool Not { get; set; }

      public bool Threaded { get; set; }

      public bool Sub { get; set; }

      public IMaybe<string> Unless { get; set; }

      public IMaybe<string> Color { get; set; }

      public bool Friendly { get; set; }

      public IMaybe<FileName> OutputFile { get; set; }

      protected static void displayHelp()
      {
         WriteLine("fgrep find --pattern <pattern> --folder <folder>");
         WriteLine();
         WriteLine("Options:");
         WriteLine("   --include pattern for including by file name");
         WriteLine("   -I");
         WriteLine("   --exclude pattern for excluding by file name -");
         WriteLine("   -X");
         WriteLine("   --ignore-case ignore case");
         WriteLine("   -i");
         WriteLine("   --multiline multiline");
         WriteLine("   -m");
         WriteLine("   --verbose verbose (include folders/files being searched, file counts, line counts and elapsed time)");
         WriteLine("   -V");
         WriteLine("   --truncate - don't truncate line displayed");
         WriteLine("   --truncate ## truncate by ## columns");
         WriteLine("   --truncate truncate by 80 columns");
         WriteLine("   --stopwatch use stopwatch");
         WriteLine("   -S");

         WriteLine("fgrep replace --pattern <pattern> --replacement <replacement> --file <file>");
         WriteLine();
         WriteLine("Options:");
         WriteLine("   --ignore-case ignore case");
         WriteLine("   -i");
         WriteLine("   --multiline multiline");
         WriteLine("   -m");
         WriteLine("   --verbose verbose (include folders/files being searched, file counts, line counts and elapsed time)");
         WriteLine("   -V");
         WriteLine("   --truncate - don't truncate line displayed");
         WriteLine("   --truncate ## truncate by ## columns");
         WriteLine("   --truncate truncate by 80 columns");
         WriteLine("   --stopwatch use stopwatch");
         WriteLine("   --delete delete matching line");
         WriteLine("   -S");
      }

      protected void matchFolder(FolderName folder, Finder finder, Func<FindResult, string> toString, bool endOfLine)
      {
         if (finder.GetFilesByLine(folder).If(out var results, out var exception))
         {
            FileName fileCompare = "x.txt";

            foreach (var result in results)
            {
               if (result.Folder.If(out var foundFolder) && Verbose)
               {
                  WriteLine($"Checking folder {foundFolder}");
                  WriteLine($"----------------{"-".Repeat(foundFolder.FullPath.Length)}");
               }

               if (result.File != fileCompare)
               {
                  WriteLine($"|{result.File.NameExtension}");
                  fileTally++;

                  fileCompare = result.File;
               }

               var text = toString(result);
               if (endOfLine)
               {
                  WriteLine(text);
               }
               else
               {
                  Write(text);
               }
            }
         }
         else
         {
            WriteLine(exception.Message);
         }
      }

      protected void matchFolderMarked(FolderName folder, Finder finder)
      {
         matchFolder(folder, finder, format, false);
      }

      protected string format(FindResult result)
      {
         using var writer = new StringWriter();

         writer.WriteLine(result.ExactLine(screenWidth));
         foreach (var indicator in result.MatchIndicators())
         {
            writer.Write(indicator);
         }

         writer.WriteLine();

         return writer.ToString();
      }

      protected void replaceText()
      {
         var fixedReplacement = Replacement.Replace("//", "~double");
         fixedReplacement = fixedReplacement.Replace("/", "$");
         fixedReplacement = fixedReplacement.Replace("~double", "/");

         if (File.If(out var file))
         {
            file.Must().Exist().OrThrow();

            if (Backup)
            {
               var backupFile = FileName.UniqueFileName(file.Folder, file);
               file.CopyTo(backupFile, true);
            }

            if (OutputFile.If(out var outputFile))
            {
               var tempFile = FolderName.Temp + $"{uniqueID()}.{file.Extension}";

               var newLines = new List<string>();

               if (Delete)
               {
                  foreach (var line in file.Lines)
                  {
                     if (!IsMatch(line))
                     {
                        newLines.Add(line);
                     }
                  }
               }
               else
               {
                  foreach (var line in file.Lines.Where(IsMatch))
                  {
                     var replacement = line.Substitute(Pattern, fixedReplacement);
                     newLines.Add(replacement);
                  }
               }

               tempFile.SetText(newLines.ToString("\r\n"), file.Encoding);

               tempFile.CopyTo(outputFile, true);
               tempFile.Delete();
            }
            else
            {
               foreach (var line in file.Lines.Where(IsMatch))
               {
                  var replacement = line.Substitute(Pattern, fixedReplacement);
                  WriteLine(replacement);
               }
            }
         }
         else
         {
            while (true)
            {
               var line = ReadLine();
               if (line == null)
               {
                  break;
               }

               var replacement = line.Substitute(Pattern, fixedReplacement);
               WriteLine(replacement);
            }
         }
      }

      protected static IMaybe<string> substitute(string line, Pattern pattern, string replacement)
      {
         return maybe(line.IsMatch(pattern), () => line.Substitute(pattern, replacement));
      }

      protected void substitute()
      {
         var replacement = Replacement;

         while (true)
         {
            var line = ReadLine();
            if (line == null)
            {
               break;
            }
            else if (substitute(line, pattern, replacement).If(out var substituted))
            {
               WriteLine(substituted);
            }
         }
      }

      protected void unfriendly()
      {
         if (Input.Matches(pattern).If(out var result))
         {
            WriteLine($"Result : {result.ToString().Guillemetify()}");
            WriteLine($"Pattern: {pattern.Regex}");
         }
         else
         {
            WriteLine("No match");
         }
      }

      public FileName CommandFile(string name) => $@"~\AppData\Local\fgrep\{name}.cli";
   }
}