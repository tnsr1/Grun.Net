﻿#region BSD 3-Clause License

// <copyright file="VisualAnalyzer.cs" company="Edgerunner.org">
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

using BrightIdeasSoftware;

using FastColoredTextBoxNS;

using JetBrains.Annotations;

using Microsoft.Msagl.Drawing;
using Microsoft.Msagl.GraphViewerGdi;
using Microsoft.Msagl.Layout.Layered;

using Org.Edgerunner.ANTLR4.Tools.Common;
using Org.Edgerunner.ANTLR4.Tools.Graphing;
using Org.Edgerunner.ANTLR4.Tools.Testing.Grammar;
using Org.Edgerunner.ANTLR4.Tools.Testing.Grammar.Errors;
using Org.Edgerunner.ANTLR4.Tools.Testing.GrunWin.Dialogs;
using Org.Edgerunner.ANTLR4.Tools.Testing.GrunWin.Editor;
using Org.Edgerunner.ANTLR4.Tools.Testing.GrunWin.Editor.SyntaxHighlighting;
using Org.Edgerunner.ANTLR4.Tools.Testing.GrunWin.Graphing;
using Org.Edgerunner.ANTLR4.Tools.Testing.GrunWin.Properties;
using Org.Edgerunner.ANTLR4.Tools.Testing.GrunWin.Tracing;

using Place = FastColoredTextBoxNS.Place;

namespace Org.Edgerunner.ANTLR4.Tools.Testing.GrunWin
{
   /// <summary>
   ///    Class that represents an ANTLR grammar test Analyzer.
   ///    Implements the <see cref="System.Windows.Forms.Form" />
   /// </summary>
   /// <seealso cref="System.Windows.Forms.Form" />
   public partial class VisualAnalyzer : Form
   {
      private GrammarReference _Grammar;

      private IEditorGuide _EditorGuide;

      private IStyleRegistry _Registry;

      private EditorSyntaxHighlighter _Highlighter = new EditorSyntaxHighlighter();

      private IGraphWorker _GraphWorker;

      private IParseTreeGrapher _Grapher;

      private List<string> _ParserRules;

      private GViewer _Viewer;

      private IList<SyntaxToken> _Tokens;

      private List<ParseMessage> _ParseErrors;

      private EditorSettings _Settings;

      #region Constructors And Finalizers

      /// <summary>
      ///    Initializes a new instance of the <see cref="VisualAnalyzer" /> class.
      /// </summary>
      public VisualAnalyzer()
      {
         InitializeComponent();
      }

      #endregion

      /// <summary>
      ///    Gets or sets a value indicating whether to parse with diagnostics enabled.
      /// </summary>
      /// <value><c>true</c> if diagnostic parsing is enabled; otherwise, <c>false</c>.</value>
      /// <remarks>If SLL is also enabled, SLL will supersede diagnostic mode.</remarks>
      public bool ParseWithDiagnostics
      {
         get => DiagnosticsToolStripMenuItem.Checked;
         set
         {
            DiagnosticsToolStripMenuItem.Checked = value;
            ParseSource();
         }
      }

      /// <summary>
      ///    Gets or sets a value indicating whether to parse with Simple LL mode.
      /// </summary>
      /// <value><c>true</c> if SLL parsing mode is enabled; otherwise, <c>false</c>.</value>
      /// <remarks>If Diagnostics are also enabled, SLL will supersede diagnostic mode.</remarks>
      public bool ParseWithSllMode
      {
         get => SimpleLLModeToolStripMenuItem.Checked;
         set
         {
            SimpleLLModeToolStripMenuItem.Checked = value;
            ParseSource();
         }
      }

      /// <summary>
      ///    Gets or sets a value indicating whether to parse with tracing enabled.
      /// </summary>
      /// <value><c>true</c> if tracing is enabled; otherwise, <c>false</c>.</value>
      public bool ParseWithTracing
      {
         get => TracingToolStripMenuItem.Checked;
         set
         {
            TracingToolStripMenuItem.Checked = value;
            ParseSource();
         }
      }

