using System;
using System.Linq;
using Core.Assertions;
using Core.Computers;
using Core.Monads;
using Core.Matching;
using Core.Strings;
using Core.Threading;

namespace FGrep
{
   internal class MultiFinder
   {
      public static IResult<MultiFinder> FromFolder(FolderName folder, Program program)
      {
         return folder.Must().Exist().OrFailure().Map(f =>
         {
            var pattern = program.Pattern;
            var multiThreaded = program.Threaded;

            Func<FileName, bool> doesInclude;
            Func<FileName, bool> doesExclude;

            if (program.Include.IsNotEmpty())
            {
               doesInclude = incF => incF.NameExtension.IsMatch(program.Include);
            }
            else if (program.IncludeExt.IsNotEmpty())
            {
               doesInclude = incF => incF.Extension.EndsWith(program.IncludeExt);
            }
            else
            {
               doesInclude = _ => true;
            }

            if (program.Exclude.IsNotEmpty())
            {
               doesExclude = excF => !excF.NameExtension.IsMatch(program.Exclude);
            }
            else if (program.ExcludeExt.IsNotEmpty())
            {
               doesExclude = excF => !excF.Extension.EndsWith(program.ExcludeExt);
            }
            else
            {
               doesExclude = _ => false;
            }

            bool including(FileName includingF) => doesInclude(includingF) && doesExclude(includingF);

            return new MultiFinder(f, pattern, multiThreaded, including);
         });
      }

      protected FolderName startingFolder;
      protected Pattern pattern;
      protected Func<FileName, bool> including;
      protected JobPool jobPool;
      protected object locker;
      protected ProgressWriter writer;

      public MultiFinder(FolderName startingFolder, Pattern pattern, bool multiThreaded, Func<FileName, bool> including)
      {
         this.startingFolder = startingFolder;
         this.pattern = pattern;
         this.including = including;

         jobPool = new JobPool(multiThreaded);
         locker = new object();
         writer = new ProgressWriter(locker, jobPool.ProcessorCount);
      }

      public void Evaluate()
      {
         overFolder(startingFolder);

         jobPool.Dispatch();
      }

      protected void overFolder(FolderName folder)
      {
         foreach (var file in folder.Files.Where(including))
         {
            jobPool.Enqueue(affinity => overFile(affinity, file));
         }

         foreach (var subFolder in folder.Folders)
         {
            overFolder(subFolder);
         }
      }

      protected void overFile(int affinity, FileName file)
      {
         var lines = file.Lines;

         foreach (var line in file.Lines)
         {
            
         }
      }
   }
}