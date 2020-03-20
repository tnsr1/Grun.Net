﻿#region BSD 3-Clause License

// <copyright file="Program.cs" company="Edgerunner.org">
// Copyright 2020 Thaddeus Ryker
// </copyright>
// 
// BSD 3-Clause License
// 
// Copyright (c) 2020, Thaddeus Ryker
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 
// 1. Redistributions of source code must retain the above copyright notice, this
//    list of conditions and the following disclaimer.
// 
// 2. Redistributions in binary form must reproduce the above copyright notice,
//    this list of conditions and the following disclaimer in the documentation
//    and/or other materials provided with the distribution.
// 
// 3. Neither the name of the copyright holder nor the names of its
//    contributors may be used to endorse or promote products derived from
//    this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
// AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
// IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
// FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
// DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
// SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
// CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
// OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

using Antlr4.Runtime;
using Antlr4.Runtime.Misc;

using CommandLine;
using CommandLine.Text;

using Microsoft.Msagl.Drawing;
using Microsoft.Msagl.GraphViewerGdi;
using Microsoft.Msagl.Layout.Layered;

using Org.Edgerunner.ANTLR4.Tools.Graphing;
using Org.Edgerunner.ANTLR4.Tools.Graphing.Extensions;
using Org.Edgerunner.ANTLR4.Tools.Testing.Configuration;
using Org.Edgerunner.ANTLR4.Tools.Testing.Grammar;
using Org.Edgerunner.ANTLR4.Tools.Testing.GrunDotNet.Properties;
using Org.Edgerunner.ANTLR4.Tools.Testing.GrunWin;
using Org.Edgerunner.ANTLR4.Tools.Testing.GrunWin.Editor.SyntaxHighlighting;

using Parser = CommandLine.Parser;

// ReSharper disable RedundantNameQualifier
namespace Org.Edgerunner.ANTLR4.Tools.Testing.GrunDotNet
{
   /// <summary>
   /// Class that represents the entry point into the program.
   /// </summary>
   internal class Program
   {
      private static Settings _Settings;

      #region Static

      [STAThread]
      // ReSharper disable once MethodTooLong
      private static void Main(string[] args)
      {
         try
         {
            LoadApplicationSettings();

            var parser = new Parser(with => with.HelpWriter = null);
            var parserResult = parser.ParseArguments<Options>(args);
            parserResult
                      .WithParsed(o =>
                         {
                            var options = Grammar.ParseOption.None;
                            var loadGui = false;
                            var showParseTree = false;
                            var writeSvg = false;

                            if (o.Tokens) options |= Grammar.ParseOption.Tokens;
                            if (o.Diagnostics) options |= Grammar.ParseOption.Diagnostics;
                            if (o.Trace) options |= Grammar.ParseOption.Trace;
                            if (o.Tree)
                            {
                               options |= Grammar.ParseOption.Tree;
                               showParseTree = true;
                            }

                            if (!string.IsNullOrEmpty(o.SvgFileName))
                            {
                               writeSvg = true;
                               options |= Grammar.ParseOption.Tree;
                            }

                            if (o.Sll) options |= Grammar.ParseOption.Sll;
                            if (o.Gui)
                            {
                               loadGui = true;
                               options |= Grammar.ParseOption.Tree;
                            }

                            var workingDirectory = Environment.CurrentDirectory;
                            var scanner = new Grammar.Scanner();

                            var grammar = scanner.LocateGrammar(workingDirectory, o.GrammarName);
                            if (grammar == null)
                            {
                               Console.WriteLine(Resources.GrammarNotFoundErrorMessage, o.GrammarName);
                               return;
                            }

                            var data = string.Empty;

                            if (!string.IsNullOrEmpty(o.FileName))
                            {
                               var encodingToUse = !string.IsNullOrEmpty(o.EncodingName) ? Encoding.GetEncoding(o.EncodingName) : Encoding.Default;
                               using (var reader = new StreamReader(o.FileName, encodingToUse))
                                  data = reader.ReadToEnd();
                            }
                            else
                            {
                               var analyzer = new Analyzer();
                               var builder = new StringBuilder();
                               var currentLine = Console.CursorTop;
                               Console.WriteLine(Resources.ReadingFromStandardInputPromptMessage);
                               while (true)
                               {
                                  var typed = Console.ReadKey(true);

                                  if ((typed.Modifiers & ConsoleModifiers.Control) == ConsoleModifiers.Control
                                      && typed.Key == ConsoleKey.Z)
                                  {
                                     Console.Write("^Z");
                                     break;
                                  }

                                  if (typed.Key == ConsoleKey.Enter)
                                  {
                                     Console.WriteLine();
                                     builder.Append("\r\n");
                                  }
                                  else if (typed.Key == ConsoleKey.Backspace)
                                  {
                                     if (Console.CursorLeft > 0)
                                     {
                                        Console.Write(typed.KeyChar);
                                        Console.Write(' ');
                                        Console.Write(typed.KeyChar);
                                        builder.Remove(builder.Length - 1, 1);
                                     }
                                  }
                                  else
                                  {
                                     Console.Write(typed.KeyChar);
                                     builder.Append(typed.KeyChar);
                                  }

                                  var grammarParser = analyzer.BuildParserWithOptions(grammar, data, ParseOption.None);
                                  grammarParser.RemoveErrorListeners();
                                  analyzer.ExecuteParsing(grammarParser, o.RuleName);
                                  HighlightSyntaxInConsole(currentLine, analyzer, null);
                               }

                               Console.WriteLine();
                               data = builder.ToString();
                            }

                            // If tokens are the only option we've received, we don't need to parse
                            if (options == Grammar.ParseOption.Tokens)
                            {
                               DisplayTokens(grammar, data);
                               return;
                            }

                            // Now we attempt to parse, but still handle a lexer-only grammar.
                            if (grammar.Parser != null)
                            {
                               var analyzer = new Analyzer();
                               var grammarParser = analyzer.BuildParserWithOptions(grammar, data, options);
                               analyzer.ExecuteParsing(grammarParser, o.RuleName);

                               if (showParseTree)
                                  Console.WriteLine(analyzer.ParserContext.ToStringTree(grammarParser));

                               if (writeSvg)
                               {
                                  var rules = scanner.GetParserRulesForGrammarParser(grammar.Parser);
                                  var grapher = new ParseTreeGrapher()
                                  {
                                     BackgroundColor = _Settings.GraphNodeBackgroundColor.GetMsAglColor(),
                                     BorderColor = _Settings.GraphNodeBorderColor.GetMsAglColor(),
                                     TextColor = _Settings.GraphNodeTextColor.GetMsAglColor()
                                  };
                                  var graph = grapher.CreateGraph(analyzer.ParserContext, rules.ToList());
                                  graph.LayoutAlgorithmSettings = new SugiyamaLayoutSettings();
                                  GraphRenderer renderer = new GraphRenderer(graph);
                                  renderer.CalculateLayout();
                                  graph.EscapeNodesForSvg();
                                  SvgGraphWriter.Write(graph, o.SvgFileName, null, null, 4);
                               }
                            }
                            else
                            {
                               if (options.HasFlag(ParseOption.Tokens))
                                  DisplayTokens(grammar, data);

                               if (showParseTree || writeSvg)
                                  Console.WriteLine(Resources.GrammarHasNoParserErrorMessage, grammar.GrammarName);
                               if (showParseTree)
                                  Console.WriteLine(Resources.UnableToDisplayParseTree);
                               if (writeSvg)
                                  Console.WriteLine(Resources.SvgWritingAbortedErrorMessage);
                            }

                            if (loadGui)
                               LoadGui(data, grammar, o.RuleName);
                         })
                      .WithNotParsed(errs => DisplayHelp(parserResult, errs));

#if DEBUG
            Console.WriteLine(Resources.PressAnyKeyMessage);
            Console.ReadKey();
#endif
         }
         // ReSharper disable once CatchAllClause
         catch (Exception ex)
         {
            Console.WriteLine(ex.Message);
            Console.WriteLine(ex.StackTrace);
#if DEBUG
            Console.WriteLine(Resources.PressAnyKeyMessage);
            Console.ReadKey();
#endif
         }
      }