      /// <summary>
      ///    Gets or sets a value indicating whether to use heuristic syntax highlighting.
      /// </summary>
      /// <value><c>true</c> if heuristic syntax highlighting is enabled; otherwise, <c>false</c>.</value>
      public bool UseHeuristicSyntaxHighlighting
      {
         get => HeuristicHighlightingtToolStripMenuItem.Checked;
         set
         {
            HeuristicHighlightingtToolStripMenuItem.Checked = value;
            ParseSource();
         }
      }

      /// <summary>
      ///    Parses the source code.
      /// </summary>
      /// <exception cref="T:Org.Edgerunner.ANTLR4.Tools.Testing.Exceptions.GrammarException">
      /// No parser found for the current grammar
      /// OR
      /// Selected parser rule does not exist for the current grammar.
      /// </exception>
      /// <exception cref="T:System.ArgumentNullException">Selected parser rule is null or empty.</exception>
      public void ParseSource()
      {
         if (_Grammar == null)
            return;

         if (_ParserRules.Count == 0)
            return;

         if (string.IsNullOrEmpty(CmbRules.SelectedItem?.ToString()))
            return;

         var errorListener = new TestingErrorListener();
         var analyzer = new Analyzer();
         var options = ParseOption.Tree;
         GuiTraceListener parseTreeListener = null;
         try
         {
            if (ParseWithDiagnostics) options |= ParseOption.Diagnostics;
            if (ParseWithSllMode) options |= ParseOption.Sll;
            var parser = analyzer.BuildParserWithOptions(_Grammar, CodeEditor.Text, options);
            if (ParseWithTracing)
            {
               parseTreeListener = new GuiTraceListener(parser);
               parser.AddParseListener(parseTreeListener);
            }

            parser.RemoveErrorListeners();
            parser.AddErrorListener(errorListener);
            analyzer.ExecuteParsing(parser, CmbRules.SelectedItem.ToString());
         }
         catch (Exception ex)
         {
            var errorDisplay = new ErrorDisplay
            {
               Text = Resources.SourceParseErrorTitle,
               ErrorMessage = ex.Message,
               ErrorStackTrace = ex.StackTrace
            };
            errorDisplay.ShowDialog();
         }

         try
         {
            _GraphWorker?.Graph(_Grapher, analyzer.ParseContext, _ParserRules);
         }
         catch (Exception ex)
         {
            var errorDisplay = new ErrorDisplay
            {
               Text = Resources.GraphQueueErrorTitle,
               ErrorMessage = ex.Message,
               ErrorStackTrace = ex.StackTrace
            };
            errorDisplay.ShowDialog();
         }

         _Tokens = analyzer.DisplayTokens;
         _ParseErrors = errorListener.Errors;
         PopulateTokens(analyzer.DisplayTokens);
         PopulateParserMessages(_ParseErrors);
         PopulateTraceEvents(parseTreeListener);
      }

      private void PopulateTraceEvents(GuiTraceListener parseTreeListener)
      {
         if (parseTreeListener == null)
            TraceListView.SetObjects(null);
         else
            TraceListView.SetObjects(parseTreeListener.Events);
      }

      /// <summary>
      ///    Sets the default parser rule.
      /// </summary>
      /// <param name="rule">The parser rule.</param>
      /// <exception cref="T:System.ArgumentException">Invalid parser rule.</exception>
      /// <exception cref="T:System.ArgumentNullException"><paramref name="rule" /> is <see langword="null" /></exception>
      public void SetDefaultParserRule(string rule)
      {
         if (string.IsNullOrEmpty(rule)) throw new ArgumentNullException(nameof(rule));

         if (!_ParserRules.Contains(rule))
            throw new ArgumentException(string.Format(Resources.InvalidParserRule, rule), nameof(rule));

         CmbRules.SelectedIndex = CmbRules.FindStringExact(rule);
      }

