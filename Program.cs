﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Core.Applications;
using Core.Assertions;
using Core.Computers;
using Core.Dates;
using Core.Enumerables;
using Core.Exceptions;
using Core.Monads;
using Core.Numbers;
using Core.RegularExpressions;
using Core.Strings;
using static System.Console;
using static Core.Assertions.AssertionFunctions;
using static Core.Lambdas.LambdaFunctions;
using static Core.Monads.MonadFunctions;
using static Core.Strings.StringFunctions;

namespace FGrep
{
   internal class Program : CommandLineInterface, ICommandFile
   {
      protected Func<FileName, bool> includes;
      protected Func<FileName, bool> excludes;
      protected int fileCount;
      protected int lineCount;
      protected IMaybe<Stopwatch> _stopwatch;
      protected Func<string, string> truncFunc;
      protected Matcher matcher;

      public Program()
      {
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
      }

      protected bool IsMatch(string input, string pattern) => input.IsMatch(pattern, IgnoreCase, Multiline, Friendly);

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

         assert(() => Pattern).Must().Not.BeNullOrEmpty().OrThrow();

         _stopwatch = maybe(Verbose || Stopwatch, () => new Stopwatch());
         _stopwatch.IfThen(s => s.Start());

         matcher = new Matcher(Friendly);

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
            WriteLine($"File count  : {fileCount}");
            WriteLine($"Line count  : {lineCount}");
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
         else
         {
            findLines();
         }
      }

      protected void findAction(FolderName folder)
      {
         assert(() => folder).Must().Value.Must().Exist().OrThrow();

         includes = f => true;
         excludes = f => false;

         if (Replacement.IsNotEmpty())
         {
            var replacement = Replacement.Replace("^", "$");
            truncFunc = line => line.Substitute(Pattern, replacement, IgnoreCase, Multiline, Friendly);
         }
         else if (Truncate.If(out var truncate))
         {
            if (truncate.IsIntegral())
            {
               var amount = truncate.ToInt();
               truncFunc = line => line.Truncate(amount);
            }
            else if (truncate == "-")
            {
               truncFunc = line => line;
            }
            else
            {
               truncFunc = line => line.Truncate(80);
            }
         }
         else
         {
            truncFunc = line => line;
         }

         if (Include.IsNotEmpty())
         {
            WriteLine($"include {Include.Guillemetify()}");
            includes = f => IsMatch(f.NameExtension);
         }
         else if (IncludeExt.IsNotEmpty())
         {
            WriteLine($"include extension {IncludeExt.Guillemetify()}");
            includes = f => f.Extension.EndsWith(IncludeExt);
         }

         if (Exclude.IsNotEmpty())
         {
            WriteLine($"exclude {Exclude.Guillemetify()}");
            excludes = f => IsMatch(f.NameExtension);
         }
         else if (ExcludeExt.IsNotEmpty())
         {
            WriteLine($"exclude extension {ExcludeExt.Guillemetify()}");
            excludes = f => f.Extension.EndsWith(ExcludeExt);
         }

         fileCount = 0;
         lineCount = 0;

         if (AllText)
         {
            var unless = Unless.Map(p => func<string, bool>(line => IsMatch(line, p))).DefaultTo(() => _ => false);
            matchFolderText(folder, 0, unless);
         }
         else
         {
            matchFolder(folder, 0);
         }
      }

      protected void findLines(FileName file)
      {
         assert(() => file).Must().Exist().OrThrow();

         Func<string, bool> matches;
         if (Not)
         {
            matches = line => !IsMatch(line);
         }
         else
         {
            matches = IsMatch;
         }

         var unless = Unless.Map(p => func<string, bool>(line => IsMatch(line, p))).DefaultTo(() => _ => false);

         foreach (var line in file.Lines.Where(line => matches(line) && !unless(line)))
         {
            WriteLine(line);
         }
      }

