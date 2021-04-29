using System;
using System.Collections.Generic;
using System.Linq;
using Core.Computers;
using Core.Monads;
using Core.RegularExpressions;
using Core.Strings;

namespace FGrep
{
   public class FilePattern
   {
      protected Func<string, bool> matches;
      protected Func<string, bool> unless;
      protected Func<FileName, bool> includeFile;
      protected Func<FileName, bool> excludeFile;

      public event EventHandler<FolderArgs> Folder;

      public FilePattern(string pattern, bool not, bool ignoreCase, bool multiline, IMaybe<string> _unless, string include, string includeExt,
         string exclude, string excludeExt, bool friendly)
      {
         if (not)
         {
            matches = line => !isMatch(line, pattern, ignoreCase, multiline, friendly);
         }
         else
         {
            matches = line => isMatch(line, pattern, ignoreCase, multiline, friendly);
         }

         unless = _unless.Map(unless => (Func<string, bool>)(line => !isMatch(line, unless, ignoreCase, multiline, friendly)))
            .DefaultTo(() => _ => false);

         if (include.IsNotEmpty())
         {
            includeFile = file => isMatch(file.NameExtension, include, ignoreCase, multiline, friendly);
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
            excludeFile = file => !isMatch(file.NameExtension, exclude, ignoreCase, multiline, friendly);
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

      protected static bool isMatch(string input, string pattern, bool ignoreCase, bool multiline, bool friendly)
      {
         return input.IsMatch(pattern, ignoreCase, multiline, friendly);
      }

      public IEnumerable<(FileName file, int lineNumber, string line)> FilesByLine(FolderName folder)
      {
         var lineMatched = false;

         foreach (var file in folder.Files.Where(f => includeFile(f) && !excludeFile(f)))
         {
            var lineNumber = 1;
            foreach (var line in file.Lines)
            {
               if (matches(line) && !unless(line))
               {
                  if (!lineMatched)
                  {
                     Folder?.Invoke(this, new FolderArgs(folder));
                     lineMatched = true;
                  }
                  yield return (file, lineNumber, line);
               }

               lineNumber++;
            }
         }

         foreach (var subfolder in folder.Folders)
         {
            foreach (var tuple in FilesByLine(subfolder))
            {
               yield return tuple;
            }
         }
      }

      public IEnumerable<string> FilesByLine(FolderName folder, Func<FileName, int, string, string> mapFunc)
      {
         foreach (var (file, lineNumber, line) in FilesByLine(folder))
         {
            yield return mapFunc(file, lineNumber, line);
         }
      }
   }
}