      /// <summary>
      ///    Sets the grammar to parse.
      /// </summary>
      /// <param name="grammar">The grammar.</param>
      public void SetGrammar([NotNull] GrammarReference grammar)
      {
         if (grammar is null)
            throw new ArgumentNullException(nameof(grammar));

         var scanner = new Scanner();
         _Grammar = grammar;

         _ParserRules = scanner.GetParserRulesForGrammar(grammar).ToList();
         LoadParserRules();
         stripLabelGrammarName.Text = grammar.GrammarName;

         // Now try to load an IEditorGuide instance for the specified Grammar
         LoadEditorGuide(grammar);
         if (_EditorGuide != null)
            _Registry = new StyleRegistry(_EditorGuide);
         else
            _Registry = new HeuristicStyleRegistry();
      }

      /// <summary>
      ///    Sets the source code to be analyzed.
      /// </summary>
      /// <param name="code">The code.</param>
      public void SetSourceCode(string code)
      {
         CodeEditor.Text = code;
         CodeEditor.ClearStyle(StyleIndex.All);
         ColorizeTokens(null);
         ColorizeErrors(null);
      }

      private void _Viewer_Click(object sender, EventArgs e)
      {
         if (_Viewer.SelectedObject is Node node)
         {
            CodeEditor.SelectSource(node.UserData as ITree ?? throw new InvalidOperationException());
            CodeEditor.Focus();
         }
      }

      private void AddTreeBranchesAndLeaves(TreeNode treeNode, ITree tree)
      {
         for (var i = 0; i < tree.ChildCount; i++)
         {
            var child = tree.GetChild(i);
            var newNode =
               new TreeNode(Trees.GetNodeText(child, _ParserRules))
               {
                  Tag = child,
                  Name = child.GetHashCode().ToString()
               };
            treeNode.Nodes.Add(newNode);
            AddTreeBranchesAndLeaves(newNode, child);
         }
      }

      private void RenderParseTreeGraph(ITree tree, int? zoomFactor = null)
      {
         if (_Viewer == null)
            return;

         if (tree == null)
            return;

         if (_Grapher == null)
            return;

         try
         {
            var graph = _Grapher.CreateGraph(tree, _ParserRules);
            graph.LayoutAlgorithmSettings = new SugiyamaLayoutSettings();
            _Viewer.SuspendLayout();
            _Viewer.Graph = graph;
            if (zoomFactor.HasValue)
               GraphZoomTrackBar.Value = zoomFactor.Value;
            _Viewer.ZoomF = GraphZoomTrackBar.Value;
            _Viewer.ResumeLayout();
         }
         catch (Exception ex)
         {
            var errorDisplay = new ErrorDisplay
            {
               Text = Resources.GraphRenderErrorTitle,
               ErrorMessage = ex.Message,
               ErrorStackTrace = ex.StackTrace
            };
            errorDisplay.ShowDialog();
         }
      }

      private void RenderParseTreeGraph(Graph graph, int? zoomFactor = null)
      {
         if (_Viewer == null)
            return;

         if (graph == null)
            return;

         if (_Grapher == null)
            return;

         _Viewer.SuspendLayout();
         _Viewer.Graph = graph;
         if (zoomFactor.HasValue)
            GraphZoomTrackBar.Value = zoomFactor.Value;
         _Viewer.ZoomF = GraphZoomTrackBar.Value;
         _Viewer.ResumeLayout();
      }

      private void BuildParseTreeTreeViewGuide(ITree tree)
      {
         ParseTreeView.SuspendLayout();
         ParseTreeView.Nodes.Clear();

         if (tree == null)
         {
            ParseTreeView.ResumeLayout();
            return;
         }

         var treeNode =
            new TreeNode(Trees.GetNodeText(tree, _ParserRules)) { Tag = tree, Name = tree.GetHashCode().ToString() };
         ParseTreeView.Nodes.Add(treeNode);
         AddTreeBranchesAndLeaves(treeNode, tree);

         ParseTreeView.ResumeLayout();
      }