      protected void findLines()
      {
         Func<string, bool> matches;
         if (Not)
         {
            matches = line => !IsMatch(line);
         }
         else
         {
            matches = IsMatch;
         }

         var unless = Unless.Map(p => func<string, bool>(line => IsMatch(line, p))).DefaultTo(() => _ => false);

         while (true)
         {
            var line = ReadLine();
            if (line == null)
            {
               break;
            }

            if (matches(line) && !unless(line))
            {
               WriteLine(line);
            }
         }
      }

      protected void replaceAction()
      {
         assert(() => Replacement).Must().Not.BeNullOrEmpty().OrThrow();
         assert(() => File).Must().HaveValue().OrThrow();
         assert(() => File).Must().Value.Must().Exist().OrThrow();

         replaceText();
      }

      protected void regexAction()
      {
         assert(() => Pattern).Must().Not.BeNullOrEmpty().OrThrow();

         matcher.IsMatch(string.Empty, Pattern);
         WriteLine(matcher.Pattern);
      }

      public bool Find { get; set; }

      public bool Replace { get; set; }

      public bool Regex { get; set; }

      public string Pattern { get; set; }

      public string Replacement { get; set; }

      public IMaybe<FolderName> Folder { get; set; }

      public IMaybe<FileName> File { get; set; }

      public bool Backup { get; set; }

      public bool IgnoreCase { get; set; }

      public bool Multiline { get; set; }

      public bool Verbose { get; set; }

      public bool Help { get; set; }

      public string Include { get; set; }

      public string IncludeExt { get; set; }

      public string Exclude { get; set; }

      public string ExcludeExt { get; set; }

      public bool Stopwatch { get; set; }

      public IMaybe<string> Truncate { get; set; }

      public bool Delete { get; set; }

      public bool AllText { get; set; }

      public bool Not { get; set; }

      public bool Threaded { get; set; }

      public bool Sub { get; set; }

      public IMaybe<string> Unless { get; set; }

      public IMaybe<string> Color { get; set; }

      public bool Friendly { get; set; }

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

      protected void matchFolder(FolderName folder, int indent)
      {
         var prefix = " ".Repeat(indent);

         if (Verbose)
         {
            WriteLine($"{prefix}Checking folder {folder}");
         }

         var wroteFolder = false;

         if (getFiles(folder).If(out var files, out var exception))
         {
            var unless = Unless.Map(p => func<string, bool>(line => IsMatch(line, p))).DefaultTo(() => _ => false);

            foreach (var file in files)
            {
               if (Verbose)
               {
                  WriteLine($"{prefix}{prefix}Checking {file.NameExtension}");
               }

               var wroteName = false;

               var _lines = file.TryTo.Lines;
               if (_lines.If(out var lines, out exception))
               {
                  for (var i = 0; i < lines.Length; i++)
                  {
                     var line = lines[i];
                     if (IsMatch(line) && !unless(line))
                     {
                        if (!wroteName)
                        {
                           if (!wroteFolder)
                           {
                              WriteLine($"{prefix}{folder}");
                              wroteFolder = true;
                           }

                           WriteLine($"{prefix}{prefix}|");
                           WriteLine($"{prefix}{prefix}|{file.NameExtension}");
                           wroteName = true;
                           fileCount++;
                        }

                        WriteLine($"{prefix}{prefix}|{prefix}{i + 1:D6}: {truncFunc(line)}");
                        lineCount++;
                     }
                  }
               }
               else
               {
                  WriteLine($"Opening {file} failed");
                  WriteLine(exception);
               }
            }
         }
         else
         {
            WriteLine($"Couldn't retrieve files: {exception.Message}");
            return;
         }

         if (getFolders(folder).If(out var folders, out exception))
         {
            foreach (var subfolder in folders)
            {
               matchFolder(subfolder, indent + 3);
            }
         }
         else
         {
            WriteLine($"Couldn't retrieve folders: {exception.Message}");
         }
      }

