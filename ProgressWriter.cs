using System;
using System.Collections.Generic;
using Core.Strings;

namespace FGrep
{
   public class ProgressWriter
   {
      protected class TextColors
      {
         public TextColors(string text, ConsoleColor foregroundColor, ConsoleColor backgroundColor)
         {
            Text = text;
            ForegroundColor = foregroundColor;
            BackgroundColor = backgroundColor;
         }

         public string Text { get; }

         public ConsoleColor ForegroundColor { get; }

         public ConsoleColor BackgroundColor { get; }
      }

      protected object locker;
      protected int width;
      protected int leftWidth;
      protected int rightWidth;
      protected List<(string text, ConsoleColor foregroundColor, ConsoleColor backgroundColor)> log;
      protected int logStart;
      protected int logHeight;

      public ProgressWriter(object locker, int processorCount)
      {
         this.locker = locker;

         width = Console.LargestWindowWidth;
         leftWidth = width / 3;
         rightWidth = width - (leftWidth + 3);

         var text = $"{"-".Repeat(leftWidth + 1)}|{"-".Repeat(rightWidth)}";
         log = new List<(string, ConsoleColor, ConsoleColor)> { (text, ConsoleColor.Black, ConsoleColor.White) };
         logStart = processorCount + 1;
         logHeight = Console.LargestWindowHeight - logStart;

         Console.CursorVisible = false;
      }

      protected string formattedMessage(string left, string right)
      {
         var leftMessage = $" {left}".Exactly(leftWidth);
         var rightMessage = $" {right}".Exactly(rightWidth);

         return $"{leftMessage} | {rightMessage}";
      }

      protected void write(int line, string message, ConsoleColor foregroundColor, ConsoleColor backgroundColor)
      {
         lock (locker)
         {
            Console.SetCursorPosition(0, line);
            Console.ForegroundColor = foregroundColor;
            Console.BackgroundColor = backgroundColor;
            Console.Write(message);
         }
      }

      protected void writeToLog(string message, ConsoleColor foregroundColor, ConsoleColor backgroundColor)
      {
         var tuple = (message, foregroundColor, backgroundColor);

         if (log.Count >= logHeight)
         {
            for (var i = 2; i < log.Count; i++)
            {
               log[i - 1] = log[i];
            }

            log[log.Count - 1] = tuple;
         }
         else
         {
            log.Add(tuple);
         }

         for (var i = logStart; i < logHeight; i++)
         {
            var logItem = log[i];
            write(i, logItem.text, logItem.foregroundColor, logItem.backgroundColor);
         }
      }

      public void WriteProgress(int affinity, int count, int current)
      {

      }
   }
}