      private void LoadEditorGuide([NotNull] GrammarReference grammar)
      {
         if (grammar == null)
            throw new ArgumentNullException(nameof(grammar));

         _EditorGuide = null;

         var scanner = new Scanner();
         var loader = new Loader();

         // First try loading a guide from the target assembly's directory
         var pathRoot = Path.GetDirectoryName(grammar.AssemblyPath);
         if (LoadGuideFromPath(grammar, scanner, loader, pathRoot))
            return;

         // Now try loading a guide from the Grun.Net Guides folder
         pathRoot = Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase);
         // ReSharper disable once AssignNullToNotNullAttribute
         pathRoot = Path.Combine(pathRoot, "Guides");
         if (Directory.Exists(pathRoot))
            LoadGuideFromPath(grammar, scanner, loader, pathRoot);
      }

      private bool LoadGuideFromPath(GrammarReference grammar, Scanner scanner, Loader loader, string pathRoot)
      {
         var guideReferences = scanner.LocateAllEditorGuides(pathRoot ?? throw new InvalidOperationException());
         foreach (var reference in guideReferences)
         {
            var guide = loader.LoadEditorGuide(reference);
            if (guide != null && guide.GrammarName == grammar.GrammarName)
            {
               _EditorGuide = guide;
               return true;
            }
         }

         return false;
      }

      private void ParserRulesCombo_SelectedIndexChanged(object sender, EventArgs e)
      {
         if (CmbRules.Items.Count > 0)
            ParseSource();
      }

      private void CodeEditor_TextChanged(object sender, TextChangedEventArgs e)
      {
         ParseSource();
         //CodeEditor.ClearStylesBuffer();
         

      }

      private void ColorizeErrors(Range range)
      {
         if (_Registry == null)
            return;

         CodeEditor.BeginUpdate();
         try
         {
            foreach (var error in _ParseErrors)
            {
               var token = error.Token;
               var startingPlace = new Place(token.Column, token.Line - 1);
               var stoppingPlace = new Place(token.Column + token.Text.Length, token.Line - 1);
               var tokenRange = CodeEditor.GetRange(startingPlace, stoppingPlace);
               tokenRange.SetStyle(_Registry.GetParseErrorStyle());
            }
         }
         catch (Exception ex)
         {
            var errorDisplay = new ErrorDisplay
            {
               Text = Resources.SyntaxErrorColoringErrorTitle,
               ErrorMessage = ex.Message,
               ErrorStackTrace = ex.StackTrace
            };
            errorDisplay.ShowDialog();
         }
         finally
         {
            CodeEditor.EndUpdate();
         }
      }

      private void ColorizeTokens(Range range)
      {
         if (_Registry == null)
            return;

         var tokensToColor = range == null ? _Tokens : FindTokensInRange(_Tokens, range);
         _Highlighter.ColorizeTokens(CodeEditor, _Registry, tokensToColor);
      }

      private IList<SyntaxToken> FindTokensInRange(IList<SyntaxToken> tokens, Range range)
      {
         var results = new List<SyntaxToken>();
         var startLine = range.FromLine;
         var stopLine = range.ToLine + 2;

         foreach (var token in tokens)
         {
            if (token.LineNumber < startLine || token.LineNumber > stopLine)
               continue;

            results.Add(token);
         }

         return results;
      }

      private void ExitToolStripMenuItem_Click(object sender, EventArgs e)
      {
         Application.Exit();
      }

      private void GraphZoomTrackBar_ValueChanged(object sender, EventArgs e)
      {
         if (_Viewer != null)
            _Viewer.ZoomF = GraphZoomTrackBar.Value * 0.1 + 1.0;
      }