      protected void matchFolderText(FolderName folder, int indent, Func<string, bool> unless)
      {
         var prefix = " ".Repeat(indent);

         if (Verbose)
         {
            WriteLine($"{prefix}Checking folder {folder}");
         }

         var wroteFolder = false;

         if (getFiles(folder).If(out var files, out var exception))
         {
            foreach (var file in files)
            {
               if (Verbose)
               {
                  WriteLine($"{prefix}{prefix}Checking {file.NameExtension}");
               }

               var wroteName = false;

               if (file.TryTo.Text.If(out var text, out exception))
               {
                  if (matcher.IsMatch(text, Pattern, IgnoreCase, Multiline) && !unless(text))
                  {
                     if (!wroteName)
                     {
                        if (!wroteFolder)
                        {
                           WriteLine($"{prefix}{folder}");
                           wroteFolder = true;
                        }

                        WriteLine($"{prefix}{prefix}|");
                        WriteLine($"{prefix}{prefix}|{file.NameExtension}");
                        wroteName = true;
                        fileCount++;
                     }

                     var builder = new StringBuilder();
                     foreach (var (substring, index, length) in matcher)
                     {
                        builder.Append("...<");
                        builder.Append(substring);
                        var restOfText = text.Drop(index + length).KeepUntil('\r', '\n');
                        lineCount++;
                        builder.Append(">");
                        builder.Append(restOfText);
                        WriteLine($"{prefix}{prefix}|{prefix}{truncFunc(builder.ToString())}");
                        builder.Clear();
                     }
                  }
               }
               else
               {
                  WriteLine($"Opening {file} failed");
                  WriteLine(exception);
               }
            }
         }
         else
         {
            WriteLine($"Couldn't retrieve files: {exception.Message}");
            return;
         }

         if (getFolders(folder).If(out var folders, out exception))
         {
            var unlessFunc = Unless.Map(p => func<string, bool>(text => IsMatch(text, p))).DefaultTo(() => _ => false);
            foreach (var subfolder in folders)
            {
               matchFolderText(subfolder, indent + 3, unlessFunc);
            }
         }
         else
         {
            WriteLine($"Couldn't retrieve folders: {exception.Message}");
         }
      }

      protected static IResult<FolderName[]> getFolders(FolderName folder) => folder.TryTo.Folders;

      protected IResult<FileName[]> getFiles(FolderName folder)
      {
         return folder.TryTo.Files.Map(fs => fs.Where(f => !excludes(f)).Where(f => includes(f)).ToArray());
      }

      protected void replaceText()
      {
         if (File.If(out var file))
         {
            if (Backup)
            {
               var backupFile = FileName.UniqueFileName(file.Folder, file);
               file.CopyTo(backupFile, true);
            }

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
               foreach (var line in file.Lines)
               {
                  var replacement = line.Substitute(Pattern, Replacement, IgnoreCase, Multiline, Friendly);
                  newLines.Add(line != replacement ? replacement : line);
               }
            }

            tempFile.SetText(newLines.ToString("\r\n"), file.Encoding);

            tempFile.CopyTo(file, true);
            tempFile.Delete();
         }
      }

      protected static IMaybe<string> substitute(string line, string pattern, string replacement, bool ignoreCase, bool multiline, bool friendly)
      {
         return maybe(line.IsMatch(pattern, ignoreCase, multiline, friendly), () => line.Substitute(pattern, replacement, ignoreCase, multiline, friendly));
      }

      protected void substitute()
      {
         var pattern = Pattern;
         var replacement = Replacement;
         var ignoreCase = IgnoreCase;
         var multiline = Multiline;
         var friendly = Friendly;

         while (true)
         {
            var line = ReadLine();
            if (line == null)
            {
               break;
            }
            else if (substitute(line, pattern, replacement, ignoreCase, multiline, friendly).If(out var substituted))
            {
               WriteLine(substituted);
            }
         }
      }

      public FileName CommandFile(string name) => $@"~\AppData\Local\fgrep\{name}.cli";
   }
}