      private static void HighlightSyntaxInConsole(int lineOffset, Analyzer analyzer, IStyleRegistry registry)
      {
         return; 

         if (analyzer?.ParserContext == null)
            return;

         if (registry == null)
            return;

         var cursorRow = Console.CursorTop;
         var cursorColumn = Console.CursorLeft;
         foreach (var token in analyzer.DisplayTokens)
            ColorToken(token, lineOffset, registry);

         Console.SetCursorPosition(cursorColumn, cursorRow);
      }

      private static void ColorToken(SyntaxToken token, int lineOffset, IStyleRegistry registry)
      {
         var startLine = token.LineNumber + lineOffset;
         var endLine = token.EndingLineNumber + lineOffset;
         for (int ln = startLine; ln < endLine + 1; ln++)
         {
            for (int col = token.ColumnPosition; col < token.EndingColumnPosition + 1; col++)
            {
               Console.SetCursorPosition(col, ln);

               // TODO: add syntax highlight logic.
            }
         }
      }

      private static void DisplayTokens(GrammarReference grammar, string data)
      {
         var analyzer = new Grammar.Analyzer();
         var tokens = analyzer.Tokenize(grammar, data);
         foreach (var token in tokens)
            Console.WriteLine(token.ToString());
      }

      private static void LoadGui(string data, GrammarReference grammar, string parserRule)
      {
         {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var visualAnalyzer = new VisualAnalyzer();
            visualAnalyzer.SetSourceCode(data);
            visualAnalyzer.SetGrammar(grammar);
            if (grammar.Parser != null || !parserRule.Equals("tokens", StringComparison.InvariantCultureIgnoreCase))
               visualAnalyzer.SetDefaultParserRule(parserRule);
            Application.Run(visualAnalyzer);
         }
      }

      /// <summary>
      /// Displays the help message.
      /// </summary>
      /// <typeparam name="T">The option type</typeparam>
      /// <param name="result">The command line parser result.</param>
      /// <param name="errors">The parser errors.</param>
      // ReSharper disable once UnusedParameter.Local
      private static void DisplayHelp<T>([NotNull] ParserResult<T> result, IEnumerable<Error> errors)
      {
         if (result is null) throw new ArgumentNullException(nameof(result));

         var helpText = HelpText.AutoBuild(
            result,
            h =>
               {
                  // ReSharper disable StringLiteralTypo
                  h.AddPostOptionsLine("Use startRuleName = 'tokens' if GrammarName is a lexer grammar.");
                  h.AddPostOptionsLine("Omitting Input Filename makes Grun.Net read from stdin.");
                  // ReSharper restore StringLiteralTypo
                  return HelpText.DefaultParsingErrorsHandler(result, h);
               },
            e => e);

         Console.WriteLine(helpText);
      }

      private static void LoadApplicationSettings()
      {
         _Settings = new Settings();
         var pathRoot = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
         if (pathRoot != null)
         {
            var configFile = Path.Combine(pathRoot, Resources.AppconfigFile);
            if (File.Exists(configFile))
            {
               _Settings.LoadFrom(configFile);
               return;
            }
         }

         _Settings.LoadDefaults();
      }

      #endregion
   }
}