      private void InitializeGraphCanvas()
      {
         SuspendLayout();
         _Viewer = new GViewer();
         PnlGraph.Controls.Add(_Viewer);
         _Viewer.Dock = DockStyle.Fill;
         ResumeLayout();
         _Viewer.LayoutAlgorithmSettingsButtonVisible = false;
         _Viewer.Click += _Viewer_Click;
         _Viewer.MouseWheel += _Viewer_MouseWheel;
         var viewerContextMenu = new ContextMenu();
         var menuItem = viewerContextMenu.MenuItems.Add("Graph from here");
         menuItem.Click += Menu_GraphFromHere_Click;
         _Viewer.ContextMenu = viewerContextMenu;
      }

      private void _Viewer_MouseWheel(object sender, MouseEventArgs e)
      {
         var factor = Math.Max(Math.Min((int)Math.Round((_Viewer.ZoomF - 1.0) / 0.1), GraphZoomTrackBar.Maximum), GraphZoomTrackBar.Minimum);
         GraphZoomTrackBar.Value = factor;
      }

      private void LoadGrammarToolStripMenuItem_Click(object sender, EventArgs e)
      {
         openFileDialog.InitialDirectory = Environment.CurrentDirectory;
         openFileDialog.Filter = Resources.AssemblyFileFilter;
         openFileDialog.DefaultExt = "dll";

         if (openFileDialog.ShowDialog() == DialogResult.Cancel)
            return;

         var fileToSearch = openFileDialog.FileName;
         LoadGrammarInternal(fileToSearch);
      }

      private void LoadGrammarInternal(string fileToSearch)
      {
         var scanner = new Scanner();
         IEnumerable<GrammarReference> grammars;
         try
         {
            grammars = scanner.LocateAllGrammarsInFile(fileToSearch);
         }
         catch (Exception ex)
         {
            var errorDisplay = new ErrorDisplay
                                  {
                                     Text = Resources.GrammarLoadErrorTitle,
                                     ErrorMessage = ex.Message,
                                     ErrorStackTrace = ex.StackTrace
                                  };
            errorDisplay.ShowDialog();
            return;
         }

         var selectableGrammars = grammars as GrammarReference[] ?? grammars.ToArray();
         var grammarCount = selectableGrammars.Length;
         GrammarReference grammarToLoad;

         switch (grammarCount)
         {
            case 0:
               MessageBox.Show(string.Format(Resources.NoGrammarsFoundInAssembly, Path.GetFileName(fileToSearch)));
               return;
            case 1:
               grammarToLoad = selectableGrammars.First();
               break;
            default:
               {
                  var selector = new GrammarSelector { GrammarsToSelectFrom = selectableGrammars };
                  if (selector.ShowDialog() == DialogResult.Cancel)
                     return;

                  grammarToLoad = selector.SelectedGrammar;
                  break;
               }
         }

         SetGrammar(grammarToLoad);
      }

      private void LoadParserRules()
      {
         CmbRules.DataSource = _ParserRules.OrderBy(x => x).Distinct().ToList();
         CmbRules.Refresh();
      }

      private void LoadSourceToolStripMenuItem_Click(object sender, EventArgs e)
      {
         openFileDialog.InitialDirectory = Environment.CurrentDirectory;
         openFileDialog.Filter = Resources.AllFilesFilter;

         if (openFileDialog.ShowDialog() == DialogResult.Cancel)
            return;

         var fileToLoad = openFileDialog.FileName;
         LoadSourceFileInternal(fileToLoad);
      }

