using System;
using System.Collections.Generic;
using System.Linq;
using Core.Computers;
using Core.Monads;
using Core.Matching;
using Core.Strings;
using static Core.Monads.AttemptFunctions;
using static Core.Monads.MonadFunctions;

namespace FGrep
{
   public class Finder
   {
      protected Func<string, bool> matches;
      protected Func<string, IMatched<Result>> getMatcher;
      protected Func<string, bool> unless;
      protected Func<FileName, bool> includeFile;
      protected Func<FileName, bool> excludeFile;

      public event EventHandler<FolderArgs> FolderMatched;
      public event EventHandler<FileArgs> FileMatched;

      public Finder(Pattern pattern, bool not, IMaybe<string> _unless, string include, string includeExt, string exclude, string excludeExt)
      {
         if (not)
         {
            matches = line => !line.IsMatch(pattern);
         }
         else
         {
            matches = line => line.IsMatch(pattern);
         }

         if (not)
         {
            getMatcher = _ => notMatched<Result>();
         }
         else
         {
            getMatcher = pattern.MatchedBy;
         }

         unless = _unless.Map(unless => (Func<string, bool>)(line => !line.IsMatch(unless))).DefaultTo(() => _ => false);

         if (include.IsNotEmpty())
         {
            includeFile = file => file.NameExtension.IsMatch(include);
         }
         else if (includeExt.IsNotEmpty())
         {
            includeFile = file => file.Extension.EndsWith(includeExt);
         }
         else
         {
            includeFile = _ => true;
         }

         if (exclude.IsNotEmpty())
         {
            excludeFile = file => !file.NameExtension.IsMatch(include);
         }
         else if (excludeExt.IsNotEmpty())
         {
            excludeFile = file => !file.Extension.EndsWith(excludeExt);
         }
         else
         {
            excludeFile = _ => false;
         }
      }

      public bool MatchedLine(string line) => matches(line) && !unless(line);

      public IMaybe<FindResult> FindResult(string line)
      {
         if (getMatcher(line).If(out var matcher))
         {
            return new FindResult { Line = line, Result = matcher }.Some();
         }
         else
         {
            return none<FindResult>();
         }
      }

      public bool MatchedFile(FileName file) => includeFile(file) && !excludeFile(file);

      public IEnumerable<FindResult> FileLines(FileName file)
      {
         var lineNumber = 1;
         var lines = file.Lines;
         var lineCount = lines.Length.Some();

         foreach (var line in lines)
         {
            if (getMatcher(line).If(out var matcher))
            {
               yield return new FindResult { LineNumber = lineNumber, Line = line, LineCount = lineCount, Result = matcher };

               if (lineCount.IsSome)
               {
                  lineCount = none<int>();
               }
            }

            lineNumber++;
         }
      }

      public IResult<IEnumerable<FindResult>> GetFileLines(FileName file) => tryTo(() => FileLines(file));

      public IEnumerable<FindResult> FilesByLine(FolderName folder, int indentLevel = 0)
      {
         var lineMatched = false;

         foreach (var file in folder.Files.Where(MatchedFile))
         {
            foreach (var result in FileLines(file))
            {
               if (!lineMatched)
               {
                  FolderMatched?.Invoke(this, new FolderArgs(folder));
               }

               var clone = result.Clone();
               clone.File = file;
               clone.IndentLevel = indentLevel;

               yield return clone;
            }

            FileMatched?.Invoke(this, new FileArgs(file));
         }

         foreach (var subfolder in folder.Folders)
         {
            foreach (var result in FilesByLine(subfolder, indentLevel + 1))
            {
               yield return result;
            }
         }
      }

      public IResult<IEnumerable<FindResult>> GetFilesByLine(FolderName folder, int indentLevel = 0) => tryTo(() => FilesByLine(folder, indentLevel));

      public IEnumerable<string> FilesByLine(FolderName folder, Func<FindResult, string> mapFunc, int indentLevel = 0)
      {
         var lineMatched = false;

         foreach (var file in folder.Files.Where(MatchedFile))
         {
            foreach (var result in FileLines(file))
            {
               if (!lineMatched)
               {
                  FolderMatched?.Invoke(this, new FolderArgs(folder));
               }

               var clone = result.Clone();
               clone.File = file;
               clone.IndentLevel = indentLevel;

               yield return mapFunc(clone);
            }

            FileMatched?.Invoke(this, new FileArgs(file));
         }

         foreach (var subfolder in folder.Folders)
         {
            foreach (var result in FilesByLine(subfolder, mapFunc, indentLevel + 1))
            {
               yield return result;
            }
         }
      }

      public IResult<IEnumerable<string>> GetFilesByLine(FolderName folder, Func<FindResult, string> mapFunc, int indentLevel = 0)
      {
         return tryTo(() => FilesByLine(folder, mapFunc, indentLevel));
      }

      public IEnumerable<FindResult> MatchedFiles(FolderName sourceFolder, int indentLevel = 0)
      {
         var _folder = none<FolderName>();
         foreach (var file in sourceFolder.Files.Where(MatchedFile))
         {
            if (_folder.If(out var folder))
            {
               if (folder == file.Folder)
               {
                  yield return new FindResult { FileNameExtension = file.NameExtension, IndentLevel = indentLevel };
               }
               else
               {
                  _folder = file.Folder.Some();
                  yield return new FindResult { Folder = _folder, FileNameExtension = file.NameExtension, IndentLevel = indentLevel };

                  FolderMatched?.Invoke(this, new FolderArgs(file.Folder));
               }
            }

            FileMatched?.Invoke(this, new FileArgs(file));
         }

         foreach (var subfolder in sourceFolder.Folders)
         {
            foreach (var result in MatchedFiles(subfolder, indentLevel + 1))
            {
               yield return result;
            }
         }
      }

      public IResult<IEnumerable<FindResult>> GetMatchedFiles(FolderName sourceFolder, int indentLevel = 0)
      {
         return tryTo(() => MatchedFiles(sourceFolder, indentLevel));
      }
   }
}