      private void LoadSourceFileInternal(string fileToLoad)
      {
         try
         {
            using (var reader = new StreamReader(fileToLoad))
            {
               SetSourceCode(reader.ReadToEnd());
            }
         }
         catch (ArgumentException)
         {
            return;
         }
         catch (FileNotFoundException)
         {
            MessageBox.Show(string.Format(Resources.FileNotFoundMessage, fileToLoad));
         }
         catch (DirectoryNotFoundException)
         {
            MessageBox.Show(Resources.FileLoadFailedMessage);
         }
         catch (IOException)
         {
            MessageBox.Show(Resources.FileLoadFailedMessage);
         }
      }

      private void Menu_GraphFromHere_Click(object sender, EventArgs e)
      {
         if (_Viewer.SelectedObject is Node node)
         {
            var treeNodes = ParseTreeView.Nodes.Find(node.UserData?.GetHashCode().ToString(), true);
            if (treeNodes.Length != 0)
            {
               var workingNode = treeNodes.First();
               ParseTreeView.SelectedNode = workingNode;
               RenderParseTreeGraph(workingNode.Tag as ITree, 1);
               ParseTreeView.Focus();
            }
         }
      }

      private void ParseMessageListView_Click(object sender, EventArgs e)
      {
         OLVListItem selected;
         if ((selected = ParseMessageListView.SelectedItem) != null)
            if (selected.RowObject != null)
            {
               var message = (ParseMessage)selected.RowObject;
               CodeEditor.SelectSource(message.Token);
               CodeEditor.Focus();
            }
      }

      private void ParseTreeView_AfterSelect(object sender, TreeViewEventArgs e)
      {
         // Now we graph and display just the selected branch
         if (e.Node.Tag is ITree selected)
         {
            RenderParseTreeGraph(selected);
            CodeEditor.SelectSource(selected);
         }
      }

      private void PopulateParserMessages(List<ParseMessage> listenerErrors)
      {
         ParseMessageListView.SetObjects(listenerErrors);
      }

      private void PopulateTokens(IList<SyntaxToken> tokens)
      {
         tokenListView.SetObjects(tokens);
      }

      private void TokenListView_Click(object sender, EventArgs e)
      {
         OLVListItem selected;
         if ((selected = tokenListView.SelectedItem) != null)
            if (selected.RowObject != null)
            {
               var tokenView = (SyntaxToken)selected.RowObject;
               CodeEditor.SelectSource(tokenView.ActualParserToken);
            }
      }

      private void VisualAnalyzer_Load(object sender, EventArgs e)
      {
         LoadApplicationSettings();
         InitializeGraphCanvas();
         ConfigureGraphWorker();

         // Handle initial parse and coloring on load
         ParseSource();
         ColorizeTokens(null);
         ColorizeErrors(null);
      }

      private void ConfigureGraphWorker()
      {
         _GraphWorker = new GraphWorker(SynchronizationContext.Current, _Settings);
         _GraphWorker.GraphingFinished += _GraphWorker_GraphingFinished;
         _Grapher = new ParseTreeGrapher
         {
            BackgroundColor = Color.LightBlue,
            BorderColor = Color.Black,
            TextColor = Color.Black
         };
      }

      private void _GraphWorker_GraphingFinished(object sender, GraphingResult e)
      {
         BuildParseTreeTreeViewGuide(e.ParseTree);
         RenderParseTreeGraph(e.Graph);
      }

      private void LoadApplicationSettings()
      {
         var appSettings = System.Configuration.ConfigurationManager.AppSettings;

         _Settings = new EditorSettings();

         // Fetch NodeThresholdCountForThrottling setting
         string result = appSettings["NodeThresholdCountForThrottling"] ?? string.Empty;
         _Settings.NodeThresholdCountForThrottling = !int.TryParse(result, out var settingValue) ? 50 : settingValue;

         // Fetch MillisecondsToDelayPerNodeWhenThrottling setting
         result = appSettings["MillisecondsToDelayPerNodeWhenThrottling"] ?? string.Empty;
         _Settings.MillisecondsToDelayPerNodeWhenThrottling = !int.TryParse(result, out settingValue) ? 5 : settingValue;

         // Fetch MaximumRenderShortDelay setting
         result = appSettings["MaximumRenderShortDelay"] ?? string.Empty;
         _Settings.MaximumRenderShortDelay = !int.TryParse(result, out settingValue) ? 1000 : settingValue;

         // Fetch MinimumRenderCountToTriggerLongDelay setting
         result = appSettings["MinimumRenderCountToTriggerLongDelay"] ?? string.Empty;
         _Settings.MinimumRenderCountToTriggerLongDelay = !int.TryParse(result, out settingValue) ? 10 : settingValue;
      }

      private void HeuristicHighlightingtToolStripMenuItem_Click(object sender, EventArgs e)
      {
         if (HeuristicHighlightingtToolStripMenuItem.Checked)
         {
            ColorizeTokens(null);
            ColorizeErrors(null);
         }
         else
            CodeEditor.ClearStyle(StyleIndex.All);
      }

      private void TracingToolStripMenuItem_Click(object sender, EventArgs e)
      {
         ParseSource();
      }

      private void DiagnosticsToolStripMenuItem_Click(object sender, EventArgs e)
      {
         ParseSource();
      }

      private void SimpleLLModeToolStripMenuItem_Click(object sender, EventArgs e)
      {
         ParseSource();
      }

      private void TraceListView_BeforeSorting(object sender, BeforeSortingEventArgs e)
      {
         e.Canceled = true;
      }

      private void SelectTokenToolStripMenuItem_Click(object sender, EventArgs e)
      {
         OLVListItem selected;
         if ((selected = TraceListView.SelectedItem) != null)
            if (selected.RowObject != null)
            {
               var traceEvent = (TraceEvent)selected.RowObject;
               CodeEditor.SelectSource(traceEvent.Token);
               var model = (from token in _Tokens where token.ActualParserToken == traceEvent.Token select token).First();
               tokenListView.SelectObject(model);
            }
      }

      private void SelectParserRuleToolStripMenuItem_Click(object sender, EventArgs e)
      {
         var graph = _Viewer?.Graph;
         if (graph != null)
         {
            OLVListItem selected;
            if ((selected = TraceListView.SelectedItem) != null)
               if (selected.RowObject != null)
               {
                  var traceEvent = (TraceEvent)selected.RowObject;
                  CodeEditor.SelectSource(traceEvent.ParserRuleContext);
               }
         }
      }

      private void CodeEditor_DragEnter(object sender, DragEventArgs e)
      {
         if (e.Data.GetDataPresent(DataFormats.UnicodeText) || e.Data.GetDataPresent(DataFormats.Text) || e.Data.GetDataPresent(DataFormats.FileDrop))
            e.Effect = DragDropEffects.Copy;
         else
            e.Effect = DragDropEffects.None;
      }

      private void CodeEditor_DragDrop(object sender, DragEventArgs e)
      {
         if (e.Data.GetDataPresent(DataFormats.Text))
            CodeEditor.Text = e.Data.GetData(DataFormats.Text).ToString();
         else if (e.Data.GetDataPresent(DataFormats.UnicodeText))
            CodeEditor.Text = e.Data.GetData(DataFormats.UnicodeText).ToString();
         else if (e.Data.GetDataPresent(DataFormats.FileDrop))
         {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
            {
               var assemblyFiles = (from file in files where file.EndsWith(".dll") select file).ToList();
               var otherFiles = (from file in files where !file.EndsWith(".dll") select file).ToList();
               if (assemblyFiles.Count > 1 || (assemblyFiles.Count + otherFiles.Count > 2))
               {
                  MessageBox.Show(Resources.DragDropLoadErrorMessage);
                  return;
               }

               if (assemblyFiles.Count != 0)
                  LoadGrammarInternal(assemblyFiles[0]);

               if (otherFiles.Count != 0)
                  LoadSourceFileInternal(otherFiles[0]);
            }
         }
      }